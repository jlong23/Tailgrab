using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using NLog;
using OllamaSharp;
using OllamaSharp.Models;
using System.Net.Http;
using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.Config;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Tailgrab.Clients.Ollama
{

    // Simplest implementation of IHavePriority<T>
    internal class QueuedProcess : IHavePriority<int>
    {
        private static readonly Regex sWhitespace = new Regex(@"\s+");

        public int Priority { get; set; }
        public string? UserId { get; set; }
        public string? UserBio { get; set; }

        public string MD5Hash 
        { 
            get
            {
                if( string.IsNullOrEmpty(UserBio))
                {
                    return string.Empty;
                }

                // Remove all whitespace for hashing
                return Checksum.CreateMD5(sWhitespace.Replace(UserBio,""));
            }
        }
    }

    public class OllamaClient
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();
        private ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new ConcurrentPriorityQueue<IHavePriority<int>, int>();
        private Dictionary<string, string> processedBios = new Dictionary<string, string>();
        private ServiceRegistry _serviceRegistry;

        public OllamaClient(ServiceRegistry registry)
        {
            if(registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            _serviceRegistry = registry;

            _ = Task.Run(() => ProfileCheckTask(priorityQueue, processedBios, registry));
        }

        public void CheckUserProfile(string userId)
        {
            logger.Debug($"Checking user profile with AI : {userId}");

            try
            {                
                QueuedProcess process = new QueuedProcess
                {
                    UserId = userId,
                    Priority = 1
                };

                priorityQueue.Enqueue(process);                
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user profile for userId: {userId}");

            }
        }
        public static async Task ProfileCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, Dictionary<string, string> processData, ServiceRegistry serviceRegistry )
        {
            string? ollamaCloudKey = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Key);

            if (ollamaCloudKey is null)
            {
                System.Windows.MessageBox.Show("Ollama API Credentials are not set yet, use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            string ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Endpoint) ?? Tailgrab.Common.Common.Default_Ollama_API_Endpoint;
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(ollamaEndpoint);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ollamaCloudKey);
            OllamaApiClient? ollamaApi = new OllamaApiClient(client);
            string ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Model) ?? Tailgrab.Common.Common.Default_Ollama_API_Model;
            ollamaApi.SelectedModel = ollamaModel;

            OllamaClient.logger.Info($"OLlama Queue Running");
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess && result.Value is QueuedProcess item && item.UserId != null)
                    {
                        try
                        {
                            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                            User profile = serviceRegistry.GetVRChatAPIClient().GetProfile(item.UserId);
                            List<LimitedUserGroups> userGroups = serviceRegistry.GetVRChatAPIClient().GetProfileGroups(item.UserId);
                            string fullProfile = $"DisplayName: {profile.DisplayName}\nStatusDesc: {profile.StatusDescription}\nPronowns: {profile.Pronouns}\nProfileBio: {profile.Bio}\n";
                            item.UserBio = fullProfile;

                            GetUserGroupInformation(serviceRegistry, dBContext, userGroups, item);

                            if (ollamaApi != null)
                            {
                                logger.Debug($"Processing AI Evaluation Queued item for userId: {item.UserId}");
                                // Process the dequeued item
                                if (!string.IsNullOrEmpty(item.MD5Hash))
                                {
                                    // Only when the Item has a valid hash
                                    // Check if already evaluated
                                    ProfileEvaluation? evaluated = serviceRegistry.GetDBContext().ProfileEvaluations.Find(item.MD5Hash);
                                    if (evaluated == null)
                                    {
                                        GetEvaluationFromCloud(ollamaApi, serviceRegistry, item);
                                    }
                                    else
                                    {
                                        GetEvaluationFromStore(serviceRegistry, evaluated, item);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Error fetching user profile for userId: {item.UserId}");
                        }

                        // Wait for a short period before checking the queue again
                        await Task.Delay(1000);

                    }
                    else
                    {
                        // No more items to process
                        break;
                    }
                }
                // Wait for a short period before checking the queue again
                await Task.Delay(5000);
            }
        }

        private async static void GetUserGroupInformation(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, List<LimitedUserGroups> userGroups, QueuedProcess item )
        {
            logger.Debug($"Processing User Group subscription for userId: {item.UserId}");
            bool isSuspectGroup = false;
            foreach (LimitedUserGroups group in userGroups)
            {
                GroupInfo? groupInfo = dBContext.GroupInfos.Find(group.GroupId);
                if (groupInfo == null)
                {
                    groupInfo = new GroupInfo
                    {
                        GroupId = group.GroupId,
                        GroupName = group.Name,
                        IsBos = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    dBContext.GroupInfos.Add(groupInfo);
                    dBContext.SaveChanges();
                }
                else
                {
                    groupInfo.GroupName = group.Name;
                    dBContext.GroupInfos.Update(groupInfo);
                    dBContext.SaveChanges();

                    if (groupInfo.IsBos)
                    {
                        isSuspectGroup = true;
                    }
                }
            }

            if (isSuspectGroup)
            {
                Player? player = serviceRegistry.GetPlayerManager().GetPlayerByUserId(item.UserId ?? string.Empty);
                if (player != null)
                {
                    player.IsGroupWatch = true;
                    serviceRegistry.GetPlayerManager().OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                }
            }
        }

        private async static void GetEvaluationFromCloud(OllamaApiClient ollamaApi, ServiceRegistry serviceRegistry, QueuedProcess item)
        {
            string? ollamaPrompt = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Prompt);
            GenerateRequest request = new GenerateRequest
            {
                Model = ollamaApi.SelectedModel,
                Prompt = ollamaPrompt ?? Tailgrab.Common.Common.Default_Ollama_API_Prompt + item.UserBio ?? string.Empty,
                Stream = false
            };

            try
            {
                await ollamaApi.GenerateAsync(request).StreamToEndAsync(responseTask =>
                {
                    string response = responseTask?.Response ?? string.Empty;
                    string logProblems = responseTask?.Logprobs != null ? string.Join(", ", responseTask.Logprobs) : "No logprobs";
                    ProfileEvaluation evaluation = new ProfileEvaluation
                    {
                        Md5checksum = item.MD5Hash ?? string.Empty,
                        ProfileText = System.Text.Encoding.UTF8.GetBytes(item.UserBio ?? string.Empty),
                        Evaluation = System.Text.Encoding.UTF8.GetBytes(response),
                        LastDateTime = DateTime.UtcNow
                    };
                    TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                    dBContext.Add(evaluation);
                    dBContext.SaveChanges();

                    Player? player = serviceRegistry.GetPlayerManager().GetPlayerByUserId(item.UserId ?? string.Empty);
                    if (player != null)
                    {
                        player.UserBio = item.UserBio;
                        player.AIEval = response;
                        if (IsEvaluated(player.AIEval))
                        {
                            player.IsProfileWatch = true;
                        }
                        serviceRegistry.GetPlayerManager().OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing Ollama request for userId: {item.UserId} - {ex.Message}");
            }
        }

        private static void GetEvaluationFromStore(ServiceRegistry serviceRegistry, ProfileEvaluation evaluated, QueuedProcess item)
        {
            if (item.UserId != null)
            {

                Player? player = serviceRegistry.GetPlayerManager().GetPlayerByUserId(item.UserId ?? string.Empty);
                if (player != null)
                {
                    player.AIEval = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                    player.UserBio = System.Text.Encoding.UTF8.GetString(evaluated.ProfileText);
                    if (IsEvaluated(player.AIEval))
                    {
                        player.IsProfileWatch = true;
                    }
                    serviceRegistry.GetPlayerManager().OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                    logger.Debug($"User profile already processed for userId: {item.UserId}");
                }
                else
                {
                    logger.Debug($"User profile lookup fails for userId: {item.UserId}");
                }

            }
            else
            {
                logger.Debug($"User profile lookup fails for a null userId");
            }
        }
               

        private static bool IsEvaluated( string? evaluated )
        {
            if (string.IsNullOrEmpty(evaluated))
            {
                return false;
            }

            if (CheckLines(evaluated, "Explicit Sexual") ||
                    CheckLines(evaluated, "Harrassment & Bullying") ||
                    CheckLines(evaluated, "Self Harm"))
            {
                    return true;
            }

            return false;
        }

        private static bool CheckLines(string input, string knownString)
        {
            string[] lines = input.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            bool firstLineContains = lines[0].Contains(knownString);

            return firstLineContains;
        }



    }
}
