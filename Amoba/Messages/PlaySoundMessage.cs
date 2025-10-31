namespace Amoba.Messages
{
    /// <summary>
    /// Egy egyszerű üzenet, ami megmondja a View-nak,
    /// hogy milyen nevű hangeffektet kell lejátszania.
    /// </summary>
    public class PlaySoundMessage
    {
        public string SoundName { get; set; }
    }
}
