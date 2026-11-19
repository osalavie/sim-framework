using System.Threading;

namespace Sim.Admin.Domain.Shared;

public static class GlobalFeatureConfigurator
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) != 0)
        {
            return;
        }

        /* You can configure (enable/disable) global features of the used modules here.
         * Please refer to the documentation to learn more about the Global Features System:
         * https://docs.abp.io/en/abp/latest/Global-Features
         */
    }
}
