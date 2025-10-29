using System;

namespace Amoba.Services
{
    // Argumentum az eseményhez, ami jelzi, ha találtunk egy játékot (meglévő)
    public class GameFoundEventArgs : EventArgs
    {
        public string HostName { get; }
        public string IpAddress { get; }
        public GameFoundEventArgs(string hostName, string ipAddress)
        {
            HostName = hostName;
            IpAddress = ipAddress;
        }
    }
}