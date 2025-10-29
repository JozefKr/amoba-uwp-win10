using Amoba.Model;
using System.Collections.Generic;

namespace Amoba.Services
{
    public static class GameLogic
    {
        // 1. VERZIÓ: ViewModel használja. ObservableCollection is kompatibilis.
        public static IconType CheckWinner(IReadOnlyList<Place> places, int boardSize)
        {
            if (places == null || places.Count != boardSize * boardSize) return IconType.None;

            // Sorok ellenőrzése
            for (int row = 0; row < boardSize; row++)
            {
                IconType firstType = places[row * boardSize].Type;
                if (firstType != IconType.None)
                {
                    bool win = true;
                    for (int col = 1; col < boardSize; col++)
                    {
                        if (places[row * boardSize + col].Type != firstType)
                        {
                            win = false;
                            break;
                        }
                    }
                    if (win) return firstType;
                }
            }

            // Oszlopok ellenőrzése
            for (int col = 0; col < boardSize; col++)
            {
                IconType firstType = places[col].Type;
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
                    if (win) return firstType;
                }
            }

            // Főátló (bal fent -> jobb lent)
            IconType mainDiagType = places[0].Type;
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
                if (win) return mainDiagType;
            }

            // Mellékátló (jobb fent -> bal lent)
            IconType antiDiagType = places[boardSize - 1].Type;
            if (antiDiagType != IconType.None)
            {
                bool win = true;
                for (int i = 1; i < boardSize; i++)
                {
                    if (places[i * boardSize + (boardSize - 1 - i)].Type != antiDiagType)
                    {
                        win = false;
                        break;
                    }
                }
                if (win) return antiDiagType;
            }

            return IconType.None;
        }

        // 2. VERZIÓ: AI használja. Optimalizálva IconType[] tömbhöz.
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
            // Oszlopok
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