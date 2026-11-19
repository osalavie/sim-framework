# Sim.Framework.Core 配置化模块

## 概述

本框架提供了基于配置文件的模块加载和 Swagger 文档生成功能，使得应用程序可以通过配置文件动态管理模块和 API 文档，无需在代码中硬编码。

## 功能特性

- ✅ **模块动态加载**: 通过配置文件动态注册应用模块和控制器
- ✅ **Swagger 配置化**: 从配置文件生成 Swagger 文档
- ✅ **零硬编码**: 代码中不包含任何模块的具体信息
- ✅ **易于扩展**: 添加新模块只需修改配置文件
- ✅ **类型安全**: 使用强类型配置类

## 配置结构

### 1. 模块配置 (Modules)

在 `appsettings.json` 中配置需要注册的应用模块：

```json
{
  "Modules": {
    "Modules": [
      {
        "Name": "admin",
        "AssemblyName": "Sim.Admin.Application",
        "TypeName": "Sim.Admin.Application.AdminApplicationModule"
      },
      {
        "Name": "approval",
        "AssemblyName": "Sim.Approval.Application",
        "TypeName": "Sim.Approval.Application.ApprovalApplicationModule"
      }
    ]
  }
}
```

**字段说明：**
- `Name`: 模块名称，用作 API 路由前缀（如 `/api/admin/`）
- `AssemblyName`: 模块程序集名称
- `TypeName`: 模块类型的完全限定名（包含命名空间）

### 2. Swagger 文档配置 (Swagger)

在 `appsettings.json` 中配置 Swagger 文档：

```json
{
  "Swagger": {
    "Documents": [
      {
        "Name": "all",
        "Title": "Sim API（全部接口）",
        "Version": "v1",
        "Url": "/swagger/all/swagger.json",
        "Description": "聚合全部模块接口，用于一次性导入接口工具",
        "IncludeAll": true
      },
      {
        "Name": "admin",
        "Title": "Admin API",
        "Version": "v1",
        "Url": "/swagger/admin/swagger.json",
        "Description": "管理模块 API"
      },
      {
        "Name": "approval",
        "Title": "Approval API",
        "Version": "v1",
        "Url": "/swagger/approval/swagger.json",
        "Description": "审批模块 API"
      }
    ]
  }
}
```

**字段说明：**
- `Name`: 文档名称，必须与模块的 `Name` 一致；设为 `all` 等聚合名称时必须同时开启 `IncludeAll`
- `Title`: 文档标题，显示在 Swagger UI 中
- `Version`: API 版本号
- `Url`: Swagger JSON 文件的 URL 路径
- `Description`: 文档描述信息
- `IncludeAll`: 可选，默认 `false`。设为 `true` 时该文档聚合全部已发现模块的路由，并排除 Host 基础设施端点（`/api/swagger-config` 等）；`Documents` 的顺序即 Swagger UI 下拉顺序，聚合文档放在首位即成为默认文档

## 使用方法

### 1. 安装依赖

在你的 Host 项目中引用 `Sim.Framework.Core`：

```xml
<ItemGroup>
  <ProjectReference Include="..\..\framework\Sim.Framework.Core\Sim.Framework.Core.csproj" />
</ItemGroup>
```

### 2. 配置模块

在 `ConfigureServices` 方法中配置常规控制器：

```csharp
using Sim.Framework.Core.Extensions;

private void ConfigureConventionalControllers(IConfiguration configuration)
{
    Configure<AbpAspNetCoreMvcOptions>(options =>
    {
        // 从配置文件动态加载模块
        options.ConfigureConventionalControllersFromConfiguration(configuration);
    });
}
```

### 3. 配置 Swagger

在 `ConfigureServices` 方法中配置 Swagger：

```csharp
using Sim.Framework.Core.Extensions;

private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
{
    context.Services.AddAbpSwaggerGen(options =>
    {
        // 从配置文件动态生成Swagger文档
        options.ConfigureSwaggerFromConfiguration(configuration);
        
        // 隐藏ABP默认端点
        options.HideAbpEndpoints();
        
        // 添加JWT认证支持（可选）
        options.AddSecurityDefinition("JWT Auth", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            Description = "JWT Authorization header using the Bearer scheme.",
            BearerFormat = "JWT"
        });
        
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "JWT Auth"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}
```

### 4. 配置 Swagger UI

在 `OnApplicationInitialization` 方法中配置 Swagger UI：

```csharp
using Sim.Framework.Core.Extensions;

if (!env.IsProduction())
{
    var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
    
    app.UseSwagger();
    app.UseAbpSwaggerUI(options =>
    {
        // 从配置文件动态添加Swagger端点
        var swaggerDocs = SwaggerConfigurationExtensions.GetSwaggerDocuments(configuration);
        foreach (var doc in swaggerDocs)
        {
            options.SwaggerEndpoint(doc.Url, doc.Title);
        }
        
        // 其他UI配置
        options.DefaultModelsExpandDepth(0);
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        options.EnablePersistAuthorization();
    });
}
```

## 完整示例

```csharp
using Sim.Framework.Core.Extensions;
using Sim.Framework.Core.Configuration;

[DependsOn(typeof(FrameworkCoreModule))]
public class MyHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        
        ConfigureConventionalControllers(configuration);
        ConfigureSwagger(context, configuration);
    }
    
    private void ConfigureConventionalControllers(IConfiguration configuration)
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConfigureConventionalControllersFromConfiguration(configuration);
        });
    }
    
    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGen(options =>
        {
            options.ConfigureSwaggerFromConfiguration(configuration);
            options.HideAbpEndpoints();
        });
    }
    
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        
        if (!env.IsProduction())
        {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options =>
            {
                // 从配置文件动态添加Swagger端点
                var swaggerDocs = SwaggerConfigurationExtensions.GetSwaggerDocuments(configuration);
                foreach (var doc in swaggerDocs)
                {
                    options.SwaggerEndpoint(doc.Url, doc.Title);
                }
            });
        }
    }
}
```

## 添加新模块

要添加新模块，只需在配置文件中添加相应的配置项：

### 步骤 1: 添加模块配置

```json
{
  "Modules": {
    "Modules": [
      {
        "Name": "newmodule",
        "AssemblyName": "Sim.NewModule.Application",
        "TypeName": "Sim.NewModule.Application.NewModuleApplicationModule"
      }
    ]
  }
}
```

### 步骤 2: 添加 Swagger 文档配置

```json
{
  "Swagger": {
    "Documents": [
      {
        "Name": "newmodule",
        "Title": "New Module API",
        "Version": "v1",
        "Url": "/swagger/newmodule/swagger.json",
        "Description": "新模块 API"
      }
    ]
  }
}
```

### 步骤 3: 重启应用

配置完成后重启应用，新模块的 API 和 Swagger 文档将自动生成。

## 注意事项

1. 模块的 `Name` 必须与 Swagger 文档的 `Name` 保持一致
2. `TypeName` 必须是完全限定的类型名称（包含命名空间）
3. 确保模块程序集已被项目引用
4. 配置错误会在应用启动时抛出异常，便于快速定位问题
5. API 路由必须遵循 `/api/{moduleName}/` 的格式才能正确分配到对应的 Swagger 文档

## API 参考

### ModuleConfigurationExtensions

- `ConfigureConventionalControllersFromConfiguration(this AbpAspNetCoreMvcOptions options, IConfiguration configuration)`
  - 从配置文件配置常规控制器

### SwaggerConfigurationExtensions

- `ConfigureSwaggerFromConfiguration(this SwaggerGenOptions options, IConfiguration configuration)`
  - 从配置文件配置 Swagger 文档生成

- `GetSwaggerDocuments(IConfiguration configuration)`
  - 从配置文件获取 Swagger 文档列表
