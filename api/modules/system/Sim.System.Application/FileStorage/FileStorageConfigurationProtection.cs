using Microsoft.AspNetCore.DataProtection;

namespace Sim.System.Application.FileStorage;

public sealed class FileStorageConfigurationProtection(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector(
        "SimFramework.System.FileStorageConfiguration.v1");

    public string? Protect(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : _protector.Protect(value.Trim());

    public string? Unprotect(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : _protector.Unprotect(value);
}
