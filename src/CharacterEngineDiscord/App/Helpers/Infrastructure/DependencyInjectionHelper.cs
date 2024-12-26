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
        services.AddSingleton<InteractionsHandler>();

        services.AddScoped<SlashCommandsHandler>();
        services.AddScoped<ButtonsHandler>();
        services.AddScoped<MessagesHandler>();
        services.AddScoped<ModalsHandler>();
        services.AddScoped<SpecialCommandsHandler>();
        services.AddScoped<AppDbContext>(_ => DatabaseHelper.GetDbContext());

        services.AddTransient<BotAdminCommandsHandler>();

        return services.BuildServiceProvider();;
    }

}
