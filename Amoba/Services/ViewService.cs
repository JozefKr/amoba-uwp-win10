using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using GalaSoft.MvvmLight;
using Autofac;
using Windows.UI.Xaml;

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

                // Ellenőrizzük, hogy az aktuális Page típusa azonos-e a cél Page-el (pl. GamePage)
                if (rootFrame != null && rootFrame.CurrentSourcePageType != pageType)
                {
                    // Navigálás csak akkor, ha még nem vagyunk ezen az oldalon
                    rootFrame.Navigate(pageType, viewModelInstance);
                }
            }
            else throw new ArgumentException($"ViewModel type {typeof(T).Name} not registered.");
        }

        public void OpenPage<T>(params NamedParameter[] parameters) where T : ViewModelBase
        {
            NavigateToView<T>(parameters);
        }
    }
}
