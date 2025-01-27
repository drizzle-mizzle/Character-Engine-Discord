using System.Text;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Domain.Models.Db;
using NLog;

namespace CharacterEngine.App.Helpers;


public static class MetricsWriter
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private static DateTime _lastMetricReport;


    public static DateTime GetLastMetricReport()
        => _lastMetricReport;

    public static void SetLastMetricReport(DateTime lastMetricReport)
    {
        _lastMetricReport = lastMetricReport;
    }


    private static bool _locked;

    public static void LockWrite() { _locked = true; }

    public static void UnlockWrite() { _locked = false; }


    public static void Create(MetricType metricType, object? entityId = null, string? payload = null, bool silent = false)
    {
        Task.Run(async () =>
        {
            _ = await CreateAsync(metricType, entityId, payload, silent);            
        });
    }

    public static async Task<Guid> CreateAsync(MetricType metricType, object? entityId = null, string? payload = null, bool silent = false)
    {
        while (_locked)
        {
            await Task.Delay(500);
        }

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

        await using var db = DatabaseHelper.GetDbContext();
        db.Metrics.Add(metric);

        await db.SaveChangesAsync();

        return metric.Id;
    }

}
