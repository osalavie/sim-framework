using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Sim.Framework.Application.Contracts.Content;

namespace Sim.Framework.Core.Conventions;

/// <summary>
/// 应用服务常规控制器约定。将应用服务方法映射为 REST 风格 API，
/// 路由模板、HTTP 动词推断与参数绑定规则与 ABP 常规控制器保持一致：
/// <code>
/// GET    api/{rootPath}/{service}            ← GetListAsync
/// GET    api/{rootPath}/{service}/{id}       ← GetAsync(id)
/// POST   api/{rootPath}/{service}/{action}   ← 其他方法（kebab-case）
/// POST   api/{rootPath}/{service}/{action}/{xxxId} ← 唯一 *Id 简单参数追加为路由参数
/// </code>
/// </summary>
public sealed class AppServiceConvention(ConventionalControllerOptions options)
    : IApplicationModelConvention
{
    private static readonly string[] ControllerNamePostfixes = ["AppService", "ApplicationService", "Service"];

    private static readonly string[] NoBodyVerbs = ["GET", "DELETE", "TRACE", "HEAD"];

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var controllerType = controller.ControllerType.AsType();
            if (!AppServiceTypeHelper.IsAppService(controllerType))
            {
                continue;
            }

            var rootPath = options.GetRootPath(controllerType.Assembly);
            controller.ControllerName = AppServiceTypeHelper.RemovePostFix(
                controller.ControllerName, ControllerNamePostfixes);
            controller.ApiExplorer.IsVisible = true;

            foreach (var action in controller.Actions)
            {
                ConfigureAction(rootPath, controller.ControllerName, action);
            }

            ConfigureParameters(controller);
        }
    }

    private static void ConfigureAction(string rootPath, string controllerName, ActionModel action)
    {
        var verb = AppServiceTypeHelper.GetConventionalVerb(action.ActionName);
        var template = BuildRouteTemplate(rootPath, controllerName, action, verb);

        var selector = new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template)),
            ActionConstraints = { new HttpMethodActionConstraint([verb]) }
        };

        // 与 MVC 默认 CreateSelectorModel 行为一致：
        // action 上的全部属性进入端点元数据（供授权、限流等中间件读取）
        foreach (var attribute in action.Attributes)
        {
            selector.EndpointMetadata.Add(attribute);
        }
        selector.EndpointMetadata.Add(new HttpMethodMetadata([verb]));

        action.Selectors.Clear();
        action.Selectors.Add(selector);

        action.ApiExplorer.IsVisible = true;
    }

    /// <summary>
    /// 与 ABP ConventionalRouteBuilder.Build 等效的路由模板构建。
    /// </summary>
    private static string BuildRouteTemplate(
        string rootPath,
        string controllerName,
        ActionModel action,
        string verb)
    {
        var url = $"api/{rootPath}/{AppServiceTypeHelper.ToKebabCase(controllerName)}";

        // 名为 id 的简单类型参数固定追加为 {id} 路由段
        var idParameter = action.Parameters.FirstOrDefault(p => p.ParameterName == "id");
        if (idParameter != null && CanBeRouteValue(idParameter.ParameterInfo.ParameterType))
        {
            url += "/{id}";
        }

        var actionName = AppServiceTypeHelper.RemovePostFix(
            AppServiceTypeHelper.RemoveVerbPrefix(action.ActionName, verb),
            "Async");

        if (actionName.Length > 0)
        {
            url += $"/{AppServiceTypeHelper.ToKebabCase(actionName)}";

            // 恰好一个 *Id 结尾的参数时追加为次级路由参数
            var secondaryIds = action.Parameters
                .Where(p => p.ParameterName.EndsWith("Id", StringComparison.Ordinal))
                .ToList();
            if (secondaryIds.Count == 1 &&
                !secondaryIds[0].Attributes.Any(x => x is OptionalAttribute))
            {
                url += $"/{{{secondaryIds[0].ParameterName}}}";
            }
        }

        return url;
    }

    private static bool CanBeRouteValue(Type type)
    {
        return AppServiceTypeHelper.IsPrimitiveExtended(type, includeEnums: true)
               || TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
    }

    /// <summary>
    /// 与 ABP 参数绑定规则一致：复杂类型在非 GET/DELETE 请求上绑定 Body，
    /// 其余参数走默认绑定（路由/查询字符串）。
    /// </summary>
    private static void ConfigureParameters(ControllerModel controller)
    {
        foreach (var action in controller.Actions)
        {
            foreach (var parameter in action.Parameters)
            {
                if (parameter.BindingInfo != null)
                {
                    continue;
                }

                if (AppServiceTypeHelper.IsPrimitiveExtended(parameter.ParameterInfo.ParameterType, includeEnums: true))
                {
                    continue;
                }

                // 远程流内容参数显式绑定到表单，由 RemoteStreamContentModelBinder 解析
                // multipart/form-data 中的文件，避免与默认 FromBody 流程冲突。
                if (IsFormBodyBindingIgnoredType(parameter.ParameterInfo.ParameterType))
                {
                    parameter.BindingInfo = BindingInfo.GetBindingInfo([new FromFormAttribute()]);
                    continue;
                }

                if (!CanUseFormBodyBinding(action, parameter))
                {
                    continue;
                }

                parameter.BindingInfo = BindingInfo.GetBindingInfo([new FromBodyAttribute()]);
            }
        }
    }

    private static bool CanUseFormBodyBinding(ActionModel action, ParameterModel parameter)
    {
        if (parameter.ParameterName == "id")
        {
            return false;
        }

        if (IsFormBodyBindingIgnoredType(parameter.ParameterInfo.ParameterType))
        {
            return false;
        }

        foreach (var selector in action.Selectors)
        {
            foreach (var constraint in selector.ActionConstraints.OfType<HttpMethodActionConstraint>())
            {
                if (constraint.HttpMethods.All(m => NoBodyVerbs.Contains(m, StringComparer.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsFormBodyBindingIgnoredType(Type type)
    {
        if (typeof(IFormFile).IsAssignableFrom(type) || typeof(Stream).IsAssignableFrom(type))
        {
            return true;
        }

        if (typeof(Sim.Framework.Application.Contracts.Content.IRemoteStreamContent).IsAssignableFrom(type))
        {
            return true;
        }

        // 迁移期兼容 ABP 远程流内容类型
        return type.FullName is "Volo.Abp.Content.IRemoteStreamContent" or "Volo.Abp.Content.RemoteStreamContent"
               || type.GetInterfaces().Any(i => i.FullName == "Volo.Abp.Content.IRemoteStreamContent");
    }
}
