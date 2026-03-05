using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.IO;
using Tailgrab.AvatarManagement;
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
        AvatarManagementService? avatarManager = null;
        PlayerManager? playerManager = null;
        OllamaClient? ollamaAPIClient = null;
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

                logger.Info("Starting VR Chat API Client...");
                await vrcAPIClient.Initialize();

                logger.Info("Starting OLLama API Client...");
                ollamaAPIClient = new OllamaClient(this);

                logger.Info("Starting Avatar Manager...");
                avatarManager = new AvatarManagementService(this);

                logger.Info("Starting Player Manager...");
                playerManager = new PlayerManager(this);

                playerManager.SyncAvatarModerations();

                logger.Info("Starting Avatar GIST Manager...");
                AvatarBosGistListManager avatarGistMgr = new AvatarBosGistListManager(avatarManager);
                _ = Task.Run(() => avatarGistMgr.ProcessAvatarGistList());

                logger.Info("Starting Group GIST Manager...");
                GroupBosGistListManager groupGistMgr = new GroupBosGistListManager(dbContext, playerManager);
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

        public AvatarManagementService GetAvatarManager()
        {
            if (avatarManager == null)
            {
                throw new InvalidOperationException("Avatar Manager has not been initialized. Call StartAllServices() first.");
            }
            return avatarManager;
        }
    }
}
