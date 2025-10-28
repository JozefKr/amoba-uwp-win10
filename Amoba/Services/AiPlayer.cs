using Amoba.Model;
using Amoba.ViewModel; // Szükséges a GameLogic eléréséhez
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Amoba.Services
{
    public class AiPlayer
    {
        private const int MaxDepth = 4; // Mélységkorlát nagyobb táblákhoz (állítható)

        // Megkeresi a legjobb lépést a Minimax algoritmussal
        public Place FindBestMove(ObservableCollection<Place> board, int boardSize)
        {
            int bestVal = int.MinValue;
            Place bestMove = null;

            // Végigmegyünk az összes üres mezőn
            foreach (var place in board.Where(p => p.IsEmpty))
            {
                // Megpróbáljuk a lépést az AI-val (Circle)
                place.Type = IconType.Circle;
                place.IsEmpty = false;

                // Kiszámoljuk a lépés értékét a Minimax segítségével
                // Az AI maximalizál, az ellenfél (Human) minimalizál
                int moveVal = Minimax(board, boardSize, 0, false); // Mélység 0, ellenfél (minimalizáló) következik

                // Visszavonjuk a lépést (backtracking)
                place.Type = IconType.None;
                place.IsEmpty = true;

                // Ha ez a lépés jobb, mint az eddigi legjobb, elmentjük
                if (moveVal > bestVal)
                {
                    bestMove = place;
                    bestVal = moveVal;
                }
            }
            // Ha valamiért nem talál lépést (pl. már teli a tábla), adjon vissza null-t
            // VAGY adjon vissza egy random üres mezőt vészhelyzetben
            if (bestMove == null)
            {
                // return board.FirstOrDefault(p => p.IsEmpty); // Opcionális: random lépés
                return null;
            }
            return bestMove; // Visszaadjuk a legjobb lépéshez tartozó Place objektumot
        }

        // A Minimax rekurzív függvény
        // board: aktuális táblaállás
        // depth: aktuális rekurziós mélység
        // isMaximizing: igaz, ha az AI (maximalizáló) lép, hamis, ha az ellenfél (minimalizáló)
        private int Minimax(ObservableCollection<Place> board, int boardSize, int depth, bool isMaximizing)
        {
            // 1. Alap esetek: Terminális állapotok (győzelem, vereség, döntetlen) vagy mélységi korlát elérése
            IconType winner = GameLogic.CheckWinner(board, boardSize); // Használjuk a kiszervezett logikát

            if (winner == IconType.Circle) // AI nyert
                return 10 - depth; // Minél kisebb mélységben nyer, annál jobb
            if (winner == IconType.Cross) // Ember nyert
                return -10 + depth; // Minél kisebb mélységben veszít, annál "jobb" (késleltetés)

            bool isMovesLeft = board.Any(p => p.IsEmpty);
            // Módosított alap eset: Ha nincs több lépés VAGY elértük a max mélységet
            if (!isMovesLeft || depth >= CalculateMaxDepth(boardSize)) // Dinamikus mélység
                return 0;

            // 2. Rekurzív lépés
            if (isMaximizing) // AI (maximalizáló) lépése
            {
                int bestVal = int.MinValue;
                // Hatékonyság: Próbáljuk meg a középső és sarokmezőket először (opcionális)
                foreach (var place in GetPossibleMoves(board, boardSize))
                {
                    place.Type = IconType.Circle;
                    place.IsEmpty = false;

                    bestVal = Math.Max(bestVal, Minimax(board, boardSize, depth + 1, false)); // Ellenfél következik

                    place.Type = IconType.None; // Visszavonás
                    place.IsEmpty = true;
                    // Alfa-Béta vágás (opcionális, itt nincs implementálva a bonyolultság miatt)
                }
                return bestVal;
            }
            else // Ellenfél (minimalizáló) lépése
            {
                int bestVal = int.MaxValue;
                foreach (var place in GetPossibleMoves(board, boardSize))
                {
                    place.Type = IconType.Cross;
                    place.IsEmpty = false;

                    bestVal = Math.Min(bestVal, Minimax(board, boardSize, depth + 1, true)); // AI következik

                    place.Type = IconType.None; // Visszavonás
                    place.IsEmpty = true;
                    // Alfa-Béta vágás (opcionális)
                }
                return bestVal;
            }
        }

        // Dinamikus mélység számítása a tábla mérete alapján
        // Ez csak egy egyszerű heurisztika, finomítható
        private int CalculateMaxDepth(int boardSize)
        {
            if (boardSize == 3) return 9; // 3x3-on teljes keresés
            if (boardSize == 4) return 4; // 4x4-en korlátozott mélység
            if (boardSize == 5) return 3; // 5x5-ön még jobban korlátozott
            return 3; // Alapértelmezett
        }

        // Optimalizálás: A lehetséges lépések sorrendjének javítása (opcionális)
        // Itt csak az üres mezőket adja vissza, de lehetne prioritizálni (pl. közép)
        private IEnumerable<Place> GetPossibleMoves(ObservableCollection<Place> board, int boardSize)
        {
            // Egyszerűen visszaadja az összes üres mezőt
            return board.Where(p => p.IsEmpty);
            // TODO: Opcionális optimalizálás: középső mezők, sarokmezők prioritizálása
        }
    }
}