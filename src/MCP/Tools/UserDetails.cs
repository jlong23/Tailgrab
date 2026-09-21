using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.MCP.Tools
{
    internal class UserDetails : McpToolBase
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public override string Name => "user_details";
        public override string Description => "Retrieves detailed information for VRChat user, including watched group membership.";
        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                userId = new
                {
                    type = "string",
                    description = "The userId of the VRChat user"
                }
            },
            required = new[] { "userId" }
        };
        private readonly ServiceRegistry _serviceRegistry;

        public UserDetails(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public override async Task<McpToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            await Task.Delay(0, cancellationToken);

            try
            {
                string userId = arguments.ContainsKey("userId") ? arguments["userId"].ToString() ?? "" : "";
                VRChatUserProfileEntry? userProfile = _serviceRegistry.GetVRChatAPIClient().GetCachedUserProfile(userId);

                if (userProfile == null)
                {
                    logger.Warn("User not found: {0}", userId);
                    return McpToolResult.FromError("User not found");
                }

                DateTime joinDate = DateTime.Parse(userProfile.JoinDate.ToString() ?? new DateTime().ToString());
                TimeSpan elapsed = DateTime.Now - joinDate;

                List<UserGroupEntry> userGroups = GetUserGroups(userProfile.GroupMemberships);
                var groupInfo = userGroups.Select(g => new
                {
                    group_id = g.GroupId,
                    group_name = g.GroupName,
                    group_watch = g.GroupWatch
                }).ToList();
                
                var info = new
                {
                    timestamp = DateTime.UtcNow,
                    application = "Tailgrab",
                    version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                    environment = new
                    {
                        ttlMs = 5000,
                        cacheScope = "private",
                        display_name = userProfile.DisplayName,
                        user_id = userProfile.UserId,
                        account_age_in_days = elapsed.TotalDays,
                        verified = ConvertBooleanToYesNo(userProfile.AgeVerified),
                        friend = ConvertBooleanToYesNo( userProfile.IsFriend ),
                        icon_url = userProfile.ProfileIconUrl ?? "",
                        banner_url = userProfile.ProfileBannerUrl ?? "",
                        bio = userProfile.Bio ?? "",
                        group_membership_count = userGroups.Count,
                        group_membership = groupInfo
                    }
                };

                logger.Info("UserDetails tool executed successfully for userId: {0}", userId);
                logger.Info(info.ToString() ?? "Empty String");   


                return McpToolResult.FromSuccess(info);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while executing the UserDetails tool.");
                return McpToolResult.FromError($"An error occurred while executing the tool: {ex.Message}");
            }
        }

        private List<UserGroupEntry> GetUserGroups(List<LimitedUserGroups> userMemberships)
        {
            List<UserGroupEntry> groupEntries = new List<UserGroupEntry>();
            TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();

            foreach (var membership in userMemberships)
            {
                GroupInfo? groupInfo = dbContext.GroupInfos.Find(membership.GroupId);

                if (groupInfo != null)
                {
                    UserGroupEntry entry = new UserGroupEntry
                    {
                        GroupId = membership.GroupId,
                        GroupName = membership.Name,
                        GroupWatch = ConvertBooleanToYesNo(groupInfo?.AlertType > AlertTypeEnum.None),
                    };
                    groupEntries.Add(entry);
                }
            }
            return groupEntries;
        }        
    }

    internal class UserGroupEntry
    {
        public string GroupId { get; set; } = "";
        public string GroupName { get; set; } = "";
        public string GroupWatch { get; set; } = "";
    }

}
