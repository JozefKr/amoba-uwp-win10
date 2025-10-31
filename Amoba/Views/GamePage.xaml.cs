using Amoba.Messages;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media; // Ez kell a VisualTreeHelper-hez
using Windows.UI.Xaml.Media.Animation; // Ez kell a Storyboard-hoz
using Windows.UI.Xaml.Navigation;

namespace Amoba.Views
{
    public sealed partial class GamePage : BasePage
    {
        public GamePage()
        {
            this.InitializeComponent();

            // =======================================================
            // 1. FELIRATKOZÁS AZ ÜZENETRE
            // Amikor ez az oldal létrejön, el kezd figyelni a hang-üzenetekre.
            // =======================================================
            Messenger.Default.Register<PlaySoundMessage>(this, OnPlaySoundMessage);
        }

        /// <summary>
        /// Ez a metódus hívódik meg, amikor a ViewModel hangot akar lejátszani.
        /// </summary>
        private void OnPlaySoundMessage(PlaySoundMessage message)
        {
            // A biztonság kedvéért UI szálon futtatjuk
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                switch (message.SoundName)
                {
                    case "Click":
                        ClickSound.Play();
                        break;
                    case "Win":
                        WinSound.Play();
                        break;
                    case "Lose":
                        LoseSound.Play();
                        break;
                }
            });
        }

        /// <summary>
        /// Ez a metódus fut le, amikor az Image mérete megváltozik.
        /// Elindítja az animációt (ha a kép megjelenik),
        /// és leállítja az animációt (ha a kép eltűnik).
        /// </summary>
        private void CellImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                var image = sender as Image;
                if (image == null) return;

                // 1. Keressük meg a szülő Border-t, hogy elérjük a Storyboard-ot
                DependencyObject parent = VisualTreeHelper.GetParent(image);
                while (parent != null && !(parent is Border))
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }
                var rootBorder = parent as Border;
                if (rootBorder == null) return;

                // 2. Keressük meg a Storyboard-ot az erőforrások között
                if (rootBorder.Resources.TryGetValue("PopInStoryboard", out object resource) && resource is Storyboard storyboard)
                {
                    if (e.NewSize.Width > 0 && e.PreviousSize.Width == 0)
                    {
                        // === A) KÉP MEGJELENIK (X vagy O) ===

                        // Keressük meg a ScaleTransform-ot az Image-en
                        var scaleTransform = image.RenderTransform as ScaleTransform;
                        if (scaleTransform == null) return;

                        // Manuálisan hozzárendeljük az animációkat
                        Storyboard.SetTarget(storyboard.Children[0], image);
                        Storyboard.SetTarget(storyboard.Children[1], scaleTransform);
                        Storyboard.SetTarget(storyboard.Children[2], scaleTransform);

                        // Animáció indítása
                        storyboard.Begin();
                    }
                    else if (e.NewSize.Width == 0 && e.PreviousSize.Width > 0)
                    {
                        // === B) KÉP ELTŰNIK (ResetBoard hívás) ===

                        // AZONNAL LEÁLLÍTJUK az éppen futó animációt.
                        // Ez megakadályozza a COMException-t, mert
                        // mire a rendszer eltávolítja a képet, az animáció már nem fut.
                        storyboard.Stop();
                    }
                }
            }
            catch (Exception ex)
            {
                // A "No installed components" hiba itt elkapásra kerül,
                // de a 'storyboard.Stop()' hívás megakadályozza a fő összeomlást.
                Debug.WriteLine($"Animációs hiba (SizeChanged): {ex.Message}");
            }
        }

        // =======================================================
        // 2. LEIRATKOZÁS (Kritikus!)
        // Amikor elnavigálunk erről az oldalról, le kell iratkozni.
        // =======================================================
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Messenger.Default.Unregister<PlaySoundMessage>(this);
        }
    }
}