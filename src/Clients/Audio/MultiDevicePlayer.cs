using NAudio.CoreAudioApi;
using NAudio.Wave;
using NLog;
using System.IO;
using System.Windows;
using System.Windows.Forms.VisualStyles;

namespace Tailgrab.Clients.Audio
{
    public class MultiDevicePlayer : IDisposable
    {
        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        public event EventHandler<OutputDeviceChangedEventArgs>? OutputDeviceChanged;
        private readonly List<IWavePlayer> _players = new();
        private readonly List<IWaveProvider> _readers = new();
        private static readonly MMDeviceEnumerator _enumerator = new();
        public string SelectedOutputDevice { get; set; } = GetDefaultOutputDevice();

        #region Audio Channel Settings
        private Dictionary<string, AudioChannelSettings> _deviceSettings = new Dictionary<string, AudioChannelSettings>();

        public void SetChannelSettings(int channel, string deviceName, float volume, bool isMuted)
        {
            string deviceKey = $"Channel{channel}";
            _deviceSettings[deviceKey] = new AudioChannelSettings(deviceName, volume, isMuted);
            _Logger.Debug($"Set settings for {deviceKey}: Device={deviceName}, Volume={volume}, IsMuted={isMuted}");
        }

        public AudioChannelSettings? GetChannelSettings(int channel)
        {
            string deviceKey = $"Channel{channel}";
            if (_deviceSettings.TryGetValue(deviceKey, out var settings))
            {
                _Logger.Debug($"Retrieved settings for {deviceKey}: Device={settings.DeviceName}, Volume={settings.Volume}, IsMuted={settings.IsMuted}");
                return settings;
            }
            return null;
        }

        #endregion

        public bool IsMuteAudio { get; set; } = false;

        public float Volume1 { get; set; } = 1.0f;

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
                    RaiseOutputDeviceChanged(device.FriendlyName);
                }
            };
        }

        private void RaiseOutputDeviceChanged(string deviceFriendlyName)
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    OutputDeviceChanged?.Invoke(this, new OutputDeviceChangedEventArgs(deviceFriendlyName))
                );
            }
            else
            {
                OutputDeviceChanged?.Invoke(this, new OutputDeviceChangedEventArgs(deviceFriendlyName));
            }
        }


        #region Play Audio to select devices
        /// <summary>
        /// Plays WAV bytes to multiple output devices simultaneously
        /// deviceNames: e.g. ["Steam Streaming Speakers", "Speakers (Realtek Audio)"]
        /// </summary>
        public void Play(byte[] wavBytes, params List<AudioChannelSettings> deviceNames)
        {
            Stop(); // stop any previous playback            

            foreach (AudioChannelSettings item in deviceNames)
            {
                if( item.IsMuted)
                {
                    _Logger.Debug($"Skipping muted device: {item.DeviceName}");
                    continue;
                }

                // Find the render device by friendly name
                var device = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .FirstOrDefault(d => d.FriendlyName.Contains(item.DeviceName, StringComparison.OrdinalIgnoreCase));

                if (device == null)
                    throw new InvalidOperationException($"Render device not found: {item.DeviceName}");

                // Each device needs its own reader (independent playback position)
                var reader = new RawSourceWaveStream(
                    new MemoryStream(wavBytes),
                    new WaveFormat(24000, 16, 1)); // adjust to your actual WAV format

                //var player = new WasapiOut(device, AudioClientShareMode.Shared, true, 200);
                var player = new WasapiPlayerBuilder()
                    .WithDevice(device)
                    .WithLatency(200)
                    .WithSharedMode()
                    .WithEventSync()
                    .Build();

                player.Init(reader);
                player.Volume = item.Volume;
                player.Play();
                _Logger.Debug($"Playing audio to device: {item.DeviceName} with volume: {item.Volume}");

                _players.Add(player);
                _readers.Add(reader);
            }
        }

        public List<AudioChannelSettings> GetSelectedDevices()
        {
            List<AudioChannelSettings> devices = new List<AudioChannelSettings>();
            foreach (AudioChannelSettings settings in _deviceSettings.Values)
            {
                if( settings.IsMuted == false)
                {
                    devices.Add(settings);
                }
            }

            return devices;
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

        #endregion
    }

    public class OutputDeviceChangedEventArgs : EventArgs
    {
        public string DeviceFriendlyName { get; }

        public OutputDeviceChangedEventArgs(string deviceFriendlyName)
        {
            DeviceFriendlyName = deviceFriendlyName;
        }
    }


    #region Channel Item Settings
    public class AudioChannelSettings
    {
        public AudioChannelSettings(string deviceName, float volume = 0.75f, bool isMuted = false)
        {
            DeviceName = deviceName;
            Volume = volume;
            IsMuted = isMuted;
        }

        public string DeviceName { get; init; }
        public float Volume { get; init; }
        public bool IsMuted { get; init; }

        public override string ToString()
        {
            return $"Device: {DeviceName}, Volume: {Volume}, IsMuted: {IsMuted}";
        }
    }
    #endregion
}
