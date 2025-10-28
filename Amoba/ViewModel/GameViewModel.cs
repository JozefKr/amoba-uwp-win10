using Amoba.Model;
using Amoba.Services; // Szükséges az AiPlayer és a GameLogic eléréséhez
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics; // Hibakereséshez
using System.Linq;
using System.Threading.Tasks; // Aszinkron AI lépéshez
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameViewModel : ViewModelBase
    {
        private int player1Score;
        private int player2Score;

        private bool isPlayer1Turn;
        private bool isPlayer2Turn;
        private bool isComputerTurn = false;
        private bool isVsComputer; // Eltávolítottuk az alapértelmezett 'true'-t
        private bool isProcessingAiMove = false;

        private ObservableCollection<Place> places;
        private ICommand setImage;
        private ICommand newGameCommand;
        private int boardSize;
        private AiPlayer aiPlayer;

        public int BoardSize
        {
            get => boardSize;
            set => Set(ref boardSize, value);
        }

        // 1. Alapértelmezett konstruktor (Paraméter nélküli)
        public GameViewModel()
        {
            // Alapértelmezett mód: Játékos vs Játékos, 3x3
            InitializeViewModel(3, false);
        }

        // 2. Paraméteres konstruktor (Ezt hívja a ViewService)
        // MOST MÁR FOGADJA AZ isVsComputer PARAMÉTERT IS
        public GameViewModel(int boardSizeParam, bool isVsComputerParam)
        {
            InitializeViewModel(boardSizeParam, isVsComputerParam);
        }

        private void InitializeViewModel(int boardSizeParam, bool isVsComputerMode)
        {
            BoardSize = boardSizeParam;
            this.isVsComputer = isVsComputerMode; // Eltároljuk a kapott játékmódot
            aiPlayer = new AiPlayer();

            Places = new ObservableCollection<Place>();
            for (int i = 0; i < Math.Pow(this.BoardSize, 2); i++)
            {
                // Biztosítjuk, hogy az alapértelmezett Type az IconType.None legyen
                Places.Add(new Place() { Id = i, Type = IconType.None });
            }

            Player1Score = 0;
            Player2Score = 0;
            IsPlayer1Turn = true; // Ember kezd (X)
            IsPlayer2Turn = false;
            isComputerTurn = false; // Kezdetben nem a gép jön

            // Ha gép ellen játszunk ÉS a gép kezdene (O), itt lehetne indítani az AI-t
            // if (this.isVsComputer && !IsPlayer1Turn) { TriggerAiMove(); }
        }

        public int Player2Score
        {
            get => player2Score;
            set => Set(ref player2Score, value);
        }

        public int Player1Score
        {
            get => player1Score;
            set => Set(ref player1Score, value);
        }

        public bool IsPlayer1Turn
        {
            get => isPlayer1Turn;
            set => Set(ref isPlayer1Turn, value);
        }

        public bool IsPlayer2Turn
        {
            get => isPlayer2Turn;
            set
            {
                // Használjuk a Set metódust, ami csak akkor futtatja a logikát, ha az érték tényleg változik
                if (Set(ref isPlayer2Turn, value))
                {
                    // Ha gép ellen játszunk, és a 2. játékos (O, a gép) következik
                    isComputerTurn = isVsComputer && value;
                    if (isComputerTurn && !isProcessingAiMove) // Csak akkor indítjuk, ha nem fut már
                    {
                        // AI lépés indítása aszinkron módon
                        TriggerAiMove();
                    }
                }
            }
        }

        public ObservableCollection<Place> Places
        {
            get => places;
            // Itt is a Set metódust használjuk a RaisePropertyChanged biztosításához
            set => Set(ref places, value);
        }

        public ICommand SetImage
        {
            get
            {
                return setImage ??
                    (setImage = new RelayCommand<Place>(
                        SetImageMethod,
                        // CanExecute: Csak akkor engedélyezett, ha a mező üres ÉS nem az AI lépése van folyamatban ÉS (Játékos vs Játékos MÓD VAGY NEM a gép jön)
                        p => p != null && p.IsEmpty && !isProcessingAiMove && (!isVsComputer || !isComputerTurn)
                    ));
            }
        }

        public ICommand NewGameCommand
        {
            get
            {
                return newGameCommand ??
                    (newGameCommand = new RelayCommand(ExecuteNewGame));
            }
        }

        private void ExecuteNewGame()
        {
            // Ez a metódus visszaállít mindent, a pontszámokat is.
            Player1Score = 0;
            Player2Score = 0;
            ResetBoard(); // A ResetBoard már gondoskodik a tábla törléséről és a körök visszaállításáról
        }

        private void SetImageMethod(Place place)
        {
            // Extra védelem: Ha gép ellen játszunk és a gép jön, vagy már folyamatban van az AI lépés, vagy a hely érvénytelen, ne csináljunk semmit
            if ((isVsComputer && isComputerTurn) || isProcessingAiMove || place == null || !place.IsEmpty)
            {
                return;
            }

            // Emberi lépés végrehajtása
            ExecuteMove(place, IsPlayer1Turn ? IconType.Cross : IconType.Circle);
        }

        // AI lépés indítása (aszinkron, hogy a UI ne fagyjon le)
        private async void TriggerAiMove()
        {
            if (isProcessingAiMove || !isVsComputer || !isComputerTurn) return; // Extra védelem

            isProcessingAiMove = true;
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged(); // Gombok letiltása

            await Task.Delay(200); // Rövid várakozás a jobb UX érdekében

            try
            {
                Place bestMovePlace = aiPlayer.FindBestMove(new ObservableCollection<Place>(Places.Select(p => p.ClonePlace())), BoardSize); // Másolatot adunk át az AI-nak

                if (bestMovePlace != null)
                {
                    // Az eredeti kollekcióban keressük meg a megfelelő elemet az ID alapján
                    var targetPlace = Places.FirstOrDefault(p => p.Id == bestMovePlace.Id);
                    if (targetPlace != null && targetPlace.IsEmpty)
                    {
                        // Gép lépésének végrehajtása (mindig O)
                        ExecuteMove(targetPlace, IconType.Circle);
                    }
                    else
                    {
                        Debug.WriteLine("AI által választott lépés már foglalt vagy érvénytelen.");
                        // Itt lehetne alternatív lépést keresni, vagy csak kihagyni
                        if (!Places.All(p => !p.IsEmpty)) // Ha még nincs tele a tábla
                            ChangeTurn(); // Visszaadjuk a vezérlést az embernek? Vagy újra próbálkozzon az AI?
                    }
                }
                else
                {
                    Debug.WriteLine("AI nem talált lépést (valószínűleg döntetlen vagy hiba).");
                    // Ha nincs több lépés, a játék véget érhetett volna már az ExecuteMove-ban
                    if (!Places.All(p => !p.IsEmpty))
                        ChangeTurn(); // Visszaadjuk a vezérlést?
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI Hiba: {ex.Message}");
                if (!Places.All(p => !p.IsEmpty))
                    ChangeTurn(); // Hiba esetén is adjuk vissza a kört, ha még van hely
            }
            finally
            {
                isProcessingAiMove = false;
                (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged(); // Gombok engedélyezése
            }
        }


        // Közös lépés végrehajtó metódus (emberi és AI)
        private void ExecuteMove(Place place, IconType type)
        {
            // Itt már a helyes Place objektumot kapjuk (FirstOrDefault ellenőrzés után)
            if (place == null || !place.IsEmpty) return;

            place.Type = type; // Ez aktiválja a PropertyChanged-et a Place modellen belül
                               // place.IsEmpty = false; // Ezt a Place modell Type settere már megteheti

            // Frissítjük a parancs futtathatóságát (az éppen megnyomott gomb letiltása)
            (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged();

            // Győztes ellenőrzése
            var winner = GameLogic.CheckWinner(Places, BoardSize); // Statikus metódus használata
            var isBoardFull = Places.All(p => !p.IsEmpty);

            if (winner != IconType.None || isBoardFull)
            {
                // Játék vége üzenet (később lehet szebb UI)
                string message = winner == IconType.Cross ? "Player 1 Wins!" :
                                 winner == IconType.Circle ? (isVsComputer ? "Computer Wins!" : "Player 2 Wins!") :
                                 "It's a Draw!";
                Debug.WriteLine(message); // Ideiglenes kiírás

                // Pontszám növelése
                if (winner == IconType.Cross) Player1Score++;
                else if (winner == IconType.Circle) Player2Score++;

                // Tábla törlése - Helyesen, ObservableCollection elemeit módosítva
                ResetBoard();
            }
            else
            {
                // Következő kör
                ChangeTurn();
            }
        }

        // Tábla törlése helyesen ObservableCollection esetén
        private void ResetBoard()
        {
            // Rövid késleltetés, hogy a felhasználó lássa a végeredményt
            // Ezt egy szebb UI megoldással (pl. Dialog) kellene helyettesíteni
            Task.Delay(1500).ContinueWith(_ => // Növelt késleltetés
            {
                // UI szálra kell visszatérni a kollekció módosításához!
                GalaSoft.MvvmLight.Threading.DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    foreach (var place in Places)
                    {
                        place.Type = IconType.None; // Visszaállítjuk None-ra
                        // place.IsEmpty = true; // Ezt a Place modell Type settere megteheti
                    }
                    IsPlayer1Turn = true; // Ember kezd újra (általában)
                    IsPlayer2Turn = false;
                    isComputerTurn = false;
                    isProcessingAiMove = false;
                    (SetImage as RelayCommand<Place>)?.RaiseCanExecuteChanged(); // Gombok frissítése
                });
            });
        }


        private void ChangeTurn()
        {
            // Egyszerű váltás
            IsPlayer1Turn = !IsPlayer1Turn;
            // A IsPlayer2Turn Property settere automatikusan kezeli az AI indítását
            IsPlayer2Turn = !IsPlayer1Turn;
        }
    }
}