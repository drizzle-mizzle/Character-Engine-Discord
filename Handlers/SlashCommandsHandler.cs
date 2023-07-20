using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using CharacterAI;
using CharacterEngineDiscord.Services;
using static CharacterEngineDiscord.Services.CommonService;
using static CharacterEngineDiscord.Services.StorageContext;
using static CharacterEngineDiscord.Services.IntegrationsService;
using Microsoft.Extensions.DependencyInjection;

namespace CharacterEngineDiscord.Handlers
{
    public class SlashCommandsHandler
    {
        private readonly StorageContext _db;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IntegrationsService _integration;
        private readonly InteractionService _interactions;

        public SlashCommandsHandler(IServiceProvider services)
        {
            _services = services;
            _db = _services.GetRequiredService<StorageContext>();
            _integration = _services.GetRequiredService<IntegrationsService>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _client.SlashCommandExecuted += (command) =>
            {
                Task.Run(async () => await HandleCommandAsync(command));
                return Task.CompletedTask;
            };
        }

        internal async Task HandleCommandAsync(SocketSlashCommand command)
        {
            var context = new InteractionContext(_client, command);
            var result = await _interactions.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
            {
                LogException(new object?[] { result.ErrorReason, result.Error });
                await command.RespondAsync(embed: InlineEmbed($"{WARN_SIGN_DISCORD} Failed to execute command: `{result.ErrorReason}`", Color.Red));
            }
        }
    }
}
