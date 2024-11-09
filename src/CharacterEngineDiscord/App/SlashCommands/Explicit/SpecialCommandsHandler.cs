using CharacterEngine.App.Helpers;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Helpers.Infrastructure;
using CharacterEngineDiscord.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CharacterEngine.App.SlashCommands.Explicit;


public class SpecialCommandsHandler
{
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactions;


    public SpecialCommandsHandler(DiscordSocketClient discordClient, InteractionService interactions)
    {
        _discordClient = discordClient;
        _interactions = interactions;
    }


    public async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        await command.RespondAsync(embed: MessagesTemplates.WAIT_MESSAGE);

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var disableCommand = ExplicitCommandBuilders.BuildDisableCommand();

        var registerCommandsToGuildAsync = _interactions.RegisterCommandsToGuildAsync(guild.Id);
        var createApplicationCommandAsync = guild.CreateApplicationCommandAsync(disableCommand);

        var integrations = Enum.GetValues<IntegrationType>()
                               .Select(i => $"- [{i.GetIcon()} **{i:G}**]({i.GetServiceLink()})")
                               .ToArray();

        var message = "**Thank you for using Character Engine**\n\n" +
                      "**Character Engine** is a powerful aggregator of various online platforms that provide access to LLM chat-bots, it allows you to create AI-driven characters " +
                      "based on [Discord Webhooks](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks) and designed to help you bring some life on your server!\n\n" +
                      "**List of supported platforms:**\n" +
                      $"{string.Join('\n', integrations)}" +
                      "\n\nUse *`/integration create`* command to begin. *Please not that you need to have an existing account on chosen platform in order to create a server integration for it.*\n\n" +
                      $"Questions, bug reports, suggestions and any other feedback: {BotConfig.ADMIN_GUILD_INVITE_LINK}"; // TODO: banner


        await registerCommandsToGuildAsync;
        await createApplicationCommandAsync;

        await command.FollowupAsync(embed: message.ToInlineEmbed(Color.Gold, bold: false));
    }


    public async Task HandleDisableCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var guild = _discordClient.Guilds.First(g => g.Id == command.GuildId);
        var startCommand = ExplicitCommandBuilders.BuildStartCommand();

        await guild.DeleteApplicationCommandsAsync();
        await guild.CreateApplicationCommandAsync(startCommand);

        await command.FollowupAsync("bye D:");
    }
}
