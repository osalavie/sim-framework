using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sim.Framework.Core.Conventions;

/// <summary>
/// 应用服务实例工厂。通过 ActivatorUtilities 完成构造函数注入后，
/// 对 ApplicationService 暴露的公共可写属性（Logger、CurrentUser、ObjectMapper）
/// 进行尽力而为的属性注入，替代 ABP 基于 Autofac 的属性注入。
/// </summary>
internal static class AppServiceFactory
{
    private static readonly string[] InjectablePropertyNames = ["Logger", "CurrentUser", "ObjectMapper"];

    public static object Create(IServiceProvider serviceProvider, Type serviceType)
    {
        var instance = ActivatorUtilities.CreateInstance(serviceProvider, serviceType);

        foreach (var property in serviceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || property.SetMethod?.IsPublic != true)
            {
                continue;
            }

            if (!InjectablePropertyNames.Contains(property.Name))
            {
                continue;
            }

            object? value = null;
            if (property.Name == "Logger" && typeof(ILogger).IsAssignableFrom(property.PropertyType))
            {
                value = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(serviceType);
            }
            else
            {
                value = serviceProvider.GetService(property.PropertyType);
            }

            if (value != null)
            {
                property.SetValue(instance, value);
            }
        }

        return instance;
    }
}
