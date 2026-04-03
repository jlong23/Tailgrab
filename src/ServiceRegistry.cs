using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.IO;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;

namespace Tailgrab
{
    public class ServiceRegistry
    {
        TailgrabDBContext? dbContext = null;
        VRChatClient vrcAPIClient = new VRChatClient();
        PlayerManager? playerManager = null;
        OllamaClient? ollamaAPIClient = null;
        AvatarBosGistListManager? avatarGistMgr = null;
        GroupBosGistListManager? groupGistMgr = null;
        static Logger logger = LogManager.GetCurrentClassLogger();
        ServiceCollection services = new ServiceCollection();

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

                logger.Info("Starting VR Chat API Client...");
                await vrcAPIClient.Initialize();

                logger.Info("Starting OLLama API Client...");
                ollamaAPIClient = new OllamaClient(this);

                logger.Info("Starting Player Manager...");
                playerManager = new PlayerManager(this);

                bool saveAvatars = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, true);
                if (saveAvatars)
                {
                    playerManager.SyncAvatarModerations();
                }

                logger.Info("Starting Avatar GIST Manager...");
                avatarGistMgr = new AvatarBosGistListManager();
                _ = Task.Run(() => avatarGistMgr.ProcessAvatarGistList());

                logger.Info("Starting Group GIST Manager...");
                groupGistMgr = new GroupBosGistListManager(dbContext, playerManager);
                _ = Task.Run(() => groupGistMgr.ProcessGroupGistList());

                logger.Info("All services started.");
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

        public async void ProcessAvatarGist()
        {
            if (avatarGistMgr == null)
            {
                logger.Info("Avatar GIST Manager not initialized, creating new instance...");
                avatarGistMgr = new AvatarBosGistListManager();
            }

            logger.Info("Processing Avatar GIST list on demand...");
            await avatarGistMgr.ProcessAvatarGistList();
            logger.Info("Avatar GIST list processing completed.");
        }

        public async void ProcessGroupGist()
        {
            if (groupGistMgr == null)
            {
                if (dbContext == null || playerManager == null)
                {
                    throw new InvalidOperationException("Database context and Player Manager must be initialized before processing Group GIST.");
                }
                logger.Info("Group GIST Manager not initialized, creating new instance...");
                groupGistMgr = new GroupBosGistListManager(dbContext, playerManager);
            }

            logger.Info("Processing Group GIST list on demand...");
            await groupGistMgr.ProcessGroupGistList();
            logger.Info("Group GIST list processing completed.");
        }
    }
}
