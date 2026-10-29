using CoreOSC;
using NLog;
using Tailgrab.Common;

namespace Tailgrab.Clients.OSC
{
    public class VRChatOSCChatbox : IDisposable
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        #region Configuration
        private const int DefaultPort = 9000;
        private const string DefaultHost = "127.0.0.1";
        private const int MaxChatLength = 144;

        public bool EnableOSCService
        {
            get => ConfigStore.GetStoredKeyBool(CommonConst.Registry_OSC_Enabled, false);
            set => ConfigStore.PutStoredKeyBool(CommonConst.Registry_OSC_Enabled, value);
        }

        public string OSCHost
        {
            get => ConfigStore.GetStoredKeyString(CommonConst.Registry_OSC_Endpoint) ?? DefaultHost;
            set => ConfigStore.PutStoredKeyString(CommonConst.Registry_OSC_Endpoint, value);
        }

        public int OSCPort
        {
            get => int.Parse(ConfigStore.GetStoredKeyString(CommonConst.Registry_OSC_Port) ?? DefaultPort.ToString());
            set => ConfigStore.PutStoredKeyString(CommonConst.Registry_OSC_Port, value.ToString());
        }

        #endregion

       
        private UDPSender? _udpSender;

        public VRChatOSCChatbox()
        {
            _udpSender = new UDPSender(OSCHost, OSCPort);
        }

        public void UpdateEndpoint()
        {
            _udpSender = new UDPSender(OSCHost, OSCPort);
        }

        /// <summary>
        /// Send a message directly to the chatbox (bypasses keyboard).
        /// Equivalent to MagicChatbox's "direct send."
        /// </summary>
        public void SendMessage(string message, bool notify = true)
        {
            if (message.Length > MaxChatLength)
                throw new ArgumentException($"Message exceeds {MaxChatLength} characters.");

            // OSC address: /chatbox/input
            // Signature: s b b  (string, bool, bool)
            //   s = message text
            //   b (1st) = true → send directly; false → populate keyboard
            //   b (2nd) = true → play notification SFX; false → silent
            OscMessage msg = new OscMessage("/chatbox/input", message, true, notify);
            Task.Run(() => _udpSender?.Send(msg));
        }

        public void SendValue(string address, params object[] values)
        {
            OscMessage msg = new OscMessage(address, values);
            Task.Run(() => _udpSender?.Send(msg));
        }

        /// <summary>
        /// Populate the in-game keyboard with text (does NOT send).
        /// The user can then edit and press send in VRChat.
        /// </summary>
        public void PopulateKeyboard(string message, bool notify = true)
        {
            if (message.Length > MaxChatLength)
                throw new ArgumentException($"Message exceeds {MaxChatLength} characters.");

            OscMessage msg = new OscMessage("/chatbox/input", message, false, notify);
            Task.Run(() => _udpSender?.Send(msg));
        }

        /// <summary>
        /// Simulate a typing indicator above the avatar's head,
        /// then send the message — mimics real typing.
        /// </summary>
        public async Task SimulateTyping(string message, int typingDurationMs = 2000)
        {
            // Trigger the "is typing" indicator
            OscMessage typingMsg = new OscMessage("/chatbox/typing", true);
            await Task.Run(() => _udpSender?.Send(typingMsg));
            await Task.Delay(typingDurationMs);

            // Send the actual message
            SendMessage(message);

            // Clear the typing indicator
            OscMessage clearTypingMsg = new OscMessage("/chatbox/typing", false);
            await Task.Run(() => _udpSender?.Send(clearTypingMsg));
        }

        /// <summary>
        /// Clear the chatbox (send an empty message).
        /// </summary>
        public void Clear()
        {
            OscMessage msg = new OscMessage("/chatbox/input", "", true, false);
            Task.Run(() => _udpSender?.Send(msg));
        }

        public void Dispose() {
            _udpSender = null;
        }
    }
}
