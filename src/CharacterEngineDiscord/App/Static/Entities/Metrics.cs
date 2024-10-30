using System.Collections.Concurrent;
using CharacterEngine.App.Helpers;
using CharacterEngineDiscord.Models.Db;
using NLog;

namespace CharacterEngine.App.Static.Entities;


public sealed class MetricsWriter
{
    private readonly ConcurrentDictionary<Guid, Metric> _metricsCache = [];
    private readonly Logger _log = LogManager.GetCurrentClassLogger();


    public void Create(MetricType metricType, string entityId, string payload)
    {
        var metric = new Metric
        {
            MetricType = metricType,
            EntityId = entityId,
            Payload = payload,
            CreatedAt = DateTime.Now
        };

        _metricsCache.TryAdd(metric.Id, metric);
    }


    public void WriteToDb()
    {
        lock (_metricsCache)
        {
            var msg = $"Writing {_metricsCache.Count} metrics to db";

            _log.Info($"[Start] {msg}");

            using var db = DatabaseHelper.GetDbContext();
            db.Metrics.AddRange(_metricsCache.Values);
            db.SaveChanges();

            _metricsCache.Clear();

            _log.Info($"[End] {msg}");
        }
    }

}
