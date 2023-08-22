using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Services;
using CharacterEngineDiscord.Models.Database;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Discord.Commands;
using static System.Net.Mime.MediaTypeNames;

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
                    catch (Exception e) { await HandleSlashCommandException(command, e); }
                });
                return Task.CompletedTask;
            };

            _interactions.InteractionExecuted += (info, context, result) =>
            {
                if (!result.IsSuccess)
                    Task.Run(async () => await HandleInteractionException(context, result));
                
                return Task.CompletedTask;
            };
        }

        private async Task HandleCommandAsync(SocketSlashCommand command)
        {
            if (await UserIsBannedCheckOnly(command.User.Id)) return;

            var context = new InteractionContext(_client, command, command.Channel);
            await _interactions.ExecuteCommandAsync(context, _services);
        }


        private async Task HandleInteractionException(IInteractionContext context, Discord.Interactions.IResult result)
        {
            try { await context.Interaction.RespondAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{result.ErrorReason}`".ToInlineEmbed(Color.Red)); }
            catch { await context.Interaction.FollowupAsync(embed: $"{WARN_SIGN_DISCORD} Failed to execute command: `{result.ErrorReason}`".ToInlineEmbed(Color.Red)); }

            var channel = context.Channel;
            var guild = context.Guild;

            if (result.Error.GetValueOrDefault().ToString().Contains("UnmetPrecondition")) return;

            var originalResponse = await context.Interaction.GetOriginalResponseAsync();
            var owner = (await guild.GetOwnerAsync()) as SocketGuildUser;

            await TryToReportInLogsChannel(_client, title: "Command Exception",
                                                    desc: $"Guild: `{guild.Name} ({guild.Id})`\n" +
                                                          $"Owner: `{owner?.GetBestName()} ({owner?.Username})`\n" +
                                                          $"Channel: `{channel.Name} ({channel.Id})`\n" +
                                                          $"User: `{context.User.Username}`\n" +
                                                          $"Command: `{originalResponse.Interaction.Name} ({originalResponse.Interaction.Id})`",
                                                    content: $"{result.ErrorReason}\n\n{result.Error.GetValueOrDefault()}",
                                                    color: Color.Red,
                                                    error: true);
        }

        private async Task HandleSlashCommandException(SocketSlashCommand command, Exception e)
        {
            LogException(new[] { e });
            var channel = command.Channel as SocketGuildChannel;
            var guild = channel?.Guild;
            await TryToReportInLogsChannel(_client, title: "Exception",
                                                    desc: $"In Guild `{guild?.Name} ({guild?.Id})`, Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                          $"User: {command.User?.Username}\n" +
                                                          $"Slash command: {command.CommandName}",
                                                    content: e.ToString(),
                                                    color: Color.Red,
                                                    error: true);
        }
    }
}
