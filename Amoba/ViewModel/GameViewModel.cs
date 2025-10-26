using Amoba.Model;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameViewModel : ViewModelBase
    {
        private int gameSize;
        private int windowWidth;

        private int windowHeight;
        private string title;

        private int player1Score;
        private int player2Score;

        private bool isPlayer1Turn;
        private bool isPlayer2Turn;

        private List<Place> places;

        private ICommand setImage;

        public GameViewModel(int gameSize)
        {
            this.gameSize = gameSize;
            Places = new List<Place>();
            for (int i = 0; i < Math.Pow(gameSize, 2); i++)
            {
                Places.Add(new Place() { Id = i, Type = null });
            }

            switch (gameSize)
            {
                case 3:
                    WindowWidth = 490;
                    WindowHeight = 325;
                    Title = "3x3 mode";
                    break;
                case 4:
                    WindowWidth = 580;
                    WindowHeight = 415;
                    Title = "4x4 mode";
                    break;
                case 5:
                    WindowWidth = 670;
                    WindowHeight = 505;
                    Title = "5x5 mode";
                    break;
            }
            IsPlayer1Turn = true;
        }

        public int WindowWidth
        {
            get { return windowWidth; }
            set { windowWidth = value; RaisePropertyChanged(); }
        }

        public int WindowHeight
        {
            get { return windowHeight; }
            set { windowHeight = value; RaisePropertyChanged(); }
        }

        public string Title
        {
            get { return title; }
            set { title = value; RaisePropertyChanged(); }
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

        public List<Place> Places
        {
            get { return places; }
            set { places = value; RaisePropertyChanged(); }
        }

        public ICommand SetImage
        {
            get
            {
                return setImage ??
                    (setImage = new RelayCommand<Place>(SetImageMethod, p => { return p.IsEmpty; }));
            }
        }

        private void SetImageMethod(Place place)
        {
            var pl = Places.Single(z => z.Id == place.Id);
            pl.IsEmpty = false;
            pl.Type = IsPlayer1Turn ? IconType.Cross : IconType.Circle;

            var winner = CheckWinner();
            if (winner != -1 || Places.Where(z => !z.IsEmpty).Count() == Math.Pow(gameSize, 2))
            {
                if (winner == 1) Player1Score++;
                else if (winner == 2) Player2Score++;

                Places.ForEach(z => { z.IsEmpty = true; z.Type = null; });
            }

            Places = new List<Place>(Places);
            ChangeTurn();
        }

        private void ChangeTurn()
        {
            if (IsPlayer1Turn) { IsPlayer1Turn = false; IsPlayer2Turn = true; }
            else if (IsPlayer2Turn) { IsPlayer2Turn = false; IsPlayer1Turn = true; }
        }

        private int CheckWinner()
        {
            int winner = -1;

            // rows
            for (int i = 0; i < Math.Pow(gameSize, 2); i += gameSize)
            {
                if (i % gameSize == 0)
                {
                    if (Places.FindAll(z => z.Id >= i && z.Id < i + gameSize).Where(z => z.Type == IconType.Circle).Count() == gameSize) winner = 2;
                    if (Places.FindAll(z => z.Id >= i && z.Id < i + gameSize).Where(z => z.Type == IconType.Cross).Count() == gameSize) winner = 1;
                }
            }

            // columns
            for (int i = 0; i < gameSize; i++)
            {
                if (Places.FindAll(z => (z.Id - i) % gameSize == 0).Where(z => z.Type == IconType.Circle).Count() == gameSize) winner = 2;
                if (Places.FindAll(z => (z.Id - i) % gameSize == 0).Where(z => z.Type == IconType.Cross).Count() == gameSize) winner = 1;
            }

            // diagonal
            var diagonal = new List<int>();

            for (int i = 0; i < Math.Pow(gameSize, 2); i += gameSize + 1) diagonal.Add(i);

            if (Places.FindAll(z => diagonal.Contains(z.Id)).Where(z => z.Type == IconType.Circle).Count() == gameSize) winner = 2;
            if (Places.FindAll(z => diagonal.Contains(z.Id)).Where(z => z.Type == IconType.Cross).Count() == gameSize) winner = 1;

            diagonal.Clear();

            for (int i = gameSize - 1; i < Math.Pow(gameSize, 2) - 1; i += gameSize - 1) diagonal.Add(i);

            if (Places.FindAll(z => diagonal.Contains(z.Id)).Where(z => z.Type == IconType.Circle).Count() == gameSize) winner = 2;
            if (Places.FindAll(z => diagonal.Contains(z.Id)).Where(z => z.Type == IconType.Cross).Count() == gameSize) winner = 1;

            return winner;
        }
    }
}
