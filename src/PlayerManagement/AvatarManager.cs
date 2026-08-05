using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Tailgrab.Clients.VRCDB;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.XSOverlay;
using Tailgrab.Common;
using Tailgrab.Models;
using VRChat.API.Model;
using VRChat.API.Client;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{
    public class AvatarManager
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly HttpClient _httpClient;
        private static ServiceRegistry? serviceRegistry;
        private static ConcurrentPriorityQueue<IHavePriority<int>, int> avatarEvaluationQueue = new();
        private static ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new();
        private static Dictionary<String, DateTime> recentlyProcessedAvatars = [];

        [SetsRequiredMembers]
        public AvatarManager(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
            _httpClient = new HttpClient();
            _ = Task.Run(() => AvatarUnpackQueueTask(avatarEvaluationQueue, serviceRegistry));
            _ = Task.Run(() => AvatarCheckTask(priorityQueue, serviceRegistry));
        }

        #region Avatar Management
        public void ProcessAvatarUnpack(string authorName, string avatarName)
        {
            logger.Debug($"Enqueue Avatar Unpack Lookup : {avatarName} by {authorName}");

            try
            {
                AvatarUnpackProcess process = new(5, avatarName, authorName);

                if (!IsAvatarUnpackInQueue(process.AvatarName, process.AuthorName))
                {
                    avatarEvaluationQueue.Enqueue(process);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error enqueueing avatar unpack process for avatar: {avatarName} by author: {authorName}");

            }
        }
        private bool IsAvatarUnpackInQueue(string avatarName, string authorName)
        {
            return avatarEvaluationQueue.Any(item => ((AvatarUnpackProcess)item).AvatarName == avatarName && ((AvatarUnpackProcess)item).AuthorName == authorName);
        }

        public static async Task AvatarUnpackQueueTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            AvatarManager.logger.Info($"Avatar Unpack Queue Running");
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess)
                    {
                        if (result.Value is AvatarUnpackProcess item && item.AvatarName != null && item.AuthorName != null)
                        {
                            await AvatarUnpackHandler(item.AuthorName, item.AvatarName);
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


        public static async Task<Avatar?> AvatarUnpackHandler(string authorName, string avatarName)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            Player? player = PlayerManager.GetPlayerByDisplayName(authorName);
            string? authorId = string.Empty;
            if (player != null)
            {
                authorId = player.UserId;
            }
            else
            {
                authorId = await serviceRegistry.GetVRChatAPIClient().SearchUserByDisplayName(authorName);
            }

            if (string.IsNullOrEmpty(authorId))
            {
                logger.Warn($"Could not find author ID for author name: {authorName}");
                return null;
            }


            Avatar? avatar = await FindModeratedAvatarByAuthorIdAndName(authorId, avatarName);
            if (avatar == null)
            {
                avatar = await FindAvatarByAuthorIdAndName(authorId, avatarName);
            }

            if (avatar != null)
            {
                logger.Info($"Unpack avatar: {avatar.Name} by author: {avatar.AuthorName} (ID: {avatar.Id})");
            }

            return null;
        }

        public static async Task<Avatar?> FindModeratedAvatarByAuthorIdAndName(string authorId, string name)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            List<AvatarInfo> matchingInfos = dBContext.AvatarInfos.Where(a => a.UserId == authorId && a.AvatarName == name)
                .OrderByDescending(a => a.AlertType)
                .ToList();

            if (matchingInfos.Count == 0)
            {
                return null;
            }
            string avatarId = matchingInfos.First().AvatarId;
            return await FindAvatarByAvatarId(avatarId);
        }

        public static async Task<Avatar?> FindAvatarByAuthorIdAndName(string authorId, string name)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            List<AvatarItem> avatarItems = await serviceRegistry.GetVRCDBClient().GetAvatarsByAuthorAsync(authorId);
            foreach (AvatarItem avatarItem in avatarItems)
            {
                if (avatarItem.Name?.Equals(name, StringComparison.Ordinal) == true && avatarItem.Id != null)
                {
                    await Task.Delay(1000); // Delay to avoid hitting API rate limits
                    return await FindAvatarByAvatarId(avatarItem.Id);
                }
            }

            return null;
        }

        public static async Task<Avatar?> FindAvatarByAvatarId(string avatarId)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            Result<Avatar?> result = serviceRegistry.GetVRChatAPIClient().GetAvatarById(avatarId);
            if (result.Value != null)
            {
                return result.Value;
            }
            return null;
        }

        public void SetAvatarForPlayer(string displayName, string avatarName)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            PlayerManager.SetAvatarByDisplayName(displayName, avatarName);

            Player? player = PlayerManager.AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarWatch, $"User switched to Avatar : {avatarName}"); ;
            if (player != null)
            {
                AvatarInfo? watchedAvatar = CheckAvatarByName(avatarName);
                if (watchedAvatar != null)
                {
                    logger.Info($"** MODERATED AVATAR DETECTED for Player {displayName}: {avatarName} with AlertType {watchedAvatar.AlertType}");
                    if (watchedAvatar.AlertType > AlertTypeEnum.None)
                    {
                        player = PlayerManager.AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarWatch, $"User has used a watched Avatar : {avatarName} alertType: {watchedAvatar.AlertType}");
                        player?.AddAlertMessage(AlertClassEnum.Avatar, watchedAvatar.AlertType, $"{avatarName}");
                        OverlayManager overlay = serviceRegistry.GetXSOverlay();
                        _ = overlay.SendNotification(watchedAvatar.AlertType, $"Player \\b1{displayName}\\b0 has used a watched Avatar \\b1\\i1{avatarName}\\i0\\b0");
                    }
                }
                if (player != null)
                {
                    player.AvatarName = avatarName;
                    PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                }
            }
        }

        public static PlayerAvatar UpdatePlayerAvatar(string avatarName, string uploadedBy)
        {

            if (PlayerManager.GetPlayerAvatarByName(avatarName) is PlayerAvatar playerAvatar )
            {
                return playerAvatar;
            }
            else
            {
                playerAvatar = new PlayerAvatar(avatarName, uploadedBy);
                PlayerManager.SetPlayerAvatarByName(avatarName, playerAvatar);

            }
            return playerAvatar;
        }
        #endregion

        #region Avatar Moderation Management
        public void SyncAvatarModerations()
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                if (dBContext != null && vrcClient != null)
                {
                    int lineNumber = 0;
                    List<VRChat.API.Model.AvatarModeration> moderations = vrcClient.GetAvatarModerations();
                    foreach (VRChat.API.Model.AvatarModeration mod in moderations)
                    {
                        logger.Debug($"Processing Avatar Moderation for Avatar ID {mod.TargetAvatarId} with Status {mod.AvatarModerationType} and CreatedAt {mod.Created}");
                        if (mod != null && mod.AvatarModerationType.Equals(AvatarModerationType.Block))
                        {

                            lineNumber++;
                            AvatarInfo? existingAvatar = dBContext.AvatarInfos.Find(mod.TargetAvatarId);
                            if (existingAvatar == null || existingAvatar.AlertType < AlertTypeEnum.Nuisance)
                            {
                                QueuedModeratedAvatarWatch watchItem = new(2, mod.TargetAvatarId, AlertTypeEnum.Nuisance, lineNumber);
                                EnqueueModeratedAvatarForCheck(watchItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to clear the database");
            }
        }

        public static int GetQueueCount()
        {
            return priorityQueue.Count;
        }

        public void AddAvatar(AvatarInfo avatar)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                serviceRegistry.GetDBContext().AvatarInfos.Add(avatar);
                serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Error($"Error creating avatar: {ex.Message}");
            }
        }

        public static AvatarInfo? GetAvatarById(string avatarId)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            return serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
        }

        public void UpdateAvatar(AvatarInfo avatar)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                avatar.UpdatedAt = DateTime.UtcNow;
                serviceRegistry.GetDBContext().AvatarInfos.Update(avatar);
                serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Error($"Error updating avatar: {ex.Message}");
            }
        }

        public void DeleteAvatar(string avatarId)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            var avatar = serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
            if (avatar != null)
            {
                serviceRegistry.GetDBContext().AvatarInfos.Remove(avatar);
                serviceRegistry.GetDBContext().SaveChanges();
            }
        }

        public static void CacheAvatars(List<string> avatarIdInCache)
        {
            foreach (var avatarId in avatarIdInCache)
            {
                EnqueueAvatarForCheck(avatarId);
            }
        }

        private static void EnqueueAvatarForCheck(string avatarId)
        {
            if (recentlyProcessedAvatars.TryGetValue(avatarId, out DateTime dateTime))
            {
                if ((DateTime.UtcNow - dateTime).TotalMinutes < 60)
                {
                    return;
                }
            }
            recentlyProcessedAvatars.Add(avatarId, DateTime.UtcNow);

            var queuedItem = new QueuedAvatarProcess(5, avatarId);

            priorityQueue.Enqueue(queuedItem);
        }

        public static void EnqueueWatchAvatarForCheck(QueuedAvatarWatch watch)
        {
            priorityQueue.Enqueue(watch);
        }

        public void EnqueueModeratedAvatarForCheck(QueuedModeratedAvatarWatch watch)
        {
            priorityQueue.Enqueue(watch);
        }


        public void GetAvatarsFromUser(string userId, string avatarName)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            logger.Debug($"Fetching avatars for user {userId} to find avatar named {avatarName}");

            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                List<Avatar> avatarData = serviceRegistry.GetVRChatAPIClient().GetAvatarsByUserId(userId);
                foreach (var avatar in avatarData)
                {
                    logger.Debug(avatar.ToString());
                    if (avatar.Name.Equals(avatarName, StringComparison.OrdinalIgnoreCase))
                    {
                        AvatarInfo? dbAvatarInfo = GetAvatarById(avatar.Id);

                        if (dbAvatarInfo == null)
                        {
                            var avatarInfo = new AvatarInfo
                            {
                                AvatarId = avatar.Id,
                                UserId = avatar.AuthorId,
                                AvatarName = avatar.Name,
                                ImageUrl = avatar.ImageUrl,
                                CreatedAt = avatar.CreatedAt,
                                UpdatedAt = DateTime.UtcNow,
                                AlertType = AlertTypeEnum.None,
                                UserName = avatar.AuthorName
                            };

                            AddAvatar(avatarInfo);
                        }
                        else
                        {
                            dbAvatarInfo.UserId = avatar.AuthorId;
                            dbAvatarInfo.UserName = avatar.AuthorName;
                            dbAvatarInfo.AvatarName = avatar.Name;
                            dbAvatarInfo.ImageUrl = avatar.ImageUrl;
                            dbAvatarInfo.CreatedAt = avatar.CreatedAt;
                            UpdateAvatar(dbAvatarInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }
        }

        public void CompactDatabase()
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            serviceRegistry.GetDBContext().Database.ExecuteSqlRaw("VACUUM;");
        }

        public static AvatarInfo? CheckAvatarByName(string avatarName)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            var bannedAvatars = serviceRegistry.GetDBContext().AvatarInfos
                                         .Where(b => b.AvatarName != null && b.AvatarName.Equals(avatarName) && b.AlertType > 0)
                                         .OrderByDescending(b => b.AlertType)
                                         .ToList();

            if (bannedAvatars.Count > 0)
            {
                // Play alert sound based on the highest alert type found for the avatar
                AlertTypeEnum maxAlertType = bannedAvatars[0].AlertType;
                SoundManager.PlayAlertSound(CommonConst.Avatar_Alert_Key, maxAlertType);

                return bannedAvatars[0];
            }

            return null;
        }

        public static async Task AvatarCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            OllamaClient.logger.Info($"Avatar Queue Running");
            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess)
                    {
                        if (result.Value is QueuedAvatarProcess item && item.AvatarId != null)
                        {
                            await UpdateAmpAvatarRecord(serviceRegistry, dBContext, item.AvatarId);
                        }
                        else if (result.Value is QueuedAvatarWatch item2)
                        {
                            await UpdateWatchedAvatarRecord(serviceRegistry, dBContext, item2);
                        }
                        else if (result.Value is QueuedModeratedAvatarWatch item3)
                        {
                            await UpdateModeratedAvatarRecord(serviceRegistry, dBContext, item3);
                        }
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

        private static async Task UpdateAmpAvatarRecord(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, string avatarId)
        {
            try
            {
                AvatarInfo? dbAvatarInfo = dBContext.AvatarInfos.Find(avatarId);
                bool updateNeeded = false;
                if (dbAvatarInfo == null)
                {
                    updateNeeded = true;
                }
                else if (dbAvatarInfo.AlertType == AlertTypeEnum.None &&
                    (!dbAvatarInfo.UpdatedAt.HasValue || dbAvatarInfo.UpdatedAt.Value >= DateTime.UtcNow.AddHours(-2)))
                {
                    updateNeeded = true;
                }

                if (updateNeeded)
                {
                    // Adds and Updates avatar info in the database, if it doesn't exist or was last updated more than 2 hours ago
                    Result<Avatar?> result = FetchUpdateAvatarData(serviceRegistry, dBContext, avatarId, dbAvatarInfo);

                    if (result.Value == null && dbAvatarInfo == null)
                    {
                        // Private Avatar
                        CreateAvatarInfoForPrivate(dBContext, avatarId);
                    }

                    // Wait for a short period before checking the queue again
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user profile for userId: {avatarId}");
            }
        }


        private static async Task UpdateModeratedAvatarRecord(ServiceRegistry _serviceRegistry, TailgrabDBContext dbContext, QueuedModeratedAvatarWatch watch)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                // Fetch the AvatarInfo record
                AvatarInfo? avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);
                FetchUpdateAvatarData(_serviceRegistry, dbContext, watch.AvatarId, avatarInfo);
                avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);

                if (avatarInfo == null)
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' not found in database/vrc, skipping.");
                    await serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(watch.AvatarId);
                }
                else if (avatarInfo.AlertType == AlertTypeEnum.None)
                {
                    avatarInfo.AlertType = AlertTypeEnum.Nuisance;
                    avatarInfo.UpdatedAt = DateTime.UtcNow;
                    dbContext.AvatarInfos.Update(avatarInfo);
                    dbContext.SaveChanges();
                }
                else
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' already has Has an Alert, skipping.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Line {watch.LineNumber}: Error processing avatar ID '{watch.AvatarId}'");
            }

            // Throttle processing to avoid overwhelming the API
            await Task.Delay(1000);
        }

        private static async Task UpdateWatchedAvatarRecord(ServiceRegistry _serviceRegistry, TailgrabDBContext dbContext, QueuedAvatarWatch watch)
        {

            try
            {
                // Fetch the AvatarInfo record
                AvatarInfo? avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);
                if (avatarInfo != null && avatarInfo.UpdatedAt > DateTime.UtcNow.AddHours(-12))
                {
                    // Skip processing if the avatar was updated within the last 12 hours
                    return;
                }

                FetchUpdateAvatarData(_serviceRegistry, dbContext, watch.AvatarId, avatarInfo);
                avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);

                if (avatarInfo == null)
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' not found in database/vrc, skipping.");
                }
                else if (avatarInfo.AlertType == AlertTypeEnum.None)
                {

                    avatarInfo.AlertType = watch.AlertType;
                    avatarInfo.UpdatedAt = DateTime.UtcNow;
                    dbContext.AvatarInfos.Update(avatarInfo);
                    dbContext.SaveChanges();

                    if (avatarInfo.AlertType >= AlertTypeEnum.Nuisance)
                    {
                        await _serviceRegistry.GetVRChatAPIClient().BlockAvatarGlobal(avatarInfo.AvatarId);
                    }
                    else
                    {
                        await _serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(avatarInfo.AvatarId);
                    }

                    logger.Debug($"Line {watch.LineNumber}: Set Watch State for Avatar ID '{watch.AvatarId}'");

                }
                else
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' already has Has an Alert, skipping.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Line {watch.LineNumber}: Error processing avatar ID '{watch.AvatarId}'");
            }

            // Throttle processing to avoid overwhelming the API
            await Task.Delay(1000);
        }


        private static void CreateAvatarInfoForPrivate(TailgrabDBContext dBContext, string AvatarId)
        {
            var avatarInfo = new AvatarInfo
            {
                AvatarId = AvatarId,
                UserId = "",
                AvatarName = $"Unknown Avatar {AvatarId}",
                ImageUrl = "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                dBContext.Add(avatarInfo);
                dBContext.SaveChanges();
                logger.Debug($"Adding fallback avatar record for {avatarInfo}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding fallback avatar record for {AvatarId}: {ex.Message}");
            }
        }

        public static Result<Avatar?> FetchUpdateAvatarData(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, string AvatarId, AvatarInfo? dbAvatarInfo)
        {
            Result<Avatar?> result = new Result<Avatar?>
            {
                Value = null,
                Exception = new InvalidOperationException("ServiceRegistry is not initialized.")
            };

            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return result;
            }

            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                result = serviceRegistry.GetVRChatAPIClient().GetAvatarById(AvatarId);
                if (result.Value != null)
                {
                    if (dbAvatarInfo == null)
                    {
                        try
                        {
                            var avatarInfo = new AvatarInfo
                            {
                                AvatarId = result.Value.Id,
                            };
                            UpdateAvatarInfoProperties(result.Value, avatarInfo);

                            dBContext.Add(avatarInfo);
                            dBContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error adding avatar record for {AvatarId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Ensure entity is attached to the dbContext before updating to avoid Detached state errors
                        var entry = dBContext.Entry(dbAvatarInfo);
                        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                        {
                            dBContext.Attach(dbAvatarInfo);
                            entry = dBContext.Entry(dbAvatarInfo);
                        }

                        UpdateAvatarInfoProperties(result.Value, dbAvatarInfo);

                        try
                        {
                            entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            dBContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error updating avatar record for {AvatarId}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    if ( result.Exception != null && result.Exception is ApiException )
                    {
                        ApiException apiEx = (ApiException)result.Exception;
                        logger.Warn($"API Exception: StatusCode={apiEx.ErrorCode}, Content={apiEx.ErrorContent}");

                        if( apiEx.ErrorCode == 404)
                        {
                            logger.Warn($"Avatar with ID {AvatarId} not found in VRChat API (404 Not Found).");
                            ResetAvatarRecordToNone(dBContext, AvatarId);
                            serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(AvatarId);
                            return result;
                        }
                    }

                    logger.Warn($"Avatar with ID {AvatarId} not found in VRChat API.");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return result;
        }

        private static void UpdateAvatarInfoProperties(Avatar avatar, AvatarInfo avatarInfo)
        {
            avatarInfo.UserId = avatar.AuthorId;
            avatarInfo.UserName = avatar.AuthorName;
            avatarInfo.AvatarName = avatar.Name;
            avatarInfo.ImageUrl = avatar.ImageUrl;
            avatarInfo.CreatedAt = avatar.CreatedAt;
            avatarInfo.UpdatedAt = DateTime.UtcNow;
        }

        private static void ResetAvatarRecordToNone(TailgrabDBContext dBContext, string AvatarId)
        {
            AvatarInfo? info = dBContext.AvatarInfos.Find(AvatarId);
            if (info != null)
            {
                try
                {
                    info.AlertType = AlertTypeEnum.None;
                    info.UpdatedAt = DateTime.UtcNow;
                    dBContext.Update(info);
                    dBContext.SaveChanges();
                    logger.Info($"Updated AvatarInfo for {AvatarId} to AlertType None due to 404 Not Found.");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error updating AvatarInfo for {AvatarId}: {ex.Message}");
                }
            }
        }

        public async Task<bool> SwitchAvatar(string avatarId)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return false;
            }

            try
            {
                Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                bool avatarResult = await vrcClient.ChangeIntoAvatar(avatarId);
                if (avatarResult)
                {
                    logger.Info($"Successfully switched avatar to {avatarId}");
                    return true;
                }
                else
                {
                    logger.Error($"Failed to switch avatar to {avatarId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error switching avatar to {avatarId}");
                return false;
            }
        }

        #endregion

        #region Avatar GIST processing
        /// <summary>
        /// Downloads a GIST content file, verifies its checksum against registry, 
        /// and processes AvatarIds if the file is new or changed.
        /// </summary>
        /// <param name="gistUrl">The URL of the GIST raw content to download</param>
        /// <returns>True if processing was successful, false otherwise</returns>
        public async Task<bool> ProcessAvatarGistList()
        {
            string? gistUrl = GetStoredUri();
            if (string.IsNullOrWhiteSpace(gistUrl))
            {
                logger.Error("Avatar GIST URL was empty, no update.");
                return false;
            }

            try
            {
                logger.Info($"Downloading GIST content from: {gistUrl}");

                // Download the GIST content
                string gistContent = await DownloadGistContentAsync(gistUrl);

                if (string.IsNullOrEmpty(gistContent))
                {
                    logger.Warn("Downloaded GIST content is empty.");
                    return false;
                }

                // Calculate MD5 checksum of the downloaded content
                string currentChecksum = CalculateMD5Checksum(gistContent);
                logger.Debug($"Calculated checksum: {currentChecksum}");

                // Get the stored checksum from registry
                string? storedChecksum = GetStoredChecksum();

                // Compare checksums
                if (storedChecksum != null && storedChecksum.Equals(currentChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Info("GIST content has not changed (checksum match). Skipping processing.");
                    return true;
                }

                logger.Info("GIST content is new or has changed. Processing avatar IDs...");

                // Process the file line by line
                int processedCount = await ProcessAvatarIdsAsync(gistContent);

                logger.Info($"Processed {processedCount} avatar IDs from GIST.");

                // Save the new checksum to registry
                SaveChecksum(currentChecksum);
                logger.Info("Checksum saved to registry.");

                return true;
            }
            catch (HttpRequestException ex)
            {
                logger.Error(ex, $"Failed to download GIST content from {gistUrl}");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while processing GIST BOS list.");
                return false;
            }
        }

        private async Task<string> DownloadGistContentAsync(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private string CalculateMD5Checksum(string content)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(content);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private string? GetStoredChecksum()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(Common.CommonConst.ConfigRegistryPath))
                {
                    if (key == null)
                    {
                        logger.Debug("Registry key does not exist. No stored checksum found.");
                        return null;
                    }

                    string? value = key.GetValue(Common.CommonConst.Registry_Avatar_Checksum) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        logger.Debug("No checksum stored in registry.");
                        return null;
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to read checksum from registry.");
                return null;
            }
        }

        private string? GetStoredUri()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(Common.CommonConst.ConfigRegistryPath))
                {
                    if (key == null)
                    {
                        logger.Debug("Registry key does not exist. No stored URI");
                        return null;
                    }

                    string? value = key.GetValue(Common.CommonConst.Registry_Avatar_Gist) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        logger.Debug("No Avatar GIST Uri stored in registry.");
                        return null;
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to read Avatar GIST Uri from registry.");
                return null;
            }
        }

        private void SaveChecksum(string checksum)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Common.CommonConst.ConfigRegistryPath))
                {
                    key.SetValue(Common.CommonConst.Registry_Avatar_Checksum, checksum, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save checksum to registry.");
            }
        }

        private async Task<int> ProcessAvatarIdsAsync(string gistContent)
        {
            int processedCount = 0;

            using (System.IO.StringReader reader = new System.IO.StringReader(gistContent))
            {
                string? line;
                int lineNumber = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    AvatarImportItem? importItem = ProcessAvatarLineItem(line, lineNumber);
                    if (importItem != null)
                    {
                        QueuedAvatarWatch watchItem = new QueuedAvatarWatch(1, importItem.AvatarId, importItem.AlertType, lineNumber);
                        EnqueueWatchAvatarForCheck(watchItem);
                        processedCount++;
                    }
                }

                logger.Info($"Total valid AvatarImportItems parsed: {processedCount}");

            }

            return processedCount;
        }

        private AvatarImportItem? ProcessAvatarLineItem(string line, int lineNumber)
        {
            // Split by whitespace or comma to get the first column
            string pattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
            string[] columns = Regex.Split(line, pattern); //.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (columns.Length < 3)
            {
                logger.Warn($"Line {lineNumber}: Expected at least 3 columns (AvatarId, AvatarName, AlertType), but got {columns.Length}. Skipping line.");
                logger.Warn(line);
                return null;
            }

            string avatarId = columns[0].Trim().Trim('"');
            string avatarName = columns[1].Trim().Trim('"');
            string avatarAlert = columns[2].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(avatarId))
            {
                logger.Warn($"Line {lineNumber}: Empty avatar ID, skipping.");
                logger.Warn(line);
                return null;
            }

            // Convert the alert type string to the AlertTypeEnum, defaulting to None if parsing fails
            AlertTypeEnum alertType = AlertTypeEnum.None;
            if (!Enum.TryParse<AlertTypeEnum>(avatarAlert, out alertType))
            {
                logger.Warn($"Line {lineNumber}: Invalid AlertType '{avatarAlert}' for Avatar ID '{avatarId}', defaulting to None.");
            }

            return new AvatarImportItem(lineNumber, avatarId, avatarName, alertType);
        }
        #endregion 
    }


    #region Support DTO Classes
    public class AvatarUnpackProcess(int priority, string avatarName, string authorName) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;
        public string AvatarName { get; set; } = avatarName;
        public string AuthorName { get; set; } = authorName;
    }

    public class AvatarImportItem
    {
        public int LineNumber { get; set; }
        public string AvatarId { get; set; }
        public string AvatarName { get; set; }
        public AlertTypeEnum AlertType { get; set; }
        public AvatarImportItem(int lineNumber, string avatarId, string avatarName, AlertTypeEnum alertType)
        {
            LineNumber = lineNumber;
            AvatarId = avatarId;
            AvatarName = avatarName;
            AlertType = alertType;
        }

        public override string ToString()
        {
            return $"Line {LineNumber}: AvatarId={AvatarId}, AvatarName={AvatarName}, AlertType={AlertType}";
        }
    }

    internal class QueuedAvatarProcess(int priority, string avatarId) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;
        public string AvatarId { get; set; } = avatarId;
    }


    public class QueuedAvatarWatch(int priority, string avatarId, AlertTypeEnum alertType, int lineNumber) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;
        public string AvatarId { get; set; } = avatarId;
        public AlertTypeEnum AlertType { get; set; } = alertType;
        public int LineNumber { get; set; } = lineNumber;
    }

    public class QueuedModeratedAvatarWatch(int priority, string avatarId, AlertTypeEnum alertType, int lineNumber) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;
        public string AvatarId { get; set; } = avatarId;
        public AlertTypeEnum AlertType { get; set; } = alertType;
        public int LineNumber { get; set; } = lineNumber;
    }
    #endregion
}
