using ConcurrentPriorityQueue.Core;
using NLog;
using OllamaSharp;
using OllamaSharp.Models;
using System.Net.Http;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;

namespace Tailgrab.Clients.Ollama
{

    public class OllamaClient
    {
        public static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new();
        private ServiceRegistry _serviceRegistry;

        public OllamaClient(ServiceRegistry registry)
        {
            _serviceRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
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
                QueuedProcess process = new()
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
            OllamaApiClient? ollamaApi = GetClient();
            if (ollamaApi is null)
            {
                System.Windows.MessageBox.Show("Ollama API Credentials are not set.\nThis is not nessasary for limited operation, the Profiles will not be profileText.\nOtherwise use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
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
                            string profilePrompt = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Prompt) ?? CommonConst.Default_Ollama_API_Prompt;
                            string promptHash = Checksum.MD5Hash(profilePrompt);

                            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                            User profile = serviceRegistry.GetVRChatAPIClient().GetProfile(item.UserId);
                            List<LimitedUserGroups> userGroups = serviceRegistry.GetVRChatAPIClient().GetProfileGroups(item.UserId);

                            string fullProfile = $"DisplayName: {profile.DisplayName}\nStatusDesc: {profile.StatusDescription}\nPronowns: {profile.Pronouns}\nProfileBio: {profile.Bio}\n";
                            item.IsFriend = profile.IsFriend;
                            item.UserBio = fullProfile;

                            serviceRegistry.GetPlayerManager().UpdatePlayerUserFromVRCProfile(profile, item.MD5Hash);
                            await GetUserGroupInformation(dBContext, userGroups, item);

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
                                    if (evaluated == null || evaluated.PromptMd5Checksum != promptHash )
                                    {
                                        ProfileEvaluation evaluation = await PerformOllamaGeneration(ollamaApi, item, ollamaApi.SelectedModel, profilePrompt);

                                        // if we got a response save it to the database
                                        if (evaluation != null)
                                        {
                                            dBContext.Add(evaluation);
                                            dBContext.SaveChanges();

                                            UpdatePlayerWithEvaluation(item, evaluation);
                                        }

                                        // Wait for a short period before checking the queue again
                                        await Task.Delay(5000);
                                    }
                                    else
                                    {
                                        GetEvaluationFromStore(evaluated, item);
                                    }
                                }
                            }

                            PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, profile.DisplayName);
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

        private static void UpdatePlayerWithEvaluation(QueuedProcess item, ProfileEvaluation evaluation)
        {
            Player? player = PlayerManager.GetPlayerByUserId(item.UserId ?? string.Empty);
            if (player != null)
            {
                player.UserBio = item.UserBio;
                player.AIEval = System.Text.Encoding.UTF8.GetString(evaluation.Evaluation);
                player.IsFriend = item.IsFriend;

                ProfileViewUpdate(player);
            }
        }

        private async static Task<bool> GetUserGroupInformation(TailgrabDBContext dBContext, List<LimitedUserGroups> userGroups, QueuedProcess item)
        {
            bool saveGroups = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Group_Caching, true);
            logger.Debug($"Processing User Group subscription for userId: {item.UserId}");
            Player? player = PlayerManager.GetPlayerByUserId(item.UserId ?? string.Empty);
            if (player != null)
            {
                AlertTypeEnum maxAlertType = AlertTypeEnum.None;
                foreach (LimitedUserGroups group in userGroups)
                {
                    GroupInfo? groupInfo = dBContext.GroupInfos.Find(group.GroupId);
                    if (groupInfo == null)
                    {
                        groupInfo = SaveGroupInfo(dBContext, saveGroups, group);
                    }
                    else
                    {
                        UpdateGroupInfo(dBContext, item, ref player, ref maxAlertType, group, groupInfo);
                    }
                }

                if (player != null && player.IsWatched)
                {
                    SoundManager.PlayAlertSound(CommonConst.Group_Alert_Key, maxAlertType);
                    return true;
                }
            }
            return false;
        }

        private static void UpdateGroupInfo(TailgrabDBContext dBContext, QueuedProcess item, ref Player? player, ref AlertTypeEnum maxAlertType, LimitedUserGroups group, GroupInfo groupInfo)
        {
            // We will update the group name on each lookup in case it changes, but not reset the alert level as that is user defined
            groupInfo.GroupName = group.Name;
            dBContext.GroupInfos.Update(groupInfo);
            dBContext.SaveChanges();

            if (groupInfo.AlertType > AlertTypeEnum.None)
            {
                player = PlayerManager.AddPlayerEventByUserId(item.UserId ?? string.Empty, PlayerEvent.EventType.GroupWatch, $"User is member of group: {groupInfo.GroupName} with alert level {groupInfo.AlertType}");
                player?.AddAlertMessage(AlertClassEnum.Group, groupInfo.AlertType, groupInfo.GroupName);
                maxAlertType = maxAlertType < groupInfo.AlertType ? groupInfo.AlertType : maxAlertType;
            }
        }

        private static GroupInfo SaveGroupInfo(TailgrabDBContext dBContext, bool saveGroups, LimitedUserGroups group)
        {
            GroupInfo groupInfo = new GroupInfo
            {
                GroupId = group.GroupId,
                GroupName = group.Name,
                AlertType = AlertTypeEnum.None,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (saveGroups)
            {
                dBContext.GroupInfos.Add(groupInfo);
                dBContext.SaveChanges();
            }

            return groupInfo;
        }

        private static void GetEvaluationFromStore(ProfileEvaluation evaluated, QueuedProcess item)
        {
            if (item != null && item.UserId != null)
            {

                Player? player = PlayerManager.GetPlayerByUserId(item.UserId);
                if (player != null)
                {
                    player.AIEval = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                    player.UserBio = System.Text.Encoding.UTF8.GetString(evaluated.ProfileText);
                    player.IsFriend = item.IsFriend;


                    ProfileViewUpdate(player);
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

        private static void ProfileViewUpdate(Player player)
        {
            string? profileWatch = EvaluateProfile(player.AIEval);
            if (profileWatch != null)
            {
                switch (profileWatch)
                {
                    case CommonConst.AI_EVALUATION_HATE: 
                        player.AddAlertMessage(AlertClassEnum.Profile, AlertTypeEnum.Nuisance, "Hate");
                        SoundManager.PlayAlertSound(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance);
                        break;
                    case CommonConst.AI_EVALUATION_SEXUAL:
                        player.AddAlertMessage(AlertClassEnum.Profile, AlertTypeEnum.Nuisance, "Sexual");
                        SoundManager.PlayAlertSound(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance);
                        break;
                    case CommonConst.AI_EVALUATION_SELFHARM:
                        player.AddAlertMessage(AlertClassEnum.Profile, AlertTypeEnum.Watch, "Self-Harm");
                        SoundManager.PlayAlertSound(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch);
                        break;
                }

                PlayerManager.AddPlayerEventByUserId(player.UserId ?? string.Empty,
                    PlayerEvent.EventType.ProfileWatch, $"User profile was flagged by the AI : {profileWatch}");
            }
            PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
        }

        private static string? EvaluateProfile(string? profileText)
        {
            if (string.IsNullOrEmpty(profileText))
            {
                return null;
            }

            if (CheckLines(profileText, "Explicit Sexual"))
            {
                return CommonConst.AI_EVALUATION_SEXUAL;
            }
            else if (CheckLines(profileText, "Harassment & Bullying"))
            {
                return CommonConst.AI_EVALUATION_HATE;
            }
            else if (CheckLines(profileText, "Self Harm"))
            {
                return CommonConst.AI_EVALUATION_SELFHARM;
            }

            return null;
        }

        private static bool CheckLines(string input, string knownString)
        {
            string[] lines = input.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            bool firstLineContains = lines[0].Contains(knownString);

            return firstLineContains;
        }

        #region Image Classification
        internal async Task<ImageEvaluation?> ClassifyImageList(string userId, string assetId, List<string> imageUrlList)
        {
            logger.Debug($"Classifying image from Asset: {assetId} URI: {imageUrlList.ToArray()}");

            try
            {
                string? ollamaCloudKey = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key);
                if (ollamaCloudKey == null)
                {
                    logger.Warn("Ollama API credentials are not set");
                    return null;
                }

                string ollamaEndpoint = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;

                ImageReference? imageReference = await _serviceRegistry.GetVRChatAPIClient().GetImageReference(assetId, userId, imageUrlList);
                if (imageReference != null)
                {
                    ImageEvaluation? imageEvaluation = CheckImageReferenceReview(imageReference);
                    if (imageEvaluation == null)
                    {
                        using HttpClient ollamaHttpClient = new();
                        // Create Ollama ollamaClient
                        ollamaHttpClient.BaseAddress = new Uri(ollamaEndpoint);
                        ollamaHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ollamaCloudKey);

                        using OllamaApiClient ollamaApi = new(ollamaHttpClient);
                        string? ollamaModel = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
                        ollamaApi.SelectedModel = ollamaModel;

                        string? ollamaPrompt = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Image_Prompt);
                        GenerateRequest request = new()
                        {
                            Model = ollamaApi.SelectedModel,
                            Prompt = ollamaPrompt ?? CommonConst.Default_Ollama_API_Image_Prompt,
                            Images = [.. imageReference.Base64Data],
                            Stream = false
                        };

                        GenerateDoneResponseStream? response = await ollamaApi.GenerateAsync(request).StreamToEndAsync();

                        logger.Debug($"Image classified for InventoryId: {imageReference.InventoryId} as {response?.Response}");
                        imageEvaluation = SaveImageEvaluation(imageReference, response?.Response);

                        return imageEvaluation;
                    }
                    else
                    {
                        logger.Debug($"Image already classified for AssetId : {imageReference.InventoryId}");
                        return imageEvaluation;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error classifying image from URI: {imageUrlList.ToArray()}");
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

        private ImageEvaluation? SaveImageEvaluation(ImageReference imageReference, string? response)
        {
            if (response != null)
            {
                ImageEvaluation evaluation = new()
                {
                    InventoryId = imageReference.InventoryId,
                    UserId = imageReference.UserId,
                    Md5checksum = imageReference.Md5Hash,
                    Evaluation = System.Text.Encoding.UTF8.GetBytes(response ?? string.Empty),
                    LastDateTime = DateTime.UtcNow,
                    IsIgnored = false
                };
                TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
                dBContext.Add(evaluation);
                dBContext.SaveChanges();
                return evaluation;
            }

            return null;
        }

        private static OllamaApiClient? GetClient()
        {
            string? ollamaCloudKey = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key);
            if (ollamaCloudKey == null)
            {
                logger.Warn("Ollama API credentials are not set");
                return null;
            }
            string ollamaEndpoint = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;
            HttpClient client = new()
            {
                BaseAddress = new Uri(ollamaEndpoint)
            };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ollamaCloudKey);
            OllamaApiClient ollamaApi = new(client);

            return ollamaApi;
        }

        public static async Task<List<string>> GetModels() 
        {
            List<string> models = [];
            OllamaApiClient? ollamaApi = GetClient();
            if (ollamaApi is not null)
            {
                IEnumerable<Model> remoteModels = await ollamaApi.ListLocalModelsAsync();
                foreach (Model model in remoteModels) {
                    logger.Debug($"Model found: {model.Name}");
                    models.Add(model.Name);
                }

                models.Sort();
            }

            return models;

        }

        /// <summary>
        /// Test method for profile prompt evaluation
        /// </summary>
        /// <param name="userId">VRChat User ID to test</param>
        /// <param name="prompt">AI prompt to use for evaluation</param>
        /// <param name="model">Ollama model name to use</param>
        /// <returns>AI evaluation result</returns>
        public static async Task<ProfileEvaluation> TestProfilePrompt(ServiceRegistry serviceRegistry, string userId, string prompt, string model)
        {
            ProfileEvaluation profileEvaluation = new();

            try
            {
                var ollamaClient = GetClient();
                if (ollamaClient == null)
                {
                    profileEvaluation.Evaluation = System.Text.Encoding.UTF8.GetBytes("Error: Could not create Ollama ollamaClient. Please check credentials.");
                    return profileEvaluation;
                }

                var vrcClient = serviceRegistry.GetVRChatAPIClient();
                if( vrcClient == null) {
                    profileEvaluation.Evaluation = System.Text.Encoding.UTF8.GetBytes("Error: Could not create VRChat API client. Please check credentials.");
                }

                User profile = vrcClient.GetProfile(userId);
                QueuedProcess item = new()
                {
                    UserId = userId,
                    Priority = 1
                };

                string fullProfile = $"DisplayName: {profile.DisplayName}\nStatusDesc: {profile.StatusDescription}\nPronowns: {profile.Pronouns}\nProfileBio: {profile.Bio}\n";
                item.IsFriend = profile.IsFriend;
                item.UserBio = fullProfile;

                logger.Debug($"Processing AI Evaluation Queued item for userId: {item.UserId}");
                // Process the dequeued item
                if (!string.IsNullOrEmpty(item.MD5Hash))
                {
                    ProfileEvaluation evaluation = await PerformOllamaGeneration(ollamaClient, item, model, prompt);
                    return evaluation;
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to test profile prompt");
                profileEvaluation.Evaluation = System.Text.Encoding.UTF8.GetBytes($"Error testing profile prompt: {ex.Message}");
            }

            profileEvaluation.Evaluation = System.Text.Encoding.UTF8.GetBytes("Error: User profile is empty or invalid.");
            return profileEvaluation;
        }

        public static async Task<ProfileEvaluation> PerformOllamaGeneration(OllamaApiClient ollamaApi, QueuedProcess item, string model, string prompt)
        {
            ProfileEvaluation evaluation = new()
            {
                Md5checksum = item.MD5Hash ?? string.Empty,
                PromptMd5Checksum = Checksum.MD5Hash(prompt),
                ProfileText = System.Text.Encoding.UTF8.GetBytes(item.UserBio ?? string.Empty),
                LastDateTime = DateTime.UtcNow
            };

            GenerateRequest request = new()
            {
                Model = model,
                Prompt = string.Concat(prompt, item.UserBio ?? string.Empty),
                Stream = false
            };

            try
            {
                await ollamaApi.GenerateAsync(request).StreamToEndAsync(responseTask =>
                {
                    string response = responseTask?.Response ?? string.Empty;
                    string logProblems = responseTask?.Logprobs != null ? string.Join(", ", responseTask.Logprobs) : "No logprobs";
                    evaluation.Evaluation = System.Text.Encoding.UTF8.GetBytes(response);
                });

                return evaluation;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing Ollama request for userId: {item.UserId} - {ex.Message}");
            }

            return evaluation;
        }

        #endregion
    }

    // Simplest implementation of IHavePriority<T>
    public class QueuedProcess : IHavePriority<int>
    {
        public int Priority { get; set; }
        public string? UserId { get; set; }
        public string? UserBio { get; set; }
        public bool IsFriend { get; set; }

        public string MD5Hash
        {
            get
            {
                if (string.IsNullOrEmpty(UserBio))
                {
                    return string.Empty;
                }

                // Remove all whitespace for hashing
                return Checksum.CreateMD5(UserBio);
            }
        }
    }

    public class ImageReference
    {
        public List<string> Base64Data { get; set; } = [];
        public string Md5Hash { get; set; } = string.Empty;
        public string InventoryId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }
}
