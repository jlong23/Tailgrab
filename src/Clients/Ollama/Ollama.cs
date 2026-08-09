using ConcurrentPriorityQueue.Core;
using NLog;
using OllamaSharp;
using OllamaSharp.Models;
using System.Diagnostics;
using System.Net.Http;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;

namespace Tailgrab.Clients.Ollama
{

    public class OllamaClient
    {
        public const int MaxRetries = 3;
        public static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new();
        private ServiceRegistry _serviceRegistry;

        public OllamaClient(ServiceRegistry registry)
        {
            _serviceRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            _ = Task.Run(() => ProcessQueueTask(priorityQueue, registry));
        }

        public int GetQueueSize()
        {
            return priorityQueue.Count;
        }

        public void EnqueuePriorityItem(IHavePriority<int> item)
        {
            priorityQueue.Enqueue(item);
        }

        public void ClearQueue()
        {
            while (true)
            {
                if (priorityQueue.Count == 0)
                {
                    break;  
                }
                priorityQueue.Dequeue();
            }
        }

        public static async Task ProcessQueueTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            using OllamaApiClient? ollamaApi = GetClient();
            if (ollamaApi is null)
            {
                System.Windows.MessageBox.Show("Ollama API Credentials are not set.\nThis is not nessasary for limited operation, the Profile/Emoji/Stickers will not be profileText.\nOtherwise use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            OllamaClient.logger.Info($"AI Queue Running");
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess)
                    {
                        if (result.Value is QueuedProcess item && item.UserId != null)
                        {
                            await ProfileEvaluateItem(priorityQueue, serviceRegistry, ollamaApi, item);
                            continue;
                        }

                        if (result.Value is ImageReference imageReference)
                        {
                            await ImageReferenceEvaluateItem(priorityQueue, serviceRegistry, ollamaApi, imageReference);
                            continue;
                        }
                    }
                    else
                    {
                        // No more items to process
                        break;
                    }

                    // Wait for a short period before getting next record
                    await Task.Delay(1000);
                }

                // Wait for a short period before checking the queue again
                await Task.Delay(10000);
            }
        }


        #region Profile Evaluation
        public void CheckUserProfile(string userId)
        {
            logger.Debug($"Checking user profile with AI : {userId}");

            try
            {
                QueuedProcess process = new()
                {
                    UserId = userId,
                    Priority = 10
                };

                string prompt = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Prompt) ?? CommonConst.Default_Ollama_API_Prompt;
                string model = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
                string promptHash = Checksum.MD5Hash(prompt);

                UpdateQueuedProcessWithPlayer(process);
                ProfileEvaluation? evaluated = _serviceRegistry.GetDBContext().ProfileEvaluations.Find(process.MD5Hash);
                if (evaluated != null && evaluated.PromptMd5Checksum == promptHash)
                {
                    UpdatePlayerWithEvaluation(process, evaluated);
                    process.Priority = 1; // Lower priority since we already have an evaluation
                }

                if (!IsUserProfileInQueue(process.UserId))
                {
                    priorityQueue.Enqueue(process);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user profile for userId: {userId}");

            }
        }

        private bool IsUserProfileInQueue(string userId)
        {
            foreach (var item in priorityQueue)
            {
                if (item is QueuedProcess queuedProcess && queuedProcess.UserId == userId)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateQueuedProcessWithPlayer(QueuedProcess item)
        {
            User profile = _serviceRegistry.GetVRChatAPIClient().GetProfile(item.UserId);
            string? accountThumbnailUrl = !string.IsNullOrEmpty(profile.ProfilePicOverrideThumbnail) ? profile.ProfilePicOverrideThumbnail : profile.CurrentAvatarThumbnailImageUrl;
            if (profile != null)
            {
                string fullProfile = FormatProfileText(profile);
                item.IsFriend = profile.IsFriend;
                item.UserBio = fullProfile;
                item.ProfileUrl = accountThumbnailUrl;
                item.UserTrustClass = TrustClassEnumMapper.MapTagsToEnum(profile.Tags);
                if (profile.AgeVerified)
                    item.AgeVerification = AgeVerificationEnumMapper.MapAgeVerificationStatusToEnum(profile.AgeVerificationStatus);
            }
        }

        private static string FormatProfileText(User profile)
        {
            return $"DisplayName: {profile.DisplayName}\n" +
                   $"StatusDesc: {profile.StatusDescription}\n" +
                   $"Pronouns: {profile.Pronouns}\n" +
                   $"UserTrust : {TrustClassEnumMapper.MapTagsToString(profile.Tags, profile.AgeVerified, profile.AgeVerificationStatus.ToString())}\n" +
                   $"UserAgeVerified: {profile.AgeVerified}\n" +
                   $"ProfileBio: {profile.Bio}\n";
        }

        private static async Task<bool> ProfileEvaluateItem(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry, OllamaApiClient? ollamaApi, QueuedProcess item)
        {
            try
            {
                string prompt = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Prompt) ?? CommonConst.Default_Ollama_API_Prompt;
                string model = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
                string promptHash = Checksum.MD5Hash(prompt);

                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                List<LimitedUserGroups> userGroups = serviceRegistry.GetVRChatAPIClient().GetProfileGroups(item.UserId);

                User profile = serviceRegistry.GetVRChatAPIClient().GetProfile(item.UserId);
                serviceRegistry.GetPlayerManager().UpdatePlayerUserFromVRCProfile(profile, item.MD5Hash);

                if (ollamaApi != null)
                {
                    logger.Debug($"Processing AI Evaluation Queued item for userId: {item.UserId}");
                    // Process the dequeued item
                    if (!string.IsNullOrEmpty(item.MD5Hash))
                    {
                        // Only when the Item has a valid hash 
                        // Check if already profileText
                        ProfileEvaluation? evaluated = serviceRegistry.GetDBContext().ProfileEvaluations.Find(item.MD5Hash);
                        if (evaluated == null || evaluated.PromptMd5Checksum != promptHash)
                        {
                            if (item.retries >= MaxRetries)
                            {
                                logger.Warn($"Max retries reached for userId: {item.UserId}. Skipping evaluation.");
                                return false;
                            }

                            ProfileEvaluation? evaluation = await PerformOllamaGeneration(ollamaApi, item, model, prompt);

                            // if we got a response save it to the database
                            if (evaluation != null)
                            {
                                ProfileEvaluation? evaluationDb = dBContext.ProfileEvaluations.FirstOrDefault(evaluation => evaluation.Md5checksum == item.MD5Hash);
                                if( evaluationDb != null) 
                                {
                                    evaluationDb.Evaluation = evaluation.Evaluation;
                                    evaluationDb.LastDateTime = DateTime.UtcNow;
                                    evaluationDb.PromptMd5Checksum = promptHash;
                                    dBContext.Update(evaluationDb);
                                }
                                else
                                {
                                    evaluation.Md5checksum = item.MD5Hash ?? string.Empty;
                                    evaluation.PromptMd5Checksum = promptHash;
                                    dBContext.Add(evaluation);
                                }
                                dBContext.SaveChanges();

                                UpdatePlayerWithEvaluation(item, evaluation);
                            }
                            else
                            {
                                // Retry the item by re-enqueuing it with incremented retries
                                item.retries++;
                                logger.Warn($"Ollama evaluation failed for userId: {item.UserId}. Retrying ({item.retries}/{MaxRetries})...");
                                if (!priorityQueue.Any(items => ((QueuedProcess)items).UserId == item.UserId))
                                {
                                    priorityQueue.Enqueue(item);
                                }
                            }
                        }
                        else
                        {
                            UpdatePlayerWithEvaluation(item, evaluated);
                        }
                    }
                }

                PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, profile.DisplayName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user profile for userId: {item.UserId}");
            }

            return true;
        }

        private static void UpdatePlayerWithEvaluation(QueuedProcess item, ProfileEvaluation evaluation)
        {
            Player? player = PlayerManager.GetPlayerByUserId(item.UserId ?? string.Empty);
            if (player != null)
            {
                player.UserBio = item.UserBio;
                player.AIEval = System.Text.Encoding.UTF8.GetString(evaluation.Evaluation);
                player.IsFriend = item.IsFriend;
                player.ProfileImage = item.ProfileUrl ?? player.ProfileImage;
                player.UserTrustClass = item.UserTrustClass;
                player.AgeVerified = item.AgeVerification;

                ProfileViewUpdate(player);
            }
        }

        private static void ProfileViewUpdate(Player player)
        {
            AIEvalutionEnum evaluationEnum = AIEvalutionEnumMapper.MapEvaluationToEnum(player.AIEval);

            if (evaluationEnum > AIEvalutionEnum.OK)
            {
                switch (evaluationEnum)
                {
                    case AIEvalutionEnum.HARASSMENT_AND_BULLYING:
                        player.AddAlertMessage(AlertClassEnum.Profile, AlertTypeEnum.Nuisance, "Hate");
                        SoundManager.PlayAlertSound(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance);
                        break;
                    case AIEvalutionEnum.EXPLICIT_SEXUAL:
                        player.AddAlertMessage(AlertClassEnum.Profile, AlertTypeEnum.Nuisance, "Sexual");
                        SoundManager.PlayAlertSound(CommonConst.Profile_Alert_Key, AlertTypeEnum.Nuisance);
                        break;
                    case AIEvalutionEnum.SELF_HARM:
                        player.AddAlertMessage(AlertClassEnum.Profile, AlertTypeEnum.Watch, "Self-Harm");
                        SoundManager.PlayAlertSound(CommonConst.Profile_Alert_Key, AlertTypeEnum.Watch);
                        break;
                }

                PlayerManager.AddPlayerEventByUserId(player.UserId ?? string.Empty,
                PlayerEvent.EventType.ProfileWatch, $"User profile was flagged by the AI : {AIEvalutionEnumMapper.MapEnumToDescription(evaluationEnum)}");
            }

            PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
        }
        #endregion


        #region Image Classification
        private static async Task ImageReferenceEvaluateItem(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry, 
            OllamaApiClient? ollamaApi, ImageReference imageReference)
        {
            logger.Debug($"Classifying image from Asset: {imageReference.InventoryId} URI: {imageReference.ItemContentUrl}");
            ImageEvaluation? imageEvaluation = new ImageEvaluation();

            try
            {
                string? ollamaCloudKey = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key);
                if (ollamaCloudKey == null)
                {
                    logger.Warn("Ollama API credentials are not set");
                    return;
                }

                string ollamaEndpoint = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;

                imageEvaluation = CheckImageReferenceReview(imageReference, serviceRegistry);
                if (imageEvaluation == null)
                {
                    string? ollamaModel = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
                    ollamaApi.SelectedModel = ollamaModel;

                    string? ollamaPrompt = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Image_Prompt);
                    GenerateRequest request = new()
                    {
                        Model = ollamaApi.SelectedModel,
                        Prompt = ollamaPrompt ?? CommonConst.Default_Ollama_API_Image_Prompt,
                        Images = [.. imageReference.Base64Data],
                        Stream = false
                    };

                    GenerateDoneResponseStream? response = await ollamaApi.GenerateAsync(request).StreamToEndAsync();

                    logger.Debug($"Image classified for Print InventoryId: {imageReference.InventoryId} as {response?.Response}");
                    imageEvaluation = SaveImageEvaluation(imageReference, response?.Response, serviceRegistry);
                }
                else
                {
                    logger.Debug($"Image already classified for AssetId : {imageReference.InventoryId}");
                }

                UpdatePlayerView(serviceRegistry, imageReference, imageEvaluation);

                return;
            }
            catch (Exception ex)
            {
                logger.Warn($"Ollama image classification failed for AssetId: {imageReference.InventoryId} / {imageReference.ItemContentUrl}. Retrying ({imageReference.retries}/{MaxRetries}) due to {ex.Message}");
                imageReference.retries++;
                if (imageReference.retries < MaxRetries)
                {
                    if (!priorityQueue.Any(items => ((ImageReference)items).InventoryId == imageReference.InventoryId))
                    {
                        priorityQueue.Enqueue(imageReference);
                    }
                }
                else
                {
                    logger.Warn($"Max retries reached for AssetId: {imageReference.InventoryId}. Skipping classification.");
                    UpdatePlayerView(serviceRegistry, imageReference, imageEvaluation);
                }
            }

            return;
        }

        private static void UpdatePlayerView(ServiceRegistry serviceRegistry, ImageReference imageReference, ImageEvaluation? imageEvaluation)
        {
            if (imageReference.ItemType == "Print" && imageReference.PrintInfo != null)
            {
                serviceRegistry.GetPrintManager().UpdatePlayerPrint(imageReference.PrintInfo, imageEvaluation);
            }
            else
            {
                serviceRegistry.GetInventoryManager().UpdatePlayerInventory(imageReference, imageEvaluation);
            }
        }

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

                string ollamaEndpoint = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;

                ImageReference? imageReference = await _serviceRegistry.GetVRChatAPIClient().GetImageReference(assetId, userId, imageUrlList);
                if (imageReference != null)
                {
                    ImageEvaluation? imageEvaluation = CheckImageReferenceReview(imageReference, _serviceRegistry);
                    if (imageEvaluation == null)
                    {
                        using HttpClient ollamaHttpClient = new();
                        // Create Ollama ollamaClient
                        ollamaHttpClient.BaseAddress = new Uri(ollamaEndpoint);
                        ollamaHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ollamaCloudKey);

                        using OllamaApiClient ollamaApi = new(ollamaHttpClient);
                        string? ollamaModel = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
                        ollamaApi.SelectedModel = ollamaModel;

                        string? ollamaPrompt = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Image_Prompt);
                        GenerateRequest request = new()
                        {
                            Model = ollamaApi.SelectedModel,
                            Prompt = ollamaPrompt ?? CommonConst.Default_Ollama_API_Image_Prompt,
                            Images = [.. imageReference.Base64Data],
                            Stream = false
                        };

                        GenerateDoneResponseStream? response = await ollamaApi.GenerateAsync(request).StreamToEndAsync();

                        logger.Debug($"Image classified for InventoryId: {imageReference.InventoryId} as {response?.Response}");
                        imageEvaluation = SaveImageEvaluation(imageReference, response?.Response, _serviceRegistry);

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

        private static ImageEvaluation? CheckImageReferenceReview(ImageReference imageReference, ServiceRegistry serviceRegistry)
        {
            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            ImageEvaluation? evaluated = dBContext.ImageEvaluations.Find(imageReference.InventoryId);
            if (evaluated != null)
            {
                logger.Debug($"Image already reviewed for InventoryId: {imageReference.InventoryId}");
                return evaluated;
            }

            return null;
        }

        private static ImageEvaluation? SaveImageEvaluation(ImageReference imageReference, string? response, ServiceRegistry serviceRegistry)
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
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                dBContext.Add(evaluation);
                dBContext.SaveChanges();
                return evaluation;
            }

            return null;
        }
        #endregion


        private static OllamaApiClient? GetClient()
        {
            string? ollamaCloudKey = ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key);
            if (ollamaCloudKey == null)
            {
                logger.Warn("Ollama API credentials are not set");
                return null;
            }
            string ollamaEndpoint = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;
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
        public static async Task<ProfileEvaluation?> TestProfilePrompt(ServiceRegistry serviceRegistry, string userId, string prompt, string model)
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
                    return profileEvaluation;
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
                    ProfileEvaluation? evaluation = await PerformOllamaGeneration(ollamaClient, item, model, prompt);
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

        public static async Task<string> TestImagePrompt(string model, string prompt, string imagePath)
        {
            string imageEvaluation = string.Empty;
            logger.Debug($"Testing image URI: {imagePath}, {model}, {prompt}");

            try
            {
                var ollamaClient = GetClient();
                if (ollamaClient == null)
                {
                    imageEvaluation = "Error: Could not create Ollama ollamaClient. Please check credentials.";
                    return imageEvaluation;
                }

                imageEvaluation = await PerformOllamaImageGeneration(ollamaClient, model, prompt, imagePath);
                return imageEvaluation;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error classifying image from : {imagePath}");
            }

            return imageEvaluation;
            ;
        }


        public static async Task<ProfileEvaluation?> PerformOllamaGeneration(OllamaApiClient ollamaApi, QueuedProcess item, string model, string prompt)
        {
            try
            {
                GenerateRequest request = new()
                {
                    Model = model,
                    Prompt = string.Concat(prompt, item.UserBio ?? string.Empty),
                    Stream = false
                };

                ProfileEvaluation? evaluation = new()
                {
                    Md5checksum = item.MD5Hash ?? string.Empty,
                    PromptMd5Checksum = Checksum.MD5Hash(prompt),
                    ProfileText = System.Text.Encoding.UTF8.GetBytes(item.UserBio ?? string.Empty),
                    LastDateTime = DateTime.UtcNow
                };

                // Create and start the stopwatch
                Stopwatch stopwatch = new();
                stopwatch.Start();

                await ollamaApi.GenerateAsync(request).StreamToEndAsync(responseTask =>
                {
                    string response = responseTask?.Response ?? string.Empty;
                    string logProblems = responseTask?.Logprobs != null ? string.Join(", ", responseTask.Logprobs) : "No logprobs";
                    evaluation.Evaluation = System.Text.Encoding.UTF8.GetBytes(response);
                });

                // Stop and retrieve elapsed time
                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;

                logger.Info($"Ollama API Call Time elapsed: {ts.TotalMilliseconds} ms");

                return evaluation;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing Ollama request for userId: {item.UserId} - {ex.Message}");
            }

            return null;
        }

        public static async Task<string> PerformOllamaImageGeneration(OllamaApiClient ollamaApi, string model, string prompt, string imagePath)
        {
            string evaluation = string.Empty;
            try
            {
                // Create and start the stopwatch
                Stopwatch stopwatch = new();
                stopwatch.Start();

                byte[] contentBytes = System.IO.File.ReadAllBytes(imagePath);
                string contentB64 = Convert.ToBase64String(contentBytes);

                GenerateRequest request = new()
                {
                    Model = model,
                    Prompt = prompt,
                    Images = [contentB64],
                    Stream = false
                };

                await ollamaApi.GenerateAsync(request).StreamToEndAsync(responseTask =>
                {
                    string response = responseTask?.Response ?? string.Empty;
                    evaluation = response;
                });

                // Stop and retrieve elapsed time
                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;

                logger.Info($"Ollama Image API Call Time elapsed: {ts.TotalMilliseconds} ms");

                return evaluation;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing Ollama request for ImageName : {imagePath} - {ex.Message}");
            }

            return evaluation;
        }
    }

    // Simplest implementation of IHavePriority<T>
    public class QueuedProcess : IHavePriority<int>
    {
        public int Priority { get; set; }
        public int retries { get; set; } = 0;
        public required string UserId { get; set; }
        public string? UserBio { get; set; }
        public bool IsFriend { get; set; }
        public string? ProfileUrl { get; set; }
        public TrustClassEnum UserTrustClass { get; set; } = TrustClassEnum.VISITOR;
        public AgeVerificationEnum AgeVerification { get; set; }

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

    public class ImageReference : IHavePriority<int> 
    {
        public int Priority { get; set; }
        public List<string> Base64Data { get; set; } = [];
        public string Md5Hash { get; set; } = string.Empty;
        public string InventoryId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int retries { get; set; } = 0;
        public string ItemName { get; set; } = string.Empty;
        public string ItemContentUrl { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public Print? PrintInfo { get; set; }
    }
}
