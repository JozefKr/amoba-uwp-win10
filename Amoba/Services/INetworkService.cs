using System;
using System.Threading.Tasks;

namespace Amoba.Services
{
    public interface INetworkService
    {
        // --- Események ---
        event EventHandler<GameFoundEventArgs> GameFound;
        event EventHandler<GameStartedEventArgs> GameStarted;
        event EventHandler<GameStartedEventArgs> HostGameReady;
        event EventHandler<int> MoveReceived;
        event EventHandler OpponentDisconnected;
        event EventHandler<string> NetworkErrorOccurred; // Általános hálózati/socket hibák jelzésére                              
        event EventHandler<int> BoardSizeReceived; // Kliens feliratkozik rá, megkapja a méretet, és navigál.

        // --- Felfedezési metódusok ---
        Task StartHostingAsync(string playerName);
        void StopHosting();
        Task StartDiscoveringAsync();
        void StopDiscovering();

        // --- TCP Kapcsolódási metódusok ---
        /// <summary>
        /// Megpróbál TCP kapcsolatot létesíteni a hosttal.
        /// </summary>
        Task<bool> ConnectToGameAsync(string hostIpAddress);

        /// <summary>
        /// TCP kapcsolat fogadása és elfogadása. Csak a Host hívja.
        /// </summary>
        Task StartAcceptingConnectionsAsync();

        /// <summary>
        /// Lépés küldése az ellenfélnek.
        /// </summary>
        Task SendMoveAsync(int index);

        /// <summary>
        /// Bármely aktív kapcsolat bontása.
        /// </summary>
        void Disconnect();

        // Host hívja meg, elküldi a választott méretet a Kliensnek, majd indítja a játékot.
        Task SendBoardSizeAsync(int boardSize);
    }
}