using Mapster;

namespace Sim.Framework.Application.Mapping;

/// <summary>
/// <see cref="IObjectMapper"/> adapter backed by Mapster.
/// Delegates all mapping calls to Mapster's <c>Adapt</c> methods,
/// which use convention-based auto-mapping (same-name properties).
/// </summary>
public sealed class MapsterObjectMapper : IObjectMapper
{
    public TTarget Map<TSource, TTarget>(TSource source)
    {
        return source.Adapt<TSource, TTarget>();
    }

    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
    {
        source.Adapt(target);
        return target;
    }
}
