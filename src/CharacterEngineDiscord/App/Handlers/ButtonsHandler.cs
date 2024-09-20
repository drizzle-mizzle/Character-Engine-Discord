using CharacterEngine.Helpers.Discord;
using CharacterEngineDiscord.Db;
using Discord.WebSocket;

namespace CharacterEngine.App.Handlers;


public class ButtonsHandler
{
    public required LocalStorage LocalStorage { get; set; }
    public required DiscordSocketClient DiscordClient { get; set; }
    public required AppDbContext db { get; set; }



    public async Task HandleButtonAsync(SocketMessageComponent component)
    {
        await component.DeferAsync();

        var sq = LocalStorage.SearchQueries.FirstOrDefault(sq => sq.ChannelId == component.ChannelId && sq.UserId == component.User.Id);
        if (sq is null)
        {
            return;
        }

        var bottomRow = Math.Min(sq.Characters.Count, 10);
        switch (component.Data.CustomId)
        {
            case "up":
            {
                sq.CurrentRow = sq.CurrentRow == 1 ? bottomRow : sq.CurrentRow - 1;
                break;
            }
            case "down":
            {
                sq.CurrentRow = sq.CurrentRow == bottomRow ? 1 : sq.CurrentRow + 1;
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
                 break;
            }
        }

        await component.Message.ModifyAsync(m => { m.Embed = DiscordInteractionsHelper.BuildSearchResultList(sq); }).ConfigureAwait(false);
    }

}
