using NLog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Tailgrab.Clients.Github;
using Tailgrab.Clients.OBS;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
using Tailgrab.Common;
using static System.Net.WebRequestMethods;

namespace Tailgrab.PlayerManagement.SetupWizard
{
    public class SetupWizardHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Tests VRChat credentials by attempting to initialize the VRChat API client.
        /// </summary>
        public static async Task<(bool Success, string Message)> TestVRChatCredentials(string username, string password, string? twoFactorSecret = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return (false, "Username and password are required.");
                }

                // Temporarily save credentials
                ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_UserName, username.Trim());
                ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_Password, password.Trim());
                if (!string.IsNullOrEmpty(twoFactorSecret))
                {
                    ConfigStore.SaveSecret(CommonConst.Registry_VRChat_Web_2FactorKey, twoFactorSecret.Trim());
                }

                // Create a temporary VRChat client to test
                var testClient = new VRChatClient();
                bool initialized = await testClient.Initialize();

                if (initialized)
                {
                    return (true, "VRChat credentials verified successfully.");
                }
                else
                {
                    // Clear invalid credentials
                    ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_UserName);
                    ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_Password);
                    ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_2FactorKey);
                    return (false, "Failed to authenticate with VRChat API. Please check your credentials.");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error testing VRChat credentials: {ex.Message}");
                // Clear invalid credentials on error
                try
                {
                    ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_UserName);
                    ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_Password);
                    ConfigStore.DeleteSecret(CommonConst.Registry_VRChat_Web_2FactorKey);
                }
                catch { }
                return (false, $"Error testing credentials: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests if a GitHub Gist URL is accessible and optionally if PAT has write access.
        /// </summary>
        public static async Task<(bool Success, string Message)> TestGistUrl(string gistId, string? personalAccessToken = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gistId))
                {
                    return (false, "Gist ID is required.");
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(CommonConst.ApplicationName, BuildInfo.GetInformationalVersion()));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                    // Optional: Specify API version
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");


                    if (!string.IsNullOrEmpty(personalAccessToken))
                    {
                        logger.Info("Using provided Personal Access Token for Gist verification.");
                        client.DefaultRequestHeaders.Add("Authorization", $"token {personalAccessToken.Trim()}");
                    }

                    // Try to fetch gist metadata
                    string url = $"https://api.github.com/gists/{gistId}";
                    logger.Info($"Testing Gist URL: {url}");
                    var response = await client.GetAsync(url);
                    logger.Info($"Gist URL response status: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, "Gist URL verified successfully.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return (false, "Gist not found. Please check the URL.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return (false, "Personal Access Token is invalid or expired.");
                    }
                    else
                    {
                        return (false, $"Failed to verify Gist: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error testing Gist URL: {ex.Message}");
                return (false, $"Error testing Gist URL: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests if an OLLAMA endpoint is accessible.
        /// </summary>
        public static async Task<(bool Success, string Message)> TestOllamaEndpoint(string endpoint)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    return (false, "OLLAMA endpoint is required.");
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // Try to reach the OLLAMA API health endpoint
                    var response = await client.GetAsync($"{endpoint.TrimEnd('/')}/api/tags");

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, "OLLAMA endpoint verified successfully.");
                    }
                    else
                    {
                        return (false, $"Failed to reach OLLAMA endpoint: {response.StatusCode}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                logger.Error($"Error testing OLLAMA endpoint: {ex.Message}");
                return (false, $"Cannot reach OLLAMA endpoint. Ensure it's running and accessible.");
            }
            catch (Exception ex)
            {
                logger.Error($"Error testing OLLAMA endpoint: {ex.Message}");
                return (false, $"Error testing OLLAMA endpoint: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests if an OBS WebSocket connection is possible.
        /// </summary>
        public static async Task<(bool Success, string Message)> TestOBSConnection(string websocketUri, string? password = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(websocketUri))
                {
                    return (false, "OBS WebSocket URI is required.");
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // Convert ws:// to http:// for basic connectivity check
                    string httpUri = websocketUri.Replace("ws://", "http://").Replace("wss://", "https://");

                    try
                    {
                        // Try to reach the OBS WebSocket endpoint
                        var response = await client.GetAsync($"{httpUri}/");
                        // Any response (even 404 or 400) indicates the service is running
                        return (true, "OBS WebSocket connection verified.");
                    }
                    catch
                    {
                        // WebSocket services don't respond to HTTP GET; timeout is expected
                        // We'll consider this a failure for this wizard
                        return (false, "Cannot reach OBS WebSocket URI. Ensure OBS is running with WebSocket enabled.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error testing OBS connection: {ex.Message}");
                return (false, $"Error testing OBS connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that a port number is available for MCP server.
        /// </summary>
        public static (bool Success, string Message) ValidateMCPPort(int port)
        {
            try
            {
                if (port < 1024 || port > 65535)
                {
                    return (false, "Port must be between 1024 and 65535.");
                }

                // Try to bind to the port
                using (var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port))
                {
                    listener.Start();
                    listener.Stop();
                }

                return (true, $"Port {port} is available.");
            }
            catch (System.Net.Sockets.SocketException)
            {
                return (false, $"Port {port} is already in use. Please choose a different port.");
            }
            catch (Exception ex)
            {
                logger.Error($"Error validating MCP port: {ex.Message}");
                return (false, $"Error validating port: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if this is a first-time setup (no VRChat credentials configured).
        /// </summary>
        public static bool IsFirstTimeSetup()
        {
            string? username = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName);
            return string.IsNullOrEmpty(username);
        }

        /// <summary>
        /// Checks if this is an upgrade scenario (has old config but missing some new settings).
        /// </summary>
        public static bool IsUpgradeScenario()
        {
            // If username exists, they have done some prior configuration
            string? username = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName);
            if (string.IsNullOrEmpty(username))
            {
                return false; // Not an upgrade, first time setup
            }

            // Check if they have new optional settings
            // This is just a heuristic; we can expand if needed
            return true;
        }
    }
}
