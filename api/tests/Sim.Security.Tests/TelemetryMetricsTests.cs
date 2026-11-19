using System.Diagnostics.Metrics;
using Sim.Framework.Domain.Shared.Telemetry;
using Sim.SqlSugar.Core.Diagnostics;
using SqlSugarException = global::SqlSugar.SqlSugarException;

namespace Sim.Security.Tests;

/// <summary>
/// 业务指标（数据库、定时任务）的名称、单位与标签约定回归。
/// 名称/单位变更会直接打断既有 Prometheus 查询与告警，因此固定在这里。
/// </summary>
public sealed class TelemetryMetricsTests
{
    [Fact]
    public void Business_instruments_should_be_published_with_expected_names_and_units()
    {
        using var collector = new MetricCollector();

        var published = collector.Instruments.ToDictionary(i => i.Name);

        Assert.Equal("{command}", published["sim.db.command.count"].Unit);
        Assert.Equal("ms", published["sim.db.command.duration"].Unit);
        Assert.Equal("{execution}", published["sim.schedule_task.count"].Unit);
        Assert.Equal("ms", published["sim.schedule_task.duration"].Unit);
        Assert.Equal(Metrics.MeterName, Metrics.Meter.Name);
    }

    [Theory]
    [InlineData("SELECT * FROM sim_system_user", "select")]
    [InlineData("select 1", "select")]
    [InlineData("  \r\n INSERT INTO sim_system_user(id) VALUES(1)", "insert")]
    [InlineData("UPDATE sim_system_user SET name='x'", "update")]
    [InlineData("DELETE FROM sim_system_user", "delete")]
    [InlineData("CREATE TABLE sim_demo(id uuid)", "ddl")]
    [InlineData("(SELECT 1)", "select")]
    [InlineData("EXPLAIN SELECT 1", "other")]
    [InlineData("", "other")]
    [InlineData(null, "other")]
    public void Classify_command_should_normalize_sql_verb_to_bounded_value(string? sql, string expected)
    {
        Assert.Equal(expected, DbCommandProbe.ClassifyCommand(sql));
    }

    [Fact]
    public void Db_command_should_record_duration_with_success_status()
    {
        using var collector = new MetricCollector();

        DbCommandProbe.Begin("SELECT * FROM sim_system_user");
        DbCommandProbe.Complete();

        var count = Assert.Single(collector.Of("sim.db.command.count"));
        Assert.Equal(1, count.Value);
        Assert.Equal("select", count.Tags["db.command"]);
        Assert.Equal("success", count.Tags["status"]);

        var duration = Assert.Single(collector.Of("sim.db.command.duration"));
        Assert.True(duration.Value >= 0);
        Assert.Equal("select", duration.Tags["db.command"]);
    }

    [Fact]
    public void Db_command_should_record_failure_without_duration()
    {
        using var collector = new MetricCollector();

        DbCommandProbe.Begin("UPDATE sim_system_user SET name='x'");
        DbCommandProbe.Fail(null);

        var count = Assert.Single(collector.Of("sim.db.command.count"));
        Assert.Equal("update", count.Tags["db.command"]);
        Assert.Equal("failure", count.Tags["status"]);
        Assert.Empty(collector.Of("sim.db.command.duration"));
    }

    [Fact]
    public void Db_command_should_record_once_when_both_callbacks_fire()
    {
        using var collector = new MetricCollector();

        DbCommandProbe.Begin("SELECT * FROM sim_system_user");
        DbCommandProbe.Complete();
        // DataReader 查询路径上 SqlSugar 还会给出自己的耗时，重复回调不应重复计数。
        DbCommandProbe.CompleteWithElapsed(TimeSpan.FromMilliseconds(5));

        Assert.Single(collector.Of("sim.db.command.count"));
        Assert.Single(collector.Of("sim.db.command.duration"));
    }

    [Fact]
    public void Db_command_without_begin_should_fall_back_to_exception_sql()
    {
        using var collector = new MetricCollector();

        DbCommandProbe.Fail(new SqlSugarException("boom") { Sql = "DELETE FROM sim_system_user" });

        var count = Assert.Single(collector.Of("sim.db.command.count"));
        Assert.Equal("delete", count.Tags["db.command"]);
        Assert.Equal("failure", count.Tags["status"]);
    }

    private sealed record Measurement(string Name, double Value, IReadOnlyDictionary<string, object?> Tags);

    private sealed record PublishedInstrument(string Name, string? Unit);

    /// <summary>
    /// 订阅 Metrics 的 Meter，只在测试线程上收集，避免其他测试类并发记录造成干扰。
    /// </summary>
    private sealed class MetricCollector : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<Measurement> _measurements = [];
        private readonly List<PublishedInstrument> _instruments = [];
        private readonly int _threadId = Environment.CurrentManagedThreadId;

        public MetricCollector()
        {
            // 触发 Metrics 静态构造，确保 Meter 上的乐器在 Start() 时已被发布。
            _ = Metrics.Meter;

            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (!ReferenceEquals(instrument.Meter, Metrics.Meter))
                {
                    return;
                }

                if (Environment.CurrentManagedThreadId == _threadId)
                {
                    _instruments.Add(new PublishedInstrument(instrument.Name, instrument.Unit));
                }

                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Captured(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Captured(instrument, value, tags));
            _listener.Start();
        }

        public IReadOnlyList<PublishedInstrument> Instruments => _instruments;

        public IReadOnlyList<Measurement> Of(string instrumentName)
            => _measurements.Where(m => m.Name == instrumentName).ToList();

        public void Dispose() => _listener.Dispose();

        private void Captured(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Environment.CurrentManagedThreadId != _threadId)
            {
                return;
            }

            _measurements.Add(new Measurement(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        }
    }
}
