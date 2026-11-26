using System.Text.Json;
using System.Xml.Linq;

namespace Sim.Security.Tests;

public class DependencyLicensePolicyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Every_central_package_must_have_an_approved_reviewed_license()
    {
        var centralPackages = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(package => !string.IsNullOrWhiteSpace(package))
            .Select(package => package!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var policyDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "dependency-license-policy.json")));
        var root = policyDocument.RootElement;
        var approvedLicenses = root.GetProperty("approvedLicenses")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(license => !string.IsNullOrWhiteSpace(license))
            .Select(license => license!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reviewedPackages = root.GetProperty("packages")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var errors = centralPackages
            .Where(package => !reviewedPackages.ContainsKey(package))
            .Select(package => $"{package} 尚未完成许可证审核")
            .Concat(reviewedPackages.Keys
                .Where(package => !centralPackages.Contains(package))
                .Select(package => $"许可证策略包含已不再直接引用的包 {package}"))
            .Concat(reviewedPackages
                .Where(item => !approvedLicenses.Contains(item.Value))
                .Select(item => $"{item.Key} 使用未批准许可证 {item.Value}"))
            .OrderBy(error => error, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(errors.Length == 0,
            $"依赖许可证策略检查失败:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
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
