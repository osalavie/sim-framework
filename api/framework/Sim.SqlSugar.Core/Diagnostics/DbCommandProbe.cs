using System.Diagnostics;
using Sim.Framework.Domain.Shared.Telemetry;
using SqlSugarException = global::SqlSugar.SqlSugarException;

namespace Sim.SqlSugar.Core.Diagnostics;

/// <summary>
/// SqlSugar AOP 事件到业务指标的桥接。
/// SqlSugar 的 OnLogExecuting / OnLogExecuted 回调不携带耗时，因此用 AsyncLocal 记录命令开始时间：
/// OnLogExecuting 记开始，OnLogExecuted（普通命令）或 OnGetDataReadered（DataReader 查询，SqlSugar 自带耗时）
/// 负责结束。两者谁先到谁生效并清空记录，保证一条命令只产生一次指标。
/// </summary>
internal static class DbCommandProbe
{
    private static readonly AsyncLocal<PendingCommand?> Pending = new();

    /// <summary>命令开始执行（Aop.OnLogExecuting）。</summary>
    public static void Begin(string sql)
        => Pending.Value = new PendingCommand(Stopwatch.GetTimestamp(), ClassifyCommand(sql));

    /// <summary>命令执行结束（Aop.OnLogExecuted）。</summary>
    public static void Complete()
    {
        if (TakePending() is { } command)
        {
            Record(command.Command, Stopwatch.GetElapsedTime(command.StartTimestamp).TotalMilliseconds, success: true);
        }
    }

    /// <summary>DataReader 查询结束（Aop.OnGetDataReadered），耗时由 SqlSugar 提供。</summary>
    public static void CompleteWithElapsed(TimeSpan elapsed)
    {
        if (TakePending() is { } command)
        {
            Record(command.Command, elapsed.TotalMilliseconds, success: true);
        }
    }

    /// <summary>执行异常（Aop.OnError）。失败只增加计数，不写入耗时直方图。</summary>
    public static void Fail(SqlSugarException? exception)
    {
        var command = TakePending()?.Command ?? ClassifyCommand(exception?.Sql);
        Record(command, durationMs: 0, success: false);
    }

    /// <summary>
    /// 按 SQL 首个关键字归一化命令类型，保证标签基数有界。
    /// 前缀可能是空白、括号或注释符号，因此跳过所有非字母字符后再取第一个单词。
    /// </summary>
    public static string ClassifyCommand(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Metrics.DbCommand.Other;
        }

        var text = sql.AsSpan();
        var start = 0;
        while (start < text.Length && !char.IsLetter(text[start]))
        {
            start++;
        }

        var end = start;
        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        return text[start..end].ToString().ToUpperInvariant() switch
        {
            "SELECT" => Metrics.DbCommand.Select,
            "INSERT" => Metrics.DbCommand.Insert,
            "UPDATE" => Metrics.DbCommand.Update,
            "DELETE" => Metrics.DbCommand.Delete,
            "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "COMMENT" or "GRANT" or "REVOKE"
                => Metrics.DbCommand.Ddl,
            _ => Metrics.DbCommand.Other,
        };
    }

    private static PendingCommand? TakePending()
    {
        var pending = Pending.Value;
        Pending.Value = null;
        return pending;
    }

    private static void Record(string command, double durationMs, bool success)
    {
        var tags = new TagList
        {
            { Metrics.Tags.DbCommand, command },
            { Metrics.Tags.Status, success ? Metrics.Status.Success : Metrics.Status.Failure },
        };

        Metrics.DbCommandCount.Add(1, tags);

        if (success)
        {
            Metrics.DbCommandDuration.Record(durationMs, tags);
        }
    }

    private readonly record struct PendingCommand(long StartTimestamp, string Command);
}
