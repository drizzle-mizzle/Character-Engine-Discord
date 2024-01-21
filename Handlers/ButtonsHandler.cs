using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.CommandsService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using CharacterEngineDiscord.Interfaces;
using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Handlers
{
    internal class ButtonsHandler(IDiscordClient client, IIntegrationsService integrations)
    {
        public Task HandleButton(SocketMessageComponent component)
        {
            Task.Run(async () => {
                try { await HandleButtonAsync(component); }
                catch (Exception e) { await HandleButtonException(component, e); }
            });

            return Task.CompletedTask;
        }

        private async Task HandleButtonException(SocketMessageComponent component, Exception e)
        {
            LogException(new[] { e });
            var channel = component.Channel as IGuildChannel;
            var guild = channel?.Guild;
            var owner = guild is null ? null : (await guild.GetOwnerAsync()) as SocketGuildUser;

            TryToReportInLogsChannel(client, title: "Button Exception",
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

            var searchQuery = integrations.SearchQueries.FirstOrDefault(sq => sq.ChannelId == component.ChannelId);
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
                    Models.Database.Character character;
                    try { character = searchQuery.SearchQueryData.Characters[index]; }
                    catch { return; }

                    var type = searchQuery.SearchQueryData.IntegrationType;
                    var context = new InteractionContext(client, component, component.Channel);
                    bool fromChub = type is not IntegrationType.CharacterAI && type is not IntegrationType.Aisekai;

                    var characterWebhook = await CreateCharacterWebhookAsync(searchQuery.SearchQueryData.IntegrationType, context, character, integrations, fromChub);
                    if (characterWebhook is null) return;

                    await using (var db = new StorageContext())
                    {
                        characterWebhook = db.Entry(characterWebhook).Entity;


                        var webhookClient =
                            new DiscordWebhookClient(characterWebhook.Id, characterWebhook.WebhookToken);
                        integrations.WebhookClients.TryAdd(characterWebhook.Id, webhookClient);

                        await component.Message.ModifyAsync(msg => msg.Embed = SpawnCharacterEmbed(characterWebhook));
                        if (type is IntegrationType.Aisekai)
                            await component.Channel.SendMessageAsync(
                                embed:
                                ":zap: Please, pay attention to the fact that Aisekai characters don't support separate chat histories. Thus, if you will spawn the same character in two different channels, both channels will continue to share the same chat context; same goes for `/reset-character` command — once it's executed, the chat history will be deleted in each channel where specified character is present."
                                    .ToInlineEmbed(Color.Gold, false));

                        string characterMessage =
                            $"{component.User.Mention} {characterWebhook.Character.Greeting.Replace("{{char}}", $"**{characterWebhook.Character.Name}**").Replace("{{user}}", $"**{(component.User as SocketGuildUser)?.GetBestName()}**")}";
                        if (characterMessage.Length > 2000) characterMessage = characterMessage[0..1994] + "[...]";

                        // Try to set avatar
                        Stream? image = null;

                        if (!string.IsNullOrWhiteSpace(characterWebhook.Character.AvatarUrl))
                        {
                            var originalMessage = await component.GetOriginalResponseAsync();
                            var imageUrl = originalMessage.Embeds?.FirstOrDefault()?.Image?.ProxyUrl;
                            image = await TryToDownloadImageAsync(imageUrl, integrations.ImagesHttpClient);
                        }

                        image ??= new MemoryStream(await File.ReadAllBytesAsync($"{EXE_DIR}{SC}storage{SC}default_avatar.png"));

                        try
                        {
                            await webhookClient.ModifyWebhookAsync(w => w.Image = new Image(image));
                        }
                        finally
                        {
                            await image.DisposeAsync();
                        }

                        await webhookClient.SendMessageAsync(characterMessage);

                        await integrations.SearchQueriesLock.WaitAsync();
                        try
                        {
                            integrations.SearchQueries.Remove(searchQuery);
                        }
                        finally
                        {
                            integrations.SearchQueriesLock.Release();
                        }
                    }

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
    }
}
