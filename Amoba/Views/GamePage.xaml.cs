using Amoba.Messages;
using Amoba.ViewModel;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media; // Ez kell a VisualTreeHelper-hez
using Windows.UI.Xaml.Media.Animation; // Ez kell a Storyboard-hoz
using Windows.UI.Xaml.Navigation;

namespace Amoba.Views
{
    public sealed partial class GamePage : BasePage
    {
        private GameViewModel Vm => DataContext as GameViewModel;

        public GamePage()
        {
            this.InitializeComponent();

            // =======================================================
            // 1. FELIRATKOZÁS AZ ÜZENETRE
            // Amikor ez az oldal létrejön, el kezd figyelni a hang-üzenetekre.
            // =======================================================
            Messenger.Default.Register<PlaySoundMessage>(this, OnPlaySoundMessage);
        }

        // =======================================================
        // JAVÍTÁS: Navigációs eseménykezelők az auto-görgetéshez
        // =======================================================

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Regisztráljuk a hang-üzenet figyelőt
            Messenger.Default.Register<PlaySoundMessage>(this, OnPlaySoundMessage);

            // Figyeljük, hogy a DataContext (ViewModel) mikor érkezik meg
            this.DataContextChanged += GamePage_DataContextChanged;

            // Ha a ViewModel már itt van (gyors betöltés), azonnal feliratkozunk
            SubscribeToChatChanges();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Leiratkozunk mindenről
            Messenger.Default.Unregister<PlaySoundMessage>(this);
            this.DataContextChanged -= GamePage_DataContextChanged;
            UnsubscribeFromChatChanges();
        }

        private void GamePage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Amikor a ViewModel betöltődik, feliratkozunk a chat eseményére
            SubscribeToChatChanges();
        }

        private void SubscribeToChatChanges()
        {
            if (Vm != null && Vm.ChatHistory != null)
            {
                // Feliratkozunk a chat-lista változásaira
                Vm.ChatHistory.CollectionChanged += ChatHistory_CollectionChanged;
            }
        }

        private void UnsubscribeFromChatChanges()
        {
            if (Vm != null && Vm.ChatHistory != null)
            {
                // Leiratkozunk
                Vm.ChatHistory.CollectionChanged -= ChatHistory_CollectionChanged;
            }
        }

        /// <summary>
        /// Ez fut le, ha új üzenet érkezik a ChatHistory-ba.
        /// </summary>
        private void ChatHistory_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Csak akkor görgetünk, ha új elem(ek)et adtak hozzá
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                try
                {
                    // Megkeressük a legutolsó elemet a listában
                    var lastItem = ChatHistoryListView.Items[ChatHistoryListView.Items.Count - 1];
                    // Parancsot adunk a ListView-nak, hogy görgessen ahhoz az elemhez
                    ChatHistoryListView.ScrollIntoView(lastItem, ScrollIntoViewAlignment.Leading);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hiba az auto-görgetés közben: {ex.Message}");
                }
            }
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

        private void ChatInputBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                if (Vm != null && Vm.SendChatCommand.CanExecute(null))
                {
                    Vm.SendChatCommand.Execute(null);
                }
            }
        }
    }
}