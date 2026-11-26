namespace Sim.Framework.Domain.Shared.Telemetry;

/// <summary>
/// 遥测标识。属于框架级稳定契约，不随产品品牌更名；修改会影响仪表盘、告警和追踪查询。
/// </summary>
public static class TelemetryIdentity
{
    /// <summary>OTLP / 追踪使用的 service.name。</summary>
    public const string ServiceName = "simframework-api";

    /// <summary>业务指标 Meter 名。</summary>
    public const string MeterName = "SimFramework";
}
