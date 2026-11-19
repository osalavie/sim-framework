using System.Diagnostics.Metrics;

namespace Sim.Framework.Domain.Shared.Telemetry;

/// <summary>
/// 业务指标定义。Host 在注册 OpenTelemetry 时订阅 <see cref="MeterName"/>，
/// 因此这里新增的计数器与直方图会同时出现在 Prometheus 抓取端点和 OTLP 导出结果中。
/// 标签值必须是有界集合（枚举量、已登记的任务键等），不要写入 SQL 文本、URL、
/// 用户输入等高基数内容，否则指标后端会被标签组合撑爆。
/// </summary>
public static class Metrics
{
    /// <summary>Meter 名称，OpenTelemetry 通过 AddMeter 订阅。</summary>
    public const string MeterName = "SimFramework";

    /// <summary>Meter 版本，指标语义变更时递增。</summary>
    public const string MeterVersion = "1.0.0";

    /// <summary>业务指标 Meter。</summary>
    public static readonly Meter Meter = new(MeterName, MeterVersion);

    /// <summary>数据库命令执行次数，标签：db.command、status。</summary>
    public static readonly Counter<long> DbCommandCount =
        Meter.CreateCounter<long>("sim.db.command.count", "{command}", "数据库命令执行次数");

    /// <summary>数据库命令执行耗时（毫秒），标签：db.command、status；失败不记录耗时。</summary>
    public static readonly Histogram<double> DbCommandDuration =
        Meter.CreateHistogram<double>("sim.db.command.duration", "ms", "数据库命令执行耗时");

    /// <summary>定时任务执行次数，标签：task.group、task.key、trigger.type、status。</summary>
    public static readonly Counter<long> ScheduleTaskCount =
        Meter.CreateCounter<long>("sim.schedule_task.count", "{execution}", "定时任务执行次数");

    /// <summary>定时任务执行耗时（毫秒），标签：task.group、task.key、trigger.type、status。</summary>
    public static readonly Histogram<double> ScheduleTaskDuration =
        Meter.CreateHistogram<double>("sim.schedule_task.duration", "ms", "定时任务执行耗时");

    /// <summary>指标标签键。</summary>
    public static class Tags
    {
        /// <summary>数据库命令类型，取值见 <see cref="DbCommand"/>。</summary>
        public const string DbCommand = "db.command";

        /// <summary>定时任务分组（Quartz JobKey.Group）。</summary>
        public const string TaskGroup = "task.group";

        /// <summary>定时任务键（Quartz JobKey.Name）。</summary>
        public const string TaskKey = "task.key";

        /// <summary>触发方式：Cron / Manual。</summary>
        public const string TriggerType = "trigger.type";

        /// <summary>执行结果，取值见 <see cref="Status"/>。</summary>
        public const string Status = "status";
    }

    /// <summary><see cref="Tags.Status"/> 取值。</summary>
    public static class Status
    {
        /// <summary>执行成功。</summary>
        public const string Success = "success";

        /// <summary>执行失败或抛出异常。</summary>
        public const string Failure = "failure";
    }

    /// <summary><see cref="Tags.DbCommand"/> 取值，由 SQL 首个关键字归一化而来。</summary>
    public static class DbCommand
    {
        /// <summary>SELECT 查询。</summary>
        public const string Select = "select";

        /// <summary>INSERT 写入。</summary>
        public const string Insert = "insert";

        /// <summary>UPDATE 更新。</summary>
        public const string Update = "update";

        /// <summary>DELETE 删除。</summary>
        public const string Delete = "delete";

        /// <summary>DDL：CREATE / ALTER / DROP / TRUNCATE 等。</summary>
        public const string Ddl = "ddl";

        /// <summary>未识别或无法归类的语句。</summary>
        public const string Other = "other";
    }

    /// <summary>定时任务触发方式取值。</summary>
    public static class TriggerTypes
    {
        /// <summary>Cron 定时触发。</summary>
        public const string Cron = "Cron";

        /// <summary>手工触发（触发器名以 MT_ 开头）。</summary>
        public const string Manual = "Manual";
    }
}
