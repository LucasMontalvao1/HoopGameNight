using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace HoopGameNight.Infrastructure.Services
{
    public interface ISyncMetricsService
    {
        void RecordSyncEvent(string syncType, bool success, TimeSpan duration, int? recordsProcessed = null);
        SyncMetrics GetMetrics();
        SyncMetrics GetMetricsByType(string syncType);
        void ResetMetrics();
        List<SyncAlert> GetAlerts();
    }

    public class SyncMetricsService : ISyncMetricsService
    {
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, SyncMetrics> _metricsByType = new();
        private readonly SyncMetrics _globalMetrics = new();
        private readonly List<SyncAlert> _alerts = new();
        private const string METRICS_KEY = "sync_metrics";

        public SyncMetricsService(IMemoryCache cache)
        {
            _cache = cache;
            LoadMetricsFromCache();
        }

        public void RecordSyncEvent(string syncType, bool success, TimeSpan duration, int? recordsProcessed = null)
        {
            var syncEvent = new SyncEvent
            {
                Type = syncType,
                Success = success,
                Duration = duration,
                Timestamp = DateTime.UtcNow,
                RecordsProcessed = recordsProcessed ?? 0
            };

            // Atualizar métricas globais
            UpdateMetrics(_globalMetrics, syncEvent);

            // Atualizar métricas por tipo
            var typeMetrics = _metricsByType.GetOrAdd(syncType, _ => new SyncMetrics { Type = syncType });
            UpdateMetrics(typeMetrics, syncEvent);

            // Verificar alertas
            CheckForAlerts(syncEvent);

            // Salvar no cache
            SaveMetricsToCache();
        }

        public SyncMetrics GetMetrics()
        {
            _globalMetrics.MetricsByType = _metricsByType.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Clone()
            );
            return _globalMetrics.Clone();
        }

        public SyncMetrics GetMetricsByType(string syncType)
        {
            return _metricsByType.TryGetValue(syncType, out var metrics)
                ? metrics.Clone()
                : new SyncMetrics { Type = syncType };
        }

        public void ResetMetrics()
        {
            _globalMetrics.Reset();
            _metricsByType.Clear();
            _alerts.Clear();
            _cache.Remove(METRICS_KEY);
        }

        public List<SyncAlert> GetAlerts()
        {
            // Limpar alertas antigos (mais de 24h)
            _alerts.RemoveAll(a => a.Timestamp < DateTime.UtcNow.AddHours(-24));
            return _alerts.ToList();
        }

        private void UpdateMetrics(SyncMetrics metrics, SyncEvent syncEvent)
        {
            metrics.Events.Add(syncEvent);
            metrics.TotalSyncs++;

            if (syncEvent.Success)
            {
                metrics.SuccessfulSyncs++;
                metrics.LastSuccessfulSync = syncEvent.Timestamp;
                metrics.TotalRecordsProcessed += syncEvent.RecordsProcessed;
            }
            else
            {
                metrics.FailedSyncs++;
                metrics.ConsecutiveFailures++;
            }

            // Resetar falhas consecutivas em caso de sucesso
            if (syncEvent.Success)
            {
                metrics.ConsecutiveFailures = 0;
            }

            // Calcular média de duração
            metrics.AverageDuration = TimeSpan.FromMilliseconds(
                metrics.Events.Average(e => e.Duration.TotalMilliseconds)
            );

            // Atualizar min/max duração
            if (metrics.MinDuration == TimeSpan.Zero || syncEvent.Duration < metrics.MinDuration)
                metrics.MinDuration = syncEvent.Duration;

            if (syncEvent.Duration > metrics.MaxDuration)
                metrics.MaxDuration = syncEvent.Duration;

            metrics.LastSyncTime = syncEvent.Timestamp;

            // Manter apenas últimos 100 eventos
            if (metrics.Events.Count > 100)
            {
                metrics.Events = metrics.Events
                    .OrderByDescending(e => e.Timestamp)
                    .Take(100)
                    .ToList();
            }
        }

        private void CheckForAlerts(SyncEvent syncEvent)
        {
            // Alerta de falha
            if (!syncEvent.Success)
            {
                _alerts.Add(new SyncAlert
                {
                    Type = AlertType.SyncFailure,
                    Severity = AlertSeverity.Warning,
                    Message = $"Sync failed for {syncEvent.Type}",
                    Details = $"Duration: {syncEvent.Duration.TotalSeconds:F2}s",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Alerta de performance
            if (syncEvent.Duration > TimeSpan.FromMinutes(5))
            {
                _alerts.Add(new SyncAlert
                {
                    Type = AlertType.SlowSync,
                    Severity = AlertSeverity.Warning,
                    Message = $"Slow sync detected for {syncEvent.Type}",
                    Details = $"Duration: {syncEvent.Duration.TotalMinutes:F2} minutes",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Alerta de falhas consecutivas
            var typeMetrics = GetMetricsByType(syncEvent.Type);
            if (typeMetrics.ConsecutiveFailures >= 3)
            {
                _alerts.Add(new SyncAlert
                {
                    Type = AlertType.ConsecutiveFailures,
                    Severity = AlertSeverity.Critical,
                    Message = $"Multiple consecutive failures for {syncEvent.Type}",
                    Details = $"{typeMetrics.ConsecutiveFailures} failures in a row",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private void LoadMetricsFromCache()
        {
            var cached = _cache.Get<SyncMetricsCache>(METRICS_KEY);
            if (cached != null)
            {
                _globalMetrics.LoadFrom(cached.GlobalMetrics);
                foreach (var kvp in cached.MetricsByType)
                {
                    _metricsByType[kvp.Key] = kvp.Value;
                }
                _alerts.AddRange(cached.Alerts);
            }
        }

        private void SaveMetricsToCache()
        {
            var cache = new SyncMetricsCache
            {
                GlobalMetrics = _globalMetrics,
                MetricsByType = _metricsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Alerts = _alerts
            };

            _cache.Set(METRICS_KEY, cache, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(7)
            });
        }
    }

    public class SyncMetrics
    {
        public string Type { get; set; } = "Global";
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncTime { get; set; }
        public DateTime? LastSuccessfulSync { get; set; }
        public int TotalSyncs { get; set; }
        public int SuccessfulSyncs { get; set; }
        public int FailedSyncs { get; set; }
        public int ConsecutiveFailures { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public int TotalRecordsProcessed { get; set; }
        public List<SyncEvent> Events { get; set; } = new();
        public Dictionary<string, SyncMetrics> MetricsByType { get; set; } = new();

        public double SuccessRate => TotalSyncs > 0
            ? (double)SuccessfulSyncs / TotalSyncs * 100
            : 0;

        public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

        public double AverageRecordsPerSync => TotalSyncs > 0
            ? (double)TotalRecordsProcessed / TotalSyncs
            : 0;

        public SyncMetrics Clone()
        {
            return new SyncMetrics
            {
                Type = Type,
                StartedAt = StartedAt,
                LastSyncTime = LastSyncTime,
                LastSuccessfulSync = LastSuccessfulSync,
                TotalSyncs = TotalSyncs,
                SuccessfulSyncs = SuccessfulSyncs,
                FailedSyncs = FailedSyncs,
                ConsecutiveFailures = ConsecutiveFailures,
                AverageDuration = AverageDuration,
                MinDuration = MinDuration,
                MaxDuration = MaxDuration,
                TotalRecordsProcessed = TotalRecordsProcessed,
                Events = Events.ToList(),
                MetricsByType = MetricsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        public void Reset()
        {
            StartedAt = DateTime.UtcNow;
            LastSyncTime = null;
            LastSuccessfulSync = null;
            TotalSyncs = 0;
            SuccessfulSyncs = 0;
            FailedSyncs = 0;
            ConsecutiveFailures = 0;
            AverageDuration = TimeSpan.Zero;
            MinDuration = TimeSpan.Zero;
            MaxDuration = TimeSpan.Zero;
            TotalRecordsProcessed = 0;
            Events.Clear();
            MetricsByType.Clear();
        }

        public void LoadFrom(SyncMetrics other)
        {
            Type = other.Type;
            StartedAt = other.StartedAt;
            LastSyncTime = other.LastSyncTime;
            LastSuccessfulSync = other.LastSuccessfulSync;
            TotalSyncs = other.TotalSyncs;
            SuccessfulSyncs = other.SuccessfulSyncs;
            FailedSyncs = other.FailedSyncs;
            ConsecutiveFailures = other.ConsecutiveFailures;
            AverageDuration = other.AverageDuration;
            MinDuration = other.MinDuration;
            MaxDuration = other.MaxDuration;
            TotalRecordsProcessed = other.TotalRecordsProcessed;
            Events = other.Events.ToList();
        }
    }

    public class SyncEvent
    {
        public string Type { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
        public int RecordsProcessed { get; set; }
    }

    public class SyncAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum AlertType
    {
        SyncFailure,
        SlowSync,
        ConsecutiveFailures,
        LowSuccessRate,
        NoRecentSync
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class SyncMetricsCache
    {
        public SyncMetrics GlobalMetrics { get; set; } = new();
        public Dictionary<string, SyncMetrics> MetricsByType { get; set; } = new();
        public List<SyncAlert> Alerts { get; set; } = new();
    }
}