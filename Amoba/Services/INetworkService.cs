using System;
using System.Threading.Tasks;

namespace Amoba.Services
{
    // =================================================================
    // 2. INETWORKSERVICE INTERFÉSZ DEFINÍCIÓJA
    // =================================================================

    public interface INetworkService
    {
        // --- Események ---

        event EventHandler<GameFoundEventArgs> GameFound; // Kliens: Hostot talált
        event EventHandler<string> NetworkErrorOccurred; // Általános hiba
        event EventHandler OpponentDisconnected; // Kapcsolat megszakadt
        event EventHandler<int> MoveReceived; // Lépés érkezett

        /// <summary>
        /// Akkor aktiválódik, ha az ellenfél a Főmenübe lépett (szándékos kilépés).
        /// </summary>
        event EventHandler OpponentLeft;

        /// <summary>
        /// Akkor aktiválódik, ha az ellenfél visszavágót kért (megnyomta az Új Játék gombot).
        /// </summary>
        event EventHandler RematchReceived;

        /// <summary>
        /// Aktiválódik a HOST oldalon, amikor a Kliens sikeresen csatlakozott TCP-n.
        /// Jelzi a Host MainViewModel-nek, hogy navigálhat a GameSizePage-re.
        /// </summary>
        event EventHandler HostConnectionEstablished;

        /// <summary>
        /// Aktiválódik mindkét oldalon (Host és Kliens), amikor a játék ténylegesen elindul
        /// (a Host elküldte a méretet a START paranccsal).
        /// </summary>
        event EventHandler<GameStartedEventArgs> GameStarted;

        /// <summary>
        /// Akkor aktiválódik, ha az ellenfél nyugtázta a kilépési szándékot.
        /// </summary>
        event EventHandler LeaveAcknowledged;


        // --- Metódusok ---

        // Felfedezés (UDP)
        Task StartHostingAsync(string playerName);
        void StopHosting();
        Task StartDiscoveringAsync();
        void StopDiscovering();

        // Kapcsolatkezelés (TCP)
        Task StartAcceptingConnectionsAsync();

        /// <summary>
        /// KLIENS: Megpróbál TCP kapcsolatot létesíteni a megadott Host IP címre.
        /// </summary>
        Task ConnectToGameAsync(string hostIpAddress);

        /// <summary>
        /// HOST: Elküldi a START üzenetet (mérettel) a Kliensnek, és lokálisan is kiváltja a GameStarted eseményt.
        /// </summary>
        Task InitiateNetworkGameStartAsync(int boardSize);

        /// <summary>
        /// Elküldi a helyi játékos lépését (a mező indexét) az ellenfélnek TCP-n.
        /// </summary>
        Task SendMoveAsync(int index);

        /// <summary>
        /// Elküldi az "Új Játék" (visszavágó) kérést az ellenfélnek.
        /// </summary>
        Task SendRematchRequestAsync();

        /// <summary>
        /// Elküldi az ellenfélnek, hogy a Főmenübe lépünk.
        /// </summary>
        Task SendLeaveGameAsync();

        /// <summary>
        /// Lezár minden aktív UDP és TCP kapcsolatot/listenert.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Elküldi a "Leave" üzenet nyugtázását. (Csak Kliens hívja)
        /// </summary>
        Task SendLeaveAckAsync();
    }
}