using Amoba.Model;
using System.Collections.Generic;

namespace Amoba.Services
{
    public static class GameLogic
    {
        // ===================================================================
        // --- VERZIÓ 1: GameViewModel használja ---
        // ===================================================================

        /// <summary>
        /// Ellenőrzi a táblát, és visszaadja a győztest,
        /// VALAMINT a győztes cellák listáját.
        /// </summary>
        public static GameResult CheckWinner(IReadOnlyList<Place> places, int boardSize)
        {
            if (places == null || places.Count != boardSize * boardSize)
                return new GameResult(); // Alapértelmezett (Nincs győztes)

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
                    if (win)
                    {
                        // Visszaadjuk a teljes eredményt
                        var result = new GameResult { Winner = firstType };
                        for (int c = 0; c < boardSize; c++)
                        {
                            result.WinningCellIDs.Add(row * boardSize + c);
                        }
                        return result;
                    }
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
                    if (win)
                    {
                        // Visszaadjuk a teljes eredményt
                        var result = new GameResult { Winner = firstType };
                        for (int r = 0; r < boardSize; r++)
                        {
                            result.WinningCellIDs.Add(r * boardSize + col);
                        }
                        return result;
                    }
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
                if (win)
                {
                    // Visszaadjuk a teljes eredményt
                    var result = new GameResult { Winner = mainDiagType };
                    for (int i = 0; i < boardSize; i++)
                    {
                        result.WinningCellIDs.Add(i * boardSize + i);
                    }
                    return result;
                }
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
                if (win)
                {
                    // Visszaadjuk a teljes eredményt
                    var result = new GameResult { Winner = antiDiagType };
                    for (int i = 0; i < boardSize; i++)
                    {
                        result.WinningCellIDs.Add(i * boardSize + (boardSize - 1 - i));
                    }
                    return result;
                }
            }

            // Alapértelmezett GameResult visszaadása
            return new GameResult();
        }

        // ===================================================================
        // --- VERZIÓ 2: AI használja ---
        // ===================================================================

        /// <summary>
        /// Ellenőrzi a táblát, és visszaadja a győztest,
        /// VALAMINT a győztes cellák listáját. (AI verzió)
        /// </summary>
        public static GameResult CheckWinner(IconType[] board, int boardSize)
        {
            if (board == null || board.Length != boardSize * boardSize)
                return new GameResult();

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
                    if (win)
                    {
                        var result = new GameResult { Winner = first };
                        for (int c = 0; c < boardSize; c++)
                        {
                            result.WinningCellIDs.Add(row * boardSize + c);
                        }
                        return result;
                    }
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
                    if (win)
                    {
                        var result = new GameResult { Winner = first };
                        for (int r = 0; r < boardSize; r++)
                        {
                            result.WinningCellIDs.Add(r * boardSize + col);
                        }
                        return result;
                    }
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
                if (win)
                {
                    var result = new GameResult { Winner = mainDiag };
                    for (int i = 0; i < boardSize; i++)
                    {
                        result.WinningCellIDs.Add(i * boardSize + i);
                    }
                    return result;
                }
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
                if (win)
                {
                    var result = new GameResult { Winner = antiDiag };
                    for (int i = 0; i < boardSize; i++)
                    {
                        result.WinningCellIDs.Add(i * boardSize + (boardSize - 1 - i));
                    }
                    return result;
                }
            }

            return new GameResult();
        }
    }
}