using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using NLog;
using Tailgrab.Clients.Ollama;
using Tailgrab.Common;
using Tailgrab.Models;
using VRChat.API.Model;

namespace Tailgrab.AvatarManagement
{
    public class AvatarManagementService
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        private ServiceRegistry _serviceRegistry;

        private ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new ConcurrentPriorityQueue<IHavePriority<int>, int>();
        private Dictionary<String, DateTime> recentlyProcessedAvatars = new Dictionary<string, DateTime>();

        public int GetQueueCount()
        {
            return priorityQueue.Count;
        }

        public AvatarManagementService(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;

            _ = Task.Run(() => AvatarCheckTask(priorityQueue, _serviceRegistry));

        }

        public void AddAvatar(AvatarInfo avatar)
        {
            try
            {                
                _serviceRegistry.GetDBContext().AvatarInfos.Add(avatar);
                _serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Error($"Error creating avatar: {ex.Message}");
            }
        }

        public AvatarInfo? GetAvatarById(string avatarId)
        {
            return _serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
        }

        public void UpdateAvatar(AvatarInfo avatar)
        {
            try
            {
                avatar.UpdatedAt = DateTime.UtcNow;
                _serviceRegistry.GetDBContext().AvatarInfos.Update(avatar);
                _serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Error($"Error updating avatar: {ex.Message}");
            }
        }

        public void DeleteAvatar(string avatarId)
        {
            var avatar = _serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
            if (avatar != null)
            {
                _serviceRegistry.GetDBContext().AvatarInfos.Remove(avatar);
                _serviceRegistry.GetDBContext().SaveChanges();
            }
        }

        public void CacheAvatars(List<string> avatarIdInCache)
        {
            TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
            foreach (var avatarId in avatarIdInCache)
            {
                EnqueueAvatarForCheck(avatarId);
            }
        }

        private void EnqueueAvatarForCheck(string avatarId)
        {
            if (recentlyProcessedAvatars.TryGetValue(avatarId, out DateTime dateTime))
            {
                if ((DateTime.UtcNow - dateTime).TotalMinutes < 60)
                {
                    logger.Debug($"Skipping adding avatar {avatarId} as it was recently processed.");
                    return;
                }
            }
            recentlyProcessedAvatars.Add(avatarId, DateTime.UtcNow);

            var queuedItem = new QueuedAvatarProcess
            {
                AvatarId = avatarId,
                Priority = 1
            };
        
            priorityQueue.Enqueue(queuedItem);
        }

        public void GetAvatarsFromUser(string userId, string avatarName)
        {

            logger.Debug($"Fetching avatars for user {userId} to find avatar named {avatarName}");

            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                List<Avatar> avatarData = _serviceRegistry.GetVRChatAPIClient().GetAvatarsByUserId(userId);
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
                                IsBos = false,
                                AlertType = AlertTypeEnum.None
                            };

                            AddAvatar(avatarInfo);
                        }
                        else
                        {
                            dbAvatarInfo.UserId = avatar.AuthorId;
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
            _serviceRegistry.GetDBContext().Database.ExecuteSqlRaw("VACUUM;");
        }

        internal bool CheckAvatarByName(string avatarName)
        {
            var bannedAvatars = _serviceRegistry.GetDBContext().AvatarInfos
                                         .Where(b => b.AvatarName != null && b.AvatarName.Equals(avatarName) && b.AlertType > 0)
                                         .OrderByDescending(b => b.AlertType)
                                         .ToList();

            if (bannedAvatars.Count > 0)
            {
                // Play alert sound based on the highest alert type found for the avatar
                AlertTypeEnum maxAlertType = bannedAvatars[0].AlertType;

                SoundManager.PlayAlertSound(CommonConst.Avatar_Alert_Key, maxAlertType);

                return true;
            }

            return false;
        }

        public static async Task AvatarCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            OllamaClient.logger.Info($"Amplitude Avatar Cache Queue Running");
            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess && result.Value is QueuedAvatarProcess item && item.AvatarId != null)
                    {
                        try
                        {
                            AvatarInfo? dbAvatarInfo = dBContext.AvatarInfos.Find(item.AvatarId);
                            bool updateNeeded = false;
                            if (dbAvatarInfo == null)
                            {
                                updateNeeded = true;
                            }
                            else if (!dbAvatarInfo.IsBos &&
                                (!dbAvatarInfo.UpdatedAt.HasValue || dbAvatarInfo.UpdatedAt.Value >= DateTime.UtcNow.AddHours(-24)))
                            {
                                updateNeeded = true;
                            }

                            if (updateNeeded)
                            {
                                Avatar? avatarData = FetchUpdateAvatarData(serviceRegistry, dBContext, item.AvatarId, dbAvatarInfo);

                                if (avatarData == null && dbAvatarInfo == null)
                                {
                                    CreateAvatarInfoForPrivate(dBContext, item.AvatarId);
                                }

                                // Wait for a short period before checking the queue again
                                await Task.Delay(1000);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Error fetching user profile for userId: {item.AvatarId}");
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

        private static void CreateAvatarInfoForPrivate(TailgrabDBContext dBContext, string AvatarId)
        {
            var avatarInfo = new AvatarInfo
            {
                AvatarId = AvatarId,
                UserId = "",
                AvatarName = $"Unknown Avatar {AvatarId}",
                ImageUrl = "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsBos = false
            };

            try
            {
                dBContext.Add(avatarInfo);
                dBContext.SaveChanges();
                logger.Debug($"Adding fallback avatar record for {avatarInfo.ToString()}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding fallback avatar record for {AvatarId}: {ex.Message}");
            }
        }

        public static Avatar? FetchUpdateAvatarData(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, string AvatarId, AvatarInfo? dbAvatarInfo)
        {
            Avatar? avatarData = null;
            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                avatarData = serviceRegistry.GetVRChatAPIClient().GetAvatarById(AvatarId);
                if (avatarData != null)
                {
                    if (dbAvatarInfo == null)
                    {
                        var avatarInfo = new AvatarInfo
                        {
                            AvatarId = avatarData.Id,
                            UserId = avatarData.AuthorId,
                            AvatarName = avatarData.Name,
                            ImageUrl = avatarData.ImageUrl,
                            CreatedAt = avatarData.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            IsBos = false
                        };

                        try
                        {
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

                        dbAvatarInfo.UserId = avatarData.AuthorId;
                        dbAvatarInfo.AvatarName = avatarData.Name;
                        dbAvatarInfo.ImageUrl = avatarData.ImageUrl;
                        dbAvatarInfo.CreatedAt = avatarData.CreatedAt;
                        dbAvatarInfo.UpdatedAt = DateTime.UtcNow;

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
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return avatarData;
        }
    }

    internal class QueuedAvatarProcess : IHavePriority<int>
    {
        public int Priority { get; set; }

        public string? AvatarId { get; set; }
    }


}
