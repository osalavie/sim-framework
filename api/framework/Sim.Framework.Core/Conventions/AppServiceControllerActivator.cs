using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Sim.Framework.Core.Conventions;

/// <summary>
/// 控制器激活器。应用服务控制器从请求作用域的 DI 容器中解析
/// （含属性注入），普通控制器保持 ActivatorUtilities 默认激活方式。
/// </summary>
public sealed class AppServiceControllerActivator : IControllerActivator
{
    public object Create(ControllerContext context)
    {
        var controllerType = context.ActionDescriptor.ControllerTypeInfo.AsType();

        if (AppServiceTypeHelper.IsAppService(controllerType))
        {
            return context.HttpContext.RequestServices.GetRequiredService(controllerType);
        }

        return ActivatorUtilities.CreateInstance(context.HttpContext.RequestServices, controllerType);
    }

    public void Release(ControllerContext context, object controller)
    {
        // 应用服务由 DI 作用域负责释放；仅释放手动创建的控制器实例
        if (controller is IDisposable disposable &&
            !AppServiceTypeHelper.IsAppService(controller.GetType()))
        {
            disposable.Dispose();
        }
    }
}
