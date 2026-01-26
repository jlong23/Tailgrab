using Microsoft.EntityFrameworkCore;
using NLog;
using System.Media;
using Tailgrab;
using Tailgrab.Models;
using Tailgrab.Clients.VRChat;
using VRChat.API.Model;

namespace Tailgrab.AvatarManagement
{
    public class AvatarManagementService
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        private ServiceRegistry _serviceRegistry;

        private static List<string> avatarsInSession = new List<string>();

        public AvatarManagementService(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;            
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

            int postion = 0;
            foreach (var avatarId in avatarIdInCache)
            { 
                AvatarInfo? dbAvatarInfo = dbContext.AvatarInfos.Find(avatarId);
                bool updateNeeded = false;
                if (dbAvatarInfo == null)
                {
                    updateNeeded = true;
                }
                else if (!dbAvatarInfo.IsBos &&
                    (!dbAvatarInfo.UpdatedAt.HasValue || dbAvatarInfo.UpdatedAt.Value >= DateTime.UtcNow.AddHours(-12)))
                {
                    updateNeeded = true;
                }

                if (updateNeeded)
                {
                    Avatar? avatarData = null;
                    try
                    {
                        // Avatar already exists in the database and was updated within the last 12 hours
                        System.Threading.Thread.Sleep(500);
                        avatarData = _serviceRegistry.GetVRChatAPIClient().GetAvatarById(avatarId);
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
                                    dbContext.Add(avatarInfo);
                                    dbContext.SaveChanges();
                                }
                                catch (Exception ex)
                                {
                                    logger.Error($"Error adding avatar record for {avatarId}: {ex.Message}");
                                }
                            }
                            else
                            {
                                // Ensure entity is attached to the dbContext before updating to avoid Detached state errors
                                var entry = dbContext.Entry(dbAvatarInfo);
                                if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                                {
                                dbContext.Attach(dbAvatarInfo);
                                    entry = _serviceRegistry.GetDBContext().Entry(dbAvatarInfo);
                                }

                                dbAvatarInfo.UserId = avatarData.AuthorId;
                                dbAvatarInfo.AvatarName = avatarData.Name;
                                dbAvatarInfo.ImageUrl = avatarData.ImageUrl;
                                dbAvatarInfo.CreatedAt = avatarData.CreatedAt;
                                dbAvatarInfo.UpdatedAt = DateTime.UtcNow;

                                try
                                {
                                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    dbContext.SaveChanges();
                                }
                                catch (Exception ex)
                                {
                                    logger.Error($"Error updating avatar record for {avatarId}: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error fetching avatar: {ex.Message}");
                    }
                    
                    if (avatarData == null && dbAvatarInfo == null )
                    {
                        var avatarInfo = new AvatarInfo
                        {
                            AvatarId = avatarId,
                            UserId = "",
                            AvatarName = $"Unknown Avatar {avatarId}",
                            ImageUrl = "",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsBos = false
                        };

                        try
                        {
                            dbContext.Add(avatarInfo);
                            dbContext.SaveChanges();
                            logger.Debug($"Adding fallback avatar record for {avatarInfo.ToString()}");
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error adding fallback avatar record for {avatarId}: {ex.Message}");
                        }
                    }
                }

                postion++;
            }

            avatarsInSession.Clear();
        }

        public void GetAvatarsFromUser( string userId, string avatarName )
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
                                IsBos = false
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
                                         .Where(b => b.AvatarName != null && b.AvatarName.Equals(avatarName) && b.IsBos)
                                         .OrderBy(b => b.CreatedAt)
                                         .ToList();

            if (bannedAvatars.Count > 0)
            {
                SystemSounds.Hand.Play();
                return true;
            }

            return false;
        }

        internal void AddAvatarsInSession(string avatarName)
        {
            if (!avatarsInSession.Contains(avatarName))
            {
                 avatarsInSession.Add(avatarName);
            }
        }
    }
}
