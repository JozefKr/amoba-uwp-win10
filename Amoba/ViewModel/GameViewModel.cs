using Amoba.Model;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameViewModel : ViewModelBase
    {
        private int player1Score;
        private int player2Score;
        private bool isPlayer1Turn;
        private bool isPlayer2Turn;
        private ObservableCollection<Place> places;
        private ICommand setImage;
        private int boardSize;

        public int BoardSize
        {
            get => boardSize;
            set => Set(ref boardSize, value);
        }

        public GameViewModel()
        {
            InitializeViewModel(3);
        }

        public GameViewModel(int boardSizeParam)
        {
            InitializeViewModel(boardSizeParam);
        }

        private void InitializeViewModel(int boardSizeParam)
        {
            this.BoardSize = boardSizeParam;
            Places = new ObservableCollection<Place>();
            for (int i = 0; i < Math.Pow(this.BoardSize, 2); i++)
            {
                Places.Add(new Place() { Id = i, Type = null });
            }
            IsPlayer1Turn = true;
            IsPlayer2Turn = false;
            Player1Score = 0;
            Player2Score = 0;
        }

        public int Player2Score
        {
            get { return player2Score; }
            set { player2Score = value; RaisePropertyChanged(); }
        }

        public int Player1Score
        {
            get { return player1Score; }
            set { player1Score = value; RaisePropertyChanged(); }
        }

        public bool IsPlayer1Turn
        {
            get { return isPlayer1Turn; }
            set { isPlayer1Turn = value; RaisePropertyChanged(); }
        }

        public bool IsPlayer2Turn
        {
            get { return isPlayer2Turn; }
            set { isPlayer2Turn = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<Place> Places
        {
            get => places;
            set { places = value; RaisePropertyChanged(); }
        }

        public ICommand SetImage
        {
            get
            {
                return setImage ??
                    (setImage = new RelayCommand<Place>(SetImageMethod, p => { return p != null && p.IsEmpty; }));
            }
        }

        private void SetImageMethod(Place place)
        {
            //Single() helyett FirstOrDefault() használata biztonságosabb
            var pl = Places.FirstOrDefault(z => z.Id == place.Id);
            if (pl == null || !pl.IsEmpty) // Ellenőrizzük, hogy a mező létezik és üres-e
            {
                return; // Ha nem, ne csináljunk semmit
            }

            pl.IsEmpty = false;
            pl.Type = IsPlayer1Turn ? IconType.Cross : IconType.Circle;

            var winner = CheckWinner();
            //A tábla törlését külön metódusba szervezzük a jobb olvashatóságért
            if (winner != -1 || Places.All(z => !z.IsEmpty)) // Használhatjuk az All()-t a tele tábla ellenőrzésére
            {
                if (winner == 1) Player1Score++;
                else if (winner == 2) Player2Score++;

                ResetBoard(); // Tábla törlése és újraindítás
            }
            else
            {
                ChangeTurn(); // Csak akkor váltunk kört, ha nincs vége a játéknak
            }

            // JAVÍTÁS: Ez a sor hibás volt, ObservableCollection-t nem lehet így frissíteni.
            // Az ObservableCollection magától kezeli a UI frissítést, ha az elemei változnak (INotifyPropertyChanged),
            // vagy ha elemeket adunk hozzá/törlünk.
            // Places = new ObservableCollection<Place>(Places); // <-- TÖRÖLVE!
        }

        // ÚJ Metódus: A tábla törlése és alaphelyzetbe állítása
        private void ResetBoard()
        {
            // A Clear() helyett a foreach ciklus is működik, mert a Place implementálja az INotifyPropertyChanged-et
            foreach (var item in Places)
            {
                item.IsEmpty = true;
                item.Type = null;
            }
            // Opcionálisan: Kezdjen újra az első játékos
            IsPlayer1Turn = true;
            IsPlayer2Turn = false;
        }


        private void ChangeTurn()
        {
            IsPlayer1Turn = !IsPlayer1Turn;
            IsPlayer2Turn = !IsPlayer1Turn;
        }

        private int CheckWinner()
        {
            int winner = -1;
            int n = BoardSize; // Egyszerűsítés
            double nSquared = Math.Pow(n, 2);

            // Sorok ellenőrzése
            for (int row = 0; row < n; row++)
            {
                var currentRow = Places.Skip(row * n).Take(n);
                if (currentRow.All(p => p.Type == IconType.Circle)) return 2;
                if (currentRow.All(p => p.Type == IconType.Cross)) return 1;
            }

            // Oszlopok ellenőrzése
            for (int col = 0; col < n; col++)
            {
                var currentCol = Places.Where((p, index) => index % n == col);
                if (currentCol.All(p => p.Type == IconType.Circle)) return 2;
                if (currentCol.All(p => p.Type == IconType.Cross)) return 1;
            }

            // Átlók ellenőrzése
            var diag1 = Places.Where((p, index) => index % (n + 1) == 0);
            if (diag1.All(p => p.Type == IconType.Circle)) return 2;
            if (diag1.All(p => p.Type == IconType.Cross)) return 1;

            // Csak négyzetes táblán van értelme a másik átlónak
            if (n > 1)
            {
                var diag2 = Places.Where((p, index) => index != 0 && index != nSquared - 1 && index % (n - 1) == 0);
                // Korrekció: Az első és utolsó elem kimarad a fenti logikából n>2 esetén, külön kell ellenőrizni,
                // vagy a Places[n-1] ... Places[nSquared-n] elemeket kell nézni (n-1)-es lépésközzel.
                // Egyszerűbb LINQ az átlóhoz:
                var diag2Indices = Enumerable.Range(0, n).Select(i => (i + 1) * (n - 1));
                var diag2Correct = Places.Where((p, index) => diag2Indices.Contains(index));

                if (diag2Correct.All(p => p.Type == IconType.Circle)) return 2;
                if (diag2Correct.All(p => p.Type == IconType.Cross)) return 1;
            }


            return winner; // -1, ha nincs győztes
        }
    }
}