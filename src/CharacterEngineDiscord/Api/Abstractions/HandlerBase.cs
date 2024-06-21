using CharacterEngine.Database;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SakuraAi;

namespace CharacterEngine.Api.Abstractions;


public abstract class HandlerBase
{
    protected static readonly Logger log = LogManager.GetCurrentClassLogger();

    protected static AppDbContext db { get; private set; } = null!;
    protected static IServiceProvider Services { get; private set; } = null!;
    protected static InteractionService Interactions { get; private set; } = null!;
    protected static DiscordSocketClient DiscordClient { get; private set; } = null!;
    protected static SakuraAiClient SakuraAiClient { get; private set; } = null!;


    public static void Inject(IServiceProvider serviceProvider)
    {
        db = serviceProvider.GetRequiredService<AppDbContext>();
        Services = serviceProvider;
        Interactions = serviceProvider.GetRequiredService<InteractionService>();
        DiscordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
        SakuraAiClient = serviceProvider.GetRequiredService<SakuraAiClient>();
    }
}
