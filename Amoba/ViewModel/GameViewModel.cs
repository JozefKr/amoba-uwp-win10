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

        // JAVÍTVA: Ez a "Visszavágó" gomb logikája
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

                // JAVÍTVA: A ChangeTurn() hívása az ExecuteMove-ból kikerült
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

        // JAVÍTVA: A metódus 'async Task<bool>' -t ad vissza
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

        // Ez csak a neveket és a köröket állítja be a játék elején
        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                AmIHost = e.IsHost;
                OpponentName = e.OpponentName;
                IsPlayer1Turn = e.IsHost; // Host (X) kezd
                IsPlayer2Turn = !e.IsHost;
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
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

        // Ez fogadja az ellenfél visszavágó kérését
        private void NetworkService_RematchReceived(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Debug.WriteLine("Visszavágó kérés fogadva, tábla törlése...");
                Player1Score = 0;
                Player2Score = 0;

                // Mielőtt resetelünk, itt is be kell zárni a dialógust,
                // ugyanúgy, ahogy a ResetBoard() tenné.
                if (_activeGameOverDialog != null)
                {
                    _activeGameOverDialog.Hide();
                    _activeGameOverDialog = null;
                }

                ResetBoard(); // Lefuttatja a helyi visszaállítást
            });
        }

        private async void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            // ZÁROLÁS ELLENŐRZÉSE ---
            // Ha a játék már véget ért (pl. a ShowGameOverDialogAsync már fut),
            // akkor már nem kell "Kapcsolat Megszakadt" üzenetet mutatni.
            // A ShowGameOverDialog majd kezeli a Főmenübe lépést.
            if (IsGameOver) return;
            IsGameOver = true; // "ZÁROLJUK"

            await DispatcherHelper.RunAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Kapcsolat Megszakadt",
                    Content = $"Az ellenfél ({OpponentName}) kilépett a játékból.",
                    PrimaryButtonText = "Főmenü" // 14393 kompatibilis
                };
                await dialog.ShowAsync();
                GoToMainMenu();
            });
        }

        // Ez a felugró ablak a játék végén
        private async Task ShowGameOverDialogAsync(string title, string message)
        {
            if (IsGameOver) return; // Ne mutassuk kétszer (ha mindkét gép egyszerre észleli)
            IsGameOver = true;

            await DispatcherHelper.RunAsync(async () =>
            {
                _activeGameOverDialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = "Visszavágó",
                    SecondaryButtonText = "Főmenü",
                };

                var result = await _activeGameOverDialog.ShowAsync();
                _activeGameOverDialog = null; // Töröljük a referenciát, miután bezárult

                if (result == ContentDialogResult.Primary)
                {
                    ExecuteNewGameAsync(); // "Visszavágó"
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // A FELHASZNÁLÓ nyomott "Főmenü"-t
                    GoToMainMenu();
                }
                // Ha a result == ContentDialogResult.None (mert Hide() zárta be),
                // akkor NEM CSINÁLUNK SEMMIT, mert a ResetBoard már lefutott.
            });
        }

        private void GoToMainMenu()
        {
            Cleanup(); // Leállítja a hálózatot
            // Főoldalra navigálás
            _viewService?.OpenPage<MainViewModel>();
        }

        // Segédmetódus a váratlan leállás kezelésére
        private void HandleDisconnection(string message)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (IsGameOver) return;
                IsGameOver = true;
                GameOverMessage = message;
                _networkService.Disconnect();
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            });
        }

        // Ez a metódus már CSAK a táblát törli és a köröket állítja be
        private void ResetBoard()
        {
            try
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    foreach (var place in Places) { place.Type = IconType.None; }

                    // A köröket mindig P1-re állítjuk vissza ---
                    // Hálózati módban a CanExecute (AmIHost) kezeli a tiltást.
                    IsPlayer1Turn = true;
                    IsPlayer2Turn = false;
                    isComputerTurn = false;
                    isProcessingAiMove = false;
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

                _networkService.Disconnect();
            }
            base.Cleanup();
        }
    }
}