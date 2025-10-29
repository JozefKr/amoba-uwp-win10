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
                    // ELLENŐRIZZÜK, HOGY P2 JÖN-E, ÉS GÉP ELLEN JÁTSZUNK-E
                    if (isVsComputer && value)
                    {
                        // **KRITIKUS HIBA JAVÍTÁSA:** Beállítjuk a gépi kör állapotot
                        isComputerTurn = true;
                        TriggerAiMove();
                    }
                    else
                    {
                        isComputerTurn = false;
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
                     !isProcessingAiMove &&

                     // ENGEDÉLYEZÉS, ha a soron lévő játékos ember és saját maga:
                     (
                        // ESET 1: HÁLÓZATI JÁTÉK
                        (IsNetworkGame && ((AmIHost && IsPlayer1Turn) || (!AmIHost && IsPlayer2Turn))) ||

                        // ESET 2: HELYI JÁTÉK (AI ellen VAGY PVP)
                        // Engedélyezzük, ha P1 jön (mindig ember) VAGY P2 jön, de nem gép
                        (!IsNetworkGame && (IsPlayer1Turn || (IsPlayer2Turn && !isVsComputer)))
                     )
            ));
        }
        public ICommand NewGameCommand => newGameCommand ?? (newGameCommand = new RelayCommand(ExecuteNewGame));


        // ===================================================================
        // --- KONSTRUKTOR ÉS INITIALIZÁLÁS ---
        // ===================================================================

        public GameViewModel(int boardSizeParam, bool isVsComputerParam, INetworkService networkService)
        {
            _networkService = networkService;

            if (_networkService != null)
            {
                // Feliratkozás a hálózati eseményekre
                _networkService.GameStarted += NetworkService_GameStarted;
                _networkService.MoveReceived += NetworkService_MoveReceived;
                _networkService.OpponentDisconnected += NetworkService_OpponentDisconnected;
            }

            InitializeViewModel(boardSizeParam, isVsComputerParam);
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
            IsPlayer1Turn = true;
            IsPlayer2Turn = false;
            isComputerTurn = false;
            IsGameOver = false;
            IsNetworkGame = false; // Alapértelmezés: Nem hálózati

            // OpponentName beállítása
            if (isVsComputerMode)
            {
                OpponentName = "A GÉP";
            }
            else
            {
                OpponentName = "JÁTÉKOS (O)";
            }
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

        private void SetImageMethod(Place place)
        {
            if (place == null || !place.IsEmpty) return;

            IconType iconToUse = IsPlayer1Turn ? IconType.Cross : IconType.Circle;
            ExecuteMove(place, iconToUse);
        }

        private void ExecuteMove(Place place, IconType type)
        {
            if (place == null || !place.IsEmpty) return;

            place.Type = type;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();

            // HÁLÓZATI LÉPÉS KÜLDÉSE:
            if (IsNetworkGame && (type == IconType.Cross && AmIHost || type == IconType.Circle && !AmIHost))
            {
                Task.Run(async () => await _networkService.SendMoveAsync(place.Id));
            }

            // GYŐZELEM ELLENŐRZÉSE
            var winner = GameLogic.CheckWinner(Places, BoardSize);
            var isBoardFull = Places.All(p => !p.IsEmpty);

            if (winner != IconType.None || isBoardFull)
            {
                if (winner == IconType.Cross) { GameOverMessage = "Játékos (X) Nyert!"; Player1Score++; }
                else if (winner == IconType.Circle) { GameOverMessage = isVsComputer ? "A Gép Nyert!" : "Játékos (O) Nyert!"; Player2Score++; }
                else { GameOverMessage = "Döntetlen!"; }

                IsGameOver = true;
                ResetBoard();
            }
            else
            {
                ChangeTurn();
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
                    ExecuteMove(bestMovePlace, IconType.Circle);
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
            IsPlayer2Turn = !IsPlayer1Turn;

            // Ha P2Turn van és gép ellen játszunk, akkor isComputerTurn lesz igaz,
            // és ez automatikusan elindítja az AI-t a frissített IsPlayer2Turn property-ben.
        }

        // ===================================================================
        // --- HÁLÓZATI ESEMÉNYKEZELŐK ÉS TAKARÍTÁS ---
        // ===================================================================

        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                IsNetworkGame = true;
                AmIHost = e.IsHost;
                OpponentName = e.OpponentName; // Hálózati ellenfél neve

                BoardSize = e.BoardSize;
                // Inicializálás hálózati módban (isVsComputer=false)
                InitializeViewModel(e.BoardSize, false);

                IsPlayer1Turn = AmIHost; // Host (X) kezd
                IsPlayer2Turn = !AmIHost;
            });
        }

        private void NetworkService_MoveReceived(object sender, int moveIndex)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                IconType opponentIcon = AmIHost ? IconType.Circle : IconType.Cross;
                var targetPlace = Places.FirstOrDefault(p => p.Id == moveIndex);

                if (targetPlace != null && targetPlace.IsEmpty)
                {
                    ExecuteMove(targetPlace, opponentIcon);
                }
            });
        }

        private void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                IsGameOver = true;
                GameOverMessage = $"A játék megszakadt: {OpponentName} kilépett.";
            });
        }

        private async void ResetBoard()
        {
            await Task.Delay(1500);

            if (IsNetworkGame)
            {
                _networkService.Disconnect();
            }

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                foreach (var place in Places) { place.Type = IconType.None; }

                // Visszaállítás alapértelmezett, nem hálózati állapotra
                IsPlayer1Turn = true;
                IsPlayer2Turn = false;
                isComputerTurn = false;
                isProcessingAiMove = false;
                IsNetworkGame = false;

                IsGameOver = false;
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            });
        }

        public override void Cleanup()
        {
            _networkService.GameStarted -= NetworkService_GameStarted;
            _networkService.MoveReceived -= NetworkService_MoveReceived;
            _networkService.OpponentDisconnected -= NetworkService_OpponentDisconnected;

            _networkService.Disconnect();

            base.Cleanup();
        }
    }
}