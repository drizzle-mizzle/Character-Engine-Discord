using Discord.Interactions;
using Discord.WebSocket;
using SakuraAi;

namespace CharacterEngine.Api.SlashCommandsHandlers;


public class SakuraAiCommands : InteractionModuleBase<InteractionContext>
{
    public required DiscordSocketClient DiscordClient { get; set; }
    public required SakuraAiClient SakuraAiClient { get; set; }



}
