using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Helpers.Common;
using CharacterEngineDiscord.Models;
using CharacterEngineDiscord.Models.Abstractions;
using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using MH = CharacterEngine.App.Helpers.Discord.MessagesHelper;

namespace CharacterEngine.App.Handlers;


public class ButtonsHandler
{
    private readonly DiscordSocketClient _discordClient;


    public ButtonsHandler(DiscordSocketClient discordClient)
    {
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
                await _discordClient.ReportErrorAsync(e, CommonHelper.NewTraceId());
            }
        });

        return Task.CompletedTask;
    }


    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

        if (component.Channel is not ITextChannel channel)
        {
            _ = component.FollowupAsync();
            return;
        }

        var validationResult = await WatchDog.ValidateAsync(component.User.Id, channel.GuildId);
        if (validationResult is WatchDogValidationResult.Blocked)
        {
            _ = component.FollowupAsync();
            return;
        }

        if (validationResult is WatchDogValidationResult.Warning)
        {
            await channel.SendMessageAsync(embed: MessagesTemplates.RATE_LIMIT_WARNING);
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
        var i = customId.IndexOf(InteractionsHelper.COMMAND_SEPARATOR, StringComparison.Ordinal);
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
        var sq = MemoryStorage.SearchQueries.GetByChannelId((ulong)component.ChannelId!);
        if (sq is null)
        {
            await component.ModifyOriginalResponseAsync(msg =>
            {
                var newEmbed = $"{MessagesTemplates.QUESTION_SIGN_DISCORD} Unobserved search request.".ToInlineEmbed(color: Color.Purple);
                msg.Embeds = new[] { msg.Embeds.Value.First(), newEmbed };
            });
            return;
        }

        var canUpdate = sq.UserId == component.User.Id || sq.UserId == _discordClient.Guilds.First(g => g.Id == component.GuildId).OwnerId;
        if (!canUpdate)
        {
            return;
        }

        var action = component.Data.CustomId.Replace($"sq{InteractionsHelper.COMMAND_SEPARATOR}", string.Empty);
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
                MemoryStorage.CachedCharacters.Add(newSpawnedCharacter);
                MemoryStorage.SearchQueries.Remove(sq.ChannelId);

                // Cache webhook
                var webhookClient = new DiscordWebhookClient(newSpawnedCharacter.WebhookId, newSpawnedCharacter.WebhookToken);
                MemoryStorage.CachedWebhookClients.Add(newSpawnedCharacter.WebhookId, webhookClient);

                var character = (ICharacter)newSpawnedCharacter;
                var characterDescription = MH.BuildCharacterDescriptionCard(character);
                await component.ModifyOriginalResponseAsync(msg => { msg.Embed = characterDescription; });

                var greetedUser = ((IGuildUser)component.User).DisplayName;
                await character.SendGreetingAsync(greetedUser);

                return;
            }
        }

        await component.ModifyOriginalResponseAsync(msg => { msg.Embed = InteractionsHelper.BuildSearchResultList(sq); });
    }

}
