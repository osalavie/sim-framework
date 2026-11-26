using System.Xml.Linq;

namespace Sim.Security.Tests;

public class ArchitectureDependencyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly IReadOnlyList<ProjectDependency> Dependencies = LoadDependencies();

    [Fact]
    public void Framework_projects_must_not_reference_business_or_host_projects()
    {
        var invalid = Dependencies.Where(dependency =>
            dependency.Source.StartsWith("framework/", StringComparison.OrdinalIgnoreCase) &&
            (dependency.Target.StartsWith("modules/", StringComparison.OrdinalIgnoreCase) ||
             dependency.Target.StartsWith("src/", StringComparison.OrdinalIgnoreCase)));

        AssertNoInvalidDependencies(invalid);
    }

    [Fact]
    public void Business_application_projects_must_not_reference_other_module_implementations()
    {
        var invalid = Dependencies.Where(dependency =>
        {
            if (!TryGetBusinessModule(dependency.Source, out var sourceModule) ||
                !dependency.Source.EndsWith(".Application.csproj", StringComparison.OrdinalIgnoreCase) ||
                !TryGetBusinessModule(dependency.Target, out var targetModule) ||
                string.Equals(sourceModule, targetModule, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !dependency.Target.EndsWith(".Application.Contracts.csproj", StringComparison.OrdinalIgnoreCase) &&
                   !dependency.Target.EndsWith(".Domain.Shared.csproj", StringComparison.OrdinalIgnoreCase);
        });

        AssertNoInvalidDependencies(invalid);
    }

    [Fact]
    public void Business_domain_projects_must_not_reference_other_business_modules()
    {
        var invalid = Dependencies.Where(dependency =>
            TryGetBusinessModule(dependency.Source, out var sourceModule) &&
            dependency.Source.EndsWith(".Domain.csproj", StringComparison.OrdinalIgnoreCase) &&
            TryGetBusinessModule(dependency.Target, out var targetModule) &&
            !string.Equals(sourceModule, targetModule, StringComparison.OrdinalIgnoreCase));

        AssertNoInvalidDependencies(invalid);
    }

    [Fact]
    public void Module_infrastructure_projects_must_not_reference_host_projects()
    {
        var invalid = Dependencies.Where(dependency =>
            dependency.Source.StartsWith("modules/", StringComparison.OrdinalIgnoreCase) &&
            dependency.Source.EndsWith(".SqlSugar.csproj", StringComparison.OrdinalIgnoreCase) &&
            dependency.Target.StartsWith("src/", StringComparison.OrdinalIgnoreCase));

        AssertNoInvalidDependencies(invalid);
    }

    [Fact]
    public void NuGet_package_versions_must_be_centrally_managed()
    {
        var centralFiles = new[]
        {
            Path.Combine(RepositoryRoot, "Directory.Packages.props"),
            Path.Combine(RepositoryRoot, "Directory.Packages.business.props")
        };
        var centralVersions = centralFiles
            .Where(File.Exists)
            .SelectMany(path => XDocument.Load(path).Descendants("PackageVersion"))
            .Select(element => new
            {
                Package = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value
            })
            .ToArray();

        var errors = centralVersions
            .Where(item => string.IsNullOrWhiteSpace(item.Package) || string.IsNullOrWhiteSpace(item.Version))
            .Select(_ => "中央版本文件包含无效的 PackageVersion")
            .Concat(centralVersions
                .Where(item => !string.IsNullOrWhiteSpace(item.Package))
                .GroupBy(item => item.Package!, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"中央版本文件重复定义 {group.Key}"))
            .ToList();

        var knownPackages = centralVersions
            .Where(item => !string.IsNullOrWhiteSpace(item.Package))
            .Select(item => item.Package!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in Directory.EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
                     .Where(path => !IsBuildOutput(path)))
        {
            var project = NormalizePath(Path.GetRelativePath(RepositoryRoot, projectPath));
            foreach (var reference in XDocument.Load(projectPath).Descendants("PackageReference"))
            {
                var package = reference.Attribute("Include")?.Value;
                if (reference.Attribute("Version") is not null || reference.Attribute("VersionOverride") is not null)
                {
                    errors.Add($"{project} 在项目文件中定义了 {package} 的版本");
                }

                if (!string.IsNullOrWhiteSpace(package) && !knownPackages.Contains(package))
                {
                    errors.Add($"{project} 引用了未集中定义版本的 {package}");
                }
            }
        }

        Assert.True(errors.Count == 0, $"NuGet 中央版本管理检查失败:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    [Fact]
    public void AspNetCore_shared_framework_references_must_be_explicit_and_reviewed()
    {
        var commonProps = XDocument.Load(Path.Combine(RepositoryRoot, "common.props"));
        Assert.Empty(commonProps.Descendants("FrameworkReference"));

        var reviewedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "framework/Sim.Framework.Application.Contracts/Sim.Framework.Application.Contracts.csproj",
            "framework/Sim.Framework.Core/Sim.Framework.Core.csproj",
            "modules/system/Sim.System.Application/Sim.System.Application.csproj",
            "modules/approval/Sim.Approval.Application/Sim.Approval.Application.csproj",
            "modules/openplatform/Sim.OpenPlatform.Application/Sim.OpenPlatform.Application.csproj",
            "tests/Sim.Security.Tests/Sim.Security.Tests.csproj"
        };

        var businessReviewFile = Path.Combine(
            RepositoryRoot,
            "tests/Sim.Security.Tests/framework-reference-reviews.business.txt");
        if (File.Exists(businessReviewFile))
        {
            reviewedProjects.UnionWith(File.ReadLines(businessReviewFile)
                .Select(line => NormalizePath(line.Trim()))
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal)));
        }

        var actualProjects = Directory
            .EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => XDocument.Load(path).Descendants("FrameworkReference")
                .Any(reference => string.Equals(
                    reference.Attribute("Include")?.Value,
                    "Microsoft.AspNetCore.App",
                    StringComparison.OrdinalIgnoreCase)))
            .Select(path => NormalizePath(Path.GetRelativePath(RepositoryRoot, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            reviewedProjects.SetEquals(actualProjects),
            $"Microsoft.AspNetCore.App 引用范围与审查清单不一致。{Environment.NewLine}" +
            $"新增: {string.Join(", ", actualProjects.Except(reviewedProjects))}{Environment.NewLine}" +
            $"缺失: {string.Join(", ", reviewedProjects.Except(actualProjects))}");
    }

    [Fact]
    public void Host_and_test_entry_projects_must_have_windows_and_linux_lock_files()
    {
        var entryProjectDirectories = new[]
        {
            "src/Sim.Admin.HttpApi.Host",
            "tests/Sim.Security.Tests"
        };

        foreach (var directory in entryProjectDirectories)
        {
            Assert.True(File.Exists(Path.Combine(RepositoryRoot, directory, "packages.lock.json")),
                $"{directory} 缺少 Windows 锁文件 packages.lock.json");
            Assert.True(File.Exists(Path.Combine(RepositoryRoot, directory, "packages.linux.lock.json")),
                $"{directory} 缺少 Linux 锁文件 packages.linux.lock.json");
        }
    }

    [Fact]
    public void Production_error_codes_must_be_centralized_and_match_their_modules()
    {
        var sourceFiles = new[] { "framework", "modules", "src" }
            .SelectMany(root => Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot, root),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        var errorCodeFiles = sourceFiles
            .Where(path => Path.GetFileName(path)
                .EndsWith("ErrorCodes.cs", StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hardcoded = sourceFiles
            .Where(path => !errorCodeFiles.Contains(path))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = NormalizePath(Path.GetRelativePath(RepositoryRoot, path)),
                    Line = index + 1,
                    Content = line
                }))
            .Where(item => item.Content.Contains("\"Sim:", StringComparison.Ordinal))
            .Select(item => $"{item.Path}:{item.Line}")
            .ToArray();

        var duplicateDeclarations = errorCodeFiles
            .SelectMany(path => File.ReadLines(path)
                .SelectMany(line => global::System.Text.RegularExpressions.Regex
                    .Matches(line, "\"(Sim:[A-Za-z]+:[A-Za-z0-9:]+)\"")
                    .Select(match => new
                    {
                        Code = match.Groups[1].Value,
                        Path = NormalizePath(Path.GetRelativePath(RepositoryRoot, path))
                    })))
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(item => item.Path))}")
            .ToArray();

        var ownershipErrors = errorCodeFiles
            .SelectMany(path =>
            {
                var relativePath = NormalizePath(Path.GetRelativePath(RepositoryRoot, path));
                var segments = relativePath.Split('/');
                var module = segments[0] switch
                {
                    "framework" => "Framework",
                    "modules" when segments.Length > 1 &&
                                   segments[1] == "openplatform" => "OpenPlatform",
                    "modules" when segments.Length > 1 =>
                        char.ToUpperInvariant(segments[1][0]) + segments[1][1..],
                    "src" when segments.Length > 1 &&
                               segments[1].StartsWith("Sim.Admin.", StringComparison.Ordinal) => "Admin",
                    _ => null
                };
                if (module is null)
                {
                    return [$"{relativePath}: 无法从项目路径识别所属模块"];
                }

                var errors = new List<string>();
                var expectedName = $"{module}ErrorCodes";
                var content = File.ReadAllText(path);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), expectedName, StringComparison.Ordinal))
                {
                    errors.Add($"{relativePath}: 文件名应为 {expectedName}.cs");
                }

                if (!content.Contains($"public static class {expectedName}", StringComparison.Ordinal))
                {
                    errors.Add($"{relativePath}: 类名应为 {expectedName}");
                }

                var invalidCodes = global::System.Text.RegularExpressions.Regex
                    .Matches(content, "\"(Sim:[A-Za-z]+:[A-Za-z0-9:]+)\"")
                    .Select(match => match.Groups[1].Value)
                    .Where(code => !code.StartsWith($"Sim:{module}:", StringComparison.Ordinal));
                errors.AddRange(invalidCodes.Select(code =>
                    $"{relativePath}: {code} 应使用 Sim:{module}: 前缀"));
                return errors;
            })
            .ToArray();

        Assert.True(
            hardcoded.Length == 0,
            $"业务代码不得硬编码 Sim 错误码，请改用对应模块的 *ErrorCodes 常量类:{Environment.NewLine}" +
            string.Join(Environment.NewLine, hardcoded));
        Assert.True(
            duplicateDeclarations.Length == 0,
            $"同一错误码只能声明一次:{Environment.NewLine}" +
            string.Join(Environment.NewLine, duplicateDeclarations));
        Assert.True(
            ownershipErrors.Length == 0,
            $"错误码文件、类名和错误码前缀必须与项目模块一致:{Environment.NewLine}" +
            string.Join(Environment.NewLine, ownershipErrors));
    }

    private static void AssertNoInvalidDependencies(IEnumerable<ProjectDependency> invalid)
    {
        var messages = invalid
            .Select(dependency => $"{dependency.Source} -> {dependency.Target}")
            .OrderBy(message => message, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(messages.Length == 0, $"检测到非法项目依赖:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
    }

    private static IReadOnlyList<ProjectDependency> LoadDependencies()
    {
        return Directory
            .EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .SelectMany(LoadProjectDependencies)
            .ToArray();
    }

    private static IEnumerable<ProjectDependency> LoadProjectDependencies(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var source = NormalizePath(Path.GetRelativePath(RepositoryRoot, projectPath));
        var document = XDocument.Load(projectPath);

        foreach (var reference in document.Descendants("ProjectReference"))
        {
            var include = reference.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            var targetPath = Path.GetFullPath(Path.Combine(projectDirectory, include));
            yield return new ProjectDependency(
                source,
                NormalizePath(Path.GetRelativePath(RepositoryRoot, targetPath)));
        }
    }

    private static bool TryGetBusinessModule(string path, out string module)
    {
        var segments = path.Split('/');
        if (segments.Length >= 3 && string.Equals(segments[0], "modules", StringComparison.OrdinalIgnoreCase))
        {
            module = segments[1];
            return true;
        }

        module = string.Empty;
        return false;
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

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

    private sealed record ProjectDependency(string Source, string Target);
}
