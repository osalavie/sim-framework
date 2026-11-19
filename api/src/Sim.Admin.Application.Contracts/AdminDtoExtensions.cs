using System.Threading;

namespace Sim.Admin.Application.Contracts;

public static class AdminDtoExtensions
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) != 0)
        {
            return;
        }

        /* You can add extension properties to DTOs
         * defined in the depended modules.
         *
         * Example:
         *
         * ObjectExtensionManager.Instance
         *   .AddOrUpdateProperty<IdentityRoleDto, string>("Title");
         *
         * See the documentation for more:
         * https://docs.abp.io/en/abp/latest/Object-Extensions
         */
    }
}
