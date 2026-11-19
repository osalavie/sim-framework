using Microsoft.Extensions.DependencyInjection;
using Sim.Framework.Core.Filters;
using Volo.Abp.Modularity;

namespace Sim.Framework.Core;

public class FrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddControllers(options =>
        {
            // 添加工作单元过滤器
            options.Filters.Add<UnitOfWorkFilter>();
        });
    }

}
