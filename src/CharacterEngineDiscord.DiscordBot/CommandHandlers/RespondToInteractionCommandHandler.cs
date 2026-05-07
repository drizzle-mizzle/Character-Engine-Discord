using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CharacterEngineDiscord.Contracts.Commands;
using CharacterEngineDiscord.Messaging.Handlers;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.DiscordBot.CommandHandlers;

/// <summary>
/// Sends a Discord interaction followup using the raw webhook endpoint
/// (<c>POST /webhooks/{application_id}/{interaction_token}</c>). The endpoint
/// accepts the interaction token as bearer and does NOT require bot authorization.
/// Going through HTTP directly side-steps the fact that the in-memory
/// <see cref="Discord.WebSocket.SocketSlashCommand"/> object that originally
/// received the interaction lives only on the Bot side and is gone by the
/// time the Server-issued command arrives.
/// </summary>
internal sealed class RespondToInteractionCommandHandler : ICeCommandHandler<RespondToInteractionCommand>
{
    private const string HttpClientName = "ce-discord-followup";
    private const int EphemeralFlag = 1 << 6; // Discord MESSAGE_FLAGS.EPHEMERAL

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RespondToInteractionCommandHandler> _logger;

    public RespondToInteractionCommandHandler(
        IHttpClientFactory httpFactory,
        ILogger<RespondToInteractionCommandHandler> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task HandleAsync(RespondToInteractionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var url = $"https://discord.com/api/v10/webhooks/{command.ApplicationId}/{command.InteractionToken}";

        var payload = new FollowupPayload
        {
            Content = command.Content,
            Flags = command.IsEphemeral ? EphemeralFlag : 0,
        };
        var json = JsonSerializer.Serialize(payload, _jsonOptions);

        using var http = _httpFactory.CreateClient(HttpClientName);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(url, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug(
                "[{Trace}] Interaction followup sent (guild {GuildId}, channel {ChannelId})",
                command.TraceId, command.OriginGuildId, command.OriginChannelId);
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "[{Trace}] Interaction token expired or unknown (404); discarding followup",
                command.TraceId);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "[{Trace}] Discord followup failed: {Status} {Body}",
            command.TraceId, (int)response.StatusCode, body);

        // Throwing causes the command consumer to nack-with-requeue, giving Discord a chance
        // to recover from transient 5xx blips before the 15-min token window closes.
        throw new InvalidOperationException(
            $"Discord followup HTTP {(int)response.StatusCode}");
    }

    private sealed class FollowupPayload
    {
        public required string Content { get; init; }
        public required int Flags { get; init; }
    }
}
