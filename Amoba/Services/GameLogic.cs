using Amoba.Model;
using System.Collections.ObjectModel;
using System.Linq;

namespace Amoba.Services // Névtér frissítve
{
    // Segédosztály a játéklogika (győztes ellenőrzés) kiszervezéséhez
    // Így az AiPlayer és a GameViewModel is használhatja
    public static class GameLogic
    {
        // Visszatérési érték: IconType.Cross (1. nyert), IconType.Circle (2. nyert), IconType.None (senki)
        public static IconType CheckWinner(ObservableCollection<Place> places, int boardSize)
        {
            if (places == null || places.Count != boardSize * boardSize) return IconType.None; // Ellenőrzés

            IconType currentWinner = IconType.None;

            // Sorok ellenőrzése
            for (int row = 0; row < boardSize; row++)
            {
                var firstType = places[row * boardSize].Type;
                if (firstType != IconType.None && places.Skip(row * boardSize).Take(boardSize).All(p => p.Type == firstType))
                {
                    currentWinner = firstType;
                    goto EndCheck; // Ha van győztes, felesleges tovább keresni
                }
            }

            // Oszlopok ellenőrzése
            for (int col = 0; col < boardSize; col++)
            {
                var firstType = places[col].Type;
                if (firstType != IconType.None)
                {
                    bool win = true;
                    for (int row = 1; row < boardSize; row++)
                    {
                        if (places[row * boardSize + col].Type != firstType)
                        {
                            win = false;
                            break;
                        }
                    }
                    if (win)
                    {
                        currentWinner = firstType;
                        goto EndCheck;
                    }
                }
            }

            // Átlók ellenőrzése
            // Főátló (bal fent -> jobb lent)
            var mainDiagType = places[0].Type;
            if (mainDiagType != IconType.None)
            {
                bool win = true;
                for (int i = 1; i < boardSize; i++)
                {
                    if (places[i * boardSize + i].Type != mainDiagType)
                    {
                        win = false;
                        break;
                    }
                }
                if (win)
                {
                    currentWinner = mainDiagType;
                    goto EndCheck;
                }
            }

            // Mellékátló (jobb fent -> bal lent)
            var antiDiagType = places[boardSize - 1].Type;
            if (antiDiagType != IconType.None)
            {
                bool win = true;
                for (int i = 1; i < boardSize; i++)
                {
                    // Index számítás javítása a mellékátlóhoz
                    if (places[i * boardSize + (boardSize - 1 - i)].Type != antiDiagType)
                    {
                        win = false;
                        break;
                    }
                }
                if (win)
                {
                    currentWinner = antiDiagType;
                    goto EndCheck;
                }
            }

        EndCheck: // Címke a goto-hoz
            return currentWinner; // Visszaadjuk a talált győztest vagy None-t
        }

        public static IconType CheckWinner(IconType[] board, int boardSize)
        {
            if (board == null || board.Length != boardSize * boardSize) return IconType.None;

            // Sorok
            for (int row = 0; row < boardSize; row++)
            {
                IconType first = board[row * boardSize];
                if (first != IconType.None)
                {
                    bool win = true;
                    for (int col = 1; col < boardSize; col++)
                    {
                        if (board[row * boardSize + col] != first) { win = false; break; }
                    }
                    if (win) return first;
                }
            }
            // Oszlopok (hasonlóan)
            for (int col = 0; col < boardSize; col++)
            {
                IconType first = board[col];
                if (first != IconType.None)
                {
                    bool win = true;
                    for (int row = 1; row < boardSize; row++)
                    {
                        if (board[row * boardSize + col] != first) { win = false; break; }
                    }
                    if (win) return first;
                }
            }
            // Főátló
            IconType mainDiag = board[0];
            if (mainDiag != IconType.None)
            {
                bool win = true;
                for (int i = 1; i < boardSize; i++)
                {
                    if (board[i * boardSize + i] != mainDiag) { win = false; break; }
                }
                if (win) return mainDiag;
            }
            // Mellékátló
            IconType antiDiag = board[boardSize - 1];
            if (antiDiag != IconType.None)
            {
                bool win = true;
                for (int i = 1; i < boardSize; i++)
                {
                    if (board[i * boardSize + (boardSize - 1 - i)] != antiDiag) { win = false; break; }
                }
                if (win) return antiDiag;
            }
            return IconType.None;
        }
    }
}