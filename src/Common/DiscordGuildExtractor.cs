using NLog;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Tailgrab.Common
{
    public sealed class DiscordInviteResult
    {
        public string Code { get; }
        public string InviteUrl { get; }
        public DiscordGuild? Guild { get; }
        public string? Error { get; }

        public DiscordInviteResult(string code, string inviteUrl, DiscordGuild? guild, string? error)
        {
            Code = code;
            InviteUrl = inviteUrl;
            Guild = guild;
            Error = error;
        }
    }

    public sealed class DiscordGuild
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("vanity_url_code")]
        public string? VanityUrlCode { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        public override string ToString()
        {
            return (Id ?? "No Guild ID") + " - " +
                (Name ?? "No Name") + " - " +
                (VanityUrlCode ?? "No Vanity URL Code") + " - " +
                (Description ?? "No Description");
        }
    }

    public static class DiscordGuildExtrator
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Matches bare links and links with http(s) or www.
        // Domain matching is case-insensitive; invite codes retain their case.
        private static readonly Regex InviteRegex = new(
            @"(?<![\w./@-])(?i:(?:https?://)?(?:www\.)?discord\.gg)/" +
            @"(?<code>[A-Za-z0-9_-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(2));

        private sealed class InviteResponse
        {
            [JsonPropertyName("guild")]
            public DiscordGuild? Guild { get; init; }
        }

        public static async Task<List<DiscordInviteResult>> FindAsync(
            string text,
            HttpClient http,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(http);

            var codes = InviteRegex.Matches(text)
                .Cast<Match>()
                .Select(match => match.Groups["code"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var results = new List<DiscordInviteResult>(codes.Count);

            // Sequential requests avoid sending a burst of requests.
            foreach (string code in codes)
            {
                string inviteUrl = $"https://discord.gg/{code}";
                string apiUrl = $"https://discord.com/api/invite/{Uri.EscapeDataString(code)}";

                try
                {
                    using var response = await http.GetAsync(
                        apiUrl, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.StatusCode switch
                        {
                            HttpStatusCode.NotFound =>
                                "Invite not found or expired.",
                            HttpStatusCode.TooManyRequests =>
                                "Discord rate limit reached; retry later.",
                            _ => $"Discord returned HTTP {(int)response.StatusCode}."
                        };

                        results.Add(new(code, inviteUrl, null, error));

                        // Stop issuing requests when rate limited.
                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            foreach (string remaining in codes.Skip(results.Count))
                            {
                                results.Add(new(
                                    remaining,
                                    $"https://discord.gg/{remaining}",
                                    null,
                                    "Not requested because Discord rate limited requests."));
                            }

                            break;
                        }

                        continue;
                    }

                    try
                    {

                        string? stringPayload = await response.Content.ReadAsStringAsync();
                        logger.Debug(stringPayload);                        
                        InviteResponse? payload = System.Text.Json.JsonSerializer.Deserialize<InviteResponse>(stringPayload);

                        results.Add(new(
                            code,
                            inviteUrl,
                            payload?.Guild,
                            payload?.Guild is null
                                ? "Invite response did not include a guild."
                                : null));

                    }
                    catch (System.Text.Json.JsonException)
                    {
                        results.Add(new(
                            code, inviteUrl, null, "Discord returned invalid JSON."));
                    }

                }
                catch (HttpRequestException ex)
                {
                    results.Add(new(code, inviteUrl, null, ex.Message));
                }
                catch (System.Text.Json.JsonException)
                {
                    results.Add(new(
                        code, inviteUrl, null, "Discord returned invalid JSON."));
                }
            }

            return results;
        }
    }
}
