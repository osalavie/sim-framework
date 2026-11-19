namespace Sim.Framework.Application.Mapping;

/// <summary>
/// Object mapping abstraction. Replaces Volo.Abp.ObjectMapping.IObjectMapper.
/// </summary>
public interface IObjectMapper
{
    TTarget Map<TSource, TTarget>(TSource source);
    TTarget Map<TSource, TTarget>(TSource source, TTarget target);
}

/// <summary>
/// Null implementation for cases where no mapper is configured.
/// Throws NotSupportedException on actual mapping calls.
/// </summary>
public class NullObjectMapper : IObjectMapper
{
    public static NullObjectMapper Instance { get; } = new();

    public TTarget Map<TSource, TTarget>(TSource source)
    => throw new NotSupportedException("No ObjectMapper is configured. Register an IObjectMapper implementation.");

    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
    => throw new NotSupportedException("No ObjectMapper is configured. Register an IObjectMapper implementation.");
}
