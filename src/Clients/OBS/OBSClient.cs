using Newtonsoft.Json.Linq;
using NLog;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types; // Added to resolve ObsDisconnectionInfo
using OBSWebsocketDotNet.Types.Events; // Add this
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

namespace Tailgrab.Clients.OBS
{
#pragma warning disable CS8602 // Dereference of a possibly null reference.
    public class OBSClient
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected OBSWebsocket? client; 

        protected Stopwatch _stopwatch = new Stopwatch();

        private readonly Queue<string> _messages = new Queue<string>();

        public int MaxLines { get; set; } = 20;

        protected string _filename = string.Empty;
        public string Filename
        {
            get { return _filename; }
        }

        protected string _eventType = string.Empty;
        public string EventType
        {
            get { return _eventType; }
        }

        protected string _eventName = string.Empty;
        public string EventName 
        { 
            get { return _eventName; } 
        }


        protected string _previousScene = string.Empty;
        public string PreviousScene
        {
            get => _previousScene;
            set => _previousScene = value;  
        }

        public bool IsConnected()
        {
            return client != null && client.IsConnected;
        }

        private List<ReplayBufferEvent> _replayBufferEvents = new List<ReplayBufferEvent>();
        public List<ReplayBufferEvent> ReplayBufferEvents { get => _replayBufferEvents; }

        protected List<SessionChapterEvent> SessionEvent = new List<SessionChapterEvent>();

        #region Hide the Registry Configuration for OBSClient 
        public bool EnableOBSIntegration
        {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_Enable, false);
            }

            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_Enable, value);
            }
        }

        public bool StartReplayBuffer {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_StartReplayBuffer, false);
            }

            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_StartReplayBuffer, value);
            }
        }

        public bool StartVirtualCamera {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_StartVirtualCamera, false);
            }

            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_StartVirtualCamera, value);
            }
        }

        public bool RecordOnWorldJoin
        {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_StartRecordOnWorldJoin, false);
            }
            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_StartRecordOnWorldJoin, value);
            }
        }

        public bool CreateMP4Chapters
        {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMp4, false);
            }
            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMp4, value);
            }
        }

        public bool CreateMKVChapters
        {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMkv, false);
            }
            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMkv, value);
            }
        }

        public string MKVMergePath
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_MKVMergePath) ?? string.Empty;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_MKVMergePath, value);
            }
        }

        public string FFMpegPath
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_FFMpegPath) ?? string.Empty;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_FFMpegPath, value);
            }
        }

        public bool KickBanEvents
        {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_SaveReplayBufferOnKickBan, false);
            }
            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_SaveReplayBufferOnKickBan, value);
            }
        }

        public string KickBanSceneName
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Scene_Name) ?? string.Empty;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Scene_Name, value);
            }
        }

        public string KickBanBrowserSourceName
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Browser_Name) ?? string.Empty;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Browser_Name, value);
            }
        }

        public bool ImageSpawnEvents
        {
            get
            {
                return ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_UserImageSpawnEvents, false);
            }
            set
            {
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_UserImageSpawnEvents, value);
            }
        }

        public string ImageSpawnSceneName
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_SceneName) ?? string.Empty;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_SceneName, value);
            }
        }

        public string ImageSpawnBrowserSourceName
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_BrowserName) ?? string.Empty;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_BrowserName, value);
            }
        }


        public string OBSWebsocketURI
        {
            get
            {
                return ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_WSURI) ?? CommonConst.Default_OBS_WSURI;
            }
            set
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_WSURI, value);
            }
        }

        public string OBSPassword
        {
            get
            {
                return ConfigStore.LoadSecret(CommonConst.Registry_OBS_Password) ?? string.Empty;
            }
            set
            {
                ConfigStore.SaveSecret(CommonConst.Registry_OBS_Password, value);
            }
        }
        #endregion


        public async Task<bool> Initialize()
        {
            string _connectionString = OBSWebsocketURI;
            string _password = OBSPassword;

            logger.Info($"Initializing OBSClient with connection string: {_connectionString} / {_password}");

            try
            {
                client = new OBSWebsocket();
                // Subscribe to the OBS Events
                client.ReplayBufferSaved += OnReplayBufferSaved;
                client.Connected += OnSocketConnected;
                client.Disconnected += OnSocketDisconnected;
                client.RecordStateChanged += OnOutputStateChanged;

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
        
        public async Task Disconnect()
        {
            if( IsConnected())
            {
                logger.Info("Disconnecting from OBS...");
                client.StopReplayBuffer();
                client.ToggleVirtualCam();
                await Task.Run(() => client.Disconnect());
            }
        }

        public async Task SaveReplayBuffer()
        {
            if(!IsConnected()) return;

            if(KickBanEvents)
            {
                await Task.Run(() => client.SaveReplayBuffer());
            }
        }

        public async Task ImageExposeEvent(string html, Player player, string spawnType)
        {
            if( !IsConnected()) return;

            ChapterEvent($"{spawnType} - {player.DisplayName}");

            string sceneName = ImageSpawnSceneName;
            string overlayName = ImageSpawnBrowserSourceName;
            logger.Info($"ImageExposeEvent( {sceneName}, {overlayName}, html)");
            await DisplayBrowserSourceOverlay(sceneName, overlayName, html, 5000);
        }

        public async Task VTKRecording(Player player, string html)
        {
            if(!IsConnected()) return;

            string chapter = $"VTK - {player.DisplayName}";
            ChapterEvent(chapter);

            string sceneName = KickBanSceneName;
            string overlayName = KickBanBrowserSourceName;
            try
            {
                _filename = $"{player.UserId}-{player.DisplayName}_{DateTime.Now:yyyyMMdd_HHmmss}";
                _eventType = "VoteToKick";
                _eventName = _filename;
                await DisplayBrowserSourceOverlay(sceneName, overlayName, html, 5000);
                await SaveReplayBuffer();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public async Task BanKickWarnRecording(Player player, string html, string action)
        {
            if (!IsConnected()) return;

            string chapter = $"MOD {action} - {player.DisplayName}";
            ChapterEvent(chapter);

            string sceneName = KickBanSceneName;
            string overlayName = KickBanBrowserSourceName;
            try
            {
                _filename = $"{player.UserId}-{player.DisplayName}_{action}_{DateTime.Now:yyyyMMdd_HHmmss}";
                _eventType = "BanKickWarn";
                _eventName = _filename;
                await DisplayBrowserSourceOverlay(sceneName, overlayName, html, 5000);
                await SaveReplayBuffer();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public async Task SessionRecording(bool start, WorldInstanceInfo sessionInfo)
        {
            if( !IsConnected()) return;
            StringBuilder sb = new StringBuilder();
            sb.Append($"{sessionInfo.WorldName}");
            if(!string.IsNullOrEmpty(sessionInfo.GroupName))
            {
                sb.Append($"-{sessionInfo.GroupName}");
            }
            else
            {
                sb.Append($"-{sessionInfo.UserName}");
            }
            string session = sb.ToString();
            string chapterName = start ? $"Start - {session}" : $"Stop - {session}";
            ChapterEvent(chapterName);

            try
            {
                if( client.GetRecordStatus().IsRecording && !start)
                {
                    _stopwatch.Stop();
                    client.StopRecord();
                }
                else if (!client.GetRecordStatus().IsRecording && start)
                {
                    _stopwatch.Start();
                    client.StartRecord();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void ChapterEvent(string eventName)
        {
            if (!IsConnected()) return;
            SessionChapterEvent chapterEvent = new SessionChapterEvent
            {
                // Subtract 10 seconds to account for the replay buffer delay
                EventTime = _stopwatch.Elapsed - TimeSpan.FromSeconds(10), 
                EventName = eventName
            };
            SessionEvent.Add(chapterEvent);
            logger.Info($"ChapterEvent: {chapterEvent}");
        }

        private void SaveCurrentScene()
        {
            if(!IsConnected()) return;

            PreviousScene = client.GetCurrentProgramScene();
        }

        private void RestorePreviousScene()
        {
            if(!IsConnected()) return;
            if (!string.IsNullOrEmpty(PreviousScene))
            {
                client.SetCurrentProgramScene(PreviousScene);
            }
        }
        private async Task DisplayBrowserSourceOverlay(string sceneName, string overlayName, string html, int durationMS)
        {
            try
            {
                logger.Info($"DisplayBrowserSourceOverlay( {sceneName}, {overlayName}, html, {durationMS})");

                SaveCurrentScene();

                var sourceSettings = new JObject
                    {
                        { "url", $"data:text/html,{Uri.EscapeDataString(html)}" },
                        { "width", 1920 },
                        { "height", 1080 },
                        { "local_file", false }
                    };
                int inputId = client.GetSceneItemId(sceneName, overlayName, 0);
                client.SetInputSettings(overlayName, sourceSettings, true);
                client.PressInputPropertiesButton(overlayName, "refreshnocache");
                client.SetSceneItemEnabled(sceneName, inputId, true);
                client.SetCurrentProgramScene(sceneName);

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
            if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath))
            {
                try
                {
                    string? directoryPath = Path.GetDirectoryName(existingPath) ?? string.Empty;
                    string extension = Path.GetExtension(existingPath);

                    string newPath = Path.Combine(directoryPath, newFilename + extension);
                    logger.Info($"Attempting to rename file from {existingPath} to {newPath}");
                    File.Move(existingPath, newPath);
                    logger.Info($"Successfully renamed file from {existingPath} to {newPath}");

                    ReplayBufferEvent replayEvent = new ReplayBufferEvent
                    {
                        EventTime = DateTime.Now,
                        EventType = _eventType,
                        EventName = _eventName,
                        Filepath = newPath
                    };

                    _replayBufferEvents.Add(replayEvent);

                    return true;
                }
                catch (IOException iOException)
                {
                    logger.Error($"Failed to rename file: {iOException.Message}");
                }
            }
            return false;
        }

        #region Message Log Management
        public void AddMessage(string message)
        {
            if (!IsConnected()) return;

            while (_messages.Count > MaxLines)
                _messages.Dequeue();
            _messages.Enqueue(message);

            UpdateMessageHtml(); 
        }

        public string BuildMessageLogHtml()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset=\"utf-8\">");
            sb.AppendLine("<style>");
            sb.AppendLine("  body { font-family: monospace; font-size: 12px; color: #d4d4d4; margin: 0; padding: 10px; }");
            sb.AppendLine("  .log {");
            sb.AppendLine("    height: 20em;");
            sb.AppendLine("    overflow-y: auto;");
            sb.AppendLine("    -webkit-mask-image: linear-gradient(to bottom, transparent 0, black 3em);");
            sb.AppendLine("    mask-image: linear-gradient(to bottom, transparent 0, black 3em);");
            sb.AppendLine("  }");
            sb.AppendLine("  .log::-webkit-scrollbar {");
            sb.AppendLine("    display: none;                  /* Chrome/Safari */");
            sb.AppendLine("  }");
            sb.AppendLine("  .line { white-space: pre-wrap; word-break: break-all; line-height: 1.4; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<div class=\"log\" id=\"log\">");

            foreach (var msg in _messages)
                sb.AppendLine($"<div class=\"line\">{System.Net.WebUtility.HtmlEncode(msg)}</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("<script>document.getElementById('log').scrollTop = 99999;</script>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        public void UpdateMessageHtml()
        {
            string sceneName = "VRChat";
            string overlayName = "MessageOverlay";
            string html = BuildMessageLogHtml();

            var sourceSettings = new JObject
                    {
                        { "url", $"data:text/html,{Uri.EscapeDataString(html)}" },
                        { "width", 400 },
                        { "height", 400 },
                        { "local_file", false }
                    };
            int inputId = client.GetSceneItemId(sceneName, overlayName, 0);
            client.SetInputSettings(overlayName, sourceSettings, true);
            client.PressInputPropertiesButton(overlayName, "refreshnocache");

        }
        #endregion


        #region Event Handlers
        private void OnSocketConnected(object? sender, System.EventArgs e)
        {
            logger.Info("OBS websocket connected.");
            try
            {
                if (IsConnected())
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
                logger.Info($"Attempting to rename: {e.SavedReplayPath} to {_filename}");
                if (RenameFile(e.SavedReplayPath, _filename))
                {
                    logger.Info($"Successfully renamed: {e.SavedReplayPath} to {_filename}");
                    _filename = string.Empty;
                    _eventType = string.Empty;
                    _eventName = string.Empty;
                }
                RestorePreviousScene();
            }
        }

        private void OnOutputStateChanged(object? sender, RecordStateChangedEventArgs e)
        {
            if( e.OutputState.State == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                logger.Info($"Recording started at: {DateTime.Now}");
            }
            else if (e.OutputState.State == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
            {
                logger.Info($"Recording stopped at: {DateTime.Now}");
                logger.Info($"Output path: {e.OutputState.OutputPath}");
                string sourcePath = e.OutputState.OutputPath;
                string xmlPath = Path.ChangeExtension(sourcePath, ".xml");
                string mkvPath = Path.ChangeExtension(sourcePath, ".mkv");
                string mp4Path = Path.ChangeExtension(sourcePath, "-converted.mp4");
                if(CreateMKVChapters)
                {
                    logger.Info($"Creating MKV chapters for: {mkvPath}");
                    WriteChaptersXml(xmlPath);
                    AddChaptersToMkv(sourcePath, mkvPath, xmlPath);
                }

                if (CreateMP4Chapters)
                {
                    logger.Info($"Creating MP4 chapters for: {mp4Path}");
                    AddChaptersToMp4(sourcePath, mp4Path, BuildMetadata(SessionEvent));
                }

                SessionEvent.Clear();

            }
        }
        #endregion

        #region mkvmerge Integration
        public void WriteChaptersXml(string outputPath)
        {
            logger.Info($"Writing chapters XML to: {outputPath}");
            var chapters = new XElement("Chapters");
            var edition = new XElement("EditionEntry");

            foreach (SessionChapterEvent evt in SessionEvent.OrderBy(e => e.EventTime))
            {
                logger.Info($"Adding chapter: {evt}");
                edition.Add(new XElement("ChapterAtom",
                    new XElement("ChapterTimeStart", evt.EventTime.ToString(@"hh\:mm\:ss\.fff")),
                    new XElement("ChapterDisplay",
                        new XElement("ChapterString", evt.EventName),
                        new XElement("ChapterLanguage", "eng")
                    )
                ));
            }

            chapters.Add(edition);

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                chapters
            );

            doc.Save(outputPath);
        }

        public void AddChaptersToMkv(string inputMkv, string outputMkv, string chaptersXmlPath)
        {
            string path = MKVMergePath; //@"C:\Program Files\mkvtoolnix\mkvmerge.exe";
            if (!File.Exists(path))
            {
                logger.Warn($"mkvmerge not found at {path}. Skipping chapter addition for MKV.");
                return;
            }
                
            logger.Info($"Adding chapters to MKV: {inputMkv} -> {outputMkv} using {chaptersXmlPath}");
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = $"-o \"{outputMkv}\" --chapters \"{chaptersXmlPath}\" \"{inputMkv}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi)!;
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new Exception($"mkvmerge failed: {err}");
        }
        #endregion

        #region FFMpeg Integration
        public string BuildMetadata(List<SessionChapterEvent> chapters)
        {
            var sb = new StringBuilder(";FFMETADATA1\n");

            for (int i = 0; i < chapters.Count; i++)
            {
                long startMs = (long)chapters[i].EventTime.TotalMilliseconds;
                long endMs = i + 1 < chapters.Count
                    ? (long)chapters[i + 1].EventTime.TotalMilliseconds - 1
                    : (long)chapters[i].EventTime.TotalMilliseconds + (long)TimeSpan.FromSeconds(10).TotalMilliseconds;

                sb.AppendLine("[CHAPTER]");
                sb.AppendLine("TIMEBASE=1/1000");
                sb.AppendLine($"START={startMs}");
                sb.AppendLine($"END={endMs}");
                sb.AppendLine($"title={chapters[i].EventName}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void AddChaptersToMp4(string inputMp4, string outputMp4, string metadataText)
        {
            var path = FFMpegPath; //@"D:\dev\ffmpeg\bin\ffmpeg.exe";
            if(!File.Exists(path))
            {
                logger.Warn($"FFmpeg not found at {path}. Skipping chapter addition for MP4.");
                return;
            }

            string metaPath = Path.GetTempFileName();
            File.WriteAllText(metaPath, metadataText);

            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = $"-y -i \"{inputMp4}\" -i \"{metaPath}\" -map_metadata 1 -map_chapters 1 -c copy \"{outputMp4}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi)!;
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            File.Delete(metaPath);

            if (proc.ExitCode != 0)
                throw new Exception($"FFmpeg failed: {err}");
        }
        #endregion
    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

    public class ReplayBufferEvent
    {
        public DateTime EventTime { get; set; } = DateTime.Now;
        public string EventType { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string Filepath { get; set; } = string.Empty;
    }

    public class SessionChapterEvent
    {
        public TimeSpan EventTime { get; set; } = TimeSpan.Zero;
        public string EventName { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{EventTime:hh\\:mm\\:ss\\.fff} - {EventName}";
        }
    }
}
