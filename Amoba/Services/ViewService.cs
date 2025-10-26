using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using GalaSoft.MvvmLight;
using Autofac;

namespace Amoba.Services
{
    public class ViewService : IViewService
    {
        private Dictionary<Type, Type> registrations;
        private IComponentContext container;
        // Az UWP-ben szükségünk van a fő Frame referenciájára a navigációhoz
        private Frame rootFrame;

        // A konstruktor frissítése a Frame paraméterrel
        public ViewService(IComponentContext container, Frame rootFrame)
        {
            this.container = container;
            this.rootFrame = rootFrame;
            registrations = new Dictionary<Type, Type>();
        }

        // A regisztráció átnevezése RegisterPage-re
        public void RegisterPage(Type vm, Type page)
        {
            if (registrations.ContainsKey(vm)) throw new ArgumentException("ViewModel already registered.");
            registrations.Add(vm, page);
        }

        /// <summary>
        /// Navigáció végrehajtása az UWP Frame-ben.
        /// Ez a metódus feloldja a ViewModel-t és átadja azt a Page-nek.
        /// </summary>
        private void NavigateToView<T>(params NamedParameter[] parameters) where T : ViewModelBase
        {
            if (rootFrame == null)
            {
                // Hiba, ha a Frame nincs beállítva. Ez tipikusan az App.xaml.cs-ben történik.
                throw new InvalidOperationException("Root frame has not been set for navigation.");
            }

            if (registrations.ContainsKey(typeof(T)))
            {
                var pageType = registrations[typeof(T)];

                // 1. Feloldjuk a ViewModel példányát a DI konténeren keresztül
                var viewModelInstance = container.Resolve<T>(parameters);

                // 2. Navigálunk a Page-re, és a ViewModel-t adjuk át paraméterként.
                // A Page-nek kell a DataContext-et beállítania az OnNavigatedTo metódusában.
                rootFrame.Navigate(pageType, viewModelInstance);
            }
            else throw new ArgumentException($"ViewModel type {typeof(T).Name} not registered.");
        }

        public void OpenPage<T>(params NamedParameter[] parameters) where T : ViewModelBase
        {
            NavigateToView<T>(parameters);
        }

        public async void OpenDialog<T>(params NamedParameter[] parameters) where T : ViewModelBase
        {
            if (registrations.ContainsKey(typeof(T)))
            {
                var dialogType = registrations[typeof(T)];

                // ContentDialog példányosítása (feltételezve, hogy a regisztrált View ContentDialog)
                var dialog = (ContentDialog)Activator.CreateInstance(dialogType);

                // DataContext beállítása a feloldott ViewModel-re
                dialog.DataContext = container.Resolve<T>(parameters);

                // Modális megjelenítés
                await dialog.ShowAsync();
            }
            else throw new ArgumentException($"ViewModel type {typeof(T).Name} not registered for dialog.");
        }
    }
}
