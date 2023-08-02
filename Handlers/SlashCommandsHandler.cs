using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Services;
using System.ComponentModel;

namespace CharacterEngineDiscord.Handlers
{
    public class SlashCommandsHandler
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        //private readonly IntegrationsService _integration;

        public SlashCommandsHandler(IServiceProvider services)
        {
            _services = services;
            //_integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.SlashCommandExecuted += (command) =>
            {
                Task.Run(async () =>
                {
                    try { await HandleCommandAsync(command); }
                    catch (Exception e)
                    {
                        LogException(new[] { e });
                        var channel = command.Channel as SocketGuildChannel;
                        var guild = channel?.Guild;
                        await TryToReportInLogsChannel(_client, title: "Exception",
                                                                desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                                      $"User: {command.User?.Username}\n" +
                                                                      $"Slash command: {command.CommandName}\n" +
                                                                      $"```cs\n" +
                                                                      $"{e}\n" +
                                                                      $"```",
                                                                color: Color.Red);
                    }
                });
                return Task.CompletedTask;
            };

            _interactions.InteractionExecuted += (info, context, result) =>
            {
                Task.Run(async () =>
                {
                    if (!result.IsSuccess)
                    {
                        LogException(new object?[] { result.ErrorReason, result.Error });
                        try { await context.Interaction.RespondAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{result.ErrorReason}`".ToInlineEmbed(Color.Red)); }
                        catch { await context.Interaction.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{result.ErrorReason}`".ToInlineEmbed(Color.Red)); }
                    }
                });
                return Task.CompletedTask;
            };
        }

        internal async Task HandleCommandAsync(SocketSlashCommand command)
        {
            if (await UserIsBannedCheckOnly(command.User)) return;

            var context = new InteractionContext(_client, command, command.Channel);
            await _interactions.ExecuteCommandAsync(context, _services);
        }
    }
}
