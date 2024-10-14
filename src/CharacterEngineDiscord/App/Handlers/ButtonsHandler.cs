using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace CharacterEngine.App.Handlers;


public class ButtonsHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;
    private readonly AppDbContext _db;

    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public ButtonsHandler(IServiceProvider serviceProvider, ILogger log, AppDbContext db, DiscordSocketClient discordClient, InteractionService interactions)
    {
        _serviceProvider = serviceProvider;
        _log = log;
        _db = db;

        _discordClient = discordClient;
        _interactions = interactions;
    }


    public Task HandleButton(SocketMessageComponent component)
        => Task.Run(async () =>
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


    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

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
            return ButtonActionType.Unknown;
        }

        return customId[..i] switch
        {
            "sq" => ButtonActionType.SearchQuery,

            _ => ButtonActionType.Unknown
        };
    }


    private async Task UpdateSearchQueryAsync(SocketMessageComponent component)
    {
        // var sq = LocalStorage.SearchQueries.FirstOrDefault(sq => sq.Value.ChannelId == component.ChannelId && sq.Value.UserId == component.User.Id);
        var sq = RuntimeStorage.SearchQueries.GetByChannelId((ulong)component.ChannelId!);
        if (sq is null)
        {
            await component.ModifyOriginalResponseAsync(msg =>
            {
                var newEmbed = $"{MessagesTemplates.QUESTION_SIGN_DISCORD} Unobserved search request.".ToInlineEmbed(color: Color.Purple);
                msg.Embeds = new Optional<Embed[]>([msg.Embeds.Value.First(), newEmbed]);
            }).ConfigureAwait(false);
            return;
        }

        if (sq.UserId != component.User.Id)
        {
            var isManager = await _db.Managers.AnyAsync(m => m.UserId == component.User.Id && m.GuildId == component.GuildId);
            if (!isManager)
            {
                return;
            }
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
                var spawnedCharacter = await InteractionsHelper.SpawnCharacterAsync(sq.ChannelId, sq.SelectedCharacter);

                var characterMessage = sq.SelectedCharacter
                                         .FirstMessage
                                         .Replace("{{char}}", sq.SelectedCharacter.Name)
                                         .Replace("{{user}}", $"**{(component.User as IGuildUser)!.DisplayName}**");

                await component.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = MessagesHelper.BuildCharacterDescriptionCard(spawnedCharacter);
                });
                RuntimeStorage.SearchQueries.Remove(sq.ChannelId);

                var webhookClient = new DiscordWebhookClient(spawnedCharacter.WebhookId, spawnedCharacter.WebhookToken);
                RuntimeStorage.WebhookClients.Add(spawnedCharacter.WebhookId, webhookClient);

                await webhookClient.SendMessageAsync(characterMessage);

                return;
            }
        }

        await component.ModifyOriginalResponseAsync(msg => { msg.Embed = InteractionsHelper.BuildSearchResultList(sq); }).ConfigureAwait(false);
    }
}
