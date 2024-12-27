using CharacterEngine.App.Handlers;
using CharacterEngine.App.SlashCommands.Explicit;
using CharacterEngineDiscord.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngine.App.Helpers.Infrastructure;


public static class DependencyInjectionHelper
{
    public static ServiceProvider BuildServiceProvider(DiscordSocketClient discordClient, InteractionService interactionService)
    {
        var services = new ServiceCollection();

        services.AddSingleton(discordClient);
        services.AddSingleton(interactionService);
        services.AddSingleton<SlashCommandsHandler>();
        services.AddSingleton<InteractionsHandler>();
        services.AddSingleton<MessagesHandler>();

        services.AddTransient<SpecialCommandsHandler>();
        services.AddTransient<BotAdminCommandsHandler>();
        services.AddTransient<ButtonsHandler>();
        services.AddTransient<ModalsHandler>();

        services.AddScoped<AppDbContext>(_ => DatabaseHelper.GetDbContext());

        return services.BuildServiceProvider();;
    }

}
