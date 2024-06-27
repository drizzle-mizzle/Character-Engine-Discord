using CharacterEngine.Api.Abstractions;
using CharacterEngine.Helpers.Discord;
using Discord.WebSocket;

namespace CharacterEngine.Api;


public class ButtonsHandler : HandlerBase
{
    public static async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

        var searchQuery = SearchQueries.FirstOrDefault(sq => sq.ChannelId == component.ChannelId && sq.UserId == component.User.Id);
        if (searchQuery is null)
        {
            return;
        }

        var bottomRow = Math.Min(searchQuery.Characters.Count, 10);
        switch (component.Data.CustomId)
        {
            case "up":
            {
                searchQuery.CurrentRow = searchQuery.CurrentRow == 1 ? bottomRow : searchQuery.CurrentRow - 1;
                break;
            }
            case "down":
            {
                searchQuery.CurrentRow = searchQuery.CurrentRow == bottomRow ? 1 : searchQuery.CurrentRow + 1;
                break;
            }
            case "left":
            {
                searchQuery.CurrentPage = searchQuery.CurrentPage == 1 ? searchQuery.Pages : searchQuery.CurrentPage - 1;
                break;
            }
            case "right":
            {
                searchQuery.CurrentPage = searchQuery.CurrentPage == searchQuery.Pages ? 1 : searchQuery.CurrentPage + 1;
                break;
            }
            case "select":
            {
                 break;
            }
        }

        await component.Message.ModifyAsync(m => m.Embed = DiscordCommandsHelper.BuildSearchResultList(searchQuery)).ConfigureAwait(false);
    }

}
