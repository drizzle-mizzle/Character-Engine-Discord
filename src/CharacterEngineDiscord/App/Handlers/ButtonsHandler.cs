using CharacterEngine.App.Exceptions;
using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Decorators;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Masters;
using CharacterEngine.App.Repositories;
using CharacterEngine.App.Services;
using CharacterEngineDiscord.Domain.Models;
using CharacterEngineDiscord.Domain.Models.Db;
using Discord;
using Discord.WebSocket;
using static CharacterEngine.App.Helpers.CommonHelper;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Handlers;


public class ButtonsHandler
{
    private readonly DiscordSocketClient _discordClient;
    private readonly CharactersRepository _charactersRepository;
    private readonly IntegrationsRepository _integrationsRepository;
    private readonly CacheRepository _cacheRepository;
    private readonly IntegrationsMaster _integrationsMaster;


    public ButtonsHandler(
        DiscordSocketClient discordClient,
        CharactersRepository charactersRepository,
        IntegrationsRepository integrationsRepository,
        CacheRepository cacheRepository,
        IntegrationsMaster integrationsMaster
    )
    {
        _discordClient = discordClient;
        _charactersRepository = charactersRepository;
        _integrationsRepository = integrationsRepository;
        _cacheRepository = cacheRepository;
        _integrationsMaster = integrationsMaster;
    }


    public Task HandleButton(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                await HandleButtonAsync(component);
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException or UserFriendlyException)
                {
                    return;
                }

                var guild = _discordClient.GetGuild((ulong)component.GuildId!);
                if (guild is null)
                {
                    return;
                }

                var traceId = NewTraceId();
                var owner = guild.Owner ?? await ((IGuild)guild).GetOwnerAsync();
                var title = $"🔘ButtonsHandler Exception [{component.User.Username}]";

                var header = $"Button: **{component.Data.CustomId}**\n" +
                             $"User: **{component.User.Username}** ({component.User.Id})\n" +
                             $"Channel: **{component.Channel.Name}** ({component.Channel.Id})\n" +
                             $"Guild: **{guild.Name}** ({guild.Id})\n" +
                             $"Owned by: **{owner?.Username}** ({owner?.Id})";

                await _discordClient.ReportErrorAsync(title, header, e, traceId, writeMetric: false);
                await InteractionsHelper.RespondWithErrorAsync(component, e, traceId);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

        var guildUser = (IGuildUser)component.User;
        var textChannel = (ITextChannel)component.Channel;

        _ = _cacheRepository.EnsureUserCached(guildUser);
        _ = _cacheRepository.EnsureChannelCached(textChannel);

        MetricsWriter.Write(MetricType.NewInteraction, guildUser.Id, $"{MetricUserSource.Button:G}:{textChannel.Id}:{textChannel.GuildId}", true);

        ValidationsHelper.ValidateInteraction(guildUser, textChannel);

        var actionType = GetActionType(component.Data.CustomId);

        await (actionType switch
        {
            ButtonActionType.SearchQuery => UpdateSearchQueryAsync(component)
        });
    }


    private static ButtonActionType GetActionType(string customId)
    {
        var i = customId.IndexOf(COMMAND_SEPARATOR, StringComparison.Ordinal);
        if (i == -1)
        {
            throw new ArgumentException($"Unknown Button Action Type: {customId}");
        }

        return customId[..i] switch
        {
            "sq" => ButtonActionType.SearchQuery,
            _ => throw new ArgumentOutOfRangeException()
        };
    }


    private async Task UpdateSearchQueryAsync(SocketMessageComponent component)
    {
        var sq = _cacheRepository.ActiveSearchQueries.Find(component.Message.Id);

        if (sq is null)
        {
            await component.ModifyOriginalResponseAsync(msg =>
            {
                var newEmbed = $"{MessagesTemplates.QUESTION_SIGN_DISCORD} Unobserved search request, try again".ToInlineEmbed(Color.Purple);
                msg.Embeds = new[] { newEmbed };
                msg.Components = null;
            });

            return;
        }

        var user = (IGuildUser)component.User;

        if (user.Id != sq.UserId && user.Guild.OwnerId != sq.UserId)
        {
            return;
        }

        var action = component.Data.CustomId.Replace($"sq{COMMAND_SEPARATOR}", string.Empty);
        switch (action)
        {
            case "up":
            {
                sq.CurrentRow = sq.CurrentRow == 1 ? Math.Min(sq.Characters.Count, 10) : sq.CurrentRow - 1;
                break;
            }
            case "down":
            {
                sq.CurrentRow = sq.CurrentRow == Math.Min(sq.Characters.Count, 10) ? 1 : sq.CurrentRow + 1;
                break;
            }
            case "left":
            {
                sq.CurrentPage = sq.CurrentPage == 1 ? sq.Pages : sq.CurrentPage - 1;
                break;
            }
            case "right":
            {
                sq.CurrentPage = sq.CurrentPage == sq.Pages ? 1 : sq.CurrentPage + 1;
                break;
            }
            case "select":
            {
                await ValidationsHelper.ValidateChannelPermissionsAsync(component.Channel);

                var modifyOriginalResponseAsync1 = component.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = MessagesTemplates.WAIT_MESSAGE;
                    msg.Components = null;
                });

                var channelId = (ulong)component.ChannelId!;
                var selectedCharacter = sq.SelectedCharacter;
                selectedCharacter.IntegrationType = sq.IntegrationType;

                var guildIntegration = await _integrationsRepository.GetGuildIntegrationAsync((ulong)component.GuildId!, selectedCharacter.IntegrationType);
                ArgumentNullException.ThrowIfNull(guildIntegration);

                var newSpawnedCharacter = await _integrationsMaster.SpawnCharacterAsync(channelId, selectedCharacter, guildIntegration);

                var embed = MH.BuildCharacterDescriptionCard(newSpawnedCharacter, justSpawned: true);
                var modifyOriginalResponseAsync2 = component.ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });

                var webhook = _cacheRepository.CachedWebhookClients.FindOrCreate(newSpawnedCharacter.WebhookId, newSpawnedCharacter.WebhookToken);
                var activeCharacter = new ActiveCharacterDecorator(newSpawnedCharacter, webhook);

                if (component.Channel is IThreadChannel)
                {
                    await activeCharacter.SendGreetingAsync(user.Mention, channelId);
                }
                else
                {
                    await activeCharacter.SendGreetingAsync(user.Mention);
                }

                await modifyOriginalResponseAsync1;
                await modifyOriginalResponseAsync2;

                _cacheRepository.ActiveSearchQueries.Remove(sq.MessageId);
                return;
            }
        }

        await component.ModifyOriginalResponseAsync(msg => { msg.Embed = MH.BuildSearchResultList(sq); });
    }

}
