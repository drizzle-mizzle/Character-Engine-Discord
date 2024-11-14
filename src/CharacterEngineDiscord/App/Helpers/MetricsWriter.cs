using System.Text;
using CharacterEngine.App.Helpers.Discord;
using CharacterEngineDiscord.Models.Db;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace CharacterEngine.App.Helpers;


public static class MetricsWriter
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private static bool _locked;

    public static void Lock() { _locked = true; }

    public static void Unlock() { _locked = false; }


    public static void Create(MetricType metricType, object? entityId = null, string? payload = null)
    {
        Task.Run(async () =>
        {
            while (_locked)
            {
                // wait
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

            _log.Info(msg.ToString());

            await using var db = DatabaseHelper.GetDbContext();
            await db.Metrics.AddAsync(metric);
            await db.SaveChangesAsync();
        });
    }

}
