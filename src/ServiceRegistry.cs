using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NLog;
using OllamaSharp;
using System.IO;
using System.Numerics;
using Tailgrab.Clients.Audio;
using Tailgrab.Clients.OBS;
using Tailgrab.Clients.Office;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.Omnivoice;
using Tailgrab.Clients.OSC;
using Tailgrab.Clients.VRCDB;
using Tailgrab.Clients.VRChat;
using Tailgrab.Clients.XSOverlay;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.Manager;
using Tailgrab.MCP;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using Tailgrab.PlayerManagement.SetupWizard;

namespace Tailgrab
{
    public class ServiceRegistry
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new();

        TailgrabDBContext? dbContext = null;
        VRChatClient vrcAPIClient = new VRChatClient();
        PlayerManager? playerManager = null;
        OllamaClient? ollamaAPIClient = null;
        OverlayManager xsOverlay = new OverlayManager();
        InventoryManager? inventoryManager = null;
        PrintManager? printManager = null;   
        AIEvaluationManager? aiEvaluationManager = null;
        AvatarManager? avatarManager = null;
        GroupManager? groupManager = null;
        ModerationManager? moderationManager = null;
        ConfigurationManager? configurationManager = null;
        TTSManager? ttsManager = null;
        ServiceCollection services = new ServiceCollection();
        private VRCDBClient _VRCDBClient = new VRCDBClient();
        OfficeClient? officeClient;
        McpSubscriptionManager? _subscriptionManager = null;
        VRChatOSCChatbox? oscChatbox = new VRChatOSCChatbox();
        OmniVoiceClient? omniVoiceClient = new OmniVoiceClient();
        MultiDevicePlayer? multiDevicePlayer = new MultiDevicePlayer();


        private OBSClient? obsClient;

        public ServiceRegistry(OBSClient obsClient, OfficeClient officeClient)
        {
            this.obsClient = obsClient;
            this.officeClient = officeClient;
            _ = Task.Run(() => ProcessEventQueue(priorityQueue, this));
        }

        public async Task StartAllServices()
        {
            try
            {
                logger.Info("Starting all services...");

                logger.Info("Starting dbContext...");

                // Define directory: %LOCALAPPDATA%\YourAppName
                string dbFolder = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "data");
                string dbPath = Path.Combine(dbFolder, CommonConst.APPLICATION_LOCAL_DATABASE);

                services.AddDbContext<TailgrabDBContext>(options => options.UseSqlite($"Data Source={dbPath}"));
                IServiceProvider serviceProvider = services.BuildServiceProvider();

                // Must have
                dbContext = serviceProvider.GetService<TailgrabDBContext>();
                if (dbContext == null)
                {
                    System.Windows.MessageBox.Show("Failed to initialize database context. Please check the application logs for details.", "Initialization Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    throw new InvalidOperationException("TailgrabDBContext could not be resolved from the service provider.");
                }
                dbContext.Database.EnsureCreated();
                dbContext.UpgradeDatabase();

                // Must have
                logger.Info("Starting Configuration Manager...");
                configurationManager = new ConfigurationManager(this);

                // Needs to run, but can wait
                logger.Info("Starting VR Chat API Client...");           
                bool result = await vrcAPIClient.Initialize();
                if (!result)
                {
                    System.Windows.MessageBox.Show("Failed to initialize VRChat API Client. Please check the application logs for details.", "Initialization Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    //throw new InvalidOperationException("Failed to initialize VRChat API Client. Please check the application logs for details.");
                }

                logger.Info("Starting OLLama API Client...");
                ollamaAPIClient = new OllamaClient(this);

                logger.Info("Starting Player Manager...");
                playerManager = new PlayerManager(this);

                logger.Info("Starting Inventory Manager...");
                inventoryManager = new InventoryManager(this);

                logger.Info("Starting AI Evaluation Manager...");
                aiEvaluationManager = new AIEvaluationManager(this);

                logger.Info("Starting Print Manager...");
                printManager = new PrintManager(this);

                logger.Info("Starting Avatar Manager...");
                avatarManager = new AvatarManager(this);
                _ = Task.Run(() => avatarManager.ProcessAvatarGistList());

                logger.Info("Starting Moderation Manager...");
                moderationManager = new ModerationManager(this);

                logger.Info("Starting TTS Manager...");
                ttsManager = new TTSManager(this);

                bool saveAvatars = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, true);
                if (saveAvatars)
                {
                    logger.Info("Syncing avatar moderations...");
                    avatarManager.SyncAvatarModerations();
                }

                logger.Info("Starting Group Manager...");
                groupManager = new GroupManager(this);
                _ = Task.Run(() => groupManager.ProcessGroupGistList( null, false ));

                logger.Info("Starting XS Overlay Manager...");
                xsOverlay = new OverlayManager();
                _ = Task.Run(() => xsOverlay.Initialize());

                logger.Info("Starting OBS Client...");
                GetOBSClient();

                logger.Info("Office Client initialized via dependency injection.");

                logger.Info("All services started.");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public async void ShutdownAllServices()
        {
            try
            {
                logger.Info("Shutting down all services...");

                if(obsClient != null)
                {
                    await obsClient.Disconnect();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        #region Get Service Instances
        public VRChatClient GetVRChatAPIClient()
        {
            return vrcAPIClient;
        }

        public TailgrabDBContext GetDBContext()
        {
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            var context = serviceProvider.GetService<TailgrabDBContext>();
            if (context == null)
            {
                throw new InvalidOperationException("TailgrabDBContext could not be resolved from the service provider.");
            }
            return context;
        }

        public PlayerManager GetPlayerManager()
        {
            if (playerManager == null)
            {
                throw new InvalidOperationException("Player Manager has not been initialized. Call StartAllServices() first.");
            }
            return playerManager;
        }

        public OllamaClient GetOllamaAPIClient()
        {
            if (ollamaAPIClient == null)
            {
                throw new InvalidOperationException("Ollama API Client has not been initialized. Call StartAllServices() first.");
            }
            return ollamaAPIClient;
        }

        public OverlayManager GetXSOverlay()
        {
            return xsOverlay;
        }

        public InventoryManager GetInventoryManager()
        {
            if (inventoryManager == null)
            {
                throw new InvalidOperationException("Inventory Manager has not been initialized. Call StartAllServices() first.");
            }
            return inventoryManager;
        }

        public AIEvaluationManager GetAIEvaluationManager()
        {
            if (aiEvaluationManager == null)
            {
                throw new InvalidOperationException("AI Evaluation Manager has not been initialized. Call StartAllServices() first.");
            }
            return aiEvaluationManager;
        }

        public PrintManager GetPrintManager()
        {
            if (printManager == null)
            {
                throw new InvalidOperationException("Print Manager has not been initialized. Call StartAllServices() first.");
            }
            return printManager;
        }

        public AvatarManager GetAvatarManager()
        {
            if (avatarManager == null)
            {
                throw new InvalidOperationException("Avatar Manager has not been initialized. Call StartAllServices() first.");
            }
            return avatarManager;
        }

        public GroupManager GetGroupManager()
        {
            if (groupManager == null)
            {
                throw new InvalidOperationException("Group Manager has not been initialized. Call StartAllServices() first.");
            }
            return groupManager;
        }

        public ModerationManager GetModerationManager()
        {
            if (moderationManager == null)
            {
                throw new InvalidOperationException("Moderation Manager has not been initialized. Call StartAllServices() first.");
            }
            return moderationManager;
        }

        public ConfigurationManager GetConfigurationManager()
        {
            if (configurationManager == null)
            {
                throw new InvalidOperationException("Configuration Manager has not been initialized. Call StartAllServices() first.");
            }
            return configurationManager;
        }

        public TTSManager GetTTSManager()
        {
            if (ttsManager == null)
            {
                throw new InvalidOperationException("TTS Manager has not been initialized. Call StartAllServices() first.");
            }
            return ttsManager;
        }

        public OBSClient? GetOBSClient()
        {
            bool enableOBS = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_Enable, false);
            if (enableOBS && obsClient != null)
            {
                try
                {
                    if (!obsClient.IsConnected())
                    {
                        Task.Run(() => obsClient.Initialize().ConfigureAwait(true)).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to initialize OBS Client. Please ensure OBS is running and the WebSocket server is enabled.");
                    return null;
                }
            }

            return enableOBS ? obsClient : null;
        }

        public OmniVoiceClient GetOmniVoiceClient()
        {
            if (omniVoiceClient == null)
            {
                throw new InvalidOperationException("OmniVoice Client has not been initialized. Call StartAllServices() first.");
            }
            return omniVoiceClient;
        }

        public MultiDevicePlayer? GetMultiDevicePlayer()
        {
            if (multiDevicePlayer == null)
            {
                throw new InvalidOperationException("MultiDevicePlayer has not been initialized. Call StartAllServices() first.");
            }
            return multiDevicePlayer;
        }

        public OfficeClient GetOfficeClient()
        {
            if (officeClient == null)
            {
                throw new InvalidOperationException("Office Client has not been initialized. Call StartAllServices() first.");
            }
            return officeClient;
        }

        public VRChatOSCChatbox GetVRChatOSCChatbox()
        {
            if (oscChatbox == null)
            {
                throw new InvalidOperationException("VRChat OSC Chatbox has not been initialized. Call StartAllServices() first.");
            }
            return oscChatbox;
        }

        public VRCDBClient GetVRCDBClient()
        {
            return _VRCDBClient;
        }

        #endregion

        #region
        public void PublishEvent(IHavePriority<int> eventItem)
        {
            priorityQueue.Enqueue(eventItem);
        }

        public void ClearQueue()
        {
            while (true)
            {
                if (priorityQueue.Count == 0)
                {
                    break;
                }
                priorityQueue.Dequeue();
            }
        }

        public static async Task ProcessEventQueue(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            if (SetupWizardHelper.IsFirstTimeSetup())
            {
                logger.Info($"Event Queue Paused due to incomplete Setup");
                return;
            }

            logger.Info($"Event Queue Running");
            while (true)
            {
                // Process items from the Event queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess)
                    {
                        String eventType = result.Value.GetType().Name;
                        switch (eventType)
                        {
                            case nameof(VoiceCloneSpeechEvent):
                                logger.Info($"Processing VoiceCloneSpeechEvent item from Event Queue");
                                VoiceCloneSpeechEvent voiceEvent = (VoiceCloneSpeechEvent)result.Value;

                                ProcessVoiceSynthesisEvent(serviceRegistry, voiceEvent);
                                break;
                            case nameof(OSCSendEvent):
                                logger.Info($"Processing OSCSendEvent item from Event Queue");
                                OSCSendEvent oscEvent = (OSCSendEvent)result.Value;

                                ProcessOSCSendEvent(serviceRegistry, oscEvent);
                                break;
                            default:
                                logger.Warn($"Unknown event type: {eventType}");
                                break;
                        }
                    }
                    else
                    {
                        // No more items to process
                        break;
                    }

                    // Wait for a short period before getting next record
                    await Task.Delay(100);
                }

                // Wait for a short period before checking the queue again
                await Task.Delay(1000);
            }
        }

        private static void ProcessVoiceSynthesisEvent(ServiceRegistry serviceRegistry, VoiceCloneSpeechEvent voiceEvent)
        {
            OmniVoiceClient synthesisClient = serviceRegistry.GetOmniVoiceClient();
            if( synthesisClient.EnableTTSService == false)
                return;
            
            MultiDevicePlayer? player = serviceRegistry.GetMultiDevicePlayer();

            synthesisClient.VoiceCloneProfile = voiceEvent.VoiceProfile ?? CommonConst.OMNIVOICE_DEFAULT_PROFILE;
            byte[]? audio = Task.Run(() => synthesisClient.GetAudioForTextAsync(voiceEvent.Text)).Result;
            string outputDevice = player?.SelectedOutputDevice ?? MultiDevicePlayer.GetDefaultOutputDevice();
            if ( voiceEvent.OutputDevice != null)
                outputDevice = voiceEvent.OutputDevice;

            player?.Play(audio, outputDevice);
        }

        private static void ProcessOSCSendEvent(ServiceRegistry serviceRegistry, OSCSendEvent oscEvent)
        {
            VRChatOSCChatbox? chatbox = serviceRegistry.GetVRChatOSCChatbox();
            if (chatbox != null && chatbox.EnableOSCService == true)
            { 
                if (oscEvent.Address != null && oscEvent.DataType != null)
                {
                    switch (oscEvent.DataType.ToLower())
                    {
                        case "int":
                            if (int.TryParse(oscEvent.Text, out int intValue))
                            {
                                chatbox.SendValue(oscEvent.Address, oscEvent.DataType, intValue);
                            }
                            else
                            {
                                logger.Warn($"Failed to parse '{oscEvent.Text}' as int for OSC message.");
                            }
                            break;
                        case "float":
                            if (float.TryParse(oscEvent.Text, out float floatValue))
                            {
                                chatbox.SendValue(oscEvent.Address, oscEvent.DataType, floatValue);
                            }
                            else
                            {
                                logger.Warn($"Failed to parse '{oscEvent.Text}' as float for OSC message.");
                            }
                            break;
                        case "string":
                            chatbox.SendValue(oscEvent.Address, oscEvent.DataType, oscEvent.Text);
                            break;
                        default:
                            logger.Warn($"Unsupported DataType '{oscEvent.DataType}' for OSC message.");
                            break;
                    }
                    chatbox.SendValue(oscEvent.Address, oscEvent.DataType, oscEvent.Text);
                }
                else
                    chatbox.SendMessage(oscEvent.Text, true);
            }
            else
            {
                logger.Warn($"VRChatOSCChatbox is not initialized. Cannot send OSC message.");
            }
        }

        #endregion

        #region Convienence Methods so not to clutter up the code with event creation and publishing
        public void PublishVoiceCloneSpeechEvent(string text, string? voiceProfile, string outputDevice)
        {
            VoiceCloneSpeechEvent voiceEvent = new VoiceCloneSpeechEvent
            {
                Text = text,
                VoiceProfile = voiceProfile,
                OutputDevice = outputDevice
            };
            PublishEvent(voiceEvent);
        }

        public void PublishOSCMessageEvent(string text)
        {
            OSCSendEvent oscEvent = new OSCSendEvent
            {
                Text = text
            };
            PublishEvent(oscEvent);            
        }

        #endregion

        public async Task ProcessAvatarGist()
        {
            if (avatarManager == null)
            {
                logger.Info("Avatar GIST Manager not initialized, creating new instance...");
                avatarManager = new AvatarManager(this);
            }

            logger.Info("Processing Avatar GIST list on demand...");
            await avatarManager.ProcessAvatarGistList();
            logger.Info("Avatar GIST list processing completed.");
        }

        public async Task ProcessGroupGist( string gistUrl, bool ignoreChecksum )
        {
            if (groupManager == null)
            {
                if (dbContext == null || playerManager == null)
                {
                    throw new InvalidOperationException("Database context and Player Manager must be initialized before processing Group GIST.");
                }
                logger.Info("Group Manager not initialized, creating new instance...");
                groupManager = new GroupManager(this);
            }

            logger.Info("Processing Group GIST list on demand...");
            await groupManager.ProcessGroupGistList( gistUrl, ignoreChecksum );
            logger.Info("Group GIST list processing completed.");
        }

        public McpSubscriptionManager GetSubscriptionManager()
        {
            if (_subscriptionManager == null)
            {
                _subscriptionManager = new McpSubscriptionManager();
            }
            return _subscriptionManager;
        }
    }

    #region Event Classes
    public class AudioEndpointChangeEvent : IHavePriority<int>
    {
        public int Priority { get; set; } = 100;
    }

    public class VoiceCloneSpeechEvent: IHavePriority<int>
    {
        public int Priority { get; set; } = 500;

        public string Text { get; set; } = string.Empty;
        public string? VoiceProfile { get; set; }
        public string OutputDevice { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"VoiceCloneSpeechEvent(Priority={Priority}, Text={Text}, VoiceProfile={VoiceProfile ?? CommonConst.OMNIVOICE_DEFAULT_PROFILE}, OutputDevice={OutputDevice})";
        }
    }

    public class OSCEnablementEvent : IHavePriority<int>
    {
        public int Priority { get; set; } = 100;
    }

    public class OSCSendEvent : IHavePriority<int>
    {
        public int Priority { get; set; } = 200;

        public string Text { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? DataType { get; set; }
    }

    #endregion
}
