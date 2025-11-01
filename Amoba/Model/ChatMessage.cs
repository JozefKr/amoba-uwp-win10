using System;

namespace Amoba.Model
{
    /// <summary>
    /// Egyetlen chat-üzenetet reprezentál.
    /// Most már tartalmaz időbélyeget és formázott kimenetet is.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Az üzenet szerzőjének neve (pl. "Pista" vagy "Jani").
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Maga az üzenet szövege.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Igaz, ha ezt az üzenetet a helyi felhasználó ("én") küldtem.
        /// </summary>
        public bool IsMine { get; set; }

        /// <summary>
        /// Az üzenet létrehozásának időpontja.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Egyetlen, formázott string-et ad vissza a UI számára.
        /// Pl.: "[14:32] Pista: Szia!"
        /// </summary>
        public string DisplayMessage
        {
            get
            {
                // Az időbélyeget "HH:mm" (óra:perc) formátumra alakítjuk
                string time = Timestamp.ToString("HH:mm");
                return $"[{time}] {Author}: {Message}";
            }
        }
    }
}
