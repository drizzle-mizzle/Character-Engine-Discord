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
                    catch (Exception e) { await HandleButtonExceptionAsync(component, e); }
                });
                return Task.CompletedTask;
            };
        }

        private async Task HandleButtonExceptionAsync(SocketMessageComponent component, Exception e)
        {
            LogException(new[] { e });
            var channel = component.Channel as IGuildChannel;
            var guild = channel?.Guild;

            await TryToReportInLogsChannel(_client, title: "Exception",
                                                    desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                          $"User: {component.User?.Username}\n" +
                                                          $"Button ID: {component.Data.CustomId}",
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
                    string characterId = searchQuery.SearchQueryData.Characters[index].Id;

                    Models.Database.Character? character;

                    if (searchQuery.SearchQueryData.IntegrationType is IntegrationType.CharacterAI)
                    {
                        character = await SelectCaiCharacterAsync(characterId, searchQuery.ChannelId);
                    }
                    else if (searchQuery.SearchQueryData.IntegrationType is IntegrationType.OpenAI)
                    {
                        var chubCharacter = await GetChubCharacterInfo(characterId, _integration.HttpClient);
                        character = CharacterFromChubCharacterInfo(chubCharacter);
                    }
                    else { return; }

                    if (character is null)
                    {
                        await component.Message.ModifyAsync(msg => msg.Embed = FailedToSetCharacterEmbed());
                        return;
                    }

                    var context = new InteractionContext(_client, component, component.Channel);

                    var characterWebhook = await CreateCharacterWebhookAsync(searchQuery.SearchQueryData.IntegrationType, context, character, _integration);
                    if (characterWebhook is null) return;

                    var webhookClient = new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                    _integration.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

                    await component.Message.ModifyAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));

                    string characterMessage = $"{component.User.Mention} {character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(component.User as SocketGuildUser)?.GetBestName()}**")}";
                    if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

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

            var caiToken = channel.Guild.GuildCaiUserToken ?? ConfigFile.DefaultCaiUserAuthToken.Value;
            var plusMode = channel.Guild.GuildCaiPlusMode ?? ConfigFile.DefaultCaiPlusMode.Value.ToBool();
            if (string.IsNullOrWhiteSpace(caiToken)) return null;

            var caiCharacter = await _integration.CaiClient.GetInfoAsync(characterId, customAuthToken: caiToken, customPlusMode: plusMode);
            return CharacterFromCaiCharacterInfo(caiCharacter);
        }
    }
}
