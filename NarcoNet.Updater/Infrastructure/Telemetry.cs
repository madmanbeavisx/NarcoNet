using System.Diagnostics;

namespace NarcoNet.Updater.Infrastructure;

/// <summary>
///     Provides telemetry and metrics collection for monitoring application performance.
///     Implements the Observer pattern for metric collection.
/// </summary>
public sealed class TelemetryCollector
{
    private static readonly Lazy<TelemetryCollector> LazyInstance = new(() => new TelemetryCollector());
    private readonly Stopwatch _applicationStopwatch = Stopwatch.StartNew();
    private readonly object _lock = new();
    private readonly Dictionary<string, MetricData> _metrics = new();

    private TelemetryCollector()
    {
    }

    /// <summary>
    ///     Gets the singleton instance of the telemetry collector.
    /// </summary>
    public static TelemetryCollector Instance => LazyInstance.Value;

    /// <summary>
    ///     Records the start of an operation.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <returns>An operation scope that should be disposed when the operation completes.</returns>
    public IDisposable BeginOperation(string operationName)
    {
        return new OperationScope(this, operationName);
    }

    /// <summary>
    ///     Records a metric value.
    /// </summary>
    public void RecordMetric(string metricName, double value, Dictionary<string, string>? tags = null)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(metricName, out MetricData? metric))
            {
                metric = new MetricData(metricName);
                _metrics[metricName] = metric;
            }

            metric.AddValue(value, tags);
        }
    }

    /// <summary>
    ///     Records an event.
    /// </summary>
    public void RecordEvent(string eventName, Dictionary<string, string>? properties = null)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue($"event:{eventName}", out MetricData? metric))
            {
                metric = new MetricData($"event:{eventName}");
                _metrics[$"event:{eventName}"] = metric;
            }

            metric.IncrementCount(properties);
        }
    }

    /// <summary>
    ///     Records the duration of an operation.
    /// </summary>
    internal void RecordOperationDuration(string operationName, TimeSpan duration, bool success)
    {
        RecordMetric($"{operationName}.Duration", duration.TotalMilliseconds, new Dictionary<string, string>
        {
            { "success", success.ToString() }
        });

        RecordEvent($"{operationName}.{(success ? "Success" : "Failure")}");
    }

    /// <summary>
    ///     Gets a summary of all collected metrics.
    /// </summary>
    public TelemetrySummary GetSummary()
    {
        lock (_lock)
        {
            return new TelemetrySummary
            {
                ApplicationUptime = _applicationStopwatch.Elapsed,
                Metrics = _metrics.Values.Select(m => m.ToSummary()).ToList(),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    ///     Clears all collected metrics.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }

    /// <summary>
    ///     Represents a scope for an operation timing.
    /// </summary>
    private class OperationScope(TelemetryCollector collector, string operationName) : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _success = true;

        public void Dispose()
        {
            _stopwatch.Stop();
            collector.RecordOperationDuration(operationName, _stopwatch.Elapsed, _success);
        }

        public void MarkAsFailure()
        {
            _success = false;
        }
    }
}

/// <summary>
///     Stores metric data with statistical aggregations.
/// </summary>
internal class MetricData(string name)
{
    private readonly List<Dictionary<string, string>> _tags =
    [
    ];
    private readonly List<double> _values =
    [
    ];
    private int _count;

    public string Name { get; } = name;

    public void AddValue(double value, Dictionary<string, string>? tags = null)
    {
        _values.Add(value);
        _tags.Add(tags ?? new Dictionary<string, string>());
        _count++;
    }

    public void IncrementCount(Dictionary<string, string>? tags = null)
    {
        _count++;
        _tags.Add(tags ?? new Dictionary<string, string>());
    }

    public MetricSummary ToSummary()
    {
        return new MetricSummary
        {
            Name = Name,
            Count = _count,
            Sum = _values.Sum(),
            Average = _values.Any() ? _values.Average() : 0,
            Min = _values.Any() ? _values.Min() : 0,
            Max = _values.Any() ? _values.Max() : 0,
            Percentile95 = CalculatePercentile(_values, 0.95),
            Percentile99 = CalculatePercentile(_values, 0.99)
        };
    }

    private static double CalculatePercentile(List<double> values, double percentile)
    {
        if (!values.Any())
        {
            return 0;
        }

        List<double> sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        return sorted[index];
    }
}

/// <summary>
///     Represents a summary of collected telemetry data.
/// </summary>
public class TelemetrySummary
{
    public TimeSpan ApplicationUptime { get; init; }
    public List<MetricSummary> Metrics { get; init; } =
    [
    ];
    public DateTime Timestamp { get; init; }

    public override string ToString()
    {
        var summary = $"=== Telemetry Summary (Uptime: {ApplicationUptime:hh\\:mm\\:ss}) ===\n";
        summary += $"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n\n";

        foreach (MetricSummary metric in Metrics.OrderBy(m => m.Name))
        {
            summary += $"{metric.Name}:\n";
            summary += $"  Count: {metric.Count}\n";

            if (metric.Sum > 0)
            {
                summary += $"  Sum: {metric.Sum:F2}\n";
                summary += $"  Avg: {metric.Average:F2}\n";
                summary += $"  Min: {metric.Min:F2}\n";
                summary += $"  Max: {metric.Max:F2}\n";
                summary += $"  P95: {metric.Percentile95:F2}\n";
                summary += $"  P99: {metric.Percentile99:F2}\n";
            }

            summary += "\n";
        }

        return summary;
    }
}

/// <summary>
///     Represents statistical summary of a metric.
/// </summary>
public class MetricSummary
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Sum { get; init; }
    public double Average { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Percentile95 { get; init; }
    public double Percentile99 { get; init; }
}
