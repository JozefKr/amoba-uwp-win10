using Amoba.Model;
using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameViewModel : ViewModelBase
    {
        // --- PRIVÁT MEZŐK ---
        private int player1Score;
        private int player2Score;
        private bool isPlayer1Turn;
        private bool isPlayer2Turn;
        private bool isComputerTurn = false;
        private bool isVsComputer;
        private bool isProcessingAiMove = false;
        private ObservableCollection<Place> places;
        private ICommand setImage;
        private ICommand newGameCommand;
        private int boardSize;
        private AiPlayer aiPlayer;
        private bool _isGameOver;
        private string _gameOverMessage;

        // --- HÁLÓZATI MEZŐK ---
        private readonly INetworkService _networkService;
        private bool _isNetworkGame = false;
        private bool _amIHost = false; // TRUE ha X (Host), FALSE ha O (Kliens)
        private string _opponentName = string.Empty;

        // --- PUBLIKUS PROPERTY-K ---
        public int BoardSize { get => boardSize; set => Set(ref boardSize, value); }
        public int Player2Score { get => player2Score; set => Set(ref player2Score, value); }
        public int Player1Score { get => player1Score; set => Set(ref player1Score, value); }

        public bool IsPlayer1Turn { get => isPlayer1Turn; set => Set(ref isPlayer1Turn, value); }
        public bool IsPlayer2Turn
        {
            get => isPlayer2Turn;
            set
            {
                if (Set(ref isPlayer2Turn, value))
                {
                    // AI LÉPÉS: Csak ha P2 jön ÉS Gép ellen játszunk
                    isComputerTurn = isVsComputer && value;
                    if (isComputerTurn && !isProcessingAiMove)
                    {
                        TriggerAiMove();
                    }
                }
            }
        }

        public bool IsNetworkGame { get => _isNetworkGame; set => Set(ref _isNetworkGame, value); }
        public bool AmIHost { get => _amIHost; set => Set(ref _amIHost, value); }
        public string OpponentName { get => _opponentName; set => Set(ref _opponentName, value); }
        public bool IsGameOver { get => _isGameOver; set => Set(ref _isGameOver, value); }
        public string GameOverMessage { get => _gameOverMessage; set => Set(ref _gameOverMessage, value); }
        public ObservableCollection<Place> Places { get => places; set => Set(ref places, value); }

        // --- PARANCSOK ---
        public ICommand SetImage
        {
            get => setImage ?? (setImage = new RelayCommand<Place>(
                SetImageMethod,
                p => p != null && p.IsEmpty &&
                     !isProcessingAiMove && // AI nem gondolkodik
                     (
                        // ESET 1: HELYI/AI JÁTÉK
                        // Engedélyezzük, ha P1 jön (mindig ember) VAGY P2 jön, de nem gép
                        (!IsNetworkGame && (IsPlayer1Turn || (IsPlayer2Turn && !isVsComputer))) ||

                        // ESET 2: HÁLÓZATI JÁTÉK
                        // Engedélyezzük, ha hálózatban vagyunk ÉS a mi körünk van
                        (IsNetworkGame && ((AmIHost && IsPlayer1Turn) || (!AmIHost && IsPlayer2Turn)))
                     )
            ));
        }
        public ICommand NewGameCommand => newGameCommand ?? (newGameCommand = new RelayCommand(ExecuteNewGame));


        // ===================================================================
        // --- KONSTRUKTOR ÉS INITIALIZÁLÁS ---
        // ===================================================================

        // JAVÍTOTT KONSTRUKTOR: Ez fogadja a DI paramétereket (4 paraméter)
        public GameViewModel(int boardSizeParam, bool isVsComputerParam, bool isNetworkGameParam, bool isHostParam, INetworkService networkService)
        {
            _networkService = networkService;

            // 1. ÁLLAPOTOK BEÁLLÍTÁSA A PARAMÉTEREKBŐL
            _isNetworkGame = isNetworkGameParam;
            _amIHost = isHostParam; // Helyes szerepkör beállítása

            // 2. FELIRATKOZÁS (Csak ha hálózati játék)
            if (_isNetworkGame)
            {
                // A GameStarted esemény fogja beállítani az ellenfél nevét és a köröket
                _networkService.GameStarted += NetworkService_GameStarted;
                _networkService.MoveReceived += NetworkService_MoveReceived;
                _networkService.OpponentDisconnected += NetworkService_OpponentDisconnected;
            }

            // 3. TÁBLA INICIALIZÁLÁSA
            InitializeViewModel(boardSizeParam, isVsComputerParam);

            // 4. KÖR BEÁLLÍTÁSA (Alapértelmezett)
            // Helyi/AI módban P1 kezd.
            // Hálózati módban a GameStarted esemény ezt felülírja.
            IsPlayer1Turn = true;
            IsPlayer2Turn = false;
        }

        // FONTOS: Az InitializeViewModel NEM állítja be az IsNetworkGame-et!
        private void InitializeViewModel(int boardSizeParam, bool isVsComputerMode)
        {
            BoardSize = boardSizeParam;
            this.isVsComputer = isVsComputerMode;

            aiPlayer = new AiPlayer();

            Places = new ObservableCollection<Place>();
            for (int i = 0; i < Math.Pow(this.BoardSize, 2); i++)
            {
                Places.Add(new Place() { Id = i, Type = IconType.None });
            }
            Player1Score = 0;
            Player2Score = 0;
            // A köröket a konstruktor vagy a GameStarted esemény állítja be!
            isComputerTurn = false;
            IsGameOver = false;

            // Az OpponentName beállítása (Helyi/AI mód)
            if (isVsComputerMode) { OpponentName = "A GÉP"; }
            else if (!_isNetworkGame) { OpponentName = "JÁTÉKOS (O)"; }
            // Ha hálózati, az OpponentName-et a GameStarted esemény fogja beállítani
        }

        // ===================================================================
        // --- FŐ LOGIKAI METÓDUSOK ---
        // ===================================================================

        private void ExecuteNewGame()
        {
            Player1Score = 0;
            Player2Score = 0;
            ResetBoard();
        }

        private async void SetImageMethod(Place place)
        {
            try
            {
                if (place == null || !place.IsEmpty) return;

                IconType iconToUse = IsPlayer1Turn ? IconType.Cross : IconType.Circle;

                await ExecuteMove(place, iconToUse);

                // A körváltást a HÍVÓ metódus végzi el,
                // miután a lépés (és a küldés) befejeződött.
                // (De csak ha nincs még vége a játéknak)
                if (!IsGameOver)
                {
                    ChangeTurn();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a SetImageMethod-ban: {ex.Message}");
                // Ha hiba történik (pl. socket lezárva), állítsuk le a játékot
                HandleDisconnection("Hálózati hiba (küldés).");
            }
        }

        /// <summary>
        /// A játéklogika központi metódusa. Végrehajt egy lépést, elküldi a hálózaton (ha kell),
        /// ellenőrzi a győztest, és átadja a kört.
        /// </summary>
        /// <param name="place">A mező, ahova léptek.</param>
        /// <param name="type">A lépő játékos típusa (X vagy O).</param>
        /// <returns>Egy Task, mivel a hálózati küldés aszinkron.</returns>
        private async Task ExecuteMove(Place place, IconType type)
        {
            // Alapvető ellenőrzés
            if (place == null || !place.IsEmpty) return;

            // 1. LÉPÉS: A LOKÁLIS TÁBLA FRISSÍTÉSE
            place.Type = type;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged(); // Gombok állapotának frissítése

            // 2. LÉPÉS: HÁLÓZATI KÜLDÉS
            // Csak akkor küldünk, ha hálózati játékban vagyunk ÉS a lépés tőlünk származik
            if (IsNetworkGame && (type == IconType.Cross && AmIHost || type == IconType.Circle && !AmIHost))
            {
                try
                {
                    // Megvárjuk, amíg a lépés elküldése befejeződik (vagy hibát dob)
                    await _networkService.SendMoveAsync(place.Id);
                }
                catch (Exception ex)
                {
                    // A hívó metódus (SetImageMethod) elkapja ezt a hibát.
                    // Ez a hiba várható, ha a másik fél bontotta a kapcsolatot.
                    Debug.WriteLine($"SendMoveAsync hiba (a hívó elkapja): {ex.Message}");
                    throw;
                }
            }

            // 3. LÉPÉS: GYŐZELEM ELLENŐRZÉSE
            // Ezt csak a hálózati küldés *után* tesszük meg, hogy elkerüljük az ObjectDisposedException-t
            var winner = GameLogic.CheckWinner(Places, BoardSize);
            var isBoardFull = Places.All(p => !p.IsEmpty);

            if (winner != IconType.None || isBoardFull)
            {
                // Játék vége: Üzenet beállítása és Reset indítása
                if (winner == IconType.Cross) { GameOverMessage = "Játékos (X) Nyert!"; Player1Score++; }
                else if (winner == IconType.Circle) { GameOverMessage = isVsComputer ? "A Gép Nyert!" : "Játékos (O) Nyert!"; Player2Score++; }
                else { GameOverMessage = "Döntetlen!"; }

                IsGameOver = true;
                ResetBoard(); // Ez 'async void', elindítja a leállítást (és a Disconnect-et)
            }
        }

        private async void TriggerAiMove()
        {
            if (isProcessingAiMove || !isVsComputer || !isComputerTurn) return;

            isProcessingAiMove = true;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();

            await Task.Delay(200);

            try
            {
                Place bestMovePlace = aiPlayer.FindBestMove(Places, BoardSize);

                if (bestMovePlace != null && bestMovePlace.IsEmpty)
                {
                    await ExecuteMove(bestMovePlace, IconType.Circle);
                    // A körváltást a HÍVÓ metódus végzi el.
                    if (!IsGameOver)
                    {
                        ChangeTurn();
                    }
                }
                else
                {
                    if (!Places.All(p => !p.IsEmpty)) ChangeTurn();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI Hiba: {ex.Message}");
                if (!Places.All(p => !p.IsEmpty)) ChangeTurn();
            }
            finally
            {
                isProcessingAiMove = false;
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            }
        }

        private void ChangeTurn()
        {
            IsPlayer1Turn = !IsPlayer1Turn;
            IsPlayer2Turn = !IsPlayer2Turn;

            // FONTOS: A CanExecute frissítésének kényszerítése
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
        }

        // ===================================================================
        // --- HÁLÓZATI ESEMÉNYKEZELŐK ÉS TAKARÍTÁS ---
        // ===================================================================

        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // 1. Állapot frissítése (IsNetworkGame már true)
                AmIHost = e.IsHost; // Megerősíti a szerepkört
                OpponentName = e.OpponentName; // Beállítja a nevet

                // 2. KÖR BEÁLLÍTÁSA: A HOST KEZD!
                IsPlayer1Turn = e.IsHost; // Ha én vagyok a Host (X), én kezdek.
                IsPlayer2Turn = !e.IsHost; // Ha én vagyok a Kliens (O), az ellenfél (X) jön.

                // 3. CANEXECUTE FRISSÍTÉSE
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            });
        }

        private async void NetworkService_MoveReceived(object sender, int moveIndex)
        {
            try
            {
                await DispatcherHelper.RunAsync(async () =>
                {
                    IconType opponentIcon = AmIHost ? IconType.Circle : IconType.Cross;
                    var targetPlace = Places.FirstOrDefault(p => p.Id == moveIndex);

                    if (targetPlace != null && targetPlace.IsEmpty)
                    {
                        await ExecuteMove(targetPlace, opponentIcon);

                        // A körváltást a HÍVÓ metódus végzi el.
                        if (!IsGameOver)
                        {
                            ChangeTurn();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a NetworkService_MoveReceived-ben: {ex.Message}");
                HandleDisconnection("Hálózati hiba (fogadás).");
            }
        }

        private void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            HandleDisconnection($"A játék megszakadt: {OpponentName} kilépett.");
        }

        // Segédmetódus a hálózati leállás kezelésére
        private void HandleDisconnection(string message)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                IsGameOver = true;
                GameOverMessage = message;
                // A ResetBoard-ot már nem hívjuk,
                // a kapcsolat már valószínűleg halott.
                // A gombokat a CanExecute letiltja, mert az IsNetworkGame=true,
                // de a körök nem egyeznek.
            });
        }

        private async void ResetBoard()
        {
            try
            {
                await Task.Delay(1500);

                // A hálózati kapcsolatot NEM bontjuk, hogy a visszavágó működjön
                // if (IsNetworkGame) { _networkService.Disconnect(); }

                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    // A táblát alaphelyzetbe állítjuk
                    foreach (var place in Places) { place.Type = IconType.None; }

                    // Mindegy, hogy hálózati vagy helyi játék,
                    // a Reset után MINDIG P1 (azaz a Host) kezdi a következő kört.
                    IsPlayer1Turn = true;
                    IsPlayer2Turn = false;
                    isComputerTurn = false;
                    isProcessingAiMove = false;
                    IsGameOver = false; // Az overlay eltüntetése

                    // A CanExecute frissítése:
                    // Most már a Host (AmIHost=true, IsPlayer1Turn=true) ENGEDÉLYEZVE lesz.
                    // A Kliens (AmIHost=false, IsPlayer1Turn=true) TILTVA lesz.
                    (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FATALIS Hiba a ResetBoard-ban: {ex.Message}");
            }
        }

        public override void Cleanup()
        {
            if (_networkService != null)
            {
                _networkService.GameStarted -= NetworkService_GameStarted;
                _networkService.MoveReceived -= NetworkService_MoveReceived;
                _networkService.OpponentDisconnected -= NetworkService_OpponentDisconnected;

                _networkService.Disconnect();
            }

            base.Cleanup();
        }
    }
}