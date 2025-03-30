using System.Text;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngine.App.Infrastructure;
using CharacterEngineDiscord.Domain.Models.Db;
using CharacterEngineDiscord.Models;
using NLog;

namespace CharacterEngine.App.Services;


public static class MetricsWriter
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private static DateTime _lastAutoMetricReport = DateTime.Now;


    public static DateTime GetLastAutoMetricReport()
        => _lastAutoMetricReport;

    public static void SetLastAutoMetricReport(DateTime lastMetricReport)
    {
        _lastAutoMetricReport = lastMetricReport;
    }


    public static void Write(MetricType metricType, object? entityId = null, string? payload = null, bool silent = false)
    {
        _ = WriteAsync(metricType, entityId, payload, silent);
    }


    public static async Task<Guid> WriteAsync(MetricType metricType, object? entityId = null, string? payload = null, bool silent = false)
    {
        var metric = new Metric
        {
            MetricType = metricType,
            EntityId = entityId?.ToString(),
            Payload = payload,
            CreatedAt = DateTime.Now
        };

        var type = metricType.ToString("G").SplitWordsBySep(' ').ToUpperInvariant();
        var msg = new StringBuilder($"[Metric] {type} ");

        if (entityId is not null)
        {
            msg.Append($" {entityId}");
        }

        if (payload is not null)
        {
            msg.Append($" | {payload}");
        }

        if (!silent)
        {
            _log.Info(msg.ToString());
        }

        await using var db = new AppDbContext(BotConfig.DATABASE_CONNECTION_STRING);
        db.Metrics.Add(metric);
        await db.SaveChangesAsync();

        return metric.Id;
    }

}
