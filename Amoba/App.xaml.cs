using Amoba.Services;
using Amoba.ViewModel;
using Autofac;
using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Amoba
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static IContainer Container { get; private set; }
        // A WPF OnStartup helyett UWP-ben az OnLaunched eseményt használjuk.
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Ne hozzon létre Frame-et, ha az már létezik
            if (rootFrame == null)
            {
                // Hozza létre a fő navigációs Frame-et
                rootFrame = new Frame();

                // Navigációs hibák kezelése
                rootFrame.NavigationFailed += OnNavigationFailed;

                // --- 1. KONTÉNER INICIALIZÁLÁSA ---
                // Fontos: A Frame referenciát át kell adni a Bootstrapper-nek,
                // hogy a ViewService helyesen tudjon inicializálódni.
                // A Bootstrapper metódus aláírásának meg kell felelnie ennek: Bootstrapper.Bootstrap(rootFrame)
                Container = Bootstrapper.Bootstrap(rootFrame);

                // Helyezze a Frame-et az aktuális ablakba
                Window.Current.Content = rootFrame;
            }

            // --- 2. AZ ELSŐ NÉZET MEGJELENÍTÉSE ---

            // Ha a Frame még nem tartalmaz Page-et, navigálunk az első oldalra.
            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // WPF: Container.Resolve<IViewService>().OpenWindow<MainViewModel>();
                    // UWP: Navigáció az első Page-re a ViewService segítségével
                    Container.Resolve<IViewService>().OpenPage<MainViewModel>();
                }

                // Győződjön meg róla, hogy az aktuális ablak aktív
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Akkor hívódik meg, ha a navigáció nem sikerül egy adott oldalra
        /// </summary>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
