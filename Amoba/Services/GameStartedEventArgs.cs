using System;

namespace Amoba.Services
{
    // UGYANIDE HOZZÁADVA: Argumentum a játék elindításához szükséges adatokhoz
    public class GameStartedEventArgs : EventArgs
    {
        public string OpponentName { get; }
        public int BoardSize { get; }
        public bool IsHost { get; }

        public GameStartedEventArgs(string opponentName, int boardSize, bool isHost)
        {
            OpponentName = opponentName;
            BoardSize = boardSize;
            IsHost = isHost;
        }
    }
}
