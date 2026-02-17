using Microsoft.Win32;
using NLog;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Tailgrab.AvatarManagement;
using Tailgrab.Models;

namespace Tailgrab.Configuration
{
    public class AvatarBosGistListManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ServiceRegistry _serviceRegistry;
        private readonly HttpClient _httpClient;

        public AvatarBosGistListManager(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _httpClient = new HttpClient();
        }

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
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(Common.Common.ConfigRegistryPath))
                {
                    if (key == null)
                    {
                        logger.Debug("Registry key does not exist. No stored checksum found.");
                        return null;
                    }

                    string? value = key.GetValue(Common.Common.Registry_Avatar_Checksum) as string;
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
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(Common.Common.ConfigRegistryPath))
                {
                    if (key == null)
                    {
                        logger.Debug("Registry key does not exist. No stored URI");
                        return null;
                    }

                    string? value = key.GetValue(Common.Common.Registry_Avatar_Gist) as string;
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
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Common.Common.ConfigRegistryPath))
                {
                    key.SetValue(Common.Common.Registry_Avatar_Checksum, checksum, RegistryValueKind.String);
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
            TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
            AvatarManagementService avatarService = _serviceRegistry.GetAvatarManager();

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

                    // Split by whitespace or comma to get the first column
                    string[] columns = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (columns.Length == 0)
                    {
                        continue;
                    }

                    string avatarId = columns[0].Trim().Trim('"');
                    logger.Info(avatarId);

                    if (string.IsNullOrWhiteSpace(avatarId))
                    {
                        logger.Warn($"Line {lineNumber}: Empty avatar ID, skipping.");
                        continue;
                    }

                    try
                    {
                        // Fetch the AvatarInfo record
                        AvatarInfo? avatarInfo = await dbContext.AvatarInfos.FindAsync(avatarId);
                        AvatarManagementService.FetchUpdateAvatarData(_serviceRegistry, dbContext, avatarId, avatarInfo);
                        avatarInfo = await dbContext.AvatarInfos.FindAsync(avatarId);

                        if (avatarInfo == null)
                        {
                            logger.Debug($"Line {lineNumber}: Avatar ID '{avatarId}' not found in database, skipping.");
                            continue;
                        }

                        // Set IsBOS to true
                        if (!avatarInfo.IsBos)
                        {
                            avatarInfo.IsBos = true;
                            avatarInfo.UpdatedAt = DateTime.UtcNow;
                            dbContext.AvatarInfos.Update(avatarInfo);
                            processedCount++;
                            logger.Debug($"Line {lineNumber}: Set IsBOS=true for Avatar ID '{avatarId}'");
                        }
                        else
                        {
                            logger.Debug($"Line {lineNumber}: Avatar ID '{avatarId}' already has IsBOS=true, skipping.");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Line {lineNumber}: Error processing avatar ID '{avatarId}'");
                    }

                    // Throttle processing to avoid overwhelming the API
                    await Task.Delay(1000);
                }
            }

            // Save all changes to the database
            try
            {
                await dbContext.SaveChangesAsync();
                logger.Info($"Successfully saved {processedCount} changes to the database.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save changes to the database.");
                throw;
            }

            return processedCount;
        }
    }
}
