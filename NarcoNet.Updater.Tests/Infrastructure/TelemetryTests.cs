using NarcoNet.Updater.Infrastructure;

namespace NarcoNet.Updater.Tests.Infrastructure;

public class TelemetryTests
{
    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Act
        TelemetryCollector instance1 = TelemetryCollector.Instance;
        TelemetryCollector instance2 = TelemetryCollector.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void RecordMetric_WithSingleValue_RecordsCorrectly()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        telemetry.RecordMetric("test_metric", 42.5);
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "test_metric");
        metric.Should().NotBeNull();
        metric!.Count.Should().Be(1);
        metric.Sum.Should().Be(42.5);
        metric.Average.Should().Be(42.5);
        metric.Min.Should().Be(42.5);
        metric.Max.Should().Be(42.5);
    }

    [Fact]
    public void RecordMetric_WithMultipleValues_CalculatesStatistics()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        telemetry.RecordMetric("test_metric", 10);
        telemetry.RecordMetric("test_metric", 20);
        telemetry.RecordMetric("test_metric", 30);
        telemetry.RecordMetric("test_metric", 40);
        telemetry.RecordMetric("test_metric", 50);

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "test_metric");
        metric.Should().NotBeNull();
        metric!.Count.Should().Be(5);
        metric.Sum.Should().Be(150);
        metric.Average.Should().Be(30);
        metric.Min.Should().Be(10);
        metric.Max.Should().Be(50);
    }

    [Fact]
    public void RecordMetric_WithTags_RecordsCorrectly()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();
        Dictionary<string, string> tags = new() { { "category", "test" } };

        // Act
        telemetry.RecordMetric("tagged_metric", 100, tags);
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "tagged_metric");
        metric.Should().NotBeNull();
        metric!.Count.Should().Be(1);
    }

    [Fact]
    public void RecordEvent_RecordsEventCount()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        telemetry.RecordEvent("test_event");
        telemetry.RecordEvent("test_event");
        telemetry.RecordEvent("test_event");

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? eventMetric = summary.Metrics.FirstOrDefault(m => m.Name == "event:test_event");
        eventMetric.Should().NotBeNull();
        eventMetric!.Count.Should().Be(3);
    }

    [Fact]
    public void RecordEvent_WithProperties_RecordsCorrectly()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();
        Dictionary<string, string> properties = new() { { "level", "info" } };

        // Act
        telemetry.RecordEvent("event_with_props", properties);
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? eventMetric = summary.Metrics.FirstOrDefault(m => m.Name == "event:event_with_props");
        eventMetric.Should().NotBeNull();
    }

    [Fact]
    public void BeginOperation_RecordsDuration()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        using (telemetry.BeginOperation("test_operation"))
        {
            Thread.Sleep(10); // Simulate work
        }

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? durationMetric = summary.Metrics.FirstOrDefault(m => m.Name == "test_operation.Duration");
        durationMetric.Should().NotBeNull();
        durationMetric!.Average.Should().BeGreaterOrEqualTo(10); // At least 10ms

        MetricSummary? successEvent = summary.Metrics.FirstOrDefault(m => m.Name == "event:test_operation.Success");
        successEvent.Should().NotBeNull();
        successEvent!.Count.Should().Be(1);
    }

    [Fact]
    public void BeginOperation_WithMultipleOperations_RecordsAll()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        using (telemetry.BeginOperation("operation1"))
        {
            Thread.Sleep(5);
        }

        using (telemetry.BeginOperation("operation1"))
        {
            Thread.Sleep(5);
        }

        using (telemetry.BeginOperation("operation1"))
        {
            Thread.Sleep(5);
        }

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? durationMetric = summary.Metrics.FirstOrDefault(m => m.Name == "operation1.Duration");
        durationMetric.Should().NotBeNull();
        durationMetric!.Count.Should().Be(3);
    }

    [Fact]
    public void BeginOperation_CalculatesPercentiles()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act - Record many operations with varying durations
        for (int i = 0; i < 100; i++)
        {
            using (telemetry.BeginOperation("perf_test"))
            {
                Thread.Sleep(i); // 0ms to 99ms
            }
        }

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "perf_test.Duration");
        metric.Should().NotBeNull();
        metric!.Count.Should().Be(100);
        metric.Percentile95.Should().BeGreaterThan(0);
        metric.Percentile99.Should().BeGreaterThan(0);
        metric.Percentile99.Should().BeGreaterOrEqualTo(metric.Percentile95);
    }

    [Fact]
    public void GetSummary_IncludesUptime()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        Thread.Sleep(100);
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        summary.ApplicationUptime.Should().BeGreaterThan(TimeSpan.Zero);
        summary.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetSummary_IncludesAllMetrics()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        telemetry.RecordMetric("metric1", 10);
        telemetry.RecordMetric("metric2", 20);
        telemetry.RecordEvent("event1");

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        summary.Metrics.Should().HaveCount(3);
        summary.Metrics.Should().Contain(m => m.Name == "metric1");
        summary.Metrics.Should().Contain(m => m.Name == "metric2");
        summary.Metrics.Should().Contain(m => m.Name == "event:event1");
    }

    [Fact]
    public void Clear_RemovesAllMetrics()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.RecordMetric("test", 123);
        telemetry.RecordEvent("test_event");

        // Act
        telemetry.Clear();
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        summary.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void TelemetrySummary_ToString_FormatsCorrectly()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();
        telemetry.RecordMetric("test_metric", 42);

        // Act
        TelemetrySummary summary = telemetry.GetSummary();
        string text = summary.ToString();

        // Assert
        text.Should().Contain("Telemetry Summary");
        text.Should().Contain("test_metric");
        text.Should().Contain("Count: 1");
        text.Should().Contain("Avg:");
    }

    [Fact]
    public void RecordMetric_CalculatesPercentiles()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act - Record values from 1 to 100
        for (int i = 1; i <= 100; i++)
        {
            telemetry.RecordMetric("percentile_test", i);
        }

        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "percentile_test");
        metric.Should().NotBeNull();
        metric!.Percentile95.Should().BeInRange(94, 96); // 95th percentile should be around 95
        metric.Percentile99.Should().BeInRange(98, 100); // 99th percentile should be around 99
    }

    [Fact]
    public void RecordMetric_WithEmptyValues_ReturnsZeroStatistics()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();

        // Act
        telemetry.RecordEvent("empty_test"); // Event with no metric values
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "event:empty_test");
        metric.Should().NotBeNull();
        metric!.Sum.Should().Be(0);
        metric.Average.Should().Be(0);
        metric.Min.Should().Be(0);
        metric.Max.Should().Be(0);
    }

    [Fact]
    public void BeginOperation_IsThreadSafe()
    {
        // Arrange
        TelemetryCollector telemetry = TelemetryCollector.Instance;
        telemetry.Clear();
        List<Task> tasks =
        [
        ];

        // Act - Record from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                using (telemetry.BeginOperation("concurrent_test"))
                {
                    Thread.Sleep(1);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        TelemetrySummary summary = telemetry.GetSummary();

        // Assert
        MetricSummary? metric = summary.Metrics.FirstOrDefault(m => m.Name == "concurrent_test.Duration");
        metric.Should().NotBeNull();
        metric!.Count.Should().Be(10);
    }
}
