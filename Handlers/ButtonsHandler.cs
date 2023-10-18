using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Models.Common;
using Polly;
using CharacterEngineDiscord.Models.Database;
using CharacterEngineDiscord.Models.CharacterHub;

namespace CharacterEngineDiscord.Handlers
{
    internal class ButtonsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;

        public ButtonsHandler(IServiceProvider services)
        {
            _services = services;
            _integration = _services.GetRequiredService<IntegrationsService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            _client.ButtonExecuted += (component) =>
            {
                Task.Run(async () => {
                    try { await HandleButtonAsync(component); }
                    catch (Exception e) { await HandleButtonException(component, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleButtonException(SocketMessageComponent component, Exception e)
        {
            LogException(new[] { e });
            var channel = component.Channel as IGuildChannel;
            var guild = channel?.Guild;
            var owner = guild is null ? null : (await guild.GetOwnerAsync()) as SocketGuildUser;

            TryToReportInLogsChannel(_client, title: "Button Exception",
                                              desc: $"Guild: `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{owner?.GetBestName()} ({owner?.Username})`\n" +
                                                    $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"User: `{component.User?.Username}`\n" +
                                                    $"Button ID: `{component.Data.CustomId}`",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }

        private async Task HandleButtonAsync(SocketMessageComponent component)
        {
            await component.DeferAsync();

            var searchQuery = _integration.SearchQueries.Find(sq => sq.ChannelId == component.ChannelId);
            if (searchQuery is null || searchQuery.SearchQueryData.IsEmpty) return;
            if (searchQuery.AuthorId != component.User.Id) return;
            if (await UserIsBannedCheckOnly(component.User.Id)) return;

            int tail = searchQuery.SearchQueryData.Characters.Count - (searchQuery.CurrentPage - 1) * 10;
            int maxRow = tail > 10 ? 10 : tail;

            switch (component.Data.CustomId)
            {
                case "up":
                    if (searchQuery.CurrentRow == 1) searchQuery.CurrentRow = maxRow;
                    else searchQuery.CurrentRow--; break;
                case "down":
                    if (searchQuery.CurrentRow > maxRow) searchQuery.CurrentRow = 1;
                    else searchQuery.CurrentRow++; break;
                case "left":
                    searchQuery.CurrentRow = 1;
                    if (searchQuery.CurrentPage == 1) searchQuery.CurrentPage = searchQuery.Pages;
                    else searchQuery.CurrentPage--; break;
                case "right":
                    searchQuery.CurrentRow = 1;
                    if (searchQuery.CurrentPage == searchQuery.Pages) searchQuery.CurrentPage = 1;
                    else searchQuery.CurrentPage++; break;
                case "select":
                    try
                    {
                        await component.Message.ModifyAsync(msg =>
                        {
                            msg.Embed = WAIT_MESSAGE;
                            msg.Components = null;
                        });
                    }
                    catch { return; }

                    int index = (searchQuery.CurrentPage - 1) * 10 + searchQuery.CurrentRow - 1;
                    var character = searchQuery.SearchQueryData.Characters[index];

                    var type = searchQuery.SearchQueryData.IntegrationType;
                    if (character is null) return;

                    var context = new InteractionContext(_client, component, component.Channel);
                    bool fromChub = type is not IntegrationType.CharacterAI && type is not IntegrationType.Aisekai;

                    var characterWebhook = await CreateCharacterWebhookAsync(searchQuery.SearchQueryData.IntegrationType, context, character, _integration, fromChub);
                    if (characterWebhook is null) return;

                    var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                    _integration.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

                    await component.Message.ModifyAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));

                    string characterMessage = $"{component.User.Mention} {characterWebhook.Character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(component.User as SocketGuildUser)?.GetBestName()}**")}";
                    if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

                    // Try to set avatar
                    Stream? image = null;
                    if (!string.IsNullOrWhiteSpace(characterWebhook.Character.AvatarUrl))
                    {
                        var originalMessage = await component.GetOriginalResponseAsync();
                        var imageUrl = originalMessage.Embeds?.Single()?.Image?.ProxyUrl;
                        image = await TryToDownloadImageAsync(imageUrl, _integration.ImagesHttpClient);
                    }
                    image ??= new MemoryStream(File.ReadAllBytes($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));
                    await webhookClient.ModifyWebhookAsync(w => w.Image = new Image(image));

                    await webhookClient.SendMessageAsync(characterMessage);

                    lock (_integration.SearchQueries)
                        _integration.SearchQueries.Remove(searchQuery);

                    return;
                default:
                    return;
            }

            try
            {   // Only if left/right/up/down is selected, either this line will never be reached
                await component.Message.ModifyAsync(c => c.Embed = BuildCharactersList(searchQuery)).ConfigureAwait(false);
            }
            catch { return; }
        }

        /// <summary>
        /// Called when user presses "select" button in search
        /// </summary>
        private async Task<Models.Database.Character?> SelectCaiCharacterAsync(string characterId, ulong channelId)
        {
            if (_integration.CaiClient is null) return null;

            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);
            if (channel is null) return null;

            var caiToken = channel.Guild.GuildCaiUserToken ?? "";
            var plusMode = channel.Guild.GuildCaiPlusMode ?? false;
            if (string.IsNullOrWhiteSpace(caiToken)) return null;

            var caiCharacter = await _integration.CaiClient.GetInfoAsync(characterId, customAuthToken: caiToken, customPlusMode: plusMode);
            return CharacterFromCaiCharacterInfo(caiCharacter);
        }

        /// <summary>
        /// Called when user presses "select" button in search
        /// </summary>
        private async Task<Models.Database.Character?> SelectAisekaiCharacterAsync(string characterId, ulong channelId, SocketUserMessage originalMessage, string? authToken = null)
        {
            var db = new StorageContext();
            var channel = await db.Channels.FindAsync(channelId);
            if (channel is null) return null;

            authToken ??= channel.Guild.GuildAisekaiAuthToken;
            if (string.IsNullOrWhiteSpace(authToken)) return null;

            var response = await _integration.AisekaiClient.GetCharacterInfoAsync(authToken, characterId);
            if (response.IsSuccessful)
            {
                return CharacterFromAisekaiCharacterInfo(response.Character!.Value);
            }
            else if (response.Code == 401)
            {   // Re-login
                var newAuthToken = await _integration.UpdateGuildAisekaiAuthTokenAsync(channel.GuildId, channel.Guild.GuildAisekaiRefreshToken!);
                if (newAuthToken is null)
                {
                    await originalMessage.ModifyAsync(m => m.Embed = $"{WARN_SIGN_DISCORD} Failed to authorize Aisekai account`".ToInlineEmbed(Color.Red));
                    return null;
                }
                else
                    return await SelectAisekaiCharacterAsync(characterId, channelId, originalMessage, newAuthToken);
            }
            else
            {
                await originalMessage.ModifyAsync(m => m.Embed = $"{WARN_SIGN_DISCORD} Failed to get character info: `{response.ErrorReason}`".ToInlineEmbed(Color.Red));
                return null;
            }
        }
    }
}
