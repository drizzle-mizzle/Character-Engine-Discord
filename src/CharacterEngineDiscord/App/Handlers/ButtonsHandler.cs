using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using NLog;

namespace CharacterEngine.App.Handlers;


public class ButtonsHandler
{
    private readonly ILogger _log;
    private readonly AppDbContext _db;

    private readonly DiscordSocketClient _discordClient;


    public ButtonsHandler(ILogger log, AppDbContext db, DiscordSocketClient discordClient)
    {
        _log = log;
        _db = db;

        _discordClient = discordClient;
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
                await _discordClient.ReportErrorAsync(e);
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

        if (component.Channel is not ITextChannel channel)
        {
            return;
        }

        await channel.EnsureExistInDbAsync();

        var actionType = GetActionType(component.Data.CustomId);
        await (actionType switch
        {
            ButtonActionType.SearchQuery => UpdateSearchQueryAsync(component)
        });
    }


    private static ButtonActionType GetActionType(string customId)
    {
        var i = customId.IndexOf(CommonHelper.COMMAND_SEPARATOR, StringComparison.Ordinal);
        if (i == -1)
        {
            throw new ArgumentException("Unknown Button Action Type");
        }

        return customId[..i] switch
        {
            "sq" => ButtonActionType.SearchQuery,
            _ => throw new ArgumentOutOfRangeException()
        };
    }


    private async Task UpdateSearchQueryAsync(SocketMessageComponent component)
    {
        // var sq = LocalStorage.SearchQueries.FirstOrDefault(sq => sq.Value.ChannelId == component.ChannelId && sq.Value.UserId == component.User.Id);
        var sq = StaticStorage.SearchQueries.GetByChannelId((ulong)component.ChannelId!);
        if (sq is null)
        {
            await component.ModifyOriginalResponseAsync(msg =>
            {
                var newEmbed = $"{MessagesTemplates.QUESTION_SIGN_DISCORD} Unobserved search request.".ToInlineEmbed(color: Color.Purple);
                msg.Embeds = new Optional<Embed[]>([msg.Embeds.Value.First(), newEmbed]);
            });
            return;
        }

        var canUpdate = sq.UserId == component.User.Id || sq.UserId == _discordClient.Guilds.First(g => g.Id == component.GuildId).OwnerId;
        if (!canUpdate)
        {
            return;
        }

        var action = component.Data.CustomId.Replace($"sq{CommonHelper.COMMAND_SEPARATOR}", string.Empty);
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
                await component.ModifyOriginalResponseAsync(msg => { msg.Embed = MessagesTemplates.WAIT_MESSAGE; });

                // Create character
                var newSpawnedCharacter = await InteractionsHelper.SpawnCharacterAsync(sq.ChannelId, sq.SelectedCharacter);
                StaticStorage.CachedCharacters.Add(newSpawnedCharacter);
                StaticStorage.SearchQueries.Remove(sq.ChannelId);

                // Cache webhook
                var webhookClient = new DiscordWebhookClient(newSpawnedCharacter.WebhookId, newSpawnedCharacter.WebhookToken);
                StaticStorage.CachedWebhookClients.Add(newSpawnedCharacter.WebhookId, webhookClient);

                var character = (ICharacter)newSpawnedCharacter;
                var characterDescription = MessagesHelper.BuildCharacterDescriptionCard(character);
                await component.ModifyOriginalResponseAsync(msg => { msg.Embed = characterDescription; });

                var greetedUser = ((IGuildUser)component.User).DisplayName;
                await character.SendGreetingAsync(greetedUser);

                return;
            }
        }

        await component.ModifyOriginalResponseAsync(msg => { msg.Embed = InteractionsHelper.BuildSearchResultList(sq); });
    }

}
