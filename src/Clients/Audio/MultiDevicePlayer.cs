using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.IO;

namespace Tailgrab.Clients.Audio
{
    public class MultiDevicePlayer : IDisposable
    {
        private readonly List<IWavePlayer> _players = new();
        private readonly List<IWaveProvider> _readers = new();

        /// <summary>
        /// Plays WAV bytes to multiple output devices simultaneously.
        /// deviceNames: e.g. ["Steam Streaming Speakers", "Speakers (Realtek Audio)"]
        /// </summary>
        public void Play(byte[] wavBytes, params string[] deviceNames)
        {
            Stop(); // stop any previous playback

            var enumerator = new MMDeviceEnumerator();

            foreach (string name in deviceNames)
            {
                // Find the render device by friendly name
                var device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
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
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            return devices.Select(d => d.FriendlyName).ToList();
        }

        public static List<string> GetAvailableInputDevices()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            return devices.Select(d => d.FriendlyName).ToList();
        }
    }
}
