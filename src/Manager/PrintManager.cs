using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Tailgrab.Clients.Ollama;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;

namespace Tailgrab.Manager
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

        public async Task<Print?> AddPrintSpawn(string printId)
        {
            if (serviceRegistry == null) { return null; }

            Print? printInfo = serviceRegistry.GetVRChatAPIClient().GetPrintInfo(printId);
            if (printInfo != null)
            {
                var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                if (ollamaClient != null)
                {
                    List<string> base64Images = await serviceRegistry.GetVRChatAPIClient().DownloadContentUrls(new List<string> { printInfo.Files.Image }) ?? new List<string>();

                    ImageReference processItem = new ImageReference
                    {
                        Priority = CommonConst.IMAGE_EVALUATION_PRIORITY_PRINT,
                        InventoryId = printId,
                        UserId = printInfo.OwnerId,
                        Base64Data = base64Images,
                        ItemName = FormatPrintInfo(printInfo),
                        ItemContentUrl = printInfo.Files.Image,
                        ItemType = "Print",
                        PrintInfo = printInfo
                    };

                    ollamaClient.EnqueueImageEvaluationRequest(processItem);
                }

                return printInfo;
            }

            return null;
        }

        public void UpdatePlayerPrint(Print? printInfo, ImageEvaluation? evaluated)
        {
            logger.Info($"Updating player print for Print item {printInfo?.ToString()} with evaluated data: {evaluated?.Evaluation}");
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
