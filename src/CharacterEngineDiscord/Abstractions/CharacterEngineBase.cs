using CharacterEngine.Helpers.Common;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi;

namespace CharacterEngine.Abstractions;


public abstract class CharacterEngineBase
{
    protected static readonly IServiceProvider Services = ServiceCollectionHelper.BuildServiceProvider();
    protected static readonly DiscordSocketClient DiscordClient = Services.GetRequiredService<DiscordSocketClient>();
    protected static readonly InteractionService Interactions = Services.GetRequiredService<InteractionService>();
    protected static readonly SakuraAiClient SakuraAiClient = Services.GetRequiredService<SakuraAiClient>();

    protected static readonly Logger log = LogManager.GetCurrentClassLogger();
}
