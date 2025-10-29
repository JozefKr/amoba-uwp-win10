using Amoba.Services;
using Amoba.ViewModel;
using Autofac;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Diagnostics; // Szükséges a Debug.WriteLine használatához
using Windows.ApplicationModel; // Szükséges az OnSuspending-hez
using Windows.ApplicationModel.Activation;
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

            // Hozzáadunk egy eseménykezelőt a globális, nem kezelt kivételek elkapásához.
            this.UnhandledException += OnUnhandledException;
        }

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
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            IContainer container = null;

            // Létrehozzuk a Frame-et, ha nem létezik (első indítás)
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                // [1] DISPATCHER INITIALIZÁLÁSA: Azonnal, ahogy a Frame létrejött
                DispatcherHelper.Initialize();

                // [2] BOOTSTRAPPER hívása (ami felhasználja a Frame-et és beállítja a szolgáltatásokat)
                container = Bootstrapper.Bootstrap(rootFrame);

                Window.Current.Content = rootFrame;
            }
            else
            {
                // Ha a Frame már létezik (például suspend/resume esetén)
                // [3] DISPATCHER INITIALIZÁLÁSA: Itt is megismételjük, ha az első if ág nem futott le
                DispatcherHelper.Initialize();

                // [4] Konténer feltételezett újrafeloldása (az egyszerűség kedvéért)
                if (container == null)
                {
                    container = Bootstrapper.Bootstrap(rootFrame);
                }
            }


            if (e.PrelaunchActivated == false)
            {
                IViewService viewService = null;

                if (container != null)
                {
                    // [5] SERVICES FELOLDÁSA
                    viewService = container.Resolve<IViewService>();
                }
                else
                {
                    // Ez egy critical hiba, ha idáig eljutunk
                    throw new InvalidOperationException("Dependency injection container is not initialized.");
                }


                if (rootFrame.Content == null)
                {
                    // [6] NAVIGÁLÁS A KEZDŐOLDALRA
                    viewService?.OpenPage<MainViewModel>();
                }

                // [7] Ablak aktiválása
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
    }
}