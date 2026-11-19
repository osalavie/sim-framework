using Microsoft.Extensions.Configuration;
using Sim.Framework.Core.Extensions;

namespace Sim.Security.Tests;

/// <summary>
/// Swagger 分组与聚合文档的契约测试：模块分组只包含自己的路由前缀，
/// 聚合分组覆盖全部业务接口并排除宿主基础设施接口。
/// </summary>
public class SwaggerDocumentTests
{
    private static readonly HashSet<string> AggregateDocuments =
        new(StringComparer.OrdinalIgnoreCase) { "all" };

    private static readonly HashSet<string> NoAggregateDocuments =
        new(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("admin", "api/admin/user", true)]
    [InlineData("admin", "api/administrator/user", false)]
    [InlineData("system", "api/system/user", true)]
    [InlineData("all", "api/admin/user", false)]
    public void Module_documents_should_only_include_their_own_route_prefix(
        string documentName,
        string relativePath,
        bool expected)
    {
        Assert.Equal(
            expected,
            SwaggerConfigurationExtensions.ShouldIncludeInDocument(
                documentName,
                NoAggregateDocuments,
                relativePath));
    }

    [Theory]
    [InlineData("api/admin/user")]
    [InlineData("api/approval/instance")]
    [InlineData("api/system/tenant")]
    [InlineData("api/system/monitor")]
    public void Aggregate_document_should_include_every_business_route(string relativePath)
    {
        Assert.True(
            SwaggerConfigurationExtensions.ShouldIncludeInDocument(
                "all",
                AggregateDocuments,
                relativePath));
    }

    [Theory]
    [InlineData("api/swagger-config")]
    [InlineData("health/live")]
    [InlineData("")]
    [InlineData(null)]
    public void Aggregate_document_should_exclude_host_infrastructure_routes(string? relativePath)
    {
        Assert.False(
            SwaggerConfigurationExtensions.ShouldIncludeInDocument(
                "all",
                AggregateDocuments,
                relativePath));
    }

    [Fact]
    public void Configuration_should_expose_one_aggregate_document_covering_all_module_groups()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(
                FindRepositoryRoot(),
                "src",
                "Sim.Admin.HttpApi.Host",
                "appsettings.json"))
            .Build();

        var documents = SwaggerConfigurationExtensions.GetSwaggerDocuments(configuration);
        var aggregateDocuments = documents.Where(document => document.IncludeAll).ToList();
        var aggregate = Assert.Single(aggregateDocuments);

        Assert.Equal("all", aggregate.Name);
        Assert.Equal("/swagger/all/swagger.json", aggregate.Url);

        var moduleDocuments = documents.Where(document => !document.IncludeAll).ToList();
        Assert.True(
            moduleDocuments.Count >= 4,
            $"聚合文档之外应保留模块分组，当前为 {moduleDocuments.Count} 组。");

        // 每个模块分组的 api/{分组名}/ 路径都必须被聚合文档覆盖。
        var aggregateNames = aggregateDocuments.Select(document => document.Name).ToHashSet();
        foreach (var moduleDocument in moduleDocuments)
        {
            Assert.True(
                SwaggerConfigurationExtensions.ShouldIncludeInDocument(
                    aggregate.Name,
                    aggregateNames,
                    $"api/{moduleDocument.Name}/probe"),
                $"聚合文档缺少模块分组 {moduleDocument.Name} 的接口。");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sim.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位包含 Sim.slnx 的仓库根目录。");
    }
}
