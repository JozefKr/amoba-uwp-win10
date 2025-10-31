using System;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media; // Ez kell a VisualTreeHelper-hez
using Windows.UI.Xaml.Media.Animation; // Ez kell a Storyboard-hoz

namespace Amoba.Views
{
    public sealed partial class GamePage : BasePage
    {
        public GamePage()
        {
            this.InitializeComponent();
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
    }
}