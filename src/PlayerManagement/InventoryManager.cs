using NLog;
using System.Diagnostics.CodeAnalysis;
using Tailgrab.Clients.Ollama;
using Tailgrab.Common;
using Tailgrab.Models;

namespace Tailgrab.PlayerManagement
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

        public async void AddInventorySpawn(string userId, string inventoryId)
        {
            if ( serviceRegistry == null) {
                logger.Warn("ServiceRegistry is not initialized. Cannot fetch inventory item.");
                return;
            }

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
                    var inventoryItem = await serviceRegistry.GetVRChatAPIClient()?.GetUserInventoryItem(userId, inventoryId)!;
                    if (inventoryItem != null)
                    {
                        logger.Info($"{inventoryItem.ToString()}");

                        itemName = inventoryItem.Name ?? inventoryItem.ItemType ?? "Unknown Item";
                        itemUrl = inventoryItem.ImageUrl ?? "";
                        itemContent = inventoryItem.Metadata?.ImageUrl ?? itemUrl;
                        inventoryType = inventoryItem.ItemTypeLabel ?? "Unknown Type";

                        logger.Info($"Fetched inventory item: {itemName} / ({inventoryItem.ItemTypeLabel}) for user {userId} / URL : {itemUrl}");
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
                            Priority = 10,
                            InventoryId = inventoryId,
                            UserId = userId,
                            Base64Data = base64Images,
                            ItemName = itemName,
                            ItemContentUrl = itemContent,
                            ItemType = inventoryType
                        };

                        // @TODO: Finish Ollama Inventory & Print Queue processing.
                        ollamaClient.EnqueuePriorityItem(processItem);
                    }
                }
            }
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
