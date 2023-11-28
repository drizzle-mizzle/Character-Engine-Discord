using CharacterEngineDiscord.Models.OpenAI;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterEngineDiscord.Services
{
    internal static partial class CommonService
    {
        public static async Task<bool> CheckIfImageIsAvailableAsync(string url, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!url.IsValidURL()) return false;

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode) return true;
                }
                catch
                {
                    break;
                }
                
                await Task.Delay(2000);
            }

            return false;
        }

        public static async Task<Stream?> TryToDownloadImageAsync(string? url, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                return await httpClient.GetStreamAsync(url);
            }
            catch
            {
                return null;
            }
        }

        internal static Embed ToInlineEmbed(this string text, Color color, bool bold = true, string? imageUrl = null)
        {
            string desc = bold ? $"**{text}**" : text;

            var result = new EmbedBuilder().WithDescription(desc).WithColor(color);
            if (!string.IsNullOrWhiteSpace(imageUrl))
                result.WithImageUrl(imageUrl);

            return result.Build();
        }

        public static bool ToBool(this string? str)
            => bool.Parse(str ?? "false");

        public static int ToInt(this string str)
            => int.Parse(str);

        public static bool IsEmpty(this string? str)
            => string.IsNullOrWhiteSpace(str);

        public static Dictionary<string, string> ToDict(this OpenAiMessage pair)
            => new() { { "role", pair.Role  }, { "content", pair.Content } };

        public static dynamic? ToDynamicJsonString(this string? str)
        {
            try
            {
                return str.IsEmpty() ? null : JsonConvert.DeserializeObject<dynamic>(str!);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return null;
            }
        }

        public static async Task<dynamic?> ReadAsJsonAsync(this HttpContent httpContent)
        {
            try
            {
                var content = await httpContent.ReadAsStringAsync();
                return content.IsEmpty() ? null : JsonConvert.DeserializeObject<dynamic>(content);
            }
            catch (Exception e)
            {
                LogException(new[] { e });
                return null;
            }
        }

        public static string RemovePrefix(this string str, string prefix)
        {
            var result = str.Trim();
            if (result.StartsWith(prefix))
                result = result.Remove(0, prefix.Length);

            return result;
        }

        public async static Task<string> AddRefQuoteAsync(this string str, IUserMessage? refMsg)
        {
            if (str.Contains("{{ref_msg_text}}"))
            {
                int start = str.IndexOf("{{ref_msg_begin}}");
                int end = str.IndexOf("{{ref_msg_end}}") + "{{ref_msg_end}}".Length;

                if (string.IsNullOrWhiteSpace(refMsg?.Content))
                {
                    str = str.Remove(start, end - start).Trim();
                }
                else
                {
                    string refName = refMsg.Author is SocketGuildUser refGuildUser ? (refGuildUser.GetBestName()) : refMsg.Author.Username;
                    string refContent = refMsg.Content.Replace("\n", " ");

                    // Replace @mentions with normal names
                    var userMentions = MentionRegex().Matches(refContent).ToArray();
                    foreach (var mention in userMentions)
                    {   try
                        {
                            var userId = MentionUtils.ParseUser(mention.Value);
                            if (await refMsg.Channel.GetUserAsync(userId) is not IGuildUser user) continue;
                            else refContent = refContent.Replace(mention.ToString(), (user.IsBot || user.IsWebhook) ? user.Username : user.GetBestName());
                        }
                        catch { continue; }
                    }

                    int refL = Math.Min(refContent.Length, 150);
                    str = str.Replace("{{ref_msg_user}}", refName)
                             .Replace("{{ref_msg_text}}", refContent[0..refL] + (refL == 150 ? "..." : ""))
                             .Replace("{{ref_msg_begin}}", "")
                             .Replace("{{ref_msg_end}}", "");
                }
            }

            return str;
        }

        public static bool IsValidURL(this string URL)
            => HttpUrlPattern().IsMatch(URL);


        [GeneratedRegex("^(?:http(s)?:\\/\\/)?[\\w.-]+(?:\\.[\\w\\.-]+)+[\\w\\-\\._~:/?#[\\]@!\\$&'\\(\\)\\*\\+,;=.]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex HttpUrlPattern();


        [GeneratedRegex("\\<(.*?)\\>")]
        public static partial Regex MentionRegex();

    }
}
