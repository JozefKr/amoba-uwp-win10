using Amoba.Services;
using Amoba.ViewModel;
using Autofac;
using System;
using System.Diagnostics; // Szükséges a Debug.WriteLine használatához
using Windows.ApplicationModel; // Szükséges az OnSuspending-hez
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups; // <-- MÓDOSÍTÁS: MessageDialog-hoz szükséges
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls; // Szükséges a Frame-hez
using Windows.UI.Xaml.Navigation;

namespace Amoba
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static IContainer Container { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            // --- BEILLESZTETT KÓD ---
            // Hozzáadunk egy eseménykezelőt a globális, nem kezelt kivételek elkapásához.
            this.UnhandledException += OnUnhandledException;
            // --- EDDIG ---
        }

        // --- BEILLESZTETT KÓD: EZ A METÓDUS KAPJA EL A KIVÉTELEKET ---
        /// <summary>
        /// Ez a metódus hívódik meg, ha az alkalmazásban bárhol
        /// egy nem kezelt (le nem kezelt) kivétel történik.
        /// </summary>
        /// <param name="sender">Az esemény forrása (maga az App objektum).</param>
        /// <param name="e">Az esemény adatai, amely tartalmazza a kivételt (Exception)
        /// és egy 'Handled' (lekezelt) tulajdonságot.</param>
        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // A 'Handled' true-ra állításával jelezzük a rendszernek,
            // hogy "láttuk" a hibát, és (opcionálisan) megpróbáljuk
            // megakadályozni az alkalmazás azonnali összeomlását.
            // Figyelem: Az app állapota innentől instabil lehet!
            e.Handled = true;

            // Kiírjuk a kivétel részleteit a Debug kimenetre (pl. Visual Studio Output ablaka)
            // Egy éles alkalmazásban itt érdemes naplózni egy fájlba vagy
            // egy online hibakövető szolgáltatásba (pl. App Center).
            Debug.WriteLine("===== ALKALMAZÁS SZINTŰ KEZELETLEN KIVÉTEL =====");
            Debug.WriteLine($"Hibaüzenet: {e.Message}");
            if (e.Exception != null)
            {
                Debug.WriteLine($"Kivétel típusa: {e.Exception.GetType().FullName}");
                Debug.WriteLine("Stack Trace:");
                Debug.WriteLine(e.Exception.StackTrace);

                if (e.Exception.InnerException != null)
                {
                    Debug.WriteLine("--- Belső kivétel (Inner Exception) ---");
                    Debug.WriteLine($"Belső hibaüzenet: {e.Exception.InnerException.Message}");
                    Debug.WriteLine(e.Exception.InnerException.StackTrace);
                    Debug.WriteLine("--------------------------------------");
                }
            }
            Debug.WriteLine("=================================================");
            // --- MÓDOSÍTÁS VÉGE ---
        }
        // --- EDDIG ---


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

        // --- BEILLESZTETT KÓD (az App konstruktor hivatkozik rá) ---
        /// <summary>
        /// Invoked when application execution is being suspended. Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
        // --- EDDIG ---
    }
}