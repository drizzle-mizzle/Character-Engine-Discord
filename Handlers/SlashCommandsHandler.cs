using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.IntegrationsService;
using static CharacterEngineDiscord.Services.CommandsService;
using Microsoft.Extensions.DependencyInjection;
using CharacterEngineDiscord.Services;

namespace CharacterEngineDiscord.Handlers
{
    public class SlashCommandsHandler
    {
        private readonly IDiscordClient _client;
        private readonly IServiceProvider _services;
        private readonly InteractionService _interactions;

        public SlashCommandsHandler(IServiceProvider services, IDiscordClient client)
        {
            _client = client;
            _services = services;
            _interactions = services.GetRequiredService<InteractionService>();    
        }


        public Task HandleCommand(SocketSlashCommand command)
        {
            Task.Run(async () =>
            {
                try { await HandleCommandAsync(command); }
                catch (Exception e) { HandleSlashCommandException(command, e); }
            });

            return Task.CompletedTask;
        }


        private async Task HandleCommandAsync(SocketSlashCommand command)
        {
            if (await UserIsBannedCheckOnly(command.User.Id)) return;

            var context = new InteractionContext(_client, command, command.Channel);
            await _interactions.ExecuteCommandAsync(context, _services);
        }


        private void HandleSlashCommandException(SocketSlashCommand command, Exception e)
        {
            LogException(new[] { e });
            var channel = command.Channel as SocketGuildChannel;
            var guild = channel?.Guild;

            List<string> commandParams = new();
            foreach (var option in command.Data.Options)
            {
                var val = option.Value.ToString() ?? "";
                int l = Math.Min(val.Length, 20);
                commandParams.Add($"{option.Name}:{val[0..l] + (val.Length > 20 ? "..." : "")}");
            }
            
            TryToReportInLogsChannel(_client, title: "Slash Command Exception",
                                              desc: $"In Guild `{guild?.Name} ({guild?.Id})`\n" +
                                                    $"Owner: `{guild?.Owner.GetBestName()} ({guild?.Owner.Username})`\n" +
                                                    $"Channel: `{channel?.Name} ({channel?.Id})`\n" +
                                                    $"User: `{command.User?.Username}`\n" +
                                                    $"Slash command: `/{command.CommandName}` `[{string.Join(" | ", commandParams)}]`",
                                              content: e.ToString(),
                                              color: Color.Red,
                                              error: true);
        }
    }
}
