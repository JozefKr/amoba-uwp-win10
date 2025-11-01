using Amoba.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Amoba.Services
{
    public class AiPlayer
    {
        private const int MaxDepth = 4; // Mélységkorlát nagyobb táblákhoz (állítható)

        // Konstansok a kiértékeléshez (Clean Code)
        private const int WIN_SCORE = 10;
        private const int LOSS_SCORE = -10;
        private const int DRAW_SCORE = 0;

        /// <summary>
        /// Megkeresi a legjobb lépést a Minimax algoritmussal, Alfa-Béta vágással optimalizálva.
        /// </summary>
        /// <param name="currentBoardPlaces">A játék jelenlegi állása ObservableCollection<Place> formában.</param>
        /// <param name="boardSize">A tábla mérete.</param>
        /// <returns>A legjobb lépéshez tartozó Place objektum, vagy null, ha nincs lépés.</returns>
        public Place FindBestMove(ObservableCollection<Place> currentBoardPlaces, int boardSize)
        {
            // --- Konvertálás egyszerű tömbre a teljesítményért ---
            IconType[] board = currentBoardPlaces.Select(p => p.Type).ToArray();
            int bestScore = int.MinValue;
            int bestMoveIndex = -1; // A legjobb lépés indexét tároljuk

            // Optimalizált lépésgenerálás (közép, sarkok, többi)
            foreach (int moveIndex in GetOptimizedPossibleMoves(board, boardSize))
            {
                // Lépés végrehajtása a tömbön (nincs Place objektum módosítás!)
                board[moveIndex] = IconType.Circle; // AI lép (mindig O)

                // Minimax hívása Alfa-Béta vágással
                int score = Minimax(board, boardSize, 0, false, int.MinValue, int.MaxValue);

                // Lépés visszavonása a tömbön
                board[moveIndex] = IconType.None;

                // Jobb lépést találtunk?
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoveIndex = moveIndex;
                }
            }

            // Ha találtunk lépést, adjuk vissza a megfelelő Place objektumot
            if (bestMoveIndex != -1)
            {
                // Keressük meg az eredeti kollekcióban az index alapján
                // Biztonságosabb, mint a FindBestMove-ból visszaadott Place-re hagyatkozni,
                // mert az közben megváltozhatott volna (bár a mi logikánkban nem).
                return currentBoardPlaces[bestMoveIndex];
            }
            else
            {
                // Vészhelyzet: Nincs üres mező vagy hiba történt.
                // Adjunk vissza egy random üres mezőt, ha van.
                var firstEmpty = currentBoardPlaces.FirstOrDefault(p => p.IsEmpty);
                return firstEmpty; // Ha ez is null, akkor a hívó kódnak kell kezelnie.
            }
        }

        /// <summary>
        /// A Minimax algoritmus rekurzív megvalósítása Alfa-Béta vágással.
        /// Egyszerű IconType[] tömbön dolgozik a maximális teljesítményért.
        /// </summary>
        /// <param name="board">A tábla aktuális állapota (IconType tömb).</param>
        /// <param name="boardSize">A tábla mérete.</param>
        /// <param name="depth">Aktuális rekurziós mélység.</param>
        /// <param name="isMaximizing">Igaz, ha az AI (maximalizáló) köre van.</param>
        /// <param name="alpha">Az eddig talált legjobb érték a maximalizáló számára.</param>
        /// <param name="beta">Az eddig talált legjobb érték a minimalizáló számára.</param>
        /// <returns>Az adott táblaállás értékelése.</returns>
        private int Minimax(IconType[] board, int boardSize, int depth, bool isMaximizing, int alpha, int beta)
        {
            GameResult result = GameLogic.CheckWinner(board, boardSize);
            IconType winner = result.Winner;

            // Alap esetek: Terminális állapotok
            if (winner == IconType.Circle) return WIN_SCORE - depth; // AI nyert
            if (winner == IconType.Cross) return LOSS_SCORE + depth; // Ember nyert

            // Gyorsabb ellenőrzés, hogy van-e még üres hely
            bool isMovesLeft = false;
            for (int i = 0; i < board.Length; i++)
            {
                if (board[i] == IconType.None)
                {
                    isMovesLeft = true;
                    break;
                }
            }

            // Alap esetek: Döntetlen vagy mélységi korlát elérése
            // Dinamikus mélység számítása
            if (!isMovesLeft || depth >= CalculateMaxDepth(boardSize))
            {
                return DRAW_SCORE;
            }

            // Rekurzív lépés
            if (isMaximizing) // AI (maximalizáló)
            {
                int bestScore = int.MinValue;
                // Optimalizált lépések bejárása
                foreach (int moveIndex in GetOptimizedPossibleMoves(board, boardSize))
                {
                    board[moveIndex] = IconType.Circle;
                    bestScore = Math.Max(bestScore, Minimax(board, boardSize, depth + 1, false, alpha, beta));
                    board[moveIndex] = IconType.None; // Visszavonás

                    // --- Alfa-Béta vágás ---
                    alpha = Math.Max(alpha, bestScore);
                    if (beta <= alpha)
                        break; // Béta vágás: A minimalizáló már talált ennél jobbat, felesleges tovább keresni
                    // --- Alfa-Béta VÉGE ---
                }
                return bestScore;
            }
            else // Ember (minimalizáló)
            {
                int bestScore = int.MaxValue;
                foreach (int moveIndex in GetOptimizedPossibleMoves(board, boardSize))
                {
                    board[moveIndex] = IconType.Cross;
                    bestScore = Math.Min(bestScore, Minimax(board, boardSize, depth + 1, true, alpha, beta));
                    board[moveIndex] = IconType.None; // Visszavonás

                    // Alfa-Béta vágás ---
                    beta = Math.Min(beta, bestScore);
                    if (beta <= alpha)
                        break; // Alfa vágás: A maximalizáló már talált ennél jobbat, felesleges tovább keresni
                    // --- Alfa-Béta VÉGE ---
                }
                return bestScore;
            }
        }

        // Dinamikus mélység számítása
        private int CalculateMaxDepth(int boardSize)
        {
            if (boardSize == 3) return 9; // 3x3: Teljes fa bejárása
            if (boardSize == 4) return 6; // 4x4: Növelt mélység Alfa-Béta miatt
            if (boardSize == 5) return 4; // 5x5: Még mindig korlátozott
            return 3; // Alapértelmezett nagyobb táblákra
        }

        private IEnumerable<int> GetOptimizedPossibleMoves(IconType[] board, int boardSize)
        {
            List<int> moves = new List<int>();
            int center = -1;
            if (boardSize % 2 != 0) // Csak páratlan méretű táblán van középső mező
            {
                center = (board.Length - 1) / 2;
                if (board[center] == IconType.None)
                {
                    yield return center; // Először a középső
                }
            }

            // Sarkok (prioritással)
            int[] corners = { 0, boardSize - 1, boardSize * (boardSize - 1), boardSize * boardSize - 1 };
            foreach (int corner in corners)
            {
                if (corner != center && board[corner] == IconType.None)
                {
                    yield return corner;
                }
            }

            // Többi mező (sorrendben)
            for (int i = 0; i < board.Length; i++)
            {
                // Ha nem a közép, nem sarok, és üres
                if (i != center && !corners.Contains(i) && board[i] == IconType.None)
                {
                    yield return i;
                }
            }
        }
    }
}