using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Sim.Framework.Core.Conventions;

/// <summary>
/// 将应用服务类识别为 MVC 控制器，替代 ABP ConventionalControllers 的类型发现机制。
/// 普通 *Controller 类的发现逻辑不受影响。
/// </summary>
public sealed class AppServiceControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        return base.IsController(typeInfo) || AppServiceTypeHelper.IsAppService(typeInfo.AsType());
    }
}
