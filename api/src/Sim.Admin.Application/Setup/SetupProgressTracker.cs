using System;

namespace Sim.Admin.Application.Setup;

/// <summary>
/// 初始化阶段名称。前端按这些稳定值展示对应步骤。
/// </summary>
public static class SetupProgressStages
{
    public const string Idle = "idle";
    public const string Preparing = "preparing";
    public const string Administrator = "administrator";
    public const string SystemAccount = "system-account";
    public const string Finalizing = "finalizing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// 初始化进度快照。
/// </summary>
public readonly record struct SetupProgressSnapshot(
    bool Running,
    string Stage,
    int Percent,
    bool Completed,
    string? Error);

/// <summary>
/// 进程内初始化进度。初始化接口为单一长请求，进度由该请求主动上报，
/// 状态查询接口读取最近一次快照。
/// </summary>
public static class SetupProgressTracker
{
    private static readonly object SyncRoot = new();
    private static SetupProgressSnapshot _current = new(
        false,
        SetupProgressStages.Idle,
        0,
        false,
        null);

    public static SetupProgressSnapshot Current
    {
        get
        {
            lock (SyncRoot)
            {
                return _current;
            }
        }
    }

    public static void Begin()
    {
        lock (SyncRoot)
        {
            _current = new SetupProgressSnapshot(
                true,
                SetupProgressStages.Preparing,
                0,
                false,
                null);
        }
    }

    public static void Report(string stage, int percent)
    {
        lock (SyncRoot)
        {
            if (!_current.Running)
            {
                return;
            }

            _current = _current with
            {
                Stage = stage,
                Percent = Math.Clamp(percent, 0, 100)
            };
        }
    }

    public static void Complete()
    {
        lock (SyncRoot)
        {
            _current = new SetupProgressSnapshot(
                false,
                SetupProgressStages.Completed,
                100,
                true,
                null);
        }
    }

    public static void Fail(string? error)
    {
        lock (SyncRoot)
        {
            _current = _current with
            {
                Running = false,
                Stage = SetupProgressStages.Failed,
                Error = error
            };
        }
    }
}
