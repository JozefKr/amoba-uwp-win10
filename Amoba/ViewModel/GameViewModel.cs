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
using Windows.UI.Xaml.Controls; // Szükséges a ContentDialog-hoz

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

        // --- HÁLÓZATI MEZŐK ---
        private readonly INetworkService _networkService;
        private bool _isNetworkGame = false;
        private bool _amIHost = false;
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

        // ===================================================================
        // --- KONSTRUKTOR ÉS INITIALIZÁLÁS ---
        // ===================================================================

        public GameViewModel(IViewService viewService, int boardSizeParam, bool isVsComputerParam, bool isNetworkGameParam, bool isHostParam, INetworkService networkService)
        {
            _viewService = viewService;
            _networkService = networkService;

            // 1. ÁLLAPOTOK BEÁLLÍTÁSA A PARAMÉTEREKBŐL
            _isNetworkGame = isNetworkGameParam;
            _amIHost = isHostParam;

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
            Player1Score = 0;
            Player2Score = 0;

            if (IsNetworkGame && _networkService != null)
            {
                try
                {
                    // 1. Elküldjük a VISSZAVÁGÓ kérést
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

        private async void SetImageMethod(Place place)
        {
            try
            {
                if (place == null || !place.IsEmpty) return;
                IconType iconToUse = IsPlayer1Turn ? IconType.Cross : IconType.Circle;

                // A ChangeTurn() hívása az ExecuteMove-ból kikerült
                bool gameIsOver = await ExecuteMove(place, iconToUse);

                if (!gameIsOver) // Csak akkor váltunk kört, ha a játék NEM ért véget
                {
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

            var winner = GameLogic.CheckWinner(Places, BoardSize);
            var isBoardFull = Places.All(p => !p.IsEmpty);

            if (winner != IconType.None || isBoardFull)
            {
                string title;
                string message;
                if (winner == IconType.Cross) { title = "Győzelem!"; message = "Játékos (X) Nyert!"; Player1Score++; }
                else if (winner == IconType.Circle) { title = isVsComputer ? "Vereség!" : "Játékos (O) Nyert!"; message = isVsComputer ? "A Gép Nyert!" : "Játékos (O) Nyert!"; Player2Score++; }
                else { title = "Döntetlen!"; message = "A tábla megtelt!"; }

                // Játék vége: Megjelenítjük a dialógust
                await ShowGameOverDialogAsync(title, message);
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
                    Player1Score = 0;
                    Player2Score = 0;

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

        // GameViewModel.cs

        private async void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            // Ez az esemény a "VÁRATLAN" leállás (pl. "forcibly closed").
            // Ennek a logikának felül kell írnia a normál játék végét.

            await DispatcherHelper.RunAsync(async () =>
            {
                // ZÁROLJUK, hogy más (pl. a ShowGameOverDialog) ne fusson le
                lock (_gameOverLock)
                {
                    if (IsGameOver)
                    {
                        // A játék már véget ért (dialógus látszik),
                        // de a kapcsolat váratlanul megszakadt.
                        // Nem lépünk ki, csak jelezzük, hogy a zárolás már aktív.
                    }
                    else
                    {
                        // A játék még futott, most zároljuk.
                        IsGameOver = true;
                    }
                }

                try
                {
                    Debug.WriteLine("VÁRATLAN kapcsolatbontás (OpponentDisconnected). Dialógusok bezárása és Főmenü.");

                    // BEZÁRJUK a "Visszavágó/Főmenü" dialógust, ha az nyitva volt
                    // Ez akadályozta meg a 'GoToMainMenu'-t a korábbi logikában.
                    if (_activeGameOverDialog != null)
                    {
                        Debug.WriteLine("Aktív 'Game Over' dialógus bezárása...");
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    //  Mutatunk egy "Kapcsolat megszakadt" dialógust
                    var dialog = new ContentDialog
                    {
                        Title = "Kapcsolat Megszakadt",
                        Content = $"Az ellenfél ({OpponentName}) váratlanul bontotta a kapcsolatot.",
                        PrimaryButtonText = "OK (Főmenü)"
                    };
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    // Elkapja, ha a .Hide() vagy .ShowAsync() hibát dob
                    Debug.WriteLine($"Hiba a 'OpponentDisconnected' dialógus megjelenítésekor: {ex.Message}");
                }
                finally
                {
                    // 5. NAVIGÁLUNK A FŐMENÜBE
                    // Akár sikeres volt a dialógus, akár nem, a játék véget ért.
                    GoToMainMenu();
                }
            });
        }

        /// <summary>
        /// Akkor fut le, ha az ellenfél nyomta meg a "Főmenü" gombot.
        /// </summary>
        private async void NetworkService_OpponentLeft(object sender, EventArgs e)
        {
            // 2. VÁLTÁS A UI SZÁLRA
            await DispatcherHelper.RunAsync(async () =>
            {
                // A zárolás logikája megváltozott.
                // Nem 'return'-ölünk, ha 'IsGameOver' true, hanem
                // csak beállítjuk, hogy biztosan 'true' legyen.
                lock (_gameOverLock)
                {
                    if (IsGameOver)
                    {
                        // A 'ShowGameOverDialog' már fut, ez rendben van.
                        // A feladatunk, hogy bezárjuk azt a dialógust.
                    }
                    else
                    {
                        // Ha a játék még futott (nem volt dialógus), most zároljuk.
                        IsGameOver = true;
                    }
                }
                Debug.WriteLine("Ellenfél 'Főmenübe lépés' kérés fogadva (OpponentLeft).");

                try
                {
                    // 3. MEGLÉVŐ DIALÓGUS BEZÁRÁSA
                    // Ha a "Visszavágó/Főmenü" dialógus nyitva volt, bezárjuk.
                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    // 4. ÚJ, TÁJÉKOZTATÓ DIALÓGUS MEGJELENÍTÉSE
                    // (Ez a lépés opcionális, de jobb UX, mint a csendes kilépés)
                    var dialog = new ContentDialog
                    {
                        Title = "Játék Vége",
                        Content = "Az ellenfél kilépett a főmenübe.",
                        PrimaryButtonText = "OK (Főmenü)"
                    };

                    // 5. A dialógus megjelenítése (ez dobhat kivételt)
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    // 6. HIBAKEZELÉS
                    // Elkapja, ha a .Hide() vagy .ShowAsync() hibát dob
                    Debug.WriteLine($"Hiba a 'NetworkService_OpponentLeft' dialógus kezelésekor: {ex.Message}");
                    // A hiba ellenére is a főmenübe kell lépnünk.
                }
                finally
                {
                    // 7. NAVIGÁCIÓ A FŐMENÜBE
                    // Akár sikeres volt a dialógus, akár nem, a játék véget ért.
                    GoToMainMenu();
                }
            });
        }

        // Ez a felugró ablak a játék végén
        private async Task ShowGameOverDialogAsync(string title, string message)
        {
            await DispatcherHelper.RunAsync(async () =>
            {
                bool navigateToMenu = false; // Jelző, hogy a főmenübe kell-e lépni

                lock (_gameOverLock)
                {
                    if (IsGameOver) return;
                    IsGameOver = true;
                }

                try
                {
                    _activeGameOverDialog = new ContentDialog
                    {
                        Title = title,
                        Content = message,
                        PrimaryButtonText = "Visszavágó",
                        SecondaryButtonText = "Főmenü",
                    };

                    var result = await _activeGameOverDialog.ShowAsync();
                    _activeGameOverDialog = null;

                    if (result == ContentDialogResult.Primary)
                    {
                        // "VISSZAVÁGÓ"
                        ExecuteNewGameAsync();
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // "FŐMENÜ"
                        navigateToMenu = true; // Jelezzük, hogy a főmenübe akarunk lépni

                        // 5. TISZTA KILÉPÉSI ÜZENET KÜLDÉSE
                        if (IsNetworkGame && _networkService != null)
                        {
                            try
                            {
                                // 1. Létrehozunk egy "jelzőt", amire várunk
                                _leaveAckTcs = new TaskCompletionSource<bool>();

                                // 2. Elküldjük a LEAVE üzenetet
                                Debug.WriteLine("ShowGameOver: LEAVE üzenet küldése...");
                                await _networkService.SendLeaveGameAsync();

                                // 3. Várunk az ACK-ra (LeaveAcknowledged esemény)
                                // VAGY várunk max 2 másodpercet (timeout)
                                Debug.WriteLine("ShowGameOver: Várakozás a LEAVE_ACK nyugtára (max 2s)...");
                                await Task.WhenAny(_leaveAckTcs.Task, Task.Delay(2000));

                                if (_leaveAckTcs.Task.IsCompleted)
                                    Debug.WriteLine("ShowGameOver: LEAVE_ACK megérkezett!");
                                else
                                    Debug.WriteLine("ShowGameOver: LEAVE_ACK időtúllépés!");
                            }
                            catch (Exception ex)
                            {
                                // Ez fogja biztosítani, hogy a 'finally' blokk ne fusson le
                                // (mert a HandleDisconnection már elnavigál).
                                // VAGY ha mégis lefut, a Cleanup már megtörtént.
                                HandleDisconnection("Hiba a kilépés jelzésekor.");

                                // Fontos: Mivel a HandleDisconnection már elnavigál,
                                // megakadályozzuk, hogy a 'finally' blokk is megpróbálja.
                                navigateToMenu = false;
                            }
                        }
                    }
                    // Ha a result == ContentDialogResult.None (mert Hide() zárta be)
                    // akkor nem csinálunk semmit.
                }
                catch (Exception ex)
                {
                    // 7. ÁLTALÁNOS HIBAKEZELÉS (ha a ShowAsync hibát dob)
                    Debug.WriteLine($"FATALIS Hiba a ShowGameOverDialogAsync-ban: {ex.Message}");
                    if (IsNetworkGame)
                    {
                        HandleDisconnection("Váratlan hiba a dialógusban (pl. Hide).");
                        navigateToMenu = false;
                    }
                    // Ha nem hálózati játék, akkor a régi viselkedés marad (finally visz tovább)
                    else
                    {
                        navigateToMenu = true;
                    }
                }
                finally
                {
                    // 8. VÉGREHAJTÁS
                    // Ez a blokk garantálja, hogy a navigáció CSAK a hálózati
                    // műveletek (vagy hibák) UTÁN történik meg.
                    if (navigateToMenu)
                    {
                        GoToMainMenu(); // TAKARÍTÁS ÉS NAVIGÁLÁS
                    }
                }
            });
        }

        /// <summary>
        /// Leállítja a hálózatot és visszanavigál a főmenübe.
        /// </summary>
        private void GoToMainMenu()
        {
            // A Cleanup() gondoskodik a Disconnect-ről és a leiratkozásokról
            Cleanup();

            // Visszanavigálunk a főoldalra
            _viewService?.OpenPage<MainViewModel>();
        }

        // GameViewModel.cs

        /// <summary>
        /// Segédmetódus a váratlan hálózati leállás (pl. küldési hiba) egységes kezelésére.
        /// Zárolja a játékot és visszanavigál a főmenübe.
        /// </summary>
        private void HandleDisconnection(string message)
        {
            // 2. VÁLTÁS A UI SZÁLRA
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // 1. ATOMIKUS ZÁROLÁS
                // Megakadályozza a ShowGameOverDialogAsync-val (játék vége) való versenyhelyzetet.
                lock (_gameOverLock)
                {
                    if (IsGameOver) return; // Egy másik dialógus (játék vége) már aktív.
                    IsGameOver = true;      // Zároljuk.
                }

                // Ez a "végső mentsvár". Ha a GoToMainMenu() hibát dob
                // (pl. a ViewService null, vagy a Cleanup hibázik),
                // az alkalmazás legalább nem omlik össze kezeletlen kivétel miatt.
                try
                {
                    Debug.WriteLine($"HandleDisconnection fut (Hiba: {message}). Navigálás a Főmenübe.");

                    // Bezárjuk az esetleg nyitva maradt "Visszavágó" dialógust
                    if (_activeGameOverDialog != null)
                    {
                        _activeGameOverDialog.Hide();
                        _activeGameOverDialog = null;
                    }

                    // Mivel ez egy váratlan hiba (nem kulturált kilépés),
                    // azonnal a főmenübe lépünk.
                    GoToMainMenu(); // Ez hívja a Cleanup()-ot és a Disconnect()-et
                }
                catch (Exception ex)
                {
                    // Ha még a főmenübe navigálás is hibát dob,
                    // akkor már csak naplózni tudjuk a végzetes hibát.
                    Debug.WriteLine($"FATALIS Hiba a HandleDisconnection végrehajtása közben: {ex.Message}");
                    // Ezen a ponton az alkalmazás valószínűleg instabil állapotban van,
                    // de legalább nem omlott össze a Dispatcher-en belül.
                }
            });
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

                _networkService.Disconnect();
            }
            base.Cleanup();
        }
    }
}