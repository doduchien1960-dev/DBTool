using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class DomainDependencyTests
{
    [Fact]
    public void DomainAndTests_HaveNoHostOrExcelDependencies()
    {
        var testsDirectory = FindProjectDirectory("DBTool.Domain.Tests.csproj");
        var domainDirectory = Path.GetFullPath(Path.Combine(testsDirectory, "..", "DBTool.Domain"));
        var runnerDirectory = Path.GetFullPath(Path.Combine(testsDirectory, "..", "DBTool.TestRunner"));
        var testProject = XDocument.Load(Path.Combine(testsDirectory, "DBTool.Domain.Tests.csproj"));
        var domainProject = XDocument.Load(Path.Combine(domainDirectory, "DBTool.Domain.csproj"));
        var runnerProject = XDocument.Load(Path.Combine(runnerDirectory, "DBTool.TestRunner.csproj"));

        Assert.DoesNotContain(testProject.Descendants("ProjectReference"), element =>
            (string?)element.Attribute("Include") is string path &&
            path.Contains("DBTool.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(domainProject.Descendants("ProjectReference"));
        Assert.Empty(domainProject.Descendants("PackageReference"));
        Assert.Empty(domainProject.Descendants("Reference"));
        Assert.Empty(domainProject.Descendants("FrameworkReference"));
        Assert.Empty(domainProject.Descendants("UseWPF"));
        Assert.DoesNotContain(runnerProject.Descendants("ProjectReference"), element =>
            (string?)element.Attribute("Include") is string path &&
            path.Contains("DBTool.csproj", StringComparison.OrdinalIgnoreCase));

        AssertNoForbiddenResolvedDependencies(Path.Combine(domainDirectory, "obj", "project.assets.json"));
        AssertNoForbiddenResolvedDependencies(Path.Combine(testsDirectory, "obj", "project.assets.json"));
    }

    private static void AssertNoForbiddenResolvedDependencies(string assetsPath)
    {
        Assert.True(File.Exists(assetsPath), $"Restore graph missing: {assetsPath}");
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        foreach (var library in assets.RootElement.GetProperty("libraries").EnumerateObject())
        {
            Assert.False(IsForbidden(library.Name), $"Forbidden transitive library: {library.Name}");
        }
        foreach (var target in assets.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (var dependency in target.Value.EnumerateObject())
                Assert.False(IsForbidden(dependency.Name), $"Forbidden resolved dependency: {dependency.Name}");
        }
    }

    private static bool IsForbidden(string name) =>
        name.Contains("Autodesk", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AcMgd", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AcDbMgd", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AcCoreMgd", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ClosedXML", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("DBTool/", StringComparison.OrdinalIgnoreCase);

    private static string FindProjectDirectory(string projectFile)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            // --artifacts-path may place the test assembly outside the project tree.
            var workspaceProject = Path.Combine(directory.FullName, "DBTool", "DBTool.Domain.Tests", projectFile);
            if (File.Exists(workspaceProject)) return Path.GetDirectoryName(workspaceProject)!;
            if (File.Exists(Path.Combine(directory.FullName, projectFile)))
                return directory.FullName;
        }
        throw new FileNotFoundException(projectFile);
    }
}
