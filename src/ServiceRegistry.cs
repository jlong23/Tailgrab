using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.IO;
using Tailgrab.Clients.VRCDB;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
using Tailgrab.Clients.XSOverlay;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;

namespace Tailgrab
{
    public class ServiceRegistry
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

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
        ServiceCollection services = new ServiceCollection();
        private VRCDBClient _VRCDBClient = new VRCDBClient();

        public VRCDBClient GetVRCDBClient()
        {
            return _VRCDBClient;
        }

        public ServiceRegistry()
        {
        }

        public async void StartAllServices()
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

                dbContext = serviceProvider.GetService<TailgrabDBContext>();
                if (dbContext == null)
                {
                    System.Windows.MessageBox.Show("Failed to initialize database context. Please check the application logs for details.", "Initialization Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    throw new InvalidOperationException("TailgrabDBContext could not be resolved from the service provider.");
                }
                dbContext.Database.EnsureCreated();
                dbContext.UpgradeDatabase();

                logger.Info("Starting Configuration Manager...");
                configurationManager = new ConfigurationManager(this);

                logger.Info("Starting VR Chat API Client...");
                await vrcAPIClient.Initialize();

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

                bool saveAvatars = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, true);
                if (saveAvatars)
                {
                    logger.Info("Syncing avatar moderations...");
                    avatarManager.SyncAvatarModerations();
                }

                logger.Info("Starting Group Manager...");
                groupManager = new GroupManager(this);
                _ = Task.Run(() => groupManager.ProcessGroupGistList( null, false ));

                logger.Info("All services started.");

                // Get Active and Closed moderation reports to ensure the database is up to date
                await moderationManager.GetModerationReports(false);
                await moderationManager.GetModerationReports(true);

            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }


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
    }
}
