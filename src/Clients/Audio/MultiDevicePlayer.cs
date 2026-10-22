using NAudio.CoreAudioApi;
using NAudio.Wave;
using NLog;
using System.IO;

namespace Tailgrab.Clients.Audio
{
    public class MultiDevicePlayer : IDisposable
    {
        public event EventHandler<OutputDeviceChangedEventArgs>? OutputDeviceChanged;

        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private readonly List<IWavePlayer> _players = new();
        private readonly List<IWaveProvider> _readers = new();
        private static readonly MMDeviceEnumerator _enumerator = new();
        public string SelectedOutputDevice { get; set; } = GetDefaultOutputDevice();

        public MultiDevicePlayer()
        {
            // 1. Initialize the enumerator and your listener implementation
            var notifications = _enumerator.CreateNotificationClient(useSynchronizationContext: true);
            notifications.DefaultDeviceChanged += (s, e) =>
            {
                // e.DataFlow  – Render (output) or Capture (input)
                // e.Role      – Multimedia, Communications, or Console
                // e.DeviceId  – the new default device ID (can be null if none available)
                _Logger.Info($"Default {e.Flow} device changed to: {e.DeviceId}");
                MMDeviceEnumerator _enumerator = new();
                var device = _enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                    .FirstOrDefault(d => d.ID.Contains(e.DeviceId, StringComparison.OrdinalIgnoreCase));
                _Logger.Info($"As Device {device?.ID} named : {device?.FriendlyName}");

                // Notify subscribers of the device change
                if (device != null)
                {
                    OutputDeviceChanged?.Invoke(this, new OutputDeviceChangedEventArgs(device.FriendlyName));
                }
            };
        }

        /// <summary>
        /// Plays WAV bytes to multiple output devices simultaneously
        /// deviceNames: e.g. ["Steam Streaming Speakers", "Speakers (Realtek Audio)"]
        /// </summary>
        public void Play(byte[] wavBytes, params string[] deviceNames)
        {
            Stop(); // stop any previous playback            

            foreach (string name in deviceNames)
            {
                // Find the render device by friendly name
                var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .FirstOrDefault(d => d.FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase));

                if (device == null)
                    throw new InvalidOperationException($"Render device not found: {name}");

                // Each device needs its own reader (independent playback position)
                var reader = new RawSourceWaveStream(
                    new MemoryStream(wavBytes),
                    new WaveFormat(24000, 16, 1)); // adjust to your actual WAV format

                var player = new WasapiOut(device, AudioClientShareMode.Shared, true, 200);
                player.Init(reader);
                player.Play();

                _players.Add(player);
                _readers.Add(reader);
            }
        }

        public void Stop()
        {
            foreach (var p in _players) { p.Stop(); p.Dispose(); }
            _players.Clear();
            _readers.Clear();
        }

        public void Dispose() => Stop();

        public static List<string> GetAvailableOutputDevices()
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            return devices.Select(d => d.FriendlyName).ToList();
        }
        public static string GetDefaultOutputDevice()
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console).FriendlyName;
        }

        public static List<string> GetAvailableInputDevices()
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            return devices.Select(d => d.FriendlyName).ToList();
        }

        public static string GetDefaultInputDevice()
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console).FriendlyName;
        }
    }

    public class OutputDeviceChangedEventArgs : EventArgs
    {
        public string DeviceFriendlyName { get; }

        public OutputDeviceChangedEventArgs(string deviceFriendlyName)
        {
            DeviceFriendlyName = deviceFriendlyName;
        }
    }
}
