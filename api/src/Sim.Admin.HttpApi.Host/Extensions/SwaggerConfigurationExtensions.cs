using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Sim.Framework.Core.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sim.Framework.Core.Extensions;

public static class SwaggerConfigurationExtensions
{
    /// <summary>
    /// 宿主基础设施接口的路径前缀。这些接口服务于 Swagger 文档自身，
    /// 不属于任何业务模块，聚合文档需要排除它们。
    /// </summary>
    private static readonly string[] HostInfrastructurePathPrefixes =
    [
        "api/swagger-config"
    ];

    public static void ConfigureSwaggerFromConfiguration(
        this SwaggerGenOptions options,
        IConfiguration configuration)
    {
        var swaggerOptions = configuration.GetSection("Swagger").Get<SwaggerOptions>();
        if (swaggerOptions?.Documents == null || !swaggerOptions.Documents.Any())
        {
            throw new InvalidOperationException("未找到Swagger文档配置，请在appsettings.json中配置Swagger:Documents节点");
        }

        foreach (var doc in swaggerOptions.Documents)
        {
            options.SwaggerDoc(doc.Name, new OpenApiInfo
            {
                Title = doc.Title,
                Version = doc.Version,
                Description = doc.Description
            });
        }

        // IncludeAll=true 的文档聚合全部业务接口，用于一次性导入接口工具。
        var aggregateDocuments = swaggerOptions.Documents
            .Where(doc => doc.IncludeAll)
            .Select(doc => doc.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        options.DocInclusionPredicate((docName, apiDesc) =>
            ShouldIncludeInDocument(docName, aggregateDocuments, apiDesc.RelativePath));

        options.CustomSchemaIds(GetSchemaId);
        IncludeXmlComments(options);
    }

    /// <summary>
    /// OpenAPI Schema ID 冲突防御表：短类名（如 RouteConfigDto）出现同名不同类型时回退全限定名。
    /// </summary>
    private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, Type> SchemaIdRegistry = new();

    /// <summary>
    /// 生成 OpenAPI Schema ID。
    /// 业务 DTO 使用短类名（如 RouteConfigDto），泛型类型（如 PagedResultDto&lt;T&gt;）
    /// 格式化为 PagedResultDtoOfRouteConfigDto；避免全限定名过长且携带程序集版本号，
    /// 导致前端基于 OpenAPI 生成的类型名随发布漂移。
    /// </summary>
    private static string GetSchemaId(Type type)
    {
        string schemaId;
        if (type.IsGenericType)
        {
            var genericName = type.Name.Split('`')[0];
            var genericArguments = string.Join("Of", type.GetGenericArguments().Select(GetSchemaId));
            schemaId = $"{genericName}Of{genericArguments}";
        }
        else
        {
            schemaId = type.Name;
        }

        SchemaIdRegistry.AddOrUpdate(schemaId, type, (_, current) => current);
        if (!ReferenceEquals(SchemaIdRegistry[schemaId], type))
        {
            return type.FullName!;
        }

        return schemaId;
    }

    /// <summary>
    /// 判断接口是否应出现在指定文档中：普通文档按 <c>api/{文档名}/</c> 前缀分组，
    /// 聚合文档（<see cref="SwaggerDocumentOptions.IncludeAll"/>）包含全部业务接口。
    /// </summary>
    public static bool ShouldIncludeInDocument(
        string documentName,
        IReadOnlySet<string> aggregateDocumentNames,
        string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (aggregateDocumentNames.Contains(documentName))
        {
            return relativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase)
                && !HostInfrastructurePathPrefixes.Any(prefix =>
                    relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return relativePath.StartsWith($"api/{documentName}/", StringComparison.OrdinalIgnoreCase);
    }

    private static void IncludeXmlComments(SwaggerGenOptions options)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var assemblyName = assembly.GetName().Name;
            if (string.IsNullOrEmpty(assemblyName) ||
                assemblyName.StartsWith("System.") ||
                assemblyName.StartsWith("Microsoft.") ||
                assemblyName.StartsWith("Volo.") ||
                assemblyName.StartsWith("netstandard") ||
                assemblyName == "mscorlib")
            {
                continue;
            }

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        }
    }

    public static List<SwaggerDocumentOptions> GetSwaggerDocuments(IConfiguration configuration)
    {
        var swaggerOptions = configuration.GetSection("Swagger").Get<SwaggerOptions>();
        return swaggerOptions?.Documents ?? [];
    }
}
