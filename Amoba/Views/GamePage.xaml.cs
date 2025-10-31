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
        /// Ez a metódus fut le, amikor az Image mérete megváltozik (pl. Kép betöltődik).
        /// Elindítja a "PopInStoryboard" animációt.
        /// </summary>
        private void CellImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                var image = sender as Image;
                if (image == null) return;

                // Csak akkor animálunk, ha a kép üresről (0-s méret) vált láthatóra (nem-0 méret).
                // Ez megakadályozza, hogy az animáció lefusson pl. ablakátméretezéskor.
                if (e.NewSize.Width > 0 && e.PreviousSize.Width == 0)
                {
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
                        // 3. Keressük meg a ScaleTransform-ot az Image-en
                        var scaleTransform = image.RenderTransform as ScaleTransform;
                        if (scaleTransform == null) return;

                        // 4. KRITIKUS LÉPÉS: Manuálisan hozzárendeljük az animációkat
                        // A Storyboard-nak megmondjuk, MELYIK konkrét elemen fusson le.

                        // storyboard.Children[0] az Opacity animáció (TargetName="CellImage")
                        Storyboard.SetTarget(storyboard.Children[0], image);

                        // storyboard.Children[1] a ScaleX animáció (TargetName="CellImageScale")
                        Storyboard.SetTarget(storyboard.Children[1], scaleTransform);

                        // storyboard.Children[2] a ScaleY animáció (TargetName="CellImageScale")
                        Storyboard.SetTarget(storyboard.Children[2], scaleTransform);

                        // 5. Animáció indítása
                        storyboard.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Animációs hiba (SizeChanged): {ex.Message}");
            }
        }
    }
}