using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Autofac.Core;
using Autofac;
using System.Diagnostics;
using GalaSoft.MvvmLight.Threading;

namespace Amoba.ViewModel
{
    /// <summary>
    /// A ViewModel a tábla méretének kiválasztásához.
    /// Különböző konstruktorokkal kezeli a helyi/AI és a hálózati (Host) indítási útvonalakat.
    /// </summary>
    public class GameSizeViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private readonly INetworkService _networkService; // Ez null lehet helyi/AI módban
        private bool _isVsComputerMode;
        private bool _isNetworkGame; // Jelzi, hogy Hostként vagyunk-e itt
        private string _statusMessage = string.Empty;
        private readonly string _myPlayerName;

        /// <summary>
        /// Konstruktor HÁLÓZATI (Host) indításhoz. A DI konténer ezt hívja, ha minden paraméter elérhető.
        /// </summary>
        public GameSizeViewModel(IViewService viewService, INetworkService networkService, bool isVsComputer, bool isNetworkGame, string myPlayerName)
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _networkService = networkService; // Lehet null, de a hívó (MainViewModel) gondoskodik róla, hogy itt ne legyen az.
            _isVsComputerMode = isVsComputer; // Ez itt mindig false lesz hálózati módban
            _isNetworkGame = isNetworkGame; // Ez itt mindig true lesz
            _myPlayerName = myPlayerName;
            Enabled = true;

            if (_isNetworkGame)
            {
                _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished;
                StatusMessage = "Várakozás a Kliens csatlakozására...";
            }
            else
            {
                StatusMessage = "Válassz pályaméretet:";
            }
        }

        /// <summary>
        /// Konstruktor HELYI/AI indításhoz. A DI konténer ezt választja, ha csak ezek a paraméterek érkeznek.
        /// </summary>
        public GameSizeViewModel(IViewService viewService, bool isVsComputer, bool isNetworkGame = false)
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _isVsComputerMode = isVsComputer;
            _isNetworkGame = isNetworkGame; // Ez itt mindig false lesz
            _networkService = null; // Nincs hálózati szolgáltatás helyi módban
            Enabled = true;
        }

        /// <summary>
        /// Visszajelzés a felhasználónak a méretválasztás állapotáról vagy hibájáról.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            // Fontos a 'private set', mert csak a ViewModel állíthatja be
            private set => Set(ref _statusMessage, value);
        }

        private bool _enabled;
        /// <summary>
        /// Engedélyezi/tiltja a méretválasztó gombokat.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        private ICommand _selectSize;
        /// <summary>
        /// Parancs a táblaméret kiválasztásához és a játék indításához.
        /// A CommandParameter tartalmazza a méretet stringként ("3", "4", "5").
        /// </summary>
        public ICommand SelectSize => _selectSize ?? (_selectSize = new RelayCommand<string>(SelectSizeMethod));

        /// <summary>
        /// Végrehajtja a méret kiválasztását. Hálózati módban elküldi a méretet, majd navigál.
        /// </summary>
        private async void SelectSizeMethod(string size)
        {
            if (!Enabled) return;

            if (int.TryParse(size, out int boardSize) && boardSize > 0)
            {
                Enabled = false;
                StatusMessage = "Feldolgozás...";

                try
                {
                    var gameParams = new List<Parameter>
                    {
                        new NamedParameter("boardSizeParam", boardSize),
                        new NamedParameter("isVsComputerParam", _isVsComputerMode),
                        new NamedParameter("isNetworkGameParam", _isNetworkGame), // Ez a paraméter kritikus!
                        new NamedParameter("myPlayerNameParam", _myPlayerName)
                    };

                    if (_isNetworkGame && _networkService != null)
                    {
                        // HÁLÓZATI: Host küld méretet
                        StatusMessage = "Méret küldése az ellenfélnek...";

                        // A Host paramétert CSAK itt adjuk hozzá
                        gameParams.Add(new NamedParameter("isHostParam", true));

                        await _networkService.InitiateNetworkGameStartAsync(boardSize, _myPlayerName);
                    }
                    else
                    {
                        // HELYI/AI JÁTÉK
                        // A Host paramétert itt is hozzá kell adni (false-ként)
                        gameParams.Add(new NamedParameter("isHostParam", false));
                    }

                    string opponentName = _networkService?.CachedOpponentName;
                    if (!string.IsNullOrEmpty(opponentName))
                    {
                        gameParams.Add(new NamedParameter("opponentNameParam", opponentName));
                    }
                    else if (_isNetworkGame)
                    {
                        // Ha hálózati játék, de a név valamiért mégis null,
                        // legalább naplózzuk, de ne adjunk át üres paramétert.
                        Debug.WriteLine("FIGYELEM (GameSizeVM): Az ellenfél neve (CachedOpponentName) null maradt a navigáció pillanatában.");
                    }

                    // NAVIGÁCIÓ (MINDIG LEFUT)
                    StatusMessage = "Játék indítása...";
                    _viewService.OpenPage<GameViewModel>(gameParams.ToArray());
                }
                catch (Exception ex)
                {
                    StatusMessage = $"HIBA: {ex.Message}";
                    Debug.WriteLine($"Hiba a méret kiválasztása/küldése során: {ex.Message}");
                    Enabled = true;
                }
            }
            else
            {
                StatusMessage = "Hiba: Érvénytelen méret.";
                Debug.WriteLine($"Érvénytelen méret paraméter: {size}");
                Enabled = true;
            }
        }

        /// <summary>
        /// Akkor fut le, amikor a Kliens sikeresen csatlakozott a TCP listenerhez.
        /// </summary>
        private void NetworkService_HostConnectionEstablished(object sender, EventArgs e)
        {
            // Amikor a kliens csatlakozik, frissítjük az üzenetet
            // (A kliens neve a háttérben töltődik be a NetworkService-be)
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                StatusMessage = "Ellenfél csatlakozott! Válassz méretet a játék indításához:";
            });
        }

        public override void Cleanup()
        {
            if (_isNetworkGame && _networkService != null)
            {
                _networkService.HostConnectionEstablished -= NetworkService_HostConnectionEstablished;
            }
            base.Cleanup();
        }
    }
}