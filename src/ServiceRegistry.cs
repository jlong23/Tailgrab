using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Tailgrab.AvatarManagement;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
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
            logger.Info("Starting all services...");

            logger.Info("Starting dbContext...");

            services.AddDbContext<TailgrabDBContext>(options => options.UseSqlite("Data Source=./data/avatars.sqlite"));
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            dbContext = serviceProvider.GetService<TailgrabDBContext>();

            logger.Info("Starting VR Chat API Client...");
            await vrcAPIClient.Initialize();

            logger.Info("Starting OLLama API Client...");
            ollamaAPIClient = new OllamaClient(this);

            logger.Info("Starting Avatar Manager...");
            avatarManager = new AvatarManagementService(this);

            logger.Info("Starting Player Manager...");
            playerManager = new PlayerManager(this);

            logger.Info("All services started.");
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
