using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Dynamic;
using System.Net;
using System.Text;
using CharacterEngineDiscord.Services.AisekaiIntegration.Models;
using CharacterEngineDiscord.Services.AisekaiIntegration.SearchEnums;
using static CharacterEngineDiscord.Services.CommonService;
using CharacterEngineDiscord.Models.Common;
using CharacterResponse = CharacterEngineDiscord.Services.AisekaiIntegration.Models.CharacterResponse;

namespace CharacterEngineDiscord.Services.AisekaiIntegration
{
    public class AisekaiClient
    {
        private readonly HttpClient _aisekaiHttpClient;
        private readonly HttpClient _authHttpClient;

        public AisekaiClient()
        {
            _aisekaiHttpClient = new HttpClient();
            _aisekaiHttpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _aisekaiHttpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _aisekaiHttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _aisekaiHttpClient.DefaultRequestHeaders.Add("User-Agent", ConfigFile.DefaultHttpClientUA.Value);
            _aisekaiHttpClient.DefaultRequestHeaders.Add("Origin", "https://www.aisekai.ai");
            _aisekaiHttpClient.DefaultRequestHeaders.Add("Referer", "https://www.aisekai.ai/");
            _aisekaiHttpClient.Timeout = new(0, 2, 0);

            _authHttpClient = new();
        }

        public async Task<LoginResponse> AuthorizeUserAsync(string email, string password)
        {
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=AIzaSyC1TNYv2fKdn5NQ-JY02Cz7InQ5TRcF5Yg";
            dynamic data = new ExpandoObject();
            data.clientType = "CLIENT_TYPE_WEB";
            data.email = email;
            data.password = password;
            data.returnSecureToken = true;

            try
            {
                using var response = await _authHttpClient.PostAsync(url, new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    string message = response.StatusCode is HttpStatusCode.BadRequest ? "Wrong email or password" : response.ReasonPhrase ?? "Something went wrong";
                    return new()
                    {
                        Message = message,
                        IsSuccessful = false
                    };
                }

                var content = await response.Content.ReadAsJsonAsync();
                if (content is null)
                {
                    return new()
                    {
                        Message = response.ReasonPhrase ?? "Something went wrong",
                        IsSuccessful = false
                    };
                }

                return ParsedLoginResponse(content);
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Message = e.Message,
                    IsSuccessful = false
                };
            }
        }

        public async Task<string?> RefreshUserTokenAsync(string refreshToken)
        {
            string url = "https://securetoken.googleapis.com/v1/token?key=AIzaSyC1TNYv2fKdn5NQ-JY02Cz7InQ5TRcF5Yg";

            dynamic data = new ExpandoObject();
            data.grant_type = "refresh_token";
            data.refresh_token = refreshToken;

            try
            {
                using var response = await _authHttpClient.PostAsync(url, new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsJsonAsync();

                return content?.access_token;
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return null;
            }
        }

        public async Task<bool> PatchToggleInitMessageAsync(string authToken, string historyId, bool enable)
        {
            string url = $"https://api.aisekai.ai/api/v1/chats/{historyId}/toggle-init-message";

            dynamic data = new ExpandoObject();
            data.allowInitMessage = enable;

            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } },
                    Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json")
                };

                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                var content = await response.Content.ReadAsJsonAsync();

                return content?.success ?? false;
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return false;
            }
        }

        public async Task<SearchResponse> GetSearchAsync(string authToken, string? query, SearchTime time, SearchType type, SearchSort sort, bool nsfw, int page, int size, string? tags = null)
        {
            string url = "https://api.aisekai.ai/api/v1/characters/search?" +
                         $"time={time}&" +
                         $"type={type.ToString().Trim('_')}&" +
                         $"sort={sort}&" +
                         $"nsfw={nsfw}&" +
                         $"page={page}&" +
                         $"size={size}";

            if (!string.IsNullOrWhiteSpace(query))
                url += $"&q={query.Replace(",", "%2C")}";
            if (!string.IsNullOrWhiteSpace(tags))
                url += $"&tag={tags.Replace(",", "%2C")}";

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } }
            };

            try
            {
                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                var characters = await TryToGetCharactersAsync(response);

                return new()
                {
                    Code = (int)response.StatusCode,
                    OriginalQuery = query ?? "",
                    ErrorReason = response.IsSuccessStatusCode ? null : response.ReasonPhrase ?? "Something went wrong",
                    IsSuccessful = response.IsSuccessStatusCode,
                    Characters = characters
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    ErrorReason = "Something went wrong",
                    IsSuccessful = false,
                    OriginalQuery = string.Empty,
                    Characters = new()
                };
            }
        }

        public async Task<CharacterInfoResponse> GetCharacterInfoAsync(string authToken, string characterId)
        {
            string url = $"https://api.aisekai.ai/api/v1/characters/simple/{characterId}";

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } }
            };

            try
            {
                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                var content = await response.Content.ReadAsJsonAsync();
                if (content is null)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                return new()
                {
                    Character = ParsedCharacter(content),
                    Code = (int)response.StatusCode,
                    IsSuccessful = true
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    IsSuccessful = false,
                    ErrorReason = e.Message
                };
            }
        }

        public async Task<EditResponse> PatchEditMessageAsync(string authToken, string historyId, string messageId, string text)
        {
            string url = $"https://api.aisekai.ai/api/v1/chats/{historyId}/messages/{messageId}/content";

            dynamic data = new ExpandoObject();
            data.content = text;
            data.createdAt = DateTime.UtcNow.ToString();

            try
            {
                var requestMessageContent = new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } },
                    Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json")
                };

                using var responseContent = await _aisekaiHttpClient.SendAsync(requestMessageContent);
                if (!responseContent.IsSuccessStatusCode)
                {
                    return new()
                    {
                        Code = (int)responseContent.StatusCode,
                        ErrorReason = responseContent.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                return new()
                {
                    Code = (int)responseContent.StatusCode,
                    IsSuccessful = true
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    ErrorReason = "Something went wrong",
                    IsSuccessful = false
                };
            }
        }

        public async Task<CallResponse> PostChatMessageAsync(string authToken, string historyId, string text)
        {
            string url = $"https://api.aisekai.ai/api/v1/chats/{historyId}/messages";

            dynamic data = new ExpandoObject();
            data.action = "";
            data.content = text;

            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } },
                    Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json")
                };

                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                var content = await response.Content.ReadAsJsonAsync();
                if (content is null)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                return new()
                {
                    CharacterResponse = new CharacterResponse()
                    {
                        LastMessageId = content._id,
                        Content = content.content
                    },
                    Code = (int)response.StatusCode,
                    IsSuccessful = true
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    ErrorReason = "Failed to fetch character response",
                    IsSuccessful = false
                };
            }
        }

        public async Task<SwipeResponse> SwipeChatMessageAsync(string authToken, string historyId, string lastMessageId)
        {
            string url = $"https://api.aisekai.ai/api/v1/chats/{historyId}/messages";

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } }
            };

            dynamic data = new ExpandoObject();
            data.chatMessageId = lastMessageId;

            try
            {
                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                var content = await response.Content.ReadAsJsonAsync();
                if (content is null)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                return new()
                {
                    Content = content.content,
                    Code = (int)response.StatusCode,
                    IsSuccessful = true
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    ErrorReason = "Something went wrong",
                    IsSuccessful = false,
                };
            }
        }

        public async Task<ChatInfoResponse> GetChatInfoAsync(string authToken, string characterId)
        {
            string url = $"https://api.aisekai.ai/api/v1/characters/{characterId}/chats?size=99999";

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } }
            };

            try
            {
                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                var content = await response.Content.ReadAsJsonAsync();
                if (content is null)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                return new()
                {
                    ChatId = content._id,
                    GreetingMessage = ((JArray)content.messages).First!["content"]!.Value<string>(),
                    InitiatorEngineEnabled = content.allowInitMessage,
                    Code = (int)response.StatusCode,
                    IsSuccessful = true
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    ErrorReason = "Something went wrong",
                    IsSuccessful = false
                };
            }
        }

        public async Task<ResetResponse> ResetChatHistoryAsync(string authToken, string historyId)
        {
            string url = $"https://api.aisekai.ai/api/v1/chats/{historyId}/reset";

            var requestMessage = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Headers = { { HttpRequestHeader.Authorization.ToString(), $"Bearer {authToken}" } }
            };

            try
            {
                using var response = await _aisekaiHttpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                var content = await response.Content.ReadAsJsonAsync();
                if (content is null)
                {
                    return new()
                    {
                        Code = (int)response.StatusCode,
                        ErrorReason = response.ReasonPhrase,
                        IsSuccessful = false
                    };
                }

                return new()
                {
                    Greeting = content.content,
                    Code = (int)response.StatusCode,
                    IsSuccessful = true
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return new()
                {
                    Code = 0,
                    ErrorReason = "Something went wrong",
                    IsSuccessful = false
                };
            }
        }


        private static string TryToGetAuthErrors(dynamic content)
        {
            var errors = content.userNotifications;
            if (errors is null)
            {
                return "Something went wrong";
            }
            else
            {
                var jerrors = (JArray)errors;
                var result = new List<string>();

                foreach (var error in jerrors)
                {
                    result.Add(error["notificationMessage"]!.ToString());
                }

                return string.Join(" | ", result);
            }
        }

        private static async Task<List<Character>> TryToGetCharactersAsync(HttpResponseMessage response)
        {
            var characters = new List<Character>();
            if (!response.IsSuccessStatusCode) return characters;

            var content = await response.Content.ReadAsJsonAsync();
            if (content is null) return characters;

            if (content is not JArray charsArray) return characters;

            foreach (var character in charsArray)
            {
                var parsedCharacter = ParsedCharacter(character);
                if (parsedCharacter.HasValue)
                    characters.Add(parsedCharacter.Value);
            }

            return characters;
        }

        private static Character? ParsedCharacter(dynamic c)
        {
            IEnumerable<string> tags;
            try { tags = JsonConvert.DeserializeObject<string[]>(((JArray)c.tags).ToString())!.ToList(); }
            catch { tags = new List<string>(); }
            try
            {
                return new()
                {
                    Id = c._id,
                    Name = c.name,
                    AvatarUrl = c.picture,
                    Description = c.description,
                    Tags = tags,
                    ChatCount = c.chatCount ?? 0u,
                    MessageCount = c.messageCount ?? 0u,
                    LikeCount = c.likeCount ?? 0u,
                    NSFW = c.nsfw ?? false,
                    Visibility = c.visibility,
                    Author = c.createdBy.username,
                    CreatedAt = c.createdAt,
                    UpdatedAt = c.updatedAt ?? DateTime.UtcNow
                };
            }
            catch (Exception e)
            {
                LogException(new[] { e.ToString() });
                return null;
            }
        }

        private static LoginResponse ParsedLoginResponse(dynamic content)
        {
            var refreshToken = content.refreshToken;

            if (refreshToken is null)
            {
                return new()
                {
                    Message = TryToGetAuthErrors(content),
                    IsSuccessful = false
                };
            }
            else
            {
                return new()
                {
                    ExpToken = content.idToken,
                    RefreshToken = refreshToken,
                    IsSuccessful = true
                };
            }
        }

    }
}
