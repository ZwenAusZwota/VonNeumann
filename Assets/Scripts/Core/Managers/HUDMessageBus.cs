// Assets/Scripts/00_Manager/HUDMessageBus.cs
using System;


    /// <summary>
    /// Globaler, leichter Nachrichtenbus fürs HUD.
    /// Von überall mit HUDMessageBus.Post("Text") nutzbar.
    /// </summary>
    public static class HUDMessageBus
    {
        /// <summary>
        /// Wird ausgelöst, wenn eine Nachricht ans HUD gesendet wird.
        /// </summary>
        public static event Action<string> OnHudMessage;

        /// <summary>
        /// Nachricht posten (Thread-Safe in Unity-Kontext: bitte vom Main-Thread aufrufen).
        /// </summary>
        public static void Post(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            OnHudMessage?.Invoke(message);
        }
    }
