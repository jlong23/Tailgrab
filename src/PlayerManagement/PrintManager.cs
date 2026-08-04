using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Tailgrab.Clients.Ollama;
using Tailgrab.Common;
using Tailgrab.Models;
using VRChat.API.Model;

namespace Tailgrab.PlayerManagement
{
    public class PrintManager
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static ServiceRegistry? serviceRegistry;

        [SetsRequiredMembers]
        public PrintManager(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
        }

        internal async void AddPrintData(string printId)
        {
            if (serviceRegistry == null) { return; }

            if (serviceRegistry.GetVRChatAPIClient() != null)
            {
                Print? printInfo = serviceRegistry.GetVRChatAPIClient().GetPrintInfo(printId);
                if (printInfo != null)
                {
                    Player? player = PlayerManager.AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"Dropped Print {printId}");
                    if (player != null)
                    {
                        logger.Info($"Fetched print info for print {printId} owned by {player.DisplayName} (ID: {printInfo.OwnerId}) / URL: {printInfo.Files.Image}");
                        string evaluatedText = "Not Evaluated";
                        string aiClassification = "OK";
                        var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                        if (ollamaClient != null)
                        {
                            List<string> imageUrls = [];
                            imageUrls.Add(printInfo.Files.Image);
                            ImageEvaluation? evaluated = await ollamaClient.ClassifyImageList(printInfo.OwnerId, printInfo.Id, imageUrls);
                            if (evaluated != null)
                            {
                                evaluatedText = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                                aiClassification = AIEvaluationManager.EvaluateImageClass(evaluatedText) ?? "OK";
                                logger.Info($"Ollama classification for inventory item {printInfo.Id}: {aiClassification}: {evaluatedText}");
                                if (!aiClassification.Equals("OK") && !evaluated.IsIgnored)
                                {
                                    player = PlayerManager.AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"AI Evaluation: Print {printId} was classified {aiClassification}");
                                    player?.AddAlertMessage(AlertClassEnum.Print, AlertTypeEnum.Nuisance, $"{aiClassification}");
                                }
                            }
                        }

                        player?.PrintData[printId] = new PlayerPrint(printInfo.Id, printInfo.OwnerId, printInfo.CreatedAt, printInfo.Files.Image, printInfo.AuthorName, evaluatedText, aiClassification);
                    }
                }
            }
        }

        private static string FormatPrintInfo(Print printInfo)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Print ID: {printInfo.Id}");
            sb.AppendLine($"Owner ID: {printInfo.OwnerId}");
            sb.AppendLine($"Author Name: {printInfo.AuthorName}");
            sb.AppendLine($"World ID: {printInfo.WorldId}");
            sb.AppendLine($"World Name: {printInfo.WorldName}");
            sb.AppendLine($"Created At: {printInfo.CreatedAt}");
            sb.AppendLine($"Files: {string.Join(", ", printInfo.Files)}");
            return sb.ToString();
        }

        public async void AddPrintSpawn(string printId)
        {
            if (serviceRegistry == null) { return; }

            Print? printInfo = serviceRegistry.GetVRChatAPIClient().GetPrintInfo(printId);
            if (printInfo != null)
            {
                Player? player = PlayerManager.AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"Dropped Print {printId}");
                if (player != null)
                {
                    var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                    if (ollamaClient != null)
                    {
                        List<string> base64Images = await serviceRegistry.GetVRChatAPIClient().DownloadContentUrls(new List<string> { printInfo.Files.Image }) ?? new List<string>();

                        ImageReference processItem = new ImageReference
                        {
                            Priority = 10,
                            InventoryId = printId,
                            UserId = printInfo.OwnerId,
                            Base64Data = base64Images,
                            ItemName = FormatPrintInfo(printInfo),
                            ItemContentUrl = printInfo.Files.Image,
                            ItemType = "Print",
                            PrintInfo = printInfo
                        };

                        // @TODO: Finish Ollama Inventory & Print Queue processing.
                        ollamaClient.EnqueuePriorityItem(processItem);
                    }
                }
            }
        }

        public void UpdatePlayerPrint(Print? printInfo, ImageEvaluation? evaluated)
        {
            if (serviceRegistry == null) { return; }
            
            if (printInfo == null) { return; }

            string evaluatedText = string.Empty;
            string aiClassification = AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnum.NOT_AVAILABLE);
            Player? player = PlayerManager.GetPlayerByUserId(printInfo.OwnerId);

            if (player != null)
            {
                if (evaluated != null)
                {
                    evaluatedText = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                    aiClassification = AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnumMapper.MapEvaluationToEnum(evaluatedText)) ?? AIEvalutionEnumMapper.MapEnumToDescription(AIEvalutionEnum.INVALID_RESPONSE);

                    logger.Info($"Ollama classification for Print item {printInfo.Id}: {aiClassification}: {evaluatedText}");
                    if (!aiClassification.Equals("OK") && !evaluated.IsIgnored)
                    {
                        player = PlayerManager.AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"AI Evaluation: Print {printInfo.Id} was classified {aiClassification}");
                        player?.AddAlertMessage(AlertClassEnum.Print, AlertTypeEnum.Nuisance, $"{aiClassification}");
                    }
                }

                player?.PrintData[printInfo.Id] = new PlayerPrint(printInfo.Id, printInfo.OwnerId, printInfo.CreatedAt,
                    printInfo.Files.Image, printInfo.AuthorName, evaluatedText, aiClassification);
            }
        }
    }
}
