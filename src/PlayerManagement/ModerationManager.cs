using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Tailgrab.Clients.VRChat;
using Tailgrab.Models;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;
using Microsoft.EntityFrameworkCore; // Add this


namespace Tailgrab.PlayerManagement
{
    public class ModerationManager
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static ServiceRegistry? serviceRegistry;

        [SetsRequiredMembers]
        public ModerationManager(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
        }

        // Reusable method to submit profile report - can be called from other places in the future if needed
        public async Task<bool> SubmitProfileReport(string userId, string category, string reportReason, string reportDescription)
        {
            if( serviceRegistry == null)    
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            bool success = true;
            ModerationReportPayload rpt = new()
            {
                Type = "user",
                Category = "profile",
                Reason = reportReason,
                ContentId = userId,
                Description = reportDescription
            };

            ModerationReportDetails rptDtls = new()
            {
                InstanceType = "Group Public",
                InstanceAgeGated = false
            };
            rpt.Details = [rptDtls];

            ModerationReportResponse? response = await serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (response != null)
            {
                logger.Info($"Profile Report submitted - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                await SaveModerationReport(rpt, response, userId, false);
            }
            else
            {
                logger.Warn($"Failed to submit profile report - UserId: {userId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                success = false;
            }
            return success;
        }

        // Reusable method to submit avatar report - can be called from other places in the future if needed
        public async Task<bool> SubmitAvatarReport(string avatarId, string category, string reportReason, string reportDescription)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            bool success = true;
            ModerationReportPayload rpt = new()
            {
                Type = "avatar",
                Category = "avatar",
                Reason = reportReason,
                ContentId = avatarId,
                Description = reportDescription
            };

            ModerationReportDetails rptDtls = new()
            {
                InstanceType = "Group Public",
                InstanceAgeGated = false
            };
            rpt.Details = [rptDtls];

            ModerationReportResponse? response = await serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (response != null)
            {
                logger.Info($"Avatar Report submitted - AvatarId: {avatarId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                await SaveModerationReport(rpt, response, string.Empty, false);
            }
            else
            {
                logger.Warn($"Failed to submit avatar report - AvatarId: {avatarId}, Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
                success = false;
            }
            return success;
        }

        public async void DeleteModerationReport(string reportId)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            try
            {
                VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                await vrcClient.DeleteModerationReportAsync(reportId);

                // Update local database to mark as deleted
                var db = serviceRegistry.GetDBContext();
                var entity = db.ModerationInfos.Find(reportId);
                if (entity != null)
                {
                    entity.IsDeleted = true;
                    entity.DeletedDate = DateTime.UtcNow;
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to delete moderation report {reportId}");
            }
        }


        #region Moderation Report Management
        public async Task GetModerationReports(bool isClosed)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            try
            {
                int offset = 0;
                while (true)
                {
                    ModerationReportListResponse? reports = await serviceRegistry.GetVRChatAPIClient().ListModerationReportAsync(offset, isClosed);
                    if (reports == null)
                        break;

                    foreach (var report in reports.Results)
                    {
                        logger.Debug($"Report ID: {report.Id}, Type: {report.Type}, ContentId: {report.ContentId}, ContentName: {report.ContentName}");
                        await SaveModerationReport(report, isClosed);
                    }

                    offset += 60;
                    if (reports.HasNext == false)
                    {
                        logger.Info("No more moderation reports to process.");
                        break;
                    }
                    await Task.Delay(1000); // Delay for 1 second before the next request
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to fetch moderation reports");
            }
        }

        public async Task SaveModerationReport(ModerationReportPayload rpt, ModerationReportResponse response, string UserId, bool isClosed)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            try
            {
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();


                ModerationInfo info = new()
                {
                    // We should get the ModerationID from the response, but for now we will generate a new GUID
                    Id = response.Id ?? Guid.NewGuid().ToString(),
                    EventDateTime = DateTime.Now,
                    ContentId = response.ContentId,
                    ContentName = response.ContentName ?? string.Empty,
                    ContentType = response.Type ?? string.Empty,
                    Thumbnail = response.ContentThumbnailImageUrl ?? string.Empty,
                    Report = System.Text.Encoding.UTF8.GetBytes(response.Description ?? string.Empty),
                    UserId = UserId,
                    IsClosed = isClosed,
                    ClosedDate = isClosed ? DateTime.Now : null,
                    IsDeleted = false,
                    DeletedDate = null
                };

                // Save the moderation info to the database
                dBContext.ModerationInfos.Add(info);
                await dBContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save moderation report");
                System.Windows.MessageBox.Show($"Failed to save moderation report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SaveModerationReport(ModerationReportResponse response, bool isClosed)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            try
            {
                TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                ModerationInfo? info = await dbContext.ModerationInfos.FindAsync(response.Id);
                if (info != null)
                {
                    if (isClosed && info.ClosedDate == null)
                    {
                        info.IsClosed = isClosed;
                        info.ClosedDate = isClosed ? DateTime.Now : null;
                        dbContext.ModerationInfos.Update(info);
                        await dbContext.SaveChangesAsync();
                    }
                }
                else
                {
                    info = new ModerationInfo
                    {
                        // We should get the ModerationID from the response, but for now we will generate a new GUID
                        Id = response.Id ?? Guid.NewGuid().ToString(),
                        EventDateTime = DateTime.Now,
                        ContentId = response.ContentId,
                        ContentName = response.ContentName ?? string.Empty,
                        ContentType = response.Type ?? string.Empty,
                        Thumbnail = response.ContentThumbnailImageUrl ?? string.Empty,
                        Report = System.Text.Encoding.UTF8.GetBytes(response.Description ?? string.Empty),
                        UserId = convertModerationsReportTypeToUserId(response),
                        IsClosed = isClosed,
                        ClosedDate = isClosed ? DateTime.Now : null,
                        IsDeleted = false,
                        DeletedDate = null
                    };

                    // Save the moderation info to the database
                    dbContext.ModerationInfos.Add(info);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save moderation report");
                System.Windows.MessageBox.Show($"Failed to save moderation report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal string convertModerationsReportTypeToUserId(ModerationReportResponse response)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            string userId = string.Empty;
            try
            {
                Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                switch (response.Type)
                {
                    case "avatar":
                        Result<Avatar?> result = vrcClient.GetAvatarById(response.ContentId);
                        if (result.Value != null)
                        {
                            userId = result.Value.AuthorId;
                        }
                        break;

                    case "world":
                        World? world = vrcClient.GetWorldById(response.ContentId);
                        if (world != null)
                        {
                            userId = world.AuthorId;
                        }
                        break;

                    case "group":
                        Result<Group?> groupResult = vrcClient.GetGroupById(response.ContentId);
                        Group? group = groupResult.Value;
                        if (group != null)
                        {
                            userId = group.OwnerId;

                        }
                        break;

                    case "user":
                        userId = response.ContentId;
                        break;

                    case "sticker":
                        break;

                    case "emoji":
                        break;

                    case "print":
                        break;

                    default:
                        userId = response.ContentId;
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to convert moderation report type to user ID");
            }
            return userId;
        }

        public Task<List<ModerationInfo>> GetModerationReportsByUserId(string userId)
        {
            if (serviceRegistry == null)
            {
                throw new InvalidOperationException("ServiceRegistry is not initialized.");
            }

            TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
            return dbContext.ModerationInfos.Where(m => m.UserId == userId).ToListAsync();
        }
        #endregion

    }
}
