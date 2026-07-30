using Microsoft.Win32;
using NLog;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;

namespace Tailgrab.Configuration
{
    public class GroupBosGistListManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly HttpClient _httpClient;
        private readonly TailgrabDBContext dbContext;
        private readonly PlayerManager playerManager;

        public GroupBosGistListManager(TailgrabDBContext tailgrabDBContext, PlayerManager management)
        {
            dbContext = tailgrabDBContext;
            playerManager = management;
            _httpClient = new HttpClient();
        }

        private int gistRecordCount = 0;
        private int gistProcessedCount = 0;

        public string GetQueueSize()
        {
            if( gistRecordCount > 0)
            {
                int percentComplete = (int)((double)gistProcessedCount / gistRecordCount * 100);
                return $"Group GIST Processing: {gistProcessedCount} of {gistRecordCount} ({percentComplete}%)";
            }

            return string.Empty;
        }

        /// <summary>
        /// Downloads a GIST content file, verifies its checksum against registry, 
        /// and processes AvatarIds if the file is new or changed.
        /// </summary>
        /// <param name="gistUrl">The URL of the GIST raw content to download</param>
        /// <returns>True if processing was successful, false otherwise</returns>
        public async Task<bool> ProcessGroupGistList( string? tempUrl, bool ignoreChecksum )
        {
            string? gistUrl = string.Empty;
            if ( !string.IsNullOrWhiteSpace(tempUrl))
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

                if( ignoreChecksum == false ) 
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

        private GroupImportItem? ProcessGroupLineItem( string line, int lineNumber)
        {
            // Split by whitespace or comma to get the first column
            string pattern = @",(?=(?:[^""]*""[^""]*"")*[^""]*$)";
            string[] columns = Regex.Split(line, pattern);
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
            gistRecordCount = importList.Count();
            gistProcessedCount = 0;
            int processedCount = 0;
            foreach (GroupImportItem item in importList)
            {
                logger.Debug($"Line {item.LineNumber}: Processing {item.ToString()}");
                try
                {
                    // Fetch/Refresh the GroupInfo from VRC 
                    GroupInfo? groupInfo = await playerManager.AddUpdateGroupFromVRC(item.GroupId);
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
                        dbContext.GroupInfos.Update(groupInfo);
                        processedCount++;
                        logger.Debug($"Line {item.LineNumber}: Set AlertType for Group ID '{item.GroupId}' to '{item.AlertType}'");
                    }
                    else
                    {
                        logger.Debug($"Line {item.LineNumber}: Group ID '{item.GroupId}' already has AlertType, skipping.");
                    }

                    gistProcessedCount++;
                    if( item.LineNumber % 50 == 0)
                    {
                        logger.Info($"GIST Group Processed {item.LineNumber} of {importList.Count()} records.");
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
    }

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
}
