namespace Sim.SqlSugar.Core.Attributes;

public class CodeFirstAttribute: Attribute
{
    public bool Enable { get; set; } = true;
    public CodeFirstAttribute()
    {
    }
    public CodeFirstAttribute(bool enable)
    {
        Enable = enable;
    }
}