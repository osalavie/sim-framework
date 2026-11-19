namespace Sim.Framework.Core.UnitOfWork;

/// <summary>
/// 工作单元配置特性
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlSugarUnitOfWorkAttribute : Attribute;
