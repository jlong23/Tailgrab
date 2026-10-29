using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NLog;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tailgrab.Clients.Github;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRCDB;
using Tailgrab.Clients.XSOverlay;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using Tailgrab.PlayerManagement.Views;
using VRChat.API.Client;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.Manager
{
    public class AvatarManager
    {
        protected static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private Task? _AmplitudeWatch;
        private readonly HttpClient _httpClient;
        private static ServiceRegistry? _serviceRegistry;
        private static ConcurrentPriorityQueue<IHavePriority<int>, int> avatarEvaluationQueue = new();
        private static ConcurrentPriorityQueue<IHavePriority<int>, int> _AvatarCheckQueue = new();
        private static Dictionary<String, DateTime> recentlyProcessedAvatars = [];
        private static Dictionary<string, Avatar> avatarUnpackCache = new();

        /// <summary>
        /// The path to the VRChat AppData directory.
        /// </summary>
        public static readonly string VRChatAmplitudePath = Path.Combine(CommonConst.UserProfile, @"AppData", "Local", "Temp", "VRChat", "VRChat");

        static FileSystemWatcher AmpWatcher = new FileSystemWatcher();

        [SetsRequiredMembers]
        public AvatarManager(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            _serviceRegistry = registry;
            _httpClient = new HttpClient();
            _ = Task.Run(() => AvatarUnpackQueueTask(avatarEvaluationQueue, _serviceRegistry));
            _ = Task.Run(() => AvatarCheckTask(_AvatarCheckQueue, _serviceRegistry));

            if (EnableAvatarModerationCaching)
            {
                _Logger.Info("Syncing avatar moderations...");
                SyncAvatarModerations();
            }

            if (EnableAmplitudeCacheWatcher)
            {
                // Start the Amplitude Cache watcher task on a background thread
                string ampPath = VRChatAmplitudePath + Path.DirectorySeparatorChar;
                _Logger.Info($"Starting Amplitude Cache watcher for: '{ampPath}'");
                _AmplitudeWatch = Task.Run(() => WatchAmpCache(ampPath));
            }
        }

        #region Configuration
        public bool EnableAmplitudeCacheWatcher
        {
            get => ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, false);
            set => ConfigStore.PutStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, value);
        }

        public bool EnableAvatarModerationCaching
        {
            get => ConfigStore.GetStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, true);
            set => ConfigStore.PutStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, value);
        }
        #endregion


        #region Avatar Management
        public void ProcessAvatarUnpack(string authorName, string avatarName)
        {
            string cacheKey = $"{avatarName} by {authorName}";
            _Logger.Debug($"Enqueue Avatar Unpack Lookup : {cacheKey}");

            try
            {
                if (avatarUnpackCache.ContainsKey(cacheKey))
                {
                    _Logger.Debug($"Avatar Unpack Cache Hit : {cacheKey}");
                    Avatar avatar = avatarUnpackCache[cacheKey];
                    UpdateMatchingAvatars(avatar);
                    return;
                }

                AvatarUnpackProcess process = new(5, avatarName, authorName);
                if (!IsAvatarUnpackInQueue(process.AvatarName, process.AuthorName))
                {
                    avatarEvaluationQueue.Enqueue(process);
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, $"Error enqueueing avatar unpack process for avatar: {avatarName} by author: {authorName}");

            }
        }
        private bool IsAvatarUnpackInQueue(string avatarName, string authorName)
        {
            return avatarEvaluationQueue.Any(item => ((AvatarUnpackProcess)item).AvatarName == avatarName && ((AvatarUnpackProcess)item).AuthorName == authorName);
        }

        public static async Task AvatarUnpackQueueTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            AvatarManager._Logger.Info($"Avatar Unpack Queue Running");
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
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            string cacheKey = $"{avatarName} by {authorName}";

            // Check if the authorName is in the instance
            Player? player = PlayerManager.GetPlayerByDisplayName(authorName);
            string? authorId = string.Empty;
            if (player != null)
            {
                // Use it for the Search
                authorId = player.UserId;
            }
            else
            {
                // Otherwise, search for the authorId using the VRChat API
                authorId = await _serviceRegistry.GetVRChatAPIClient().SearchUserByDisplayName(authorName);
            }

            if (string.IsNullOrEmpty(authorId))
            {
                _Logger.Warn($"Could not find author ID for author name: {authorName}");
                return null;
            }

            // Attempt to fine the Avatar by AuthorId and AvatarName, first checking for moderated avatars, then regular avatars
            Avatar? avatar = await FindModeratedAvatarByAuthorIdAndName(authorId, avatarName);
            if (avatar == null)
            {
                avatar = await FindAvatarByAuthorIdAndName(authorId, avatarName);
            }

            if (avatar != null)
            {
                avatarUnpackCache[cacheKey] = avatar;
                _Logger.Info($"Unpack avatar: {avatar.Name} by author: {avatar.AuthorName} (ID: {avatar.Id})");
                UpdateMatchingAvatars(avatar);
            }

            return null;
        }

        /*
         * Updates all players that are currently using the specified avatar to use the new avatar ID.
         * This is useful when an avatar has been updated or changed, and we want to ensure that all 
         * players using that avatar are updated accordingly.
         */
        public static void UpdateMatchingAvatars(Avatar avatar)
        {
            List<Player> matchingPlayers = PlayerManager.FindPlayersByAvatar(avatar.Name);
            foreach (Player player in matchingPlayers)
            {
                if (player.AvatarId != avatar.Id)
                {
                    player.AvatarId = avatar.Id;
                    _Logger.Info($"Updated Player {player.DisplayName} to use Avatar ID: {avatar.Id} for Avatar Name: {avatar.Name}");
                    player.AddEvent(new PlayerEvent(PlayerEvent.EventType.AvatarLookup, $"Updated to use Avatar ID: {avatar.Id} for Avatar Name: {avatar.Name}"));
                    PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                }
            }
        }

        public static async Task<Avatar?> FindModeratedAvatarByAuthorIdAndName(string authorId, string name)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
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
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            List<AvatarItem> avatarItems = await _serviceRegistry.GetVRCDBClient().GetAvatarsByAuthorAsync(authorId);
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
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            Result<Avatar?> result = _serviceRegistry.GetVRChatAPIClient().GetAvatarById(avatarId);
            if (result.Value != null)
            {
                return result.Value;
            }
            return null;
        }

        public void SetAvatarForPlayer(string displayName, string avatarName)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            PlayerManager.SetAvatarByDisplayName(displayName, avatarName);

            Player? player = PlayerManager.AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarWatch, $"User switched to Avatar : {avatarName}"); ;
            if (player != null && player.AvatarName != avatarName )
            {
                AvatarInfo? watchedAvatar = CheckAvatarByName(avatarName);
                if (watchedAvatar != null)
                {
                    _Logger.Info($"** MODERATED AVATAR DETECTED for Player {displayName}: {avatarName} with AlertType {watchedAvatar.AlertType}");
                    if (watchedAvatar.AlertType > AlertTypeEnum.None)
                    {
                        PlayerManager.AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarWatch, $"User has used a watched Avatar : {avatarName} alertType: {watchedAvatar.AlertType}");
                        player.AddAlertMessage(AlertClassEnum.Avatar, watchedAvatar.AlertType, $"{avatarName}");
                        OverlayManager overlay = _serviceRegistry.GetXSOverlay();

                        Image? avatarAlert = null;

                        _ = overlay.SendNotification(
                            watchedAvatar.AlertType,
                            "<b>Avatar Watch Alert</b>",
                            $"<b>{displayName}</b> has used a watched Avatar <b><i>{avatarName}</i></b>",
                            avatarAlert);
                    }
                }

                player.AvatarName = avatarName;
                player.AvatarId = string.Empty; // Reset AvatarId to null, will be updated when unpacked
                PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
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
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                TailgrabDBContext dBContext = _serviceRegistry.GetDBContext();
                Tailgrab.Clients.VRChat.VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                if (dBContext != null && vrcClient != null)
                {
                    int lineNumber = 0;
                    List<VRChat.API.Model.AvatarModeration> moderations = vrcClient.GetAvatarModerations();
                    foreach (VRChat.API.Model.AvatarModeration mod in moderations)
                    {
                        _Logger.Debug($"Processing Avatar Moderation for Avatar ID {mod.TargetAvatarId} with Status {mod.AvatarModerationType} and CreatedAt {mod.Created}");
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
                _Logger.Error(ex, "Failed to clear the database");
            }
        }

        public static int GetQueueCount()
        {
            return _AvatarCheckQueue.Count;
        }

        public void AddAvatar(AvatarInfo avatar)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                _serviceRegistry.GetDBContext().AvatarInfos.Add(avatar);
                _serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                _Logger.Error($"Error creating avatar: {ex.Message}");
            }
        }

        public static AvatarInfo? GetAvatarById(string avatarId)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            return _serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
        }

        public void UpdateAvatar(AvatarInfo avatar)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                avatar.UpdatedAt = DateTime.UtcNow;
                _serviceRegistry.GetDBContext().AvatarInfos.Update(avatar);
                _serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                _Logger.Error($"Error updating avatar: {ex.Message}");
            }
        }

        public void DeleteAvatar(string avatarId)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            var avatar = _serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
            if (avatar != null)
            {
                _serviceRegistry.GetDBContext().AvatarInfos.Remove(avatar);
                _serviceRegistry.GetDBContext().SaveChanges();
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

            _AvatarCheckQueue.Enqueue(queuedItem);
        }

        public static void EnqueueWatchAvatarForCheck(QueuedAvatarWatch watch)
        {
            _AvatarCheckQueue.Enqueue(watch);
        }

        public void EnqueueModeratedAvatarForCheck(QueuedModeratedAvatarWatch watch)
        {
            _AvatarCheckQueue.Enqueue(watch);
        }


        public void GetAvatarsFromUser(string userId, string avatarName)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            _Logger.Debug($"Fetching avatars for user {userId} to find avatar named {avatarName}");

            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                List<Avatar> avatarData = _serviceRegistry.GetVRChatAPIClient().GetAvatarsByUserId(userId);
                foreach (var avatar in avatarData)
                {
                    _Logger.Debug(avatar.ToString());
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
                _Logger.Error($"Error fetching avatar: {ex.Message}");
            }
        }

        public void CompactDatabase()
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            _serviceRegistry.GetDBContext().Database.ExecuteSqlRaw("VACUUM;");
        }

        public static AvatarInfo? CheckAvatarByName(string avatarName)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            var bannedAvatars = _serviceRegistry.GetDBContext().AvatarInfos
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

        public static async Task AvatarCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> queue, ServiceRegistry serviceRegistry)
        {
            int processedCount = 0;
            OllamaClient.logger.Info($"Avatar Queue Running");
            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = queue.Dequeue();
                    if (result.IsSuccess)
                    {
                        processedCount++;
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

                        if(processedCount % 50 == 0)
                        {
                            _Logger.Info($"Processed {processedCount} avatars from the queue. Throttling for 10 seconds to avoid overwhelming the API.");
                            await Task.Delay(10000); // Throttle processing to avoid overwhelming the API
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
                _Logger.Error(ex, $"Error fetching user profile for userId: {avatarId}");
            }
        }


        private static async Task UpdateModeratedAvatarRecord(ServiceRegistry _serviceRegistry, TailgrabDBContext dbContext, QueuedModeratedAvatarWatch watch)
        {
            if (AvatarManager._serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            try
            {
                // Fetch the AvatarInfo record
                AvatarInfo? avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);
                Result<Avatar?> result = FetchUpdateAvatarData(_serviceRegistry, dbContext, watch.AvatarId, avatarInfo);
                if (result.Value != null)
                {
                    avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);

                    if (avatarInfo == null)
                    {
                        _Logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' not found in database/vrc, skipping.");
                        await AvatarManager._serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(watch.AvatarId);
                    }
                    else if (avatarInfo.AlertType == AlertTypeEnum.None)
                    {
                        avatarInfo.AlertType = AlertTypeEnum.Nuisance;
                        avatarInfo.UpdatedAt = DateTime.UtcNow;
                        dbContext.AvatarInfos.Update(avatarInfo);
                        dbContext.SaveChanges();

                        _Logger.Info($"Line {watch.LineNumber}: Set Watch State for Avatar ID '{watch.AvatarId}' /  '{avatarInfo.AvatarName} by {avatarInfo.UserName}' to Nuisance due to moderation."); ")
                    }
                    else
                    {
                        _Logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' already has Has an Alert, skipping.");
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, $"Line {watch.LineNumber}: Error processing avatar ID '{watch.AvatarId}'");
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
                //if (avatarInfo != null && avatarInfo.UpdatedAt > DateTime.UtcNow.AddHours(-12))
                //{
                //    // Skip processing if the avatar was updated within the last 12 hours
                //    return;
                //}

                Result<Avatar?> result = FetchUpdateAvatarData(_serviceRegistry, dbContext, watch.AvatarId, avatarInfo);
                if (result.Value != null)
                {
                    avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);

                    if (avatarInfo == null)
                    {
                        _Logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' not found in database/vrc, skipping.");
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

                        _Logger.Debug($"Line {watch.LineNumber}: Set Watch State for Avatar ID '{watch.AvatarId}'");

                    }
                    else
                    {
                        _Logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' already has Has an Alert, skipping.");
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, $"Line {watch.LineNumber}: Error processing avatar ID '{watch.AvatarId}'");
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
                _Logger.Debug($"Adding fallback avatar record for {avatarInfo}");
            }
            catch (Exception ex)
            {
                _Logger.Error($"Error adding fallback avatar record for {AvatarId}: {ex.Message}");
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
                _Logger.Warn("ServiceRegistry is not initialized.");
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
                            _Logger.Error($"Error adding avatar record for {AvatarId}: {ex.Message}");
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
                            _Logger.Error($"Error updating avatar record for {AvatarId}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    if ( result.Exception != null && result.Exception is ApiException )
                    {
                        ApiException apiEx = (ApiException)result.Exception;
                        _Logger.Warn($"API Exception: StatusCode={apiEx.ErrorCode}, Content={apiEx.ErrorContent}");

                        if( apiEx.ErrorCode == 404)
                        {
                            _Logger.Warn($"Avatar with ID {AvatarId} not found in VRChat API (404 Not Found).");
                            ResetAvatarRecordToNone(dBContext, AvatarId);
                            result.Value = null;
                            result.Exception = apiEx;
                            return result;
                        }
                    }

                    _Logger.Warn($"Avatar with ID {AvatarId} not found in VRChat API.");
                }
            }
            catch (Exception ex)
            {
                _Logger.Error($"Error fetching avatar: {ex.Message}");
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
                    _Logger.Info($"Updated AvatarInfo for {AvatarId} to AlertType None due to 404 Not Found.");

                    if( _serviceRegistry != null)
                        Task.Run(() => _serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(AvatarId));

                }
                catch (Exception ex)
                {
                    _Logger.Error($"Error updating AvatarInfo for {AvatarId}: {ex.Message}");
                }
            }
        }

        public async Task<bool> SwitchAvatar(string avatarId)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return false;
            }

            try
            {
                Tailgrab.Clients.VRChat.VRChatClient vrcClient = _serviceRegistry.GetVRChatAPIClient();
                bool avatarResult = await vrcClient.ChangeIntoAvatar(avatarId);
                if (avatarResult)
                {
                    _Logger.Info($"Successfully switched avatar to {avatarId}");
                    return true;
                }
                else
                {
                    _Logger.Error($"Failed to switch avatar to {avatarId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, $"Error switching avatar to {avatarId}");
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
            string? gistUrl = null;
            string? gistId = ConfigStore.GetStoredKeyString(CommonConst.Registry_Github_Gist_ID);

            if ( ConfigStore.GetStoredKeyBool(CommonConst.Registry_Github_Use_Automation, false) == true && !string.IsNullOrEmpty(gistId))
            {
                gistUrl = await GithubClient.GetGistFileUrl(gistId, "Avatars.csv");
                _Logger.Info($"Using GIST URL from GitHub Automation: {gistUrl}");
            } 
            else
            {
                gistUrl = GetStoredUri();
                _Logger.Info($"Using GIST URL from stored URI: {gistUrl}");
            }

            if (string.IsNullOrWhiteSpace(gistUrl))
            {
                _Logger.Error("Avatar GIST URL was empty, no update.");
                return false;
            }

            try
            {
                _Logger.Info($"Downloading GIST content from: {gistUrl}");

                // Download the GIST content
                string gistContent = await DownloadGistContentAsync(gistUrl);

                if (string.IsNullOrEmpty(gistContent))
                {
                    _Logger.Warn("Downloaded GIST content is empty.");
                    return false;
                }

                // Calculate MD5 checksum of the downloaded content
                string currentChecksum = CalculateMD5Checksum(gistContent);
                _Logger.Debug($"Calculated checksum: {currentChecksum}");

                // Get the stored checksum from registry
                string? storedChecksum = GetStoredChecksum();

                // Compare checksums
                if (storedChecksum != null && storedChecksum.Equals(currentChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    _Logger.Info("GIST content has not changed (checksum match). Skipping processing.");
                    return true;
                }

                _Logger.Info("GIST content is new or has changed. Processing avatar IDs...");

                // Process the file line by line
                int processedCount = await ProcessAvatarIdsAsync(gistContent);

                _Logger.Info($"Processed {processedCount} avatar IDs from GIST.");

                // Save the new checksum to registry
                SaveChecksum(currentChecksum);
                _Logger.Info("Checksum saved to registry.");

                return true;
            }
            catch (HttpRequestException ex)
            {
                _Logger.Error(ex, $"Failed to download GIST content from {gistUrl}");
                return false;
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, "An error occurred while processing GIST BOS list.");
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
                        _Logger.Debug("Registry key does not exist. No stored checksum found.");
                        return null;
                    }

                    string? value = key.GetValue(Common.CommonConst.Registry_Avatar_Checksum) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        _Logger.Debug("No checksum stored in registry.");
                        return null;
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, "Failed to read checksum from registry.");
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
                        _Logger.Debug("Registry key does not exist. No stored URI");
                        return null;
                    }

                    string? value = key.GetValue(Common.CommonConst.Registry_Avatar_Gist) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        _Logger.Debug("No Avatar GIST Uri stored in registry.");
                        return null;
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, "Failed to read Avatar GIST Uri from registry.");
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
                _Logger.Error(ex, "Failed to save checksum to registry.");
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

                _Logger.Info($"Total valid AvatarImportItems parsed: {processedCount}");

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
                _Logger.Warn($"Line {lineNumber}: Expected at least 3 columns (AvatarId, AvatarName, AlertType), but got {columns.Length}. Skipping line.");
                _Logger.Warn(line);
                return null;
            }

            string avatarId = columns[0].Trim().Trim('"');
            string avatarName = columns[1].Trim().Trim('"');
            string avatarAlert = columns[2].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(avatarId))
            {
                _Logger.Warn($"Line {lineNumber}: Empty avatar ID, skipping.");
                _Logger.Warn(line);
                return null;
            }

            // Convert the alert type string to the AlertTypeEnum, defaulting to None if parsing fails
            AlertTypeEnum alertType = AlertTypeEnum.None;
            if (!Enum.TryParse<AlertTypeEnum>(avatarAlert, out alertType))
            {
                _Logger.Warn($"Line {lineNumber}: Invalid AlertType '{avatarAlert}' for Avatar ID '{avatarId}', defaulting to None.");
            }

            return new AvatarImportItem(lineNumber, avatarId, avatarName, alertType);
        }


        public async Task<string> GetAvatarExport()
        {
            try
            {
                if(_serviceRegistry == null)
                {
                    _Logger.Warn("ServiceRegistry is not initialized.");
                    return string.Empty;
                }

                var context = _serviceRegistry.GetDBContext();
                if (context == null)
                {
                    _Logger.Warn("Database context is not available.");
                    return string.Empty;
                }

                var avatarInfos = context.AvatarInfos
                    .Where(a => a.AlertType > AlertTypeEnum.None)
                    .OrderBy(a => a.AvatarName)
                    .ToList();

                if (avatarInfos.Count == 0)
                {
                    _Logger.Info("No avatars with alerts found to export.");
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var avatar in avatarInfos)
                {
                    string alertTypeString = AlertTypeEnumMapper.MapEnumToString(avatar.AlertType);
                    sb.AppendLine($"{Utility.EscapeCsvField(avatar.AvatarId)},{Utility.EscapeCsvField(avatar.AvatarName)},{Utility.EscapeCsvField(alertTypeString)}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, "Failed to get avatar export data");
                return string.Empty;
            }
        }
        #endregion

        #region User Avatar Overlay
        public async Task<List<UserAvatarViewModel>> LoadUserAvatarAsync(string userId)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return new List<UserAvatarViewModel>();
            }

            var avatarViewModels = new List<UserAvatarViewModel>();

            try
            {
                // Fetch DB data on background thread
                TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                VRCDBClient vrcdbClient = _serviceRegistry.GetVRCDBClient();

                // Fetch groups from API on background thread
                List<AvatarItem> avatarItems = await Task.Run(() => vrcdbClient.GetAvatarsByAuthorAsync(userId));
                _Logger.Info($"Fetched {avatarItems?.Count ?? 0} avatars for user {userId}");

                if (avatarItems == null || avatarItems.Count == 0)
                {
                    _Logger.Info($"No avatars found for user {userId}");
                    return avatarViewModels;
                }


                foreach (var avatarItem in avatarItems)
                {
                    if (avatarItem.Id != null)
                    {

                        UserAvatarViewModel model = new UserAvatarViewModel()
                        {
                            AvatarId = avatarItem.Id ?? string.Empty,
                            Name = avatarItem.Name ?? string.Empty,
                            ThumbnailUrl = avatarItem.ImageUrl ?? string.Empty,
                            OwnerName = avatarItem.AuthorName ?? string.Empty,
                            OwnerId = avatarItem.AuthorId ?? string.Empty,
                            IsOwnedByUser = avatarItem.AuthorId == userId,
                            DatabaseAlertType = AlertTypeEnum.None,
                            AlertType = AlertTypeEnum.None,
                            PCPerformance = AvatarPerformanceEnumMapper.MapEnumToAlertDisplayItem(avatarItem.Performance?.PcRating ?? string.Empty),
                            QuestPerformance = AvatarPerformanceEnumMapper.MapEnumToAlertDisplayItem(avatarItem.Performance?.AndroidRating ?? string.Empty),
                            IOSPerformance = AvatarPerformanceEnumMapper.MapEnumToAlertDisplayItem(avatarItem.Performance?.IosRating ?? string.Empty),    
                        };

                        AvatarInfo? existingAvatar = dbContext.AvatarInfos.Find(avatarItem.Id);
                        if (existingAvatar != null)
                        {
                            model.AlertType = existingAvatar.AlertType;
                            model.DatabaseAlertType = existingAvatar.AlertType;
                            model.ExistsInDatabase = true;
                        }


                        Result<Avatar?> avatarResult = _serviceRegistry.GetVRChatAPIClient().GetAvatarById(model.AvatarId);
                        if (avatarResult != null && avatarResult.Value != null)
                        {
                            Avatar avatar = avatarResult.Value;
                            model.ThumbnailUrl = avatar.ThumbnailImageUrl ?? string.Empty;
                            model.Description = avatar.Description ?? string.Empty;
                            model.UpdatedAt = avatar.UpdatedAt;
                            model.CreatedAt = avatar.CreatedAt;
                        }
                        else
                        {
                            _Logger.Warn($"Failed to fetch avatar details for Avatar ID {avatarItem.Id}. Exception: {avatarResult?.Exception?.Message}");
                            model.Description = $"Failed to fetch avatar details. {avatarResult?.Exception?.Message}";
                        }

                        avatarViewModels.Add(model);
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, $"Error loading user avatars for {userId}");
                throw;
            }

            return avatarViewModels
                .OrderByDescending(g => g.IsOwnedByUser)
                .ThenByDescending(g => g.AlertType)
                .ThenBy(g => g.Name)
                .ToList();
        }

        public async Task<List<AvatarEntry>?> GetPrismicAvatarsByAuthor(string author)
        {
            using var client = new HttpClient();

            // GitHub API requires a User-Agent header
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(CommonConst.ApplicationName, BuildInfo.GetAssemblyVersion()));

            try
            {
                string uri = $"http://warren01.rabbitearsvideoproduction.net:5000/api/prismic/avatar?author={Uri.EscapeDataString(author)}";
                _Logger.Info($"Fetching Prismic avatars for author: {author} from {uri}");
                HttpResponseMessage response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<List<AvatarEntry>?>(jsonResponse, options);
            }
            catch (JsonException ex)
            {
                _Logger.Error($"JSON deserialization error: {ex.Message}");
                return null;
            }
        }


        public async Task<List<UserAvatarViewModel>> LoadPrismicUserAvatarAsync(string displayName, List<AvatarEntry>? avatarsLookupResponse)
        {
            if (_serviceRegistry == null)
            {
                _Logger.Warn("ServiceRegistry is not initialized.");
                return new List<UserAvatarViewModel>();
            }

            var avatarViewModels = new List<UserAvatarViewModel>();

            try
            {
                // Fetch DB data on background thread
                TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();

                // Fetch groups from API on background thread
                _Logger.Info($"Fetched {avatarsLookupResponse?.Count ?? 0} avatars for user {displayName}");

                if (avatarsLookupResponse == null || avatarsLookupResponse.Count == 0)
                {
                    _Logger.Info($"No avatars found for user {displayName}");
                    return avatarViewModels;
                }


                foreach (AvatarEntry avatarItem in avatarsLookupResponse)
                {
                    if (avatarItem.AvatarId != null)
                    {

                        UserAvatarViewModel model = new UserAvatarViewModel()
                        {
                            AvatarId = avatarItem.AvatarId ?? string.Empty,
                            Name = avatarItem.Name ?? string.Empty,
                            ThumbnailUrl = string.Empty,
                            OwnerName = avatarItem.Author ?? string.Empty,
                            // Todo Get the correct owner ID from the API response if available
                            OwnerId = avatarItem.Author ?? string.Empty,
                            IsOwnedByUser = true,
                            DatabaseAlertType = AlertTypeEnum.None,
                            AlertType = AlertTypeEnum.None,
                            PCPerformance = AvatarPerformanceEnumMapper.MapEnumToAlertDisplayItem(avatarItem.PCRating ?? string.Empty),
                            QuestPerformance = AvatarPerformanceEnumMapper.MapEnumToAlertDisplayItem(avatarItem.QuestRating ?? string.Empty),
                            IOSPerformance = AvatarPerformanceEnumMapper.MapEnumToAlertDisplayItem(avatarItem.IOSRating ?? string.Empty),
                            AvatarEntry = avatarItem
                        };

                        AvatarInfo? existingAvatar = dbContext.AvatarInfos.Find(avatarItem.AvatarId);
                        if (existingAvatar != null)
                        {
                            model.AlertType = existingAvatar.AlertType;
                            model.DatabaseAlertType = existingAvatar.AlertType;
                            model.ExistsInDatabase = true;
                        }


                        Result<Avatar?> avatarResult = _serviceRegistry.GetVRChatAPIClient().GetAvatarById(model.AvatarId);
                        if (avatarResult != null && avatarResult.Value != null)
                        {
                            Avatar avatar = avatarResult.Value;
                            model.ThumbnailUrl = avatar.ThumbnailImageUrl ?? string.Empty;
                            model.Description = avatar.Description ?? string.Empty;
                            model.UpdatedAt = avatar.UpdatedAt;
                            model.CreatedAt = avatar.CreatedAt;
                        }
                        else
                        {
                            _Logger.Warn($"Failed to fetch avatar details for Avatar ID {avatarItem.AvatarId}. Exception: {avatarResult?.Exception?.Message}");
                            model.Description = $"Failed to fetch avatar details. {avatarResult?.Exception?.Message}";
                        }

                        avatarViewModels.Add(model);
                    }
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, $"Error loading user avatars for {displayName}");
                throw;
            }

            return avatarViewModels
                .OrderByDescending(g => g.IsOwnedByUser)
                .ThenByDescending(g => g.AlertType)
                .ThenBy(g => g.Name)
                .ToList();
        }

        public UpdateAvatarInfoResult InsertUpdateAvatarInfo(UserAvatarViewModel vm)
        {
            UpdateAvatarInfoResult result = new UpdateAvatarInfoResult(false, string.Empty);

            if (_serviceRegistry == null)
            {
                result.Success = false;
                result.Message = "ServiceRegistry is not initialized.";
                _Logger.Warn(result.Message);

                return result;
            }
            try
            {
                var dbContext = _serviceRegistry.GetDBContext();

                // Check if already exists
                var existingAvatarInfo = dbContext.AvatarInfos.Find(vm.AvatarId);

                if (existingAvatarInfo != null)
                {
                    // Update existing record
                    existingAvatarInfo.AlertType = vm.AlertType;
                    existingAvatarInfo.UpdatedAt = DateTime.UtcNow;

                    dbContext.AvatarInfos.Update(existingAvatarInfo);
                    dbContext.SaveChanges();

                    result.Message = $"Updated avatar {vm.AvatarId} ({vm.Name}) with alert type {vm.AlertType}";
                }
                else
                {
                    // Create new avatar info record
                    var newAvatarInfo = new AvatarInfo()
                    {
                        AvatarId = vm.AvatarId,
                        AvatarName = vm.Name,
                        AlertType = vm.AlertType,
                        UserId = vm.OwnerId,
                        UserName = vm.OwnerName,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    dbContext.AvatarInfos.Add(newAvatarInfo);
                    dbContext.SaveChanges();
                    result.Message = $"Added Avatar {vm.AvatarId} ({vm.Name}) with alert type {vm.AlertType}";
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Error updating alert type for Avatar ID {vm.AvatarId}";
                _Logger.Error(ex, result.Message);
                return result;
            }

            result.Success = true;
            _Logger.Info(result.Message);
            return result;
        }
        #endregion

        #region Amplitude Cache Watching
        public async Task WatchAmpCache(string path)
        {
            Console.OutputEncoding = Encoding.UTF8;

            _Logger.Info($"Tailgrab Version: {BuildInfo.GetInformationalVersion()}");

            AmpWatcher.Path = Path.GetDirectoryName(path)!;
            AmpWatcher.Filter = Path.GetFileName("amplitude.cache");
            AmpWatcher.NotifyFilter = NotifyFilters.LastWrite;

            AmpWatcher.Changed += async (source, e) =>
            {
                await ProcessAmplitudeCache(e.FullPath);
            };

            AmpWatcher.EnableRaisingEvents = true;

            // Keep the application running to monitor changes
            await Task.Delay(Timeout.Infinite);
        }

        public async Task ProcessAmplitudeCache(string filePath)
        {
            bool saveAvatars = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, true);
            if (!saveAvatars)
            {
                _Logger.Info($"Ignoring changes in Amplitude cache at '{filePath}'. Due to Discover Avatar Flag...");
                return;
            }

            // Resolve path: prefer explicit, then local repo config.json, then user config path
            if (!System.IO.File.Exists(filePath))
            {
                _Logger.Warn($"AmplitudeCache file not found at '{filePath}'.");
                return;
            }

            try
            {
                string jsonString = System.IO.File.ReadAllText(filePath);

                // Parse the JSON and extract ROOT[0]/event_properties/avatarIdsEncountered
                var avatarIds = new List<string>();

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var first = root[0];
                        if (first.TryGetProperty("event_properties", out JsonElement eventProps))
                        {
                            if (eventProps.TryGetProperty("avatarIdsEncountered", out JsonElement avatarArray) && avatarArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in avatarArray.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.String)
                                    {
                                        avatarIds.Add(item.GetString()!);
                                    }
                                }

                                CacheAvatars(avatarIds);
                            }
                        }
                    }
                }

                // Do something with the extracted avatar IDs; for now log the count and a sample
                if (avatarIds.Count > 0)
                {
                    CompactDatabase();
                }

            }
            catch
            {
                //_Logger.Error(ex, $"Failed to read or parse Amplitude cache file '{filePath}'.");
                return;
            }
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

    public class UpdateAvatarInfoResult
    {
        public bool Success{ get; set; }
        public string Message { get; set; }
        public UpdateAvatarInfoResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }

    public class AvatarEntry
    {
        [JsonPropertyName("avatarId")]
        public required string AvatarId { get; set; }
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("author")]
        public required string Author { get; set; }
        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonIgnore]
        public bool Quest { get; set; }

        [JsonIgnore]
        public bool Ios { get; set; }

        [JsonPropertyName("flags")]
        public required int[] Flags { get; set; } // [Platform, Impostor, PC Rating, Quest Rating, IOS Rating, Content Warnings, Style Filter, Marketplace]

        [JsonIgnore]
        // Platform: 1 - PC, 2 - Quest, 4 - IOS
        public string Platform => Flags.Length > 0 ? GetPlatformString(Flags[0]) : "Unknown";

        [JsonIgnore]
        // Impostor: 1 - PC, 2 - Quest, 4 - IOS
        public string Impostor => Flags.Length > 1 ? GetImpostorString(Flags[1]) : "Unknown";

        [JsonIgnore]
        // PC Rating: 0 - Unknown, 1 - Excellent, 2 - Good, 3 - Medium, 4 - Poor, 5 - Very Poor
        public string PCRating => Flags.Length > 2 ? GetRatingString(Flags[2]) : "Unknown";

        [JsonIgnore]
        // Quest Rating: 0 - Unknown, 1 - Excellent, 2 - Good, 3 - Medium, 4 - Poor, 5 - Very Poor
        public string QuestRating => Flags.Length > 3 ? GetRatingString(Flags[3]) : "Unknown";

        [JsonIgnore]
        // IOS Rating: 0 - Unknown, 1 - Excellent, 2 - Good, 3 - Medium, 4 - Poor, 5 - Very Poor
        public string IOSRating => Flags.Length > 4 ? GetRatingString(Flags[4]) : "Unknown";

        [JsonIgnore]
        // Content Warnings: 1 - Sexually suggestive, 2 - Adult Language, 4 - Graphic Violence, 8 - Excessive Gore, 16 - Extreme Horror
        public string ContentWarnings => Flags.Length > 5 ? GetContentWarningsString(Flags[5]) : "None";

        [JsonIgnore]
        // Style Filter: 1 - Pop Culture, 2 - Furry, 4 - Sci-Fi, 8 - Anime, 16 - Cartoon, 32 - Objects, 64 - Human, 128 - Realistic, 256 - Animal, 512 - Fantasy, 1024 - Fashion
        public string StyleFilter => Flags.Length > 6 ? GetStyleFilterString(Flags[6]) : "None";

        [JsonIgnore]
        // Marketplace: 0 - Not in Marketplace, 1 - In Marketplace
        public string Marketplace => Flags.Length > 7 ? (Flags[7] == 1 ? "In Marketplace" : "Not in Marketplace") : "Unknown";

        private static string GetPlatformString(int value)
        {
            var platforms = new List<string>();
            if ((value & 1) != 0) platforms.Add("PC");
            if ((value & 2) != 0) platforms.Add("Quest");
            if ((value & 4) != 0) platforms.Add("IOS");
            return platforms.Count > 0 ? string.Join(", ", platforms) : "Unknown";
        }

        private static string GetImpostorString(int value)
        {
            var impostors = new List<string>();
            if ((value & 1) != 0) impostors.Add("PC");
            if ((value & 2) != 0) impostors.Add("Quest");
            if ((value & 4) != 0) impostors.Add("IOS");
            return impostors.Count > 0 ? string.Join(", ", impostors) : "Unknown";
        }

        private static string GetRatingString(int value)
        {
            return value switch
            {
                0 => "Unknown",
                1 => "Excellent",
                2 => "Good",
                3 => "Medium",
                4 => "Poor",
                5 => "Very Poor",
                _ => "Unknown"
            };
        }

        private static string GetContentWarningsString(int value)
        {
            var warnings = new List<string>();
            if ((value & 1) != 0) warnings.Add("Sexually suggestive");
            if ((value & 2) != 0) warnings.Add("Adult Language");
            if ((value & 4) != 0) warnings.Add("Graphic Violence");
            if ((value & 8) != 0) warnings.Add("Excessive Gore");
            if ((value & 16) != 0) warnings.Add("Extreme Horror");
            return warnings.Count > 0 ? string.Join(", ", warnings) : "None";
        }

        private static string GetStyleFilterString(int value)
        {
            var styles = new List<string>();
            if ((value & 1) != 0) styles.Add("Pop Culture");
            if ((value & 2) != 0) styles.Add("Furry");
            if ((value & 4) != 0) styles.Add("Sci-Fi");
            if ((value & 8) != 0) styles.Add("Anime");
            if ((value & 16) != 0) styles.Add("Cartoon");
            if ((value & 32) != 0) styles.Add("Objects");
            if ((value & 64) != 0) styles.Add("Human");
            if ((value & 128) != 0) styles.Add("Realistic");
            if ((value & 256) != 0) styles.Add("Animal");
            if ((value & 512) != 0) styles.Add("Fantasy");
            if ((value & 1024) != 0) styles.Add("Fashion");
            return styles.Count > 0 ? string.Join(", ", styles) : "None";
        }

        public override string ToString()
        {
            string avKey = "avtr:" + Checksum.CreateMD5(Name + ":" + Author);

            return $"AvatarId: {AvatarId}, Name: {Name}, Author: {Author}, Description: {Description}, Quest: {Quest}, IOS: {Ios}, Platform: {Platform}, Impostor: {Impostor}, PC Rating: {PCRating}, Quest Rating: {QuestRating}, IOS Rating: {IOSRating}, Content Warnings: {ContentWarnings}, Style Filter: {StyleFilter}, Marketplace: {Marketplace}, avKey: {avKey}";
        }
    }

    #endregion
}
