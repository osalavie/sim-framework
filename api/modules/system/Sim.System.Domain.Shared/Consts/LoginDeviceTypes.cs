namespace Sim.System.Domain.Shared.Consts;

public static class LoginDeviceTypes
{
    public const string Pc = "PC";
    public const string Pad = "Pad";
    public const string Phone = "Phone";

    public static readonly IReadOnlyList<string> All = [Pc, Pad, Phone];
}
