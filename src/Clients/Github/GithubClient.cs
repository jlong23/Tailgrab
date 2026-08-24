using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tailgrab.Common;

namespace Tailgrab.Clients.Github
{
    public class GithubClient
    {
        public static async Task<bool> UpdateGist(string? personalAccessToken, string? gistId, string fileNameToUpdate, string newContent)
        {
            if( personalAccessToken == null || gistId == null)
            {
                Console.WriteLine("Personal Access Token or Gist ID is null.");
                return false;
            }

            using (var client = new HttpClient())
            {
                // Configure Headers
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(CommonConst.ApplicationName, BuildInfo.GetInformationalVersion()));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", personalAccessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                // Optional: Specify API version
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

                // Prepare the JSON payload
                // Structure: { "files": { "filename.ext": { "content": "new content" } } }
                var updateData = new
                {
                    files = new Dictionary<string, object>
                    {
                        {
                            fileNameToUpdate,
                            new { content = newContent }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(updateData);
                var contentPayload = new StringContent(json, Encoding.UTF8, "application/json");

                // Send PATCH request
                var response = await client.PatchAsync($"https://api.github.com/gists/{gistId}", contentPayload);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Gist updated successfully!");
                    var responseBody = await response.Content.ReadAsStringAsync();
                    // Optionally parse responseBody to get the new HTML URL or revision hash
                    return true;
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to update Gist. Status: {response.StatusCode}");
                Console.WriteLine($"Error: {errorBody}");
                return false;
            }
        }

        public static async Task<string?> GetGistFileUrl(string gistId, string fileNameToRetrieve)
        {
            using (var client = new HttpClient())
            {
                BuildClientHeaders(client);
                // Send GET request
                var response = await client.GetAsync($"https://api.github.com/gists/{gistId}");
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(responseBody))
                    {
                        if (doc.RootElement.TryGetProperty("files", out JsonElement filesElement) &&
                            filesElement.TryGetProperty(fileNameToRetrieve, out JsonElement fileElement) &&
                            fileElement.TryGetProperty("raw_url", out JsonElement contentElement))
                        {
                            return contentElement.GetString();
                        }
                    }
                }
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to retrieve Gist. Status: {response.StatusCode}");
                Console.WriteLine($"Error: {errorBody}");
                return null;
            }
        }

        private static void BuildClientHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(CommonConst.ApplicationName, BuildInfo.GetInformationalVersion()));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            // Optional: Specify API version
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }
    }
}
