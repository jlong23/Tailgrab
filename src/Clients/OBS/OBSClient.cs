using Newtonsoft.Json.Linq;
using NLog;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types.Events; // Add this
using System.IO;

namespace Tailgrab.Clients.OBS
{
    public class OBSClient
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private string _connectionString;
        private string _password;

        protected OBSWebsocket _socket; 

        protected string _filename;
        protected string _eventType;
        protected string _eventName;

        private static List<ReplayBufferEvent> replayBufferEvents = [];

        public static List<ReplayBufferEvent> ReplayBufferEvents { get => replayBufferEvents; }

        public OBSClient( string connectionString, string password )
        {
            _connectionString = connectionString;
            _password = password;
            _socket = new OBSWebsocket();
            Task.Run(() => _socket.ConnectAsync(_connectionString, _password));
            if(!_socket.IsConnected)
            {
                throw new Exception("Failed to connect to OBS WebSocket server, Is it running and accessible?");
            }

            _socket.StartReplayBuffer();
            _socket.ToggleVirtualCam();
            _socket.ReplayBufferSaved += OnReplayBufferSaved;
        
        }
        
        public async Task Disconnect()
        {
            if (_socket != null && _socket.IsConnected)
            {
                _socket.StopReplayBuffer();
                _socket.ToggleVirtualCam();
                await Task.Run(() => _socket.Disconnect());
            }
        }

        public async Task SaveReplayBuffer()
        {
            if(_socket != null && _socket.IsConnected)
            {
                await Task.Run(() => _socket.SaveReplayBuffer());

            }
        }

        public async Task VTKRecording(string sceneName, string overlayName, string filename, string html)
        {
            if (_socket != null && _socket.IsConnected)
            {
                if( _socket.GetReplayBufferStatus() == false)
                {
                    _filename = filename;
                    _eventType = "VoteToKick";
                    _eventName = filename;
                }

                var sourceSettings = new JObject
                {
                    { "uri", "data:text/html,"+ Uri.EscapeDataString(html)},
                    { "width", 1920 },
                    { "height", 1080 },
                    { "local_file", false }
                };
                int inputId = _socket.CreateInput(sceneName, overlayName, "browser_source", sourceSettings, true);
                _socket.SetInputSettings(overlayName, sourceSettings);
                await Task.Delay(5000); // Record for 5 seconds
                _socket.SetSceneItemEnabled(sceneName, inputId, false);

                await Task.Run(() => _socket.SaveReplayBuffer());
            }
        }

        private void OnReplayBufferSaved(object sender, ReplayBufferSavedEventArgs e)
        {
            logger.Info($"Replay buffer saved to: {e.SavedReplayPath}");
            if (!string.IsNullOrEmpty(_filename))
            {
                if( RenameFile(e.SavedReplayPath, _filename))
                {
                    _filename = null;
                    _eventType = string.Empty;
                    _eventName = string.Empty;
                }
            }
        }

        private bool RenameFile(string existingPath, string newFilename)
        {
            if (string.IsNullOrEmpty(existingPath) && File.Exists(existingPath))
            {
                try
                {
                    string? directoryPath = Path.GetDirectoryName(existingPath);
                    string extension = Path.GetExtension(existingPath);

                    string newPath = Path.Combine(directoryPath, newFilename + extension);
                    File.Move(existingPath, newPath);
                    logger.Info($"Renamed file from {existingPath} to {newPath}");

                    ReplayBufferEvent replayEvent = new ReplayBufferEvent
                    {
                        EventTime = DateTime.Now,
                        EventType = _eventType,
                        EventName = _eventName,
                        Filepath = newPath
                    };

                    replayBufferEvents.Add(replayEvent);

                    return true;
                } 
                catch (IOException iOException)
                {
                    logger.Error($"Failed to rename file: {iOException.Message}");
                }
            }
            return false;
        }
    }

    public class ReplayBufferEvent
    {
        public DateTime EventTime { get; set; } = DateTime.Now;
        public string EventType { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string Filepath { get; set; } = string.Empty;
    }
}
