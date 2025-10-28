using Amoba.Model;
using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
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
        // ... (A tulajdonságok: player1Score, places, isGameOver stb. változatlanok) ...
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

        // ... (A publikus property-k: Player1Score, Places, IsGameOver stb. változatlanok) ...
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
        public bool IsGameOver { get => _isGameOver; set => Set(ref _isGameOver, value); }
        public string GameOverMessage { get => _gameOverMessage; set => Set(ref _gameOverMessage, value); }
        public ObservableCollection<Place> Places { get => places; set => Set(ref places, value); }
        public ICommand SetImage
        {
            get => setImage ?? (setImage = new RelayCommand<Place>(SetImageMethod,
                        p => p != null && p.IsEmpty && !isProcessingAiMove && (!isVsComputer || !isComputerTurn)));
        }
        public ICommand NewGameCommand
        {
            get => newGameCommand ?? (newGameCommand = new RelayCommand(ExecuteNewGame));
        }

        // --- Konstruktorok és Inicializálás (változatlan) ---
        public GameViewModel()
        {
            InitializeViewModel(3, false);
        }
        public GameViewModel(int boardSizeParam, bool isVsComputerParam)
        {
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
            IsGameOver = false; // Kezdetben nincs vége a játéknak
        }

        // --- Parancsok végrehajtói (ExecuteNewGame, SetImageMethod változatlan) ---
        private void ExecuteNewGame()
        {
            Player1Score = 0;
            Player2Score = 0;
            ResetBoard();
        }
        private void SetImageMethod(Place place)
        {
            if ((isVsComputer && isComputerTurn) || isProcessingAiMove || place == null || !place.IsEmpty)
            {
                return;
            }
            ExecuteMove(place, IsPlayer1Turn ? IconType.Cross : IconType.Circle);
        }


        // ===================================================================
        // --- TriggerAiMove MÓDOSÍTVA ---
        // ===================================================================
        private async void TriggerAiMove()
        {
            if (isProcessingAiMove || !isVsComputer || !isComputerTurn) return;

            isProcessingAiMove = true;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();

            await Task.Delay(200);

            try
            {
                // JAVÍTÁS: Nincs többé szükség klónozásra. Az új AiPlayer
                // nem módosítja az eredeti 'Places' kollekciót.
                Place bestMovePlace = aiPlayer.FindBestMove(Places, BoardSize);

                if (bestMovePlace != null)
                {
                    // Az AI által visszaadott Place objektum az EREDETI kollekcióból származik.
                    // Közvetlenül használhatjuk. (Nincs szükség ID alapján keresésre sem).
                    if (bestMovePlace.IsEmpty) // Dupla ellenőrzés (elvileg felesleges)
                    {
                        ExecuteMove(bestMovePlace, IconType.Circle);
                    }
                    else
                    {
                        Debug.WriteLine("AI által választott lépés már foglalt vagy érvénytelen (váratlan hiba!).");
                        if (!Places.All(p => !p.IsEmpty))
                            ChangeTurn();
                    }
                }
                else
                {
                    Debug.WriteLine("AI nem talált lépést (valószínűleg döntetlen vagy hiba).");
                    if (!Places.All(p => !p.IsEmpty))
                        ChangeTurn();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI Hiba: {ex.Message}");
                if (!Places.All(p => !p.IsEmpty))
                    ChangeTurn();
            }
            finally
            {
                isProcessingAiMove = false;
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            }
        }
        // ===================================================================
        // --- TriggerAiMove VÉGE ---
        // ===================================================================


        // --- ExecuteMove (változatlan, a CheckWinner hívás már jó volt) ---
        private void ExecuteMove(Place place, IconType type)
        {
            if (place == null || !place.IsEmpty) return;

            place.Type = type;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();

            // Ez a hívás már helyes, mert ObservableCollection -> IReadOnlyList konverzió működik.
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

        // --- ResetBoard (változatlan) ---
        private async void ResetBoard()
        {
            await Task.Delay(1500);

            GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                foreach (var place in Places) { place.Type = IconType.None; }
                IsPlayer1Turn = true;
                IsPlayer2Turn = false;
                isComputerTurn = false;
                isProcessingAiMove = false;
                IsGameOver = false; // Overlay elrejtése
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();
            });
        }

        // --- ChangeTurn (változatlan) ---
        private void ChangeTurn()
        {
            IsPlayer1Turn = !IsPlayer1Turn;
            IsPlayer2Turn = !IsPlayer1Turn;
        }
    }
}