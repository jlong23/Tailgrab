using NLog;
using System.Diagnostics.CodeAnalysis;
using Tailgrab.Clients.Ollama;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using static Tailgrab.Clients.VRChat.VRChatClient;
using VRChat.API.Model;

namespace Tailgrab.Manager
{
    public class InventoryManager
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static ServiceRegistry? serviceRegistry;

        [SetsRequiredMembers]

        public InventoryManager(ServiceRegistry registry)
        {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
        }

        public async Task<InventoryItem?> AddInventorySpawn(string userId, string inventoryId)
        {
            if ( serviceRegistry == null) {
                logger.Warn("ServiceRegistry is not initialized. Cannot fetch inventory item.");
                return null;
            }

            InventoryItem? item = null;

            Player? player = PlayerManager.GetPlayerByUserId(userId);
            if (player != null)
            {
                string itemName = "Unknown Item";
                string itemUrl = "";
                string itemContent = "";
                string inventoryType = "Unknown Type";
                string aiClassification = AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnum.NOT_AVAILABLE);
                try
                {
                    item = await serviceRegistry.GetVRChatAPIClient()?.GetUserInventoryItem(userId, inventoryId)!;
                    if (item != null)
                    {
                        logger.Debug($"{item.ToString()}");

                        itemName = item.Name ?? item.ItemType.ToString() ?? "Unknown Item";
                        itemUrl = item.ImageUrl ?? "";
                        itemContent = item.Metadata?.ImageUrl ?? itemUrl;
                        inventoryType = item.ItemTypeLabel ?? "Unknown Type";

                        logger.Info($"Fetched inventory item: {itemName} / ({item.ItemTypeLabel}) for user {userId} / URL : {itemUrl}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to fetch inventory item {inventoryId} / {inventoryType} for user {userId}: {ex.Message}");
                }

                if (inventoryType.Contains("Emoji") || inventoryType.Contains("Sticker"))
                {
                    string evaluatedText = string.Empty;
                    var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                    if (ollamaClient != null)
                    {
                        List<string> base64Images = await serviceRegistry.GetVRChatAPIClient().DownloadContentUrls(new List<string> { itemUrl, itemContent }) ?? new List<string>();

                        ImageReference processItem = new ImageReference
                        {
                            Priority = CommonConst.IMAGE_EVALUATION_PRIORITY_STICKER,
                            InventoryId = inventoryId,
                            UserId = userId,
                            Base64Data = base64Images,
                            ItemName = itemName,
                            ItemContentUrl = itemContent,
                            ItemType = inventoryType
                        };

                        ollamaClient.EnqueueImageEvaluationRequest(processItem);
                    }

                    PlayerManager.UserInventorySpawnPublic(itemUrl, player, inventoryType);
                }
            }

            return item;
        }

        public void UpdatePlayerInventory(ImageReference processItem, ImageEvaluation? evaluated)
        {
            string evaluatedText = string.Empty;
            string aiClassification = AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnum.NOT_AVAILABLE);
            Player? player = PlayerManager.GetPlayerByUserId(processItem.UserId);

            if (player != null)
            {
                if (evaluated != null)
                {
                    evaluatedText = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                    aiClassification = AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnumMapper.MapEvaluationToEnum(evaluatedText)) ?? AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnum.INVALID_RESPONSE);
                    logger.Info($"Ollama classification for inventory item {processItem.InventoryId}: {aiClassification}: {evaluatedText}");
                    if (!aiClassification.Equals("OK") && !evaluated.IsIgnored)
                    {
                        PlayerManager.AddPlayerEventByUserId(processItem.UserId, PlayerEvent.EventType.Emoji, $"AI Evaluation: Spawned Item {processItem.ItemName} ({processItem.ItemType}) was classified {aiClassification}");
                        player.AddAlertMessage(AlertClassEnum.EmojiSticker, AlertTypeEnum.Nuisance, $"{aiClassification}");
                    }
                }

                PlayerInventory inventory = new(processItem.InventoryId, processItem.ItemName, processItem.ItemContentUrl, processItem.ItemType, aiClassification, evaluatedText);
                player.Inventory.Add(inventory);

                PlayerManager.AddPlayerEventByUserId(processItem.UserId, PlayerEvent.EventType.Emoji, $"Spawned Item: {processItem.ItemName} ({processItem.InventoryId})");
                PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }

        public static void AddStickerEvent(string displayName, string fileURL)
        {
            Player? player = PlayerManager.GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.LastStickerUrl = fileURL;
                PlayerManager.AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.Sticker, $"Spawned sticker: {fileURL}");
                PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }


    }
}
