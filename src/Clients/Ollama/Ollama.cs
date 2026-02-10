using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using NLog;
using OllamaSharp;
using OllamaSharp.Models;
using System.Net.Http;
using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;

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
                if (string.IsNullOrEmpty(UserBio))
                {
                    return string.Empty;
                }

                // Remove all whitespace for hashing
                return Checksum.CreateMD5(sWhitespace.Replace(UserBio, ""));
            }
        }
    }

    public class OllamaClient
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();
        private ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new ConcurrentPriorityQueue<IHavePriority<int>, int>();
        private ServiceRegistry _serviceRegistry;

        public OllamaClient(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            _serviceRegistry = registry;

            _ = Task.Run(() => ProfileCheckTask(priorityQueue, registry));
        }

        public int GetQueueSize()
        {
            return priorityQueue.Count;
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


        
        public static async Task ProfileCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            string? ollamaCloudKey = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Key);
            OllamaApiClient? ollamaApi = null;
            if (ollamaCloudKey is null)
            {
                System.Windows.MessageBox.Show("Ollama API Credentials are not set.\nThis is not nessasary for limited operation, the Profiles will not be profileText.\nOtherwise use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            } 
            else
            {
                string ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Endpoint) ?? Tailgrab.Common.Common.Default_Ollama_API_Endpoint;
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(ollamaEndpoint);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ollamaCloudKey);
                ollamaApi = new OllamaApiClient(client);
                string? ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Model) ?? Tailgrab.Common.Common.Default_Ollama_API_Model;
                ollamaApi.SelectedModel = ollamaModel;
            }

            OllamaClient.logger.Info($"Profile/Group Queue Running");
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

                            await GetUserGroupInformation(serviceRegistry, dBContext, userGroups, item);

                            // Wait for a short period before checking the queue again
                            await Task.Delay(1000);

                            if (ollamaApi != null)
                            {
                                logger.Debug($"Processing AI Evaluation Queued item for userId: {item.UserId}");
                                // Process the dequeued item
                                if (!string.IsNullOrEmpty(item.MD5Hash))
                                {
                                    // Only when the Item has a valid hash
                                    // Check if already profileText
                                    ProfileEvaluation? evaluated = serviceRegistry.GetDBContext().ProfileEvaluations.Find(item.MD5Hash);
                                    if (evaluated == null)
                                    {
                                        GetEvaluationFromCloud(ollamaApi, serviceRegistry, item);
                                    }
                                    else
                                    {
                                        GetEvaluationFromStore(serviceRegistry, evaluated, item.UserId);
                                    }
                                }
                            }

                            serviceRegistry.GetPlayerManager().OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, profile.DisplayName);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Error fetching user profile for userId: {item.UserId}");
                        }

                    }
                    else
                    {
                        // No more items to process
                        break;
                    }
                }
                // Wait for a short period before checking the queue again
                await Task.Delay(10000);
            }
        }

        private async static Task<bool> GetUserGroupInformation(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, List<LimitedUserGroups> userGroups, QueuedProcess item)
        {
            logger.Debug($"Processing User Group subscription for userId: {item.UserId}");
            bool isSuspectGroup = false;
            string? watchedGroups = string.Empty;
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
                        watchedGroups = string.Concat( watchedGroups,  " " + groupInfo.GroupName );
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
                    player.PenActivity = watchedGroups;
                    serviceRegistry.GetPlayerManager().AddPlayerEventByUserId(item.UserId ?? string.Empty, PlayerEvent.EventType.GroupWatch, $"User is member of watched group(s): {watchedGroups}");
                }

                string? soundSetting = ConfigStore.LoadSecret(Common.Common.Registry_Alert_Group) ?? "Hand";
                SoundManager.PlaySound(soundSetting);
            }

            return isSuspectGroup;
        }

        private async static void GetEvaluationFromCloud(OllamaApiClient ollamaApi, ServiceRegistry serviceRegistry, QueuedProcess item )
        {
            string? ollamaPrompt = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Prompt);
            GenerateRequest request = new GenerateRequest
            {
                Model = ollamaApi.SelectedModel,
                Prompt = string.Concat( ollamaPrompt ?? Tailgrab.Common.Common.Default_Ollama_API_Prompt, item.UserBio ?? string.Empty ),
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

                        string? profileWatch = EvaluateProfile(player.AIEval);
                        if (profileWatch != null)
                        {
                            player.IsProfileWatch = true;
                            player.PenActivity = $"Bio: {profileWatch}";
                            serviceRegistry.GetPlayerManager().AddPlayerEventByUserId(item.UserId ?? string.Empty, PlayerEvent.EventType.ProfileWatch, $"User profile was flagged by the AI : {profileWatch}");
                            serviceRegistry.GetPlayerManager().OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
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

        private static void GetEvaluationFromStore(ServiceRegistry serviceRegistry, ProfileEvaluation evaluated, string? userId)
        {
            if (userId != null)
            {

                Player? player = serviceRegistry.GetPlayerManager().GetPlayerByUserId(userId ?? string.Empty);
                if (player != null)
                {
                    player.AIEval = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                    player.UserBio = System.Text.Encoding.UTF8.GetString(evaluated.ProfileText);
                    string? profileWatch = EvaluateProfile(player.AIEval);
                    if (profileWatch != null)
                    {
                        player.IsProfileWatch = true;
                        player.PenActivity = profileWatch;
                        serviceRegistry.GetPlayerManager().AddPlayerEventByUserId(userId ?? string.Empty, PlayerEvent.EventType.ProfileWatch, $"User profile was flagged by the AI : {profileWatch}");
                    }
                    serviceRegistry.GetPlayerManager().OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                    logger.Debug($"User profile already processed for userId: {userId}");
                }
                else
                {
                    logger.Debug($"User profile lookup fails for userId: {userId}");
                }

            }
            else
            {
                logger.Debug($"User profile lookup fails for a null userId");
            }
        }


        private static string? EvaluateProfile(string? profileText)
        {
            if (string.IsNullOrEmpty(profileText))
            {
                return null;
            }

            if (CheckLines(profileText, "Explicit Sexual"))
            {
                return "Explicit Sexual";
            }
            else if (CheckLines(profileText, "Harrassment & Bullying"))
            {
                return "Harrassment & Bullying";
            }
            else if (CheckLines(profileText, "Self Harm"))
            {
                return "Self Harm";
            }

            return null;
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

        #region Image Classification
        internal async Task<string?> ClassifyImageList(string userId, string assetId, List<string> imageUrlList)
        {
            logger.Debug($"Classifying image from Asset: {assetId} URI: {imageUrlList}");

            try
            {
                string? ollamaCloudKey = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Key);
                if (ollamaCloudKey == null)
                {
                    logger.Warn("Ollama API credentials are not set");
                    return null;
                }

                string ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Endpoint) ?? Tailgrab.Common.Common.Default_Ollama_API_Endpoint;

                ImageReference? imageReference = await _serviceRegistry.GetVRChatAPIClient().GetImageReference(assetId, userId, imageUrlList);
                if (imageReference != null)
                {
                    ImageEvaluation? imageEvaluation = CheckImageReferenceReview(imageReference);
                    if (imageEvaluation == null)
                    {
                        using (HttpClient httpClient = new HttpClient())
                        {
                            // Create Ollama client
                            HttpClient ollamaHttpClient = new HttpClient();
                            ollamaHttpClient.BaseAddress = new Uri(ollamaEndpoint);
                            ollamaHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ollamaCloudKey);

                            using (OllamaApiClient ollamaApi = new OllamaApiClient(ollamaHttpClient))
                            {
                                string? ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Model) ?? Tailgrab.Common.Common.Default_Ollama_API_Model;
                                ollamaApi.SelectedModel = ollamaModel;

                                string? ollamaPrompt = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_Ollama_API_Image_Prompt);
                                GenerateRequest request = new GenerateRequest
                                {
                                    Model = ollamaApi.SelectedModel,
                                    Prompt = ollamaPrompt ?? Tailgrab.Common.Common.Default_Ollama_API_Image_Prompt,
                                    Images = imageReference.Base64Data.ToArray(),
                                    Stream = false
                                };

                                var response = await ollamaApi.GenerateAsync(request).StreamToEndAsync();

                                logger.Debug($"Image classified for InventoryId: {imageReference.InventoryId} as {response?.Response}");
                                SaveImageEvaluation(imageReference, response?.Response);

                                return response?.Response;
                            }
                        }
                    }
                    else
                    {
                        logger.Debug($"Image already classified for AssetId : {imageReference.InventoryId}");
                        return System.Text.Encoding.UTF8.GetString(imageEvaluation.Evaluation);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error classifying image from URI: {imageUrlList}");
            }

            return null;
        }

        private ImageEvaluation? CheckImageReferenceReview(ImageReference imageReference)
        {
            TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
            ImageEvaluation? evaluated = dBContext.ImageEvaluations.Find(imageReference.InventoryId);
            if (evaluated != null)
            {
                logger.Debug($"Image already reviewed for InventoryId: {imageReference.InventoryId}");
                return evaluated;
            }
            return null;
        }

        private void SaveImageEvaluation(ImageReference imageReference, string? response)
        {
            if (response != null)
            {
                ImageEvaluation evaluation = new ImageEvaluation
                {
                    InventoryId = imageReference.InventoryId,
                    UserId = imageReference.UserId,
                    Md5checksum = imageReference.Md5Hash,
                    Evaluation = System.Text.Encoding.UTF8.GetBytes(response ?? string.Empty),
                    LastDateTime = DateTime.UtcNow
                };
                TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
                dBContext.Add(evaluation);
                dBContext.SaveChanges();
            }
        }
        #endregion
    }

    public class ImageReference
    {
        public List<string> Base64Data { get; set; } = new List<string>();
        public string Md5Hash { get; set; } = string.Empty;
        public string InventoryId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }
}
