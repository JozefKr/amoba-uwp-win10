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
        private bool _isGameOver; // Ez jelzi, ha a ShowGameOverDialogAsync már fut
        private string _gameOverMessage;
        private readonly IViewService _viewService; // A Főmenübe navigáláshoz
        private ContentDialog _activeGameOverDialog = null;
        private readonly object _gameOverLock = new object();
        // Ez fogja jelezni, ha megérkezett a LEAVE_ACK
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
                    // Értesítjük a UI-t, hogy az inverz property is változott
                    RaisePropertyChanged(nameof(IsGameInProgress));
                }
            }
        }
        // Ez az IsGameOver fordítottja. Akkor true, amikor a játék Még FOLYIK.
        public bool IsGameInProgress => !IsGameOver;
        public string GameOverMessage { get => _gameOverMessage; set => Set(ref _gameOverMessage, value); }
        public ObservableCollection<Place> Places { get => places; set => Set(ref places, value); }

        // --- PARANCSOK ---
        public ICommand SetImage
        {
            get => setImage ?? (setImage = new RelayCommand<Place>(
                SetImageMethod,
                // CanExecute: Csak akkor engedélyezett, ha a játék NEM ért véget,
                // a mező üres, és a helyi/hálózati körünk van.
                p => p != null && p.IsEmpty &&
                     !isProcessingAiMove &&
                     !IsGameOver && // Ne engedjünk lépni, ha a dialógus már aktív
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

            // 1. ÁLLAPOTOK BEÁLLÍTÁSA A PARAMÉTEREKBŐL
            _isNetworkGame = isNetworkGameParam;
            _amIHost = isHostParam;

            // --- SAJÁT NÉV BEÁLLÍTÁSA ---
            MyPlayerName = "JÁTÉKOS (X)"; // Alapértelmezett
            if (isNetworkGameParam && !string.IsNullOrEmpty(myPlayerNameParam))
            {
                // Ha hálózati játék, és kaptunk nevet, felülírjuk
                MyPlayerName = myPlayerNameParam;
            }

            // =======================================================
            // EGYSÉGES ELLENFÉL NÉV BEÁLLÍTÁS ---
            // =======================================================
            // Mindegy, hogy Host vagy Kliens, ha kaptunk opponentNameParam-ot
            // a navigáció során, akkor azt használjuk.
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
                _networkService.RematchReceived += NetworkService_RematchReceived; // VISSZAVÁGÓ FOGADÁSA
                _networkService.OpponentLeft += NetworkService_OpponentLeft;
                _networkService.LeaveAcknowledged += NetworkService_LeaveAcknowledged;
                _networkService.ChatMessageReceived += NetworkService_ChatMessageReceived; // CHAT
            }

            // 3. TÁBLA INICIALIZÁLÁSA
            InitializeViewModel(boardSizeParam, isVsComputerParam);

            // 4. KÖR BEÁLLÍTÁSA (Alapértelmezett)
            // Helyi/AI módban P1 kezd.
            // Hálózati módban a GameStarted esemény ezt felülírja.
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
            // (A pontszámokat NEM nullázzuk)
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

                    // Várunk a nyugtára VAGY időtúllépésre
                    await Task.WhenAny(_leaveAckTcs.Task, Task.Delay(2000));

                    if (_leaveAckTcs.Task.IsCompleted)
                        Debug.WriteLine("ExecuteMainMenu: LEAVE_ACK megérkezett!");
                    else
                        Debug.WriteLine("ExecuteMainMenu: LEAVE_ACK időtúllépés!");
                }
                catch (Exception ex)
                {
                    // Ha a kulturált kilépés (LEAVE küldése) hibát dob, az NEM VÁRATLAN HIBA.
                    // A szándékunk továbbra is a Főmenübe lépés.
                    // Egyszerűen naplózzuk a hibát, és hagyjuk, hogy a kód
                    // tovább fusson a 'GoToMainMenu()' hívásra.
                    Debug.WriteLine($"Hiba a 'Leave' küldésekor (elkapva, de figyelmen kívül hagyva): {ex.Message}");
                }
            }

            // Ha nem hálózati játék, VAGY a hálózati küldés sikeres volt (nem dobott hibát),
            // akkor a normál GoToMainMenu-t hívjuk.
            GoToMainMenu();
        }

        private async void SetImageMethod(Place place)
        {
            try
            {
                if (place == null || !place.IsEmpty) return;
                IconType iconToUse = IsPlayer1Turn ? IconType.Cross : IconType.Circle;

                // A ChangeTurn() hívása az ExecuteMove-ból kikerült
                bool gameIsOver = await ExecuteMove(place, iconToUse);

                if (!gameIsOver)
                {
                    // =======================================================
                    // Ha a játék NEM ért véget (ez egy normál lépés volt):

                    // 1. Játsszuk le a "Click" hangot
                    Messenger.Default.Send(new PlaySoundMessage { SoundName = "Click" });

                    // 2. Váltsunk kört
                    ChangeTurn();
                    // =======================================================
                }
                // Ha a gameIsOver == true, akkor NEM küldünk "Click" hangot,
                // mert a 'TriggerGameOver' metódus (amit az 'ExecuteMove' hívott)
                // már elküldte a "Win" vagy "Lose" hangot.
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

            var winner = GameLogic.CheckWinner(Places, BoardSize);
            var isBoardFull = Places.All(p => !p.IsEmpty);

            if (winner != IconType.None || isBoardFull)
            {
                string title;
                string message;

                if (winner == IconType.Cross) // Játékos 1 (X) nyert
                {
                    // A cím (Title) a te nézőpontodból (EZ MÁR JÓ VOLT)
                    title = (!IsNetworkGame || AmIHost) ? "Győzelem!" : "Vereség!";

                    // =======================================================
                    // Megnézzük, ki az X.
                    // Ha helyi játék VAGY én vagyok a Host, akkor én (MyPlayerName) vagyok az X.
                    // Különben az ellenfél (OpponentName) az X.
                    // =======================================================
                    message = (!IsNetworkGame || AmIHost)
                                ? $"{MyPlayerName} Nyert!"
                                : $"{OpponentName} Nyert!";

                    Player1Score++;
                }
                else if (winner == IconType.Circle) // Játékos 2 (O) nyert
                {
                    // A cím (Title) a te nézőpontodból (EZ MÁR JÓ VOLT)
                    title = (IsNetworkGame && !AmIHost) ? "Győzelem!" : "Vereség!";

                    // =======================================================
                    // Megnézzük, ki az O.
                    // Ha helyi játék VAGY én vagyok a Host, akkor az ellenfél (OpponentName) az O.
                    // Különben (ha Kliens vagyok) én (MyPlayerName) vagyok az O.
                    // =======================================================
                    message = (!IsNetworkGame || AmIHost)
                                ? $"{OpponentName} Nyert!"
                                : $"{MyPlayerName} Nyert!";

                    Player2Score++;
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

            // Beviteli mező azonnali törlése
            ChatMessageInput = string.Empty;

            try
            {
                // 1. Elküldjük a hálózaton
                await _networkService.SendChatMessageAsync(messageToSend);

                // 2. Hozzáadjuk a saját előzményeinkhez
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
                // Hiba esetén visszaállítjuk a szöveget, hogy újra próbálhassa
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

        /// <summary>
        /// Ez az eseménykezelő fut le, amikor a hálózati játék ténylegesen elindul.
        /// Beállítja a szerepeket (Host/Kliens) és az ellenfél nevét.
        /// </summary>
        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            // A CheckBeginInvokeOnUI egy háttérszálról hívódhat meg.
            // A benne lévő kódnak "golyóállónak" kell lennie.
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    // 1. Állapot frissítése (IsNetworkGame már true)
                    AmIHost = e.IsHost; // Megerősíti a szerepkört
                    OpponentName = e.OpponentName; // Beállítja a nevet

                    // 2. KÖR BEÁLLÍTÁSA: A HOST KEZD!
                    IsPlayer1Turn = e.IsHost; // Ha én vagyok a Host (X), én kezdek.
                    IsPlayer2Turn = !e.IsHost; // Ha én vagyok a Kliens (O), az ellenfél (X) jön.

                    // 3. CANEXECUTE FRISSÍTÉSE (Ez a kulcs!)
                    // Frissítjük a gombok állapotát az új kör alapján.
                    (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
                }
                catch (Exception ex)
                {
                    // 4. HIBAKEZELÉS
                    // Ha bármi hiba történik a játék indításakor
                    // (pl. a CanExecute logikája hibát dob),
                    // azt itt elkapjuk és kulturáltan leállítjuk a játékot.
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
                // Csak akkor dolgozzuk fel a lépést, ha az ellenfél volt soron.
                // Ha én vagyok a Host (X) és a Játékos 2 (O) volt soron, VAGY
                // ha én vagyok a Kliens (O) és a Játékos 1 (X) volt soron.
                bool isOpponentsTurn = (AmIHost && IsPlayer2Turn) || (!AmIHost && IsPlayer1Turn);

                if (!isOpponentsTurn)
                {
                    // Csalási kísérlet vagy hálózati deszinkronizáció.
                    // Csendben figyelmen kívül hagyjuk a lépést.
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
        /// Bezárja a helyi dialógust és alaphelyzetbe állítja a táblát.
        /// </summary>
        private void NetworkService_RematchReceived(object sender, EventArgs e)
        {
            // A DispatcherHelper.CheckBeginInvokeOnUI egy UI-szálra küldött "fire-and-forget" hívás.
            // Bármilyen kivétel, ami a lambda-n belül történik, kezeletlen marad
            // és összeomlasztja az alkalmazást. Ezért a teljes belső logikát
            // try-catch blokkba kell tenni.
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    Debug.WriteLine("Visszavágó kérés fogadva, tábla törlése...");
                    //Player1Score = 0;
                    //Player2Score = 0;

                    // 1. Dialógus bezárása
                    // Bezárjuk a "Győzelem/Vereség" dialógust, hogy a tábla láthatóvá váljon.
                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    // 2. Helyi visszaállítás
                    // A ResetBoard() metódus (ami szintén a UI szálon fut)
                    // elvégzi a tábla törlését és a körök visszaállítását P1-re.
                    ResetBoard();
                }
                catch (Exception ex)
                {
                    // 3. Vészhelyzeti hibakezelés
                    // Elkapja, ha pl. a .Hide() vagy a ResetBoard() hibát dob
                    Debug.WriteLine($"FATALIS Hiba a RematchReceived feldolgozása közben: {ex.Message}");

                    // Ha a reset meghiúsul, valami nagyon elromlott.
                    // A legbiztonságosabb, ha visszaküldjük a felhasználót a főmenübe.
                    GoToMainMenu();
                }
            });
        }

        // Ez oldja fel a várakozást a ShowGameOverDialogAsync-ban
        private void NetworkService_LeaveAcknowledged(object sender, EventArgs e)
        {
            Debug.WriteLine("LEAVE_ACK nyugta fogadva.");
            // Jelezzük a TaskCompletionSource-nak, hogy megérkezett a válasz
            _leaveAckTcs?.TrySetResult(true);
        }

        /// <summary>
        /// Akkor fut le, ha a kapcsolat VÁRATLANUL megszakad (FOGADÁSI hiba).
        /// </summary>
        private void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            // Ez egy VÁRATLAN hiba (a kapcsolat váratlanul lezárult).
            // Mutatunk egy felugró ablakot.
            Debug.WriteLine("VÁRATLAN kapcsolatbontás (OpponentDisconnected).");
            ShowDisconnectionErrorAsync("Kapcsolat Megszakadt", $"Az ellenfél ({OpponentName}) váratlanul bontotta a kapcsolatot.");
        }

        /// <summary>
        /// Akkor fut le, ha az ellenfél nyomta meg a "Főmenü" gombot (kulturált kilépés).
        /// </summary>
        private void NetworkService_OpponentLeft(object sender, EventArgs e)
        {
            // Ez egy kulturált kilépés, de a felhasználót (aki nem kezdeményezte)
            // tájékoztatni kell róla.
            // A 'ShowDisconnectionErrorAsync' metódusunk tökéletes erre:
            // 1. Megjelenít egy dialógust (ez adja a "késleltetést").
            // 2. Az 'await' (várakozás) alatt a NetworkService el tudja küldeni a 'LEAVE_ACK'-ot.
            // 3. A 'finally' blokkja kezeli a 'GoToMainMenu'-t, miután a felhasználó "OK"-t nyomott.

            Debug.WriteLine("Ellenfél 'Főmenübe lépés' kérés fogadva (OpponentLeft). Dialógus megjelenítése...");

            // Nem "Hiba"-ként, hanem "Tájékoztatásként" hívjuk.
            ShowDisconnectionErrorAsync("Játék Vége", "Az ellenfél kilépett a főmenübe.");
        }

        private void NetworkService_ChatMessageReceived(object sender, string messageText)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // Hozzáadjuk az ellenfél üzenetét az előzményekhez
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
        /// NEM indít automatikus visszavágót.
        /// </summary>
        private void TriggerGameOver(string title, string message)
        {
            // =======================================================
            // HANG LEJÁTSZÁSA (GYŐZELEM/VERESÉG)
            // =======================================================
            if (title == "Győzelem!")
            {
                Messenger.Default.Send(new PlaySoundMessage { SoundName = "Win" });
            }
            else if (title == "Vereség!")
            {
                Messenger.Default.Send(new PlaySoundMessage { SoundName = "Lose" });
            }
            // (Döntetlennél nem játszunk hangot, hacsak nem adsz hozzá egy "Draw" esetet)

            // UI szálra váltunk
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                lock (_gameOverLock)
                {
                    if (IsGameOver) return; // Már lekezeltük
                    IsGameOver = true;
                }

                // 1. Beállítjuk az új UI property-t
                GameOverMessage = $"{title} {message}";

                // 2. Frissítjük a CanExecute állapotokat
                // (Letiltja a táblát, és aktiválja/deaktiválja az AppBar gombokat)
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

            // 2. Ha egy másik esemény már elindította a navigációt, nem teszünk semmit
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
                    // 4A. Lekérjük az alkalmazás fő navigációs Frame-jét
                    if (!(Window.Current.Content is Frame rootFrame)) return;

                    // 4B. A meglévő IViewService hívásával elnavigálunk.
                    // Ez biztosítja, hogy a MainViewModel -> MainPage konverzió működjön.
                    _viewService?.OpenPage<MainViewModel>();

                    // 4C. A NAVIGÁCIÓ UTÁN AZONNAL TÖRÖLJÜK AZ ELŐZMÉNYEKET
                    // Ez a kulcs: eltávolítja a GamePage-et a "vissza" listáról.
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
                    // Ha a navigáció már elindult (pl. egy másik hiba miatt),
                    // akkor ne mutassunk még egy dialógust.
                    if (_isNavigatingToMenu) return;

                    // Ha a játék még futott, zároljuk.
                    IsGameOver = true;

                    // FONTOS: A _isNavigatingToMenu zárat NEM itt állítjuk be,
                    // hanem a GoToMainMenu-re bízzuk, MIUTÁN a dialógus bezárult.
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
                    // Miután a felhasználó le-OK-zta, a GoToMainMenu-re bízzuk
                    // a tiszta navigációt, zárolást és takarítást.
                    GoToMainMenu();
                }
            });
        }

        /// <summary>
        /// Segédmetódus a váratlan hálózati leállás (pl. KÜLDÉSI hiba) kezelésére.
        /// </summary>
        private void HandleDisconnection(string message)
        {
            // Ez egy VÁRATLAN hiba (pl. küldés meghiúsult).
            // Mutatunk egy felugró ablakot.
            Debug.WriteLine($"HandleDisconnection fut (Hiba: {message}).");
            ShowDisconnectionErrorAsync("Hálózati Hiba", $"A művelet nem sikerült. A kapcsolat megszakadt.");
        }

        /// <summary>
        /// Ez a metódus CSAK a táblát törli és a köröket állítja be.
        /// NEM bontja a hálózati kapcsolatot (azt a Cleanup() végzi).
        /// </summary>
        private void ResetBoard()
        {
            try
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    // 1. DIALÓGUS BEZÁRÁSA (Kritikus a versenyhelyzet elkerüléséhez)
                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    // 2. TÁBLA TÖRLÉSE
                    foreach (var place in Places) { place.Type = IconType.None; }

                    // 3. KÖRÖK VISSZAÁLLÍTÁSA (Mindig P1/Host kezd)
                    IsPlayer1Turn = true;
                    IsPlayer2Turn = false;

                    isComputerTurn = false;
                    isProcessingAiMove = false;

                    // 4. ZÁROLÁS FELOLDÁSA
                    // FONTOS: Az IsGameOver-t false-ra állítjuk,
                    // hogy a következő játék elindulhasson.
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