using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;  
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.XSOverlay;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.Models;
using VRChat.API.Client;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{
    public class GroupManager
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly HttpClient _httpClient;
        private static ServiceRegistry? serviceRegistry;
        private ConcurrentPriorityQueue<IHavePriority<int>, int> groupPriorityQueue = new();
        private int gistRecordCount = 0;
        private int gistProcessedCount = 0;

        [SetsRequiredMembers]
        public GroupManager(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
            _httpClient = new HttpClient();
            _ = Task.Run(() => GroupCheckTask(groupPriorityQueue, serviceRegistry));
        }

        #region User Group Membership Evaluation
        public void CheckUserGroups(string userId)
        {
            logger.Debug($"Checking user profile with AI : {userId}");

            try
            {
                QueuedProcess process = new()
                {
                    UserId = userId,
                    Priority = 10
                };

                if (!IsUserProfileInQueue(process.UserId))
                {
                    UpdateQueuedProcessWithPlayer(process);
                    groupPriorityQueue.Enqueue(process);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user groups for userId: {userId}");

            }
        }

        private bool IsUserProfileInQueue(string userId)
        {
            return groupPriorityQueue.Any(item => ((QueuedProcess)item).UserId == userId);
        }

        public void ClearQueue()
        {
            while (true)
            {
                if (groupPriorityQueue.Count == 0)
                {
                    break;
                }
                groupPriorityQueue.Dequeue();
            }
        }


        private void UpdateQueuedProcessWithPlayer(QueuedProcess item)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return;
            }

            User profile = serviceRegistry.GetVRChatAPIClient().GetProfile(item.UserId);
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

        public static string FormatProfileText(User profile)
        {
            return $"DisplayName: {profile.DisplayName}\n" +
                   $"StatusDesc: {profile.StatusDescription}\n" +
                   $"Pronouns: {profile.Pronouns}\n" +
                   $"UserTrust : {TrustClassEnumMapper.MapTagsToString(profile.Tags, profile.AgeVerified, profile.AgeVerificationStatus.ToString())}\n" +
                   $"UserAgeVerified: {profile.AgeVerified}\n" +
                   $"ProfileBio: {profile.Bio}\n";
        }


        public static async Task GroupCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            logger.Info($"Group Queue Running");
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
                            await EvaluateItemGroups(priorityQueue, serviceRegistry, item);
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

        private static async Task<bool> EvaluateItemGroups(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry, QueuedProcess item)
        {
            try
            {
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                List<LimitedUserGroups> userGroups = serviceRegistry.GetVRChatAPIClient().GetProfileGroups(item.UserId);

                User profile = serviceRegistry.GetVRChatAPIClient().GetProfile(item.UserId);
                await GetUserGroupInformation(serviceRegistry, dBContext, userGroups, item);
                await GetUserModerations(item);
                UpdatePlayerWithEvaluation(item);
                PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, profile.DisplayName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user profile for userId: {item.UserId}");
            }

            return true;
        }

        private async static Task<bool> GetUserGroupInformation(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, List<LimitedUserGroups> userGroups, QueuedProcess item)
        {
            bool saveGroups = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Group_Caching, true);
            logger.Debug($"Processing User Group subscription for userId: {item.UserId}");
            Player? player = PlayerManager.GetPlayerByUserId(item.UserId ?? string.Empty);
            if (player != null)
            {
                AlertTypeEnum maxAlertType = AlertTypeEnum.None;
                string groupNames = string.Empty;
                foreach (LimitedUserGroups group in userGroups)
                {
                    GroupInfo? groupInfo = dBContext.GroupInfos.Find(group.GroupId);
                    if (groupInfo == null)
                    {
                        groupInfo = SaveGroupInfo(dBContext, saveGroups, group);
                    }
                    else
                    {
                        groupNames += UpdateGroupInfo(dBContext, item, ref player, ref maxAlertType, group, groupInfo);
                    }
                }

                if (player != null && player.IsWatched)
                {
                    OverlayManager overlay = serviceRegistry.GetXSOverlay();
                    await overlay.SendNotification(maxAlertType, $"Player \b1{player.DisplayName}\b0 has questionable group memberships:\r\n{groupNames}");

                    SoundManager.PlayAlertSound(CommonConst.Group_Alert_Key, maxAlertType);
                    return true;
                }
            }
            return false;
        }

        private async static Task<bool> GetUserModerations(QueuedProcess item)
        {
            bool userModerations = false;
            logger.Debug($"Processing User Group subscription for userId: {item.UserId}");
            Player? player = PlayerManager.GetPlayerByUserId(item.UserId ?? string.Empty);

            if (player != null)
            {
                List<ModerationInfo> moderationReports = await PlayerManager.GetModerationReportsByUserId(item.UserId ?? string.Empty);
                if (moderationReports.Count != 0)
                {
                    userModerations = true;
                    foreach (ModerationInfo report in moderationReports)
                    {
                        player = PlayerManager.AddPlayerEventByUserId(item.UserId ?? string.Empty, PlayerEvent.EventType.AvatarWatch, $"Had Past Moderations : {report.Id} - {report.ContentType} for \"{report.ContentName}\"");
                    }
                    player?.AddAlertMessage(AlertClassEnum.Moderation, AlertTypeEnum.Nuisance, "Past Moderations");
                }

            }

            return userModerations;
        }


        private static string UpdateGroupInfo(TailgrabDBContext dBContext, QueuedProcess item, ref Player? player, ref AlertTypeEnum maxAlertType, LimitedUserGroups group, GroupInfo groupInfo)
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
                return groupInfo.GroupName + "\r\n";
            }

            return string.Empty;
        }

        private static GroupInfo SaveGroupInfo(TailgrabDBContext dBContext, bool saveGroups, LimitedUserGroups group)
        {
            GroupInfo groupInfo = new()
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

        private static void UpdatePlayerWithEvaluation(QueuedProcess item)
        {
            Player? player = PlayerManager.GetPlayerByUserId(item.UserId ?? string.Empty);
            if (player != null)
            {
                player.UserBio = item.UserBio;
                player.IsFriend = item.IsFriend;
                player.ProfileImage = item.ProfileUrl ?? player.ProfileImage;
                player.UserTrustClass = item.UserTrustClass;
                player.AgeVerified = item.AgeVerification;

                PlayerManager.OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }
        #endregion

        #region User Group Membership Overlay
        public async Task<List<UserGroupViewModel>> LoadUserGroupsAsync(string userId)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return new List<UserGroupViewModel>();
            }

            var groupViewModels = new List<UserGroupViewModel>();

            try
            {
                Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();

                // Fetch groups from API on background thread
                List<LimitedUserGroups> usersLimitedGroups = await Task.Run(() => vrcClient.GetProfileGroups(userId));
                logger.Info($"Fetched {usersLimitedGroups?.Count ?? 0} groups for user {userId}");

                if (usersLimitedGroups == null || usersLimitedGroups.Count == 0)
                {
                    logger.Info($"No groups found for user {userId}");
                    return groupViewModels;
                }


                // Fetch DB data on background thread
                List<GroupInfoDTO> dbGroupDataList = await FindMatchingWatchGroupInfo(usersLimitedGroups);

                // Create view models on UI thread (required for WPF Brush creation in UpdateAlertColors)
                foreach (var group in usersLimitedGroups)
                {
                    try
                    {
                        UserGroupViewModel? item = await BuildUserGroupViewItem(userId, group, dbGroupDataList);
                        if (item != null)
                        {
                            groupViewModels.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error processing group {group.Id} for user {userId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error loading user groups for {userId}");
                throw;
            }

            return [.. groupViewModels
                .OrderByDescending(g => g.IsOwnedByUser)
                .ThenByDescending(g => g.AlertType)
                .ThenBy(g => g.Name)];
        }

        private async Task<List<GroupInfoDTO>> FindMatchingWatchGroupInfo(List<LimitedUserGroups> groupList)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return new List<GroupInfoDTO>();
            }

            List<GroupInfoDTO> matchingGroups = new List<GroupInfoDTO>();
            // Fetch DB data on background thread
            TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
            foreach (var group in groupList)
            {
                GroupInfo? existingGroup = dbContext.GroupInfos.Find(group.GroupId);
                GroupInfoDTO groupInfoDTO = new(group.GroupId ?? string.Empty,
                    existingGroup?.AlertType ?? AlertTypeEnum.None,
                    existingGroup != null
                );
                matchingGroups.Add(groupInfoDTO);
            }
            return matchingGroups;
        }

        private async Task<UserGroupViewModel?> BuildUserGroupViewItem(string ownerId, LimitedUserGroups group, List<GroupInfoDTO> dbGroup)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
            Result<Group?> fullGroupResult = await Task.Run(() => vrcClient.GetGroupById(group.GroupId));
            Group? fullGroup = fullGroupResult.Value;

            UserGroupViewModel item = new()
            {
                GroupId = group.GroupId ?? string.Empty,
                Name = group.Name ?? string.Empty,
                BannerUrl = group.BannerUrl ?? "https://assets.vrchat.com/www/groups/default_banner.png",
                IconUrl = group.IconUrl ?? "https://assets.vrchat.com/www/groups/default_banner.png",
                ShortCode = $"{group.ShortCode}.{group.Discriminator}",
                Description = fullGroup?.Description ?? string.Empty,
                Rules = fullGroup?.Rules ?? string.Empty,
                JoinState = fullGroup?.JoinState.ToString() ?? "N/A",
                MemberCount = fullGroup?.MemberCount ?? 0,
                OwnerId = fullGroup?.OwnerId ?? string.Empty,
                IsOwnedByUser = fullGroup?.OwnerId == ownerId
            };

            // Apply DB data
            GroupInfoDTO? watchedItem = dbGroup.FirstOrDefault(d => d.GroupId == item.GroupId);
            item.ExistsInDatabase = watchedItem?.Exists ?? false;
            item.AlertType = watchedItem?.AlertType ?? AlertTypeEnum.None;
            item.DatabaseAlertType = watchedItem?.AlertType ?? AlertTypeEnum.None;

            item.UpdateAlertColors();

            return item;
        }
        #endregion

        #region Group GIST Moderation 
        public string GetQueueSize()
        {
            if (gistRecordCount > 0)
            {
                string gistProcessing = Utility.I18NString("UI.StatusBar.GroupGISTProcessing");
                string gistProcessingOf = Utility.I18NString("UI.StatusBar.GroupGISTProcessingOf");
                int percentComplete = (int)((double)gistProcessedCount / gistRecordCount * 100);
                return $"{gistProcessing} {gistProcessedCount} {gistProcessingOf} {gistRecordCount} ({percentComplete}%)";
            }

            return string.Empty;
        }

        /// <summary>
        /// Downloads a GIST content file, verifies its checksum against registry, 
        /// and processes AvatarIds if the file is new or changed.
        /// </summary>
        /// <param name="gistUrl">The URL of the GIST raw content to download</param>
        /// <returns>True if processing was successful, false otherwise</returns>
        public async Task<bool> ProcessGroupGistList(string? tempUrl, bool ignoreChecksum)
        {
            string? gistUrl = string.Empty;
            if (!string.IsNullOrWhiteSpace(tempUrl))
            {
                gistUrl = tempUrl;
            }
            else
            {
                gistUrl = GetStoredUri();
            }

            if (string.IsNullOrWhiteSpace(gistUrl))
            {
                logger.Error("Group GIST URL passed was empty, cannot update.");
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

                logger.Info($"Downloaded GIST content length (bytes): {gistContent.Length}");

                // Calculate MD5 checksum of the downloaded content
                string currentChecksum = CalculateMD5Checksum(gistContent);
                logger.Info($"Downloaded GIST content calculated checksum: {currentChecksum}");

                if (ignoreChecksum == false)
                {
                    // Get the stored checksum from registry
                    string? storedChecksum = GetStoredChecksum();

                    // Compare checksums
                    if (storedChecksum != null && storedChecksum.Equals(currentChecksum, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Info("GIST content has not changed (checksum match). Skipping processing.");
                        return true;
                    }

                    logger.Info("GIST content is new or has changed. Processing Group IDs...");
                }

                // Process the file line by line
                int processedCount = await ProcessGroupListData(gistContent);  //await ProcessGroupIdsAsync(gistContent);
                logger.Info($"Processed {processedCount} Group IDs from GIST.");

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

                    string? value = key.GetValue(Common.CommonConst.Registry_Group_Checksum) as string;
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

                    string? value = key.GetValue(Common.CommonConst.Registry_Group_Gist) as string;
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
                    key.SetValue(Common.CommonConst.Registry_Group_Checksum, checksum, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save checksum to registry.");
            }
        }

        private async Task<int> ProcessGroupListData(string gistContent)
        {
            List<GroupImportItem> importList = new List<GroupImportItem>();
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

                    GroupImportItem? item = ProcessGroupLineItem(line, lineNumber);
                    if (item != null)
                    {
                        importList.Add(item);
                    }
                }

                logger.Info($"Total valid GroupImportItems parsed: {importList.Count}");
            }

            return await ProcessGroupListData(importList);
        }

        private GroupImportItem? ProcessGroupLineItem(string line, int lineNumber)
        {
            // Split by whitespace or comma to get the first column
            string pattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
            string[] columns = System.Text.RegularExpressions.Regex.Split(line, pattern);
            if (columns.Length < 3)
            {
                logger.Warn($"Line {lineNumber}: Expected at least 3 columns (GroupId, GroupName, AlertType), but got {columns.Length}. Skipping line.");
                logger.Warn(line);
                return null;
            }
            string groupId = columns[0].Trim().Trim('"');
            string groupName = columns[1].Trim().Trim('"');
            string groupAlert = columns[2].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(groupId))
            {
                logger.Warn($"Line {lineNumber}: Empty Group ID, skipping.");
                logger.Warn(line);
                return null;
            }

            // Convert the alert type string to the AlertTypeEnum, defaulting to None if parsing fails
            AlertTypeEnum alertType = AlertTypeEnum.None;
            if (!Enum.TryParse<AlertTypeEnum>(groupAlert, out alertType))
            {
                logger.Warn($"Line {lineNumber}: Invalid AlertType '{groupAlert}' for Group ID '{groupId}', defaulting to None.");
            }

            return new GroupImportItem(lineNumber, groupId, groupName, alertType);
        }

        private async Task<int> ProcessGroupListData(List<GroupImportItem> importList)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return 0;
            }

            gistRecordCount = importList.Count();
            gistProcessedCount = 0;
            int processedCount = 0;
            foreach (GroupImportItem item in importList)
            {
                logger.Debug($"Line {item.LineNumber}: Processing {item.ToString()}");
                try
                {
                    // Fetch/Refresh the GroupInfo from VRC 
                    GroupInfo? groupInfo = await AddUpdateGroupFromVRC(item.GroupId);
                    if (groupInfo == null)
                    {
                        logger.Debug($"Line {item.LineNumber}: Group ID '{item.GroupId}' not found, skipping.");
                        continue;
                    }

                    // Update alert types only if the current AlertType is lower than the new one (i.e., None < Watch < Nuisance < Crasher)
                    if (groupInfo.AlertType < item.AlertType)
                    {
                        groupInfo.AlertType = item.AlertType;
                        groupInfo.UpdatedAt = DateTime.UtcNow;
                        serviceRegistry.GetDBContext().GroupInfos.Update(groupInfo);
                        processedCount++;
                        logger.Debug($"Line {item.LineNumber}: Set AlertType for Group ID '{item.GroupId}' to '{item.AlertType}'");
                    }
                    else
                    {
                        logger.Debug($"Line {item.LineNumber}: Group ID '{item.GroupId}' already has AlertType, skipping.");
                    }

                    gistProcessedCount++;
                    if (item.LineNumber % 50 == 0)
                    {
                        logger.Info($"GIST Group Processed {item.LineNumber} of {importList.Count()} records.");
                        await Task.Delay(10000); // Throttle processing to avoid overwhelming the API
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Line {item.LineNumber}: Error processing Group ID '{item.GroupId}'");
                }
            }

            logger.Info($"GIST Group Updated/Added {processedCount} records");
            gistRecordCount = 0;
            gistProcessedCount = 0;

            return processedCount;
        }

        public async Task<GroupInfo?> AddUpdateGroupFromVRC(string? groupId)
        {
            if (serviceRegistry == null)
            {
                logger.Warn("ServiceRegistry is not initialized.");
                return null;
            }

            if (string.IsNullOrEmpty(groupId))
                return null;

            TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
            GroupInfo? existing = dbContext.GroupInfos.Find(groupId);

            try
            {
                bool shouldUpdate = false;
                // Only update if the existing record is older than 12 hours
                if (existing != null)
                {
                    if (existing.UpdatedAt < DateTime.UtcNow.AddHours(-12))
                        shouldUpdate = true;
                }
                else
                {
                    shouldUpdate = true;
                }


                if (shouldUpdate)
                {
                    // Throttle processing to avoid overwhelming the API
                    await Task.Delay(1000);

                    Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                    Result<Group?> groupResult = vrcClient.GetGroupById(groupId);
                    Group? group = groupResult.Value;

                    if (groupResult.HasException)
                    {
                        if (groupResult.Exception is ApiException apiException)
                        {
                            if (apiException.ErrorCode == 404)
                            {
                                logger.Warn($"Group '{groupId}' not found in VRChat API.");
                                if (existing != null)
                                {
                                    existing.AlertType = AlertTypeEnum.None;
                                    dbContext.GroupInfos.Update(existing);
                                    dbContext.SaveChanges();
                                    logger.Info($"Removed Group '{groupId}' from local database as it no longer exists in VRChat API.");
                                }
                                return null;
                            }
                            else
                            {
                                logger.Warn($"Failed to fetch Group '{groupId}': {apiException.Message}");
                                return null;
                            }
                        }
                    }

                    if (existing == null)
                    {

                        if (group == null)
                        {
                            logger.Warn($"Group '{groupId}' not found in VRChat API.");
                            return null;
                        }

                        GroupInfo newEntity = new()
                        {
                            GroupId = group.Id,
                            GroupName = group.Name ?? string.Empty,
                            CreatedAt = group.CreatedAt,
                            UpdatedAt = DateTime.UtcNow
                        };

                        dbContext.GroupInfos.Add(newEntity);
                        dbContext.SaveChanges();
                        return newEntity;
                    }
                    else
                    {
                        if (group == null)
                        {
                            logger.Warn($"Group '{groupId}' not found in VRChat API.");
                            return null;
                        }

                        existing.GroupId = group.Id;
                        existing.GroupName = group.Name ?? string.Empty;
                        existing.CreatedAt = group.CreatedAt;
                        existing.UpdatedAt = DateTime.UtcNow;
                        dbContext.GroupInfos.Update(existing);
                        dbContext.SaveChanges();
                        return existing;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to fetch Group '{groupId}': {ex.Message}");
            }

            return existing;
        }
        #endregion
    }


    #region Support DTO Classes
    public class GroupImportItem
    {
        public int LineNumber { get; set; }
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public AlertTypeEnum AlertType { get; set; }
        public GroupImportItem(int lineNumber, string groupId, string groupName, AlertTypeEnum alertType)
        {
            LineNumber = lineNumber;
            GroupId = groupId;
            GroupName = groupName;
            AlertType = alertType;
        }

        public override string ToString()
        {
            return $"Line {LineNumber}: GroupId={GroupId}, GroupName={GroupName}, AlertType={AlertType}";
        }
    }

    public class GroupInfoDTO(string groupId, AlertTypeEnum alertType, bool exists)
    {
        public string GroupId { get; set; } = groupId;
        public AlertTypeEnum AlertType { get; set; } = alertType;
        public bool Exists { get; set; } = exists;
    }
    #endregion
}
