using System.Net.Http.Json;
using System.Text.Json;

namespace Sim.System.Application.Security;

internal static class ExternalLoginHttpExtensions
{
    public static async Task<T> ReadRequiredJsonAsync<T>(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? throw ExternalLoginErrors.ProviderUnavailable()
                : value;
        }
        catch (JsonException)
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }
    }

    public static async Task<string> ReadRequiredTextAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw ExternalLoginErrors.ProviderUnavailable();
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
