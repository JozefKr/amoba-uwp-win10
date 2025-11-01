using Amoba.Model;
using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls; // Szükséges a ContentDialog-hoz
using Amoba.Messages;
using System.Collections.Generic;

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
        private readonly IViewService _viewService;
        private ContentDialog _activeGameOverDialog = null;
        private readonly object _gameOverLock = new object();
        private TaskCompletionSource<bool> _leaveAckTcs;
        private bool _isNavigatingToMenu = false;
        private ICommand _mainMenuCommand;

        private string _chatMessageInput;
        private ICommand _sendChatCommand;
        public ObservableCollection<ChatMessage> ChatHistory { get; private set; }
        public string ChatMessageInput
        {
            get => _chatMessageInput;
            set
            {
                if (Set(ref _chatMessageInput, value))
                {
                    // Frissítjük a "Küldés" gomb állapotát
                    (SendChatCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand SendChatCommand => _sendChatCommand ?? (_sendChatCommand = new RelayCommand(
            async () => await ExecuteSendChatAsync(),
            () => IsNetworkGame && !string.IsNullOrWhiteSpace(ChatMessageInput)
        ));

        // --- HÁLÓZATI MEZŐK ---
        private readonly INetworkService _networkService;
        private bool _isNetworkGame = false;
        private bool _amIHost = false;
        private string _opponentName = string.Empty;
        private string _myPlayerName;

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
        public string MyPlayerName
        {
            get => _myPlayerName;
            set => Set(ref _myPlayerName, value);
        }
        public bool IsGameOver
        {
            get => _isGameOver;
            set
            {
                if (Set(ref _isGameOver, value))
                {
                    RaisePropertyChanged(nameof(IsGameInProgress));
                }
            }
        }
        public bool IsGameInProgress => !IsGameOver;
        public string GameOverMessage { get => _gameOverMessage; set => Set(ref _gameOverMessage, value); }
        public ObservableCollection<Place> Places { get => places; set => Set(ref places, value); }

        // --- PARANCSOK ---
        public ICommand SetImage
        {
            get => setImage ?? (setImage = new RelayCommand<Place>(
                SetImageMethod,
                p => p != null && p.IsEmpty &&
                     !isProcessingAiMove &&
                     !IsGameOver &&
                     (
                         (!IsNetworkGame && (IsPlayer1Turn || (IsPlayer2Turn && !isVsComputer))) ||
                         (IsNetworkGame && ((AmIHost && IsPlayer1Turn) || (!AmIHost && IsPlayer2Turn)))
                     )
            ));
        }
        public ICommand NewGameCommand => newGameCommand ?? (newGameCommand = new RelayCommand(ExecuteNewGameAsync));
        public ICommand MainMenuCommand => _mainMenuCommand ?? (_mainMenuCommand = new RelayCommand(async () => await ExecuteMainMenuAsync()));

        // ===================================================================
        // --- KONSTRUKTOR ÉS INITIALIZÁLÁS ---
        // ===================================================================

        public GameViewModel(IViewService viewService, int boardSizeParam, bool isVsComputerParam,
                             bool isNetworkGameParam, bool isHostParam, INetworkService networkService,
                             string myPlayerNameParam = null,
                             string opponentNameParam = null)
        {
            _viewService = viewService;
            _networkService = networkService;
            _isNetworkGame = isNetworkGameParam;
            _amIHost = isHostParam;

            // --- SAJÁT NÉV BEÁLLÍTÁSA ---
            MyPlayerName = "JÁTÉKOS (X)"; // Alapértelmezett
            if (isNetworkGameParam && !string.IsNullOrEmpty(myPlayerNameParam))
            {
                MyPlayerName = myPlayerNameParam;
            }

            // --- EGYSÉGES ELLENFÉL NÉV BEÁLLÍTÁS ---
            if (!string.IsNullOrEmpty(opponentNameParam))
            {
                OpponentName = opponentNameParam;
            }

            ChatHistory = new ObservableCollection<ChatMessage>();

            // 2. FELIRATKOZÁS (Csak ha hálózati játék)
            if (_isNetworkGame)
            {
                // Feliratkozás az összes szükséges hálózati eseményre
                _networkService.GameStarted += NetworkService_GameStarted;
                _networkService.MoveReceived += NetworkService_MoveReceived;
                _networkService.OpponentDisconnected += NetworkService_OpponentDisconnected;
                _networkService.RematchReceived += NetworkService_RematchReceived;
                _networkService.OpponentLeft += NetworkService_OpponentLeft;
                _networkService.LeaveAcknowledged += NetworkService_LeaveAcknowledged;
                _networkService.ChatMessageReceived += NetworkService_ChatMessageReceived; // CHAT
            }

            // 3. TÁBLA INICIALIZÁLÁSA
            InitializeViewModel(boardSizeParam, isVsComputerParam);

            // 4. KÖR BEÁLLÍTÁSA (Alapértelmezett)
            IsPlayer1Turn = true;
            IsPlayer2Turn = false;
        }

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
            isComputerTurn = false;
            IsGameOver = false;

            if (isVsComputerMode) { OpponentName = "A GÉP"; }
            else if (!_isNetworkGame) { OpponentName = "JÁTÉKOS (O)"; }
        }

        // ===================================================================
        // --- FŐ LOGIKAI METÓDUSOK ---
        // ===================================================================

        // Ez a "Visszavágó" gomb logikája
        private async void ExecuteNewGameAsync()
        {
            // 1. Elküldjük a VISSZAVÁGÓ kérést
            if (IsNetworkGame && _networkService != null)
            {
                try
                {
                    await _networkService.SendRematchRequestAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hiba a 'Rematch' küldésekor: {ex.Message}");
                    HandleDisconnection("Az ellenfél már kilépett.");
                    return;
                }
            }

            // 2. A küldő is alaphelyzetbe állítja a saját tábláját
            ResetBoard();
        }

        private async Task ExecuteMainMenuAsync()
        {
            // Hálózati játék esetén megpróbálunk kulturáltan kilépni
            if (IsNetworkGame && _networkService != null)
            {
                try
                {
                    _leaveAckTcs = new TaskCompletionSource<bool>();
                    Debug.WriteLine("ExecuteMainMenu: LEAVE üzenet küldése...");
                    await _networkService.SendLeaveGameAsync();
                    await Task.WhenAny(_leaveAckTcs.Task, Task.Delay(2000));

                    if (_leaveAckTcs.Task.IsCompleted)
                        Debug.WriteLine("ExecuteMainMenu: LEAVE_ACK megérkezett!");
                    else
                        Debug.WriteLine("ExecuteMainMenu: LEAVE_ACK időtúllépés!");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hiba a 'Leave' küldésekor (elkapva, de figyelmen kívül hagyva): {ex.Message}");
                }
            }

            GoToMainMenu();
        }

        private async void SetImageMethod(Place place)
        {
            try
            {
                if (place == null || !place.IsEmpty) return;
                IconType iconToUse = IsPlayer1Turn ? IconType.Cross : IconType.Circle;

                bool gameIsOver = await ExecuteMove(place, iconToUse);

                if (!gameIsOver)
                {
                    // Ha a játék NEM ért véget (ez egy normál lépés volt):
                    // 1. Játsszuk le a "Click" hangot
                    Messenger.Default.Send(new PlaySoundMessage { SoundName = "Click" });
                    // 2. Váltsunk kört
                    ChangeTurn();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a SetImageMethod-ban (küldés hiba): {ex.Message}");
                HandleDisconnection("Hálózati hiba (küldés).");
            }
        }

        private async Task<bool> ExecuteMove(Place place, IconType type)
        {
            if (place == null || !place.IsEmpty) return false;

            place.Type = type;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();

            if (IsNetworkGame && (type == IconType.Cross && AmIHost || type == IconType.Circle && !AmIHost))
            {
                try
                {
                    await _networkService.SendMoveAsync(place.Id);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SendMoveAsync hiba (a hívó elkapja): {ex.Message}");
                    throw;
                }
            }

            GameResult result = GameLogic.CheckWinner(Places, BoardSize);
            IconType winner = result.Winner; // Kiolvassuk a győztest

            var isBoardFull = Places.All(p => !p.IsEmpty);

            if (winner != IconType.None || isBoardFull)
            {
                string title;
                string message;

                // --- GYŐZELMI LOGIKA ---
                if (winner != IconType.None && result.WinningCellIDs.Any())
                {
                    // A kiemelést ASZINKRON módon végezzük, hogy időt adjunk a UI-nak
                    HighlightWinningCellsAsync(result.WinningCellIDs);

                    if (winner == IconType.Cross) // Játékos 1 (X) nyert
                    {
                        title = (!IsNetworkGame || AmIHost) ? "Győzelem!" : "Vereség!";
                        message = (!IsNetworkGame || AmIHost)
                                    ? $"{MyPlayerName} Nyert!"
                                    : $"{OpponentName} Nyert!";
                        Player1Score++;
                    }
                    else // Játékos 2 (O) nyert
                    {
                        title = (IsNetworkGame && !AmIHost) ? "Győzelem!" : "Vereség!";
                        message = (!IsNetworkGame || AmIHost)
                                    ? $"{OpponentName} Nyert!"
                                    : $"{MyPlayerName} Nyert!";
                        Player2Score++;
                    }
                }
                else // Döntetlen
                {
                    title = "Döntetlen!";
                    message = "A tábla megtelt!";
                }

                // Játék vége: Beállítjuk az állapotot és az üzenetet
                TriggerGameOver(title, message);
                return true; // A játék véget ért
            }
            else
            {
                // Nincs győztes, a játék folytatódik
                return false;
            }
        }

        /// <summary>
        /// Késleltetett kiemelést végez. Ez ad időt a Kliens UI-nak a frissítésre.
        /// </summary>
        private async void HighlightWinningCellsAsync(List<int> winningCellIDs)
        {
            // Várakozás 50ms-ot (ez a Kliens oldalon kritikus)
            await Task.Delay(50);

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // Végigmegyünk a győztes cellák ID-in
                foreach (int id in winningCellIDs)
                {
                    // Megkeressük a Place objektumot az ID alapján
                    var winningPlace = Places.FirstOrDefault(p => p.Id == id);
                    if (winningPlace != null)
                    {
                        winningPlace.IsWinningCell = true; // Ez aktiválja a kiemelést a XAML-ben
                    }
                }
            });
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
                    bool gameIsOver = await ExecuteMove(bestMovePlace, IconType.Circle);
                    if (!gameIsOver)
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
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
        }

        private async Task ExecuteSendChatAsync()
        {
            if (!IsNetworkGame || string.IsNullOrWhiteSpace(ChatMessageInput))
            {
                return;
            }

            string messageToSend = ChatMessageInput;
            ChatMessageInput = string.Empty;

            try
            {
                await _networkService.SendChatMessageAsync(messageToSend);

                ChatHistory.Add(new ChatMessage
                {
                    Author = MyPlayerName,
                    Message = messageToSend,
                    IsMine = true, // Ez a saját üzenetünk
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                ChatMessageInput = messageToSend;
                ChatHistory.Add(new ChatMessage
                {
                    Author = "Hiba",
                    Message = $"Küldés sikertelen: {ex.Message}",
                    IsMine = true,
                    Timestamp = DateTime.Now
                });
            }
        }

        // ===================================================================
        // --- HÁLÓZATI ESEMÉNYKEZELŐK ÉS TAKARÍTÁS ---
        // ===================================================================

        // Ez a metódus a Host oldalon már nem fog lefutni (mert a GameSizeViewModel
        // előbb érkezik), de a Kliens oldalon igen (bár ott az OpponentName
        // már a konstruktorban beállítódik).
        // A biztonság kedvéért itt hagyjuk, de a fő logika már a konstruktorban van.
        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    // Ha az OpponentName valamiért még üres lenne,
                    // ez a Kliens oldalon beállítja.
                    if (string.IsNullOrEmpty(OpponentName) && !e.IsHost)
                    {
                        OpponentName = e.OpponentName;
                    }

                    AmIHost = e.IsHost;
                    IsPlayer1Turn = e.IsHost;
                    IsPlayer2Turn = !e.IsHost;
                    (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATALIS Hiba a NetworkService_GameStarted feldolgozása közben: {ex.Message}");
                    HandleDisconnection("Kritikus hiba a játék indításakor.");
                }
            });
        }

        // Ez fogadja az ellenfél lépését
        private async void NetworkService_MoveReceived(object sender, int moveIndex)
        {
            try
            {
                // [CSALÁS-VÉDELEM]
                bool isOpponentsTurn = (AmIHost && IsPlayer2Turn) || (!AmIHost && IsPlayer1Turn);
                if (!isOpponentsTurn)
                {
                    Debug.WriteLine($"[CSALÁS-VÉDELEM] Lépés fogadva, de nem az ellenfél volt soron. Lépés eldobva.");
                    return;
                }

                await DispatcherHelper.RunAsync(async () =>
                {
                    IconType opponentIcon = AmIHost ? IconType.Circle : IconType.Cross;
                    var targetPlace = Places.FirstOrDefault(p => p.Id == moveIndex);
                    if (targetPlace != null && targetPlace.IsEmpty)
                    {
                        bool gameIsOver = await ExecuteMove(targetPlace, opponentIcon);
                        if (!gameIsOver)
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

        /// <summary>
        /// Akkor fut le, ha az ellenfél nyomta meg az "Új Játék" gombot.
        /// </summary>
        private void NetworkService_RematchReceived(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    Debug.WriteLine("Visszavágó kérés fogadva, tábla törlése...");

                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    ResetBoard();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATALIS Hiba a RematchReceived feldolgozása közben: {ex.Message}");
                    GoToMainMenu();
                }
            });
        }

        // Ez oldja fel a várakozást a 'ExecuteMainMenuAsync'-ban
        private void NetworkService_LeaveAcknowledged(object sender, EventArgs e)
        {
            Debug.WriteLine("LEAVE_ACK nyugta fogadva.");
            _leaveAckTcs?.TrySetResult(true);
        }

        /// <summary>
        /// Akkor fut le, ha a kapcsolat VÁRATLANUL megszakad (FOGADÁSI hiba).
        /// </summary>
        private void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            Debug.WriteLine("VÁRATLAN kapcsolatbontás (OpponentDisconnected).");
            ShowDisconnectionErrorAsync("Kapcsolat Megszakadt", $"Az ellenfél ({OpponentName}) váratlanul bontotta a kapcsolatot.");
        }

        /// <summary>
        /// Akkor fut le, ha az ellenfél nyomta meg a "Főmenü" gombot (kulturált kilépés).
        /// </summary>
        private void NetworkService_OpponentLeft(object sender, EventArgs e)
        {
            Debug.WriteLine("Ellenfél 'Főmenübe lépés' kérés fogadva (OpponentLeft). Dialógus megjelenítése...");
            ShowDisconnectionErrorAsync("Játék Vége", "Az ellenfél kilépett a főmenübe.");
        }

        private void NetworkService_ChatMessageReceived(object sender, string messageText)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                ChatHistory.Add(new ChatMessage
                {
                    Author = OpponentName,
                    Message = messageText,
                    IsMine = false, // Ez az ellenfél üzenete
                    Timestamp = DateTime.Now
                });
            });
        }

        /// Beállítja a Játék Vége állapotot és a győzelmi üzenetet.
        private void TriggerGameOver(string title, string message)
        {
            // HANG LEJÁTSZÁSA
            if (title == "Győzelem!")
            {
                Messenger.Default.Send(new PlaySoundMessage { SoundName = "Win" });
            }
            else if (title == "Vereség!")
            {
                Messenger.Default.Send(new PlaySoundMessage { SoundName = "Lose" });
            }

            // UI szálra váltunk
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                lock (_gameOverLock)
                {
                    if (IsGameOver) return; // Már lekezeltük
                    IsGameOver = true;
                }

                GameOverMessage = $"{title} {message}";

                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            });
        }

        /// <summary>
        /// Leállítja a hálózatot és visszanavigál a főmenübe.
        /// </summary>
        private void GoToMainMenu()
        {
            bool shouldNavigate = false;

            // 1. Atomikusan ellenőrizzük és beállítjuk a navigációs zárat
            lock (_gameOverLock)
            {
                if (!_isNavigatingToMenu)
                {
                    _isNavigatingToMenu = true;
                    shouldNavigate = true;
                    Debug.WriteLine("GoToMainMenu: Navigáció elindítva.");
                }
                else
                {
                    Debug.WriteLine("GoToMainMenu: Navigáció már folyamatban, kérés kihagyva.");
                }
            }

            if (!shouldNavigate)
            {
                return;
            }

            // 3. A Cleanup() gondoskodik a Disconnect-ről és a leiratkozásokról
            Cleanup();

            // 4. A navigációt és az előzmények törlését a UI szálon végezzük
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    if (!(Window.Current.Content is Frame rootFrame)) return;
                    _viewService?.OpenPage<MainViewModel>();
                    rootFrame.BackStack.Clear();
                    Debug.WriteLine("GoToMainMenu: Navigáció Főoldalra sikeres, előzmények törölve.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATALIS Hiba a GoToMainMenu navigációja közben: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Segédmetódus, ami VÁRATLAN hiba esetén felugró ablakot mutat,
        /// majd utána biztonságosan a Főmenübe navigál.
        /// </summary>
        private async void ShowDisconnectionErrorAsync(string title, string content)
        {
            await DispatcherHelper.RunAsync(async () =>
            {
                // 1. Zárolás
                lock (_gameOverLock)
                {
                    if (_isNavigatingToMenu) return;
                    IsGameOver = true;
                }

                Debug.WriteLine($"VÁRATLAN kapcsolatbontás: {content}");

                try
                {
                    // 2. Esetlegesen nyitva lévő (régi) dialógus bezárása
                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    // 3. HIBAÜZENET MEGJELENÍTÉSE
                    var dialog = new ContentDialog
                    {
                        Title = title,
                        Content = content,
                        PrimaryButtonText = "OK (Főmenü)"
                    };
                    await dialog.ShowAsync(); // Várjuk meg, amíg a felhasználó le-OK-zza
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hiba a 'Disconnection' dialógus megjelenítésekor: {ex.Message}");
                }
                finally
                {
                    // 4. NAVIGÁLÁS A FŐMENÜBE
                    GoToMainMenu();
                }
            });
        }

        /// <summary>
        /// Segédmetódus a váratlan hálózati leállás (pl. KÜLDÉSI hiba) kezelésére.
        /// </summary>
        private void HandleDisconnection(string message)
        {
            Debug.WriteLine($"HandleDisconnection fut (Hiba: {message}).");
            ShowDisconnectionErrorAsync("Hálózati Hiba", $"A művelet nem sikerült. A kapcsolat megszakadt.");
        }

        /// <summary>
        /// Ez a metódus CSAK a táblát törli és a köröket állítja be.
        /// </summary>
        private void ResetBoard()
        {
            try
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    // 2. TÁBLA TÖRLÉSE
                    foreach (var place in Places)
                    {
                        place.Type = IconType.None;
                        place.IsWinningCell = false;
                    }

                    // 3. KÖRÖK VISSZAÁLLÍTÁSA
                    IsPlayer1Turn = true;
                    IsPlayer2Turn = false;
                    isComputerTurn = false;
                    isProcessingAiMove = false;

                    // 4. ZÁROLÁS FELOLDÁSA
                    IsGameOver = false;

                    // 5. ÜZENET TÖRLÉSE
                    GameOverMessage = string.Empty;

                    // 6. NAVIGÁCIÓS ZÁR FELOLDÁSA
                    lock (_gameOverLock)
                    {
                        _isNavigatingToMenu = false;
                    }

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
                // Az összes eseményről leiratkozunk
                _networkService.GameStarted -= NetworkService_GameStarted;
                _networkService.MoveReceived -= NetworkService_MoveReceived;
                _networkService.OpponentDisconnected -= NetworkService_OpponentDisconnected;
                _networkService.RematchReceived -= NetworkService_RematchReceived;
                _networkService.OpponentLeft -= NetworkService_OpponentLeft;
                _networkService.LeaveAcknowledged -= NetworkService_LeaveAcknowledged;
                _networkService.ChatMessageReceived -= NetworkService_ChatMessageReceived;

                _networkService.Disconnect();
            }
            base.Cleanup();
        }
    }
}
