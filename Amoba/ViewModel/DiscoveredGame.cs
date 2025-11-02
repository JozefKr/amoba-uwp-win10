using GalaSoft.MvvmLight;
using System;

namespace Amoba.ViewModel
{
    /// <summary>
    /// A MainViewModel-ben (Kliens) felfedezett, elérhető Host játékok modellje.
    /// </summary>
    public class DiscoveredGame : ViewModelBase
    {
        private string _displayName;
        /// <summary>
        /// A játék neve (pl. "Pista játéka")
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => Set(ref _displayName, value);
        }

        private string _ipAddress;
        /// <summary>
        /// A Host IP címe, ez az egyedi azonosító.
        /// </summary>
        public string IpAddress
        {
            get => _ipAddress;
            set => Set(ref _ipAddress, value);
        }

        private DateTime _lastSeen;
        /// <summary>
        /// Az időbélyeg, amikor utoljára érkezett UDP broadcast ettől a Hosttól.
        /// A MainViewModel időzítője ez alapján távolítja el a lejárt játékokat.
        /// </summary>
        public DateTime LastSeen
        {
            get => _lastSeen;
            set => Set(ref _lastSeen, value);
        }
    }
}
