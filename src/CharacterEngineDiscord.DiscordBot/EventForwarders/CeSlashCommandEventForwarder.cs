using System.Collections.Immutable;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Helpers;
using CharacterEngineDiscord.Messaging.Abstractions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace CharacterEngineDiscord.DiscordBot.EventForwarders;

/// <summary>
/// Translates a Discord <see cref="SocketSlashCommand"/> gateway event into a
/// <see cref="SlashCommandInvokedRequest"/> on the request bus. The interaction is
/// deferred BEFORE publication so the 3-second ack window is respected; the resulting
/// 15-minute followup window is what <see cref="SlashCommandInvokedRequest.InteractionToken"/>
/// gives the Server access to.
/// </summary>
internal sealed class CeSlashCommandEventForwarder
{
    private readonly ICeMessagePublisher _publisher;
    private readonly ILogger<CeSlashCommandEventForwarder> _logger;

    public CeSlashCommandEventForwarder(
        ICeMessagePublisher publisher,
        ILogger<CeSlashCommandEventForwarder> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task OnSlashCommandExecutedAsync(SocketSlashCommand interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var traceId = TraceId.New();

        // 1. Acknowledge inside Discord's 3-second ack window.
        try
        {
            await interaction.DeferAsync(ephemeral: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Trace}] DeferAsync failed for /{Command}; aborting publish", traceId, interaction.CommandName);
            return;
        }

        // 2. Publish to Server.
        try
        {
            var request = new SlashCommandInvokedRequest
            {
                TraceId = traceId,
                MessageId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                CommandName = interaction.CommandName,
                ApplicationId = interaction.ApplicationId,
                GuildId = interaction.GuildId.GetValueOrDefault(),
                ChannelId = interaction.ChannelId.GetValueOrDefault(),
                UserId = interaction.User.Id,
                Username = interaction.User.Username,
                InteractionId = interaction.Id,
                InteractionToken = interaction.Token,
                Options = ExtractOptions(interaction),
            };

            await _publisher.PublishRequestAsync(request);

            _logger.LogDebug(
                "[{Trace}] Published SlashCommandInvokedRequest /{Command} (user {UserId}, guild {GuildId})",
                traceId, request.CommandName, request.UserId, request.GuildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Trace}] Publish failed; sending best-effort error followup", traceId);
            try
            {
                await interaction.FollowupAsync("Internal error; please retry.", ephemeral: true);
            }
            catch (Exception followupEx)
            {
                _logger.LogDebug(followupEx, "[{Trace}] Best-effort error followup also failed", traceId);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ExtractOptions(SocketSlashCommand interaction)
    {
        var options = interaction.Data.Options;
        if (options is null || options.Count == 0)
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        var dict = new Dictionary<string, string>(options.Count, StringComparer.Ordinal);
        foreach (var opt in options)
        {
            dict[opt.Name] = opt.Value?.ToString() ?? string.Empty;
        }
        return dict;
    }
}
