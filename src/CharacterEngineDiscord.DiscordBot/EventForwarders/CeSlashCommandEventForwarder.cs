using System.Collections.Immutable;
using System.Globalization;
using CharacterEngineDiscord.Contracts.Requests;
using CharacterEngineDiscord.Core.Abstractions.Logging;
using CharacterEngineDiscord.Core.Helpers;
using CharacterEngineDiscord.DiscordBot.RateLimiting;
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
    private readonly ICeWatchDog _watchDog;
    private readonly IDiscordLogger _discordLogger;
    private readonly ILogger<CeSlashCommandEventForwarder> _logger;

    public CeSlashCommandEventForwarder(
        ICeMessagePublisher publisher,
        ICeWatchDog watchDog,
        IDiscordLogger discordLogger,
        ILogger<CeSlashCommandEventForwarder> logger)
    {
        _publisher = publisher;
        _watchDog = watchDog;
        _discordLogger = discordLogger;
        _logger = logger;
    }

    public async Task OnSlashCommandExecutedAsync(SocketSlashCommand interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        // Rate-limit check BEFORE DeferAsync — if rejected, reply with an ephemeral
        // message inside the 3s window so the user sees feedback. We must NOT defer
        // first, because deferring locks us into a 15-minute followup flow.
        var decision = _watchDog.Check(interaction.User.Id);
        if (!decision.IsAllowed)
        {
            var reason = BuildRateLimitMessage(decision);
            try
            {
                await interaction.RespondAsync(reason, ephemeral: true);
            }
            catch (Exception ex)
            {
                // If RespondAsync fails (e.g. interaction already expired), there is nothing
                // useful we can do — log at Debug because rate-limit feedback is best-effort.
                _logger.LogDebug(ex, "Rate-limit response failed for user {UserId}", interaction.User.Id);
            }

            // Notify admins exactly once per ban transition. JustBlocked is true ONLY when
            // this Check call flipped the user from allowed -> blocked; subsequent rejected
            // attempts during the same block window leave it false, so we don't spam.
            if (decision.JustBlocked)
            {
                await NotifyAdminOfBlockAsync(interaction, decision, cancellationToken: default);
            }
            return;
        }

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

    private async Task NotifyAdminOfBlockAsync(
        SocketSlashCommand interaction,
        RateLimitDecision decision,
        CancellationToken cancellationToken)
    {
        try
        {
            var blockedUntilStr = decision.BlockedUntil.HasValue
                ? decision.BlockedUntil.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                : "unknown";

            var entry = new DiscordLogEntry
            {
                Title = "User auto-blocked by WatchDog",
                Message = $"User: **{interaction.User.Username}** ({interaction.User.Id})\n"
                          + $"Guild: {interaction.GuildId?.ToString(CultureInfo.InvariantCulture) ?? "(DM)"}\n"
                          + $"Channel: {interaction.ChannelId?.ToString(CultureInfo.InvariantCulture) ?? "(unknown)"}\n"
                          + $"Last command: /{interaction.CommandName}\n"
                          + $"Blocked until: **{blockedUntilStr}**",
                TraceId = TraceId.New(),
            };

            // LogLevel.Warning routes to LogsChannelId (not ErrorsChannelId) inside CeDiscordLogger.
            await _discordLogger.ReportAsync(entry, LogLevel.Warning, cancellationToken);
        }
        catch (Exception ex)
        {
            // Auto-block notification is best-effort: a failure here must NEVER crash the
            // forwarder, since the user-side ephemeral reply has already been sent.
            _logger.LogError(ex, "Failed to report user auto-block to admin channel");
        }
    }

    private static string BuildRateLimitMessage(RateLimitDecision decision)
    {
        if (decision.BlockedUntil is null)
        {
            return "You're being rate-limited. Try again in a moment.";
        }

        // Use Discord's <t:UNIX:R> token so each user sees a localised "in N minutes" relative
        // time without us having to compute or format it ourselves.
        var unix = ((DateTimeOffset)decision.BlockedUntil.Value).ToUnixTimeSeconds();
        return $"You're rate-limited until <t:{unix}:R>.";
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
