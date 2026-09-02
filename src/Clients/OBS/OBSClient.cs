using Newtonsoft.Json.Linq;
using NLog;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types; // Added to resolve ObsDisconnectionInfo
using OBSWebsocketDotNet.Types.Events; // Add this
using OBSWebsocketDotNet.Communication;
using System.IO;

namespace Tailgrab.Clients.OBS
{
    public class OBSClient
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private string _connectionString;
        private string _password;

        protected OBSWebsocket client; 

        protected string _filename = string.Empty;
        protected string _eventType = string.Empty;
        protected string _eventName = string.Empty;

        protected string previousScene = string.Empty;

        private static List<ReplayBufferEvent> replayBufferEvents = [];

        public static List<ReplayBufferEvent> ReplayBufferEvents { get => replayBufferEvents; }

        public async Task<bool> Initialize( string connectionString, string password )
        {
            _connectionString = connectionString;
            _password = password;
            try
            {
                client = new OBSWebsocket();
                // Subscribe to the OBS Events
                client.ReplayBufferSaved += OnReplayBufferSaved;
                client.Connected += OnSocketConnected;
                client.Disconnected += OnSocketDisconnected;

                // ConnectAsync returns void in the OBS API; run it on a background thread and await that task.
                await Task.Run(() => client.ConnectAsync(_connectionString, _password)).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex) 
            { 
                logger.Error( ex );
            }
            return false;
        }

        private bool GetSocketConnectedState()
        {
            if( client != null )
            {
                bool connected = client.IsConnected;
                if (connected)
                    return connected;
            }

            return false;
        }
        
        public async Task Disconnect()
        {
            if (client != null && client.IsConnected)
            {
                client.StopReplayBuffer();
                client.ToggleVirtualCam();
                await Task.Run(() => client.Disconnect());
            }
        }

        public async Task SaveReplayBuffer()
        {
            if(GetSocketConnectedState())
            {
                await Task.Run(() => client.SaveReplayBuffer());

            }
        }

        public async Task ImageExposeEvent(string sceneName, string overlayName, string html)
        {
            if (GetSocketConnectedState())
            {
                await DisplayBrowserSourceOverlay(sceneName, overlayName, html, 5000);
            }
        }

        public async Task VTKRecording(string sceneName, string overlayName, string filename, string html)
        {
            if (GetSocketConnectedState())
            {
                try
                {
                    await DisplayBrowserSourceOverlay(sceneName, overlayName, html, 5000);
                    if (client.GetReplayBufferStatus() == false)
                    {
                        _filename = filename;
                        _eventType = "VoteToKick";
                        _eventName = filename;
                    }

                    await SaveReplayBuffer();
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        private void SaveCurrentScene()
        {
            previousScene = client.GetCurrentProgramScene();
        }

        private void RestorePreviousScene()
        {
            if (!string.IsNullOrEmpty(previousScene))
            {
                client.SetCurrentProgramScene(previousScene);
            }
        }
        private async Task DisplayBrowserSourceOverlay(string sceneName, string overlayName, string html, int durationMS)
        {
            try
            {
                SaveCurrentScene();

                var sourceSettings = new JObject
                    {
                        { "uri", "data:text/html,"+ Uri.EscapeDataString(html)},
                        { "width", 1920 },
                        { "height", 1080 },
                        { "local_file", false }
                    };
                int inputId = client.GetSceneItemId(sceneName, overlayName, 0);
                client.SetInputSettings(overlayName, sourceSettings);
                client.SetSceneItemEnabled(sceneName, inputId, true);
                await Task.Delay(durationMS);
                client.SetSceneItemEnabled(sceneName, inputId, false);
                RestorePreviousScene();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private bool RenameFile(string existingPath, string newFilename)
        {
            if (string.IsNullOrEmpty(existingPath) && File.Exists(existingPath))
            {
                try
                {
                    string? directoryPath = Path.GetDirectoryName(existingPath) ?? string.Empty;
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

        #region Event Handlers
        private void OnSocketConnected(object? sender, System.EventArgs e)
        {
            logger.Info("OBS websocket connected.");
            try
            {
                if (GetSocketConnectedState())
                {
                    client.StartReplayBuffer();
                    client.ToggleVirtualCam();
                    SaveCurrentScene();
                }
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void OnSocketDisconnected(object? sender, ObsDisconnectionInfo e)
        {
            logger.Info($"OBS websocket disconnected. Reason: {e?.DisconnectReason ?? "Unknown"}");
            try
            {
                // Basic cleanup or state handling on disconnect
                if (client != null && !client.IsConnected)
                {
                    // no heavy work here; caller can reconnect if needed
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
        private void OnReplayBufferSaved(object? sender, ReplayBufferSavedEventArgs e)
        {
            logger.Info($"Replay buffer saved to: {e.SavedReplayPath}");
            if (!string.IsNullOrEmpty(_filename))
            {
                if (RenameFile(e.SavedReplayPath, _filename))
                {
                    _filename = string.Empty;
                    _eventType = string.Empty;
                    _eventName = string.Empty;
                }
                RestorePreviousScene();
            }
        }
        #endregion
    }

    public class ReplayBufferEvent
    {
        public DateTime EventTime { get; set; } = DateTime.Now;
        public string EventType { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string Filepath { get; set; } = string.Empty;
    }
}
