using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Static;
using CharacterEngineDiscord.Models;
using Discord;
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
                var traceId = CommonHelper.NewTraceId();

                var guild = _discordClient.GetGuild((ulong)component.GuildId!);
                var owner = guild.Owner ?? await ((IGuild)guild).GetOwnerAsync();

                var content = $"Button: {component.Data.CustomId}\n" +
                              $"User: **{component.User.GlobalName ?? component.User.Username}** ({component.User.Id})\n" +
                              $"Channel: **{component.Channel.Name}** ({component.Channel.Id})\n" +
                              $"Guild: **{guild.Name}** ({guild.Id})\n" +
                              $"Owned by: **{owner?.DisplayName ?? owner?.Username}** ({owner?.Id})\n\n" +
                              $"Exception:\n{e}";

                await _discordClient.ReportErrorAsync("ButtonsHandler exception", content, traceId);

                await InteractionsHelper.RespondWithErrorAsync(component, e, traceId);
            }
        });

        return Task.CompletedTask;
    }


    private static async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

        InteractionsHelper.ValidateUser(component);

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
            throw new ArgumentException($"Unknown Button Action Type: {customId}");
        }

        return customId[..i] switch
        {
            "sq" => ButtonActionType.SearchQuery,
            _ => throw new ArgumentOutOfRangeException()
        };
    }


    private static async Task UpdateSearchQueryAsync(SocketMessageComponent component)
    {
        var sq = MemoryStorage.SearchQueries.GetByChannelId(component.ChannelId!.Value);

        if (sq is null)
        {
            await component.ModifyOriginalResponseAsync(msg =>
            {
                var newEmbed = $"{MessagesTemplates.QUESTION_SIGN_DISCORD} Unobserved search request, try again".ToInlineEmbed(Color.Purple);
                msg.Embeds = !msg.Embeds.IsSpecified ? [msg.Embeds.Value!.First(), newEmbed] : new[] { newEmbed };
            });
            return;
        }

        var user = (IGuildUser)component.User;

        if (user.Id != sq.UserId && user.Guild.OwnerId != sq.UserId)
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
                var modifyOriginalResponseAsync1 = component.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = MessagesTemplates.WAIT_MESSAGE;
                    msg.Components = null;
                });

                var newSpawnedCharacter = await InteractionsHelper.SpawnCharacterAsync(sq.ChannelId, sq.SelectedCharacter);

                var embed = await MH.BuildCharacterDescriptionCardAsync(newSpawnedCharacter, justSpawned: true);
                var modifyOriginalResponseAsync2 = component.ModifyOriginalResponseAsync(msg => { msg.Embed = embed; });

                await newSpawnedCharacter.SendGreetingAsync(user.DisplayName ?? user.Username);
                await modifyOriginalResponseAsync1;
                await modifyOriginalResponseAsync2;

                MemoryStorage.SearchQueries.Remove(sq.ChannelId);
                return;
            }
        }

        await component.ModifyOriginalResponseAsync(msg => { msg.Embed = MH.BuildSearchResultList(sq); });
    }

}
