using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using GalaSoft.MvvmLight;
using Autofac;
using Autofac.Core; // Szükséges a Parameter[]-hez
using System.Diagnostics; // Hibakereséshez
using System.Linq; // LINQ szükséges a paraméterek logolásához
using System.Reflection;

namespace Amoba.Services
{
    public class ViewService : IViewService
    {
        private Dictionary<Type, Type> registrations;
        private IComponentContext container;
        private Frame rootFrame;

        public ViewService(IComponentContext container, Frame rootFrame)
        {
            this.container = container;
            this.rootFrame = rootFrame;
            registrations = new Dictionary<Type, Type>();
        }

        public void RegisterPage(Type vm, Type page)
        {
            if (registrations.ContainsKey(vm)) throw new ArgumentException("ViewModel already registered.");
            registrations.Add(vm, page);
        }

        /// <summary>
        /// Privát metódus a ViewModel feloldására, hibakezeléssel és részletes logolással.
        /// </summary>
        private TViewModel ResolveViewModel<TViewModel>(params Parameter[] parameters) where TViewModel : ViewModelBase
        {
            // Logoljuk a kapott paramétereket
            if (parameters != null && parameters.Any())
            {
                Debug.WriteLine($"Attempting to resolve {typeof(TViewModel).Name} with parameters:");
                foreach (var p in parameters)
                {
                    if (p is NamedParameter np)
                    {
                        Debug.WriteLine($" - NamedParameter: Name='{np.Name}', Value='{np.Value}'");
                    }
                    else if (p is TypedParameter tp)
                    {
                        Debug.WriteLine($" - TypedParameter: Type='{tp.Type.Name}', Value='{tp.Value}'");
                    }
                    else
                    {
                        Debug.WriteLine($" - Parameter: Type='{p.GetType().Name}'");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"Attempting to resolve {typeof(TViewModel).Name} without parameters.");
            }

            try
            {
                // Feloldjuk a ViewModel példányát a DI konténeren keresztül a kapott paraméterekkel
                var viewModelInstance = container.Resolve<TViewModel>(parameters);

                if (viewModelInstance == null)
                {
                    throw new InvalidOperationException($"Autofac returned null when resolving {typeof(TViewModel).Name}.");
                }

                Debug.WriteLine($"Successfully resolved {typeof(TViewModel).Name}. Constructor used: {viewModelInstance.GetType().GetConstructors().FirstOrDefault(c => c.GetParameters().Length == parameters?.Length)}"); // Megpróbáljuk kitalálni, melyik konstruktor futott le

                return viewModelInstance;
            }
            catch (Exception ex)
            {
                // Elkapjuk az Autofac feloldási hibát és RÉSZLETES üzenetet adunk (beleértve az InnerException-t is!)
                Debug.WriteLine($"!!! Autofac Error resolving {typeof(TViewModel).Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                }
                // Dobjuk tovább a kivételt, hogy az alkalmazás (vagy a debugger) lássa
                throw new InvalidOperationException($"Failed to resolve ViewModel '{typeof(TViewModel).Name}'. See Debug Output and inner exception for details.", ex);
            }
        }


        // A metódus többi része változatlan
        public void OpenPage<TViewModel>(params Parameter[] parameters) where TViewModel : ViewModelBase
        {
            if (rootFrame == null)
            {
                throw new InvalidOperationException("Root frame has not been set for navigation.");
            }

            if (registrations.ContainsKey(typeof(TViewModel)))
            {
                var pageType = registrations[typeof(TViewModel)];

                // 1. Feloldjuk a ViewModel példányát a privát metódussal
                var viewModelInstance = ResolveViewModel<TViewModel>(parameters); // Itt adjuk át a paramétereket

                // 2. Navigálunk a Page-re, és a ViewModel-t adjuk át paraméterként.
                bool navigationResult = rootFrame.Navigate(pageType, viewModelInstance);

                // Opcionális: Ellenőrizzük a navigáció sikerességét
                if (!navigationResult)
                {
                    Debug.WriteLine($"Navigáció sikertelen a(z) {pageType.Name} oldalra.");
                }
            }
            else
            {
                throw new ArgumentException($"ViewModel type {typeof(TViewModel).Name} not registered.");
            }
        }
    }
}