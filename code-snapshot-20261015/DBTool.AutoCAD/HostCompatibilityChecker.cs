using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application.Host;
using Microsoft.Win32;

namespace DBTool.AutoCAD.Host;

public sealed class HostCompatibilityChecker
{
    private static readonly string[] RequiredManagedAssemblies = ["acmgd.dll", "acdbmgd.dll", "accoremgd.dll"];

    public HostSnapshot ReadHostSnapshot(string? templatePath = null)
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("The AutoCAD host executable path is unavailable.");
        var executable = new FileInfo(executablePath);
        var versionInfo = FileVersionInfo.GetVersionInfo(executable.FullName);
        var managedPath = Path.GetDirectoryName(typeof(Database).Assembly.Location)
            ?? throw new InvalidOperationException("The AutoCAD managed SDK directory is unavailable.");
        var release = ReadProductRelease(out var product);
        var update = ReadInstalledUpdate(versionInfo.ProductName ?? product);
        var runtimeConfigPath = Path.Combine(managedPath, "acdbmgd.runtimeconfig.json");
        var runtimeConfigMajor = ReadRuntimeConfigMajor(runtimeConfigPath);

        var assemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assemblyVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assemblyHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in RequiredManagedAssemblies)
        {
            var path = Path.Combine(managedPath, name);
            if (!File.Exists(path)) continue;
            assemblyPaths[name] = path;
            var info = FileVersionInfo.GetVersionInfo(path);
            assemblyVersions[name] = info.FileVersion ?? string.Empty;
            assemblyHashes[name] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        }

        var resources = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["template"] = !string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath)
        };

        return new HostSnapshot(
            product,
            release,
            executable.FullName,
            versionInfo.FileVersion ?? string.Empty,
            update,
            runtimeConfigMajor,
            ReadInstalledRuntimeMajors(),
            assemblyVersions,
            resources,
            "AutoCAD host process, installed registry and runtime evidence",
            assemblyPaths,
            assemblyHashes,
            runtimeConfigPath);
    }

    public HostRequirement CreateRequirement() => new(
        "Autodesk Civil 3D 2026 - English",
        "25.1",
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Autodesk AutoCAD 2026.1.1 Update"] = 8,
            ["AutoCAD 2026 through Update 1.1"] = 8,
            ["Autodesk AutoCAD 2026 Update 1.2 and later"] = 10,
            ["AutoCAD 2026 Update 1.2 and later"] = 10
        },
        "25.1.164.0.0",
        RequiredManagedAssemblies,
        ["template"]);

    private static string ReadProductRelease(out string product)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD");
        if (root is not null)
        {
            foreach (var releaseName in root.GetSubKeyNames().OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase))
            {
                using var releaseKey = root.OpenSubKey(releaseName);
                if (releaseKey is null) continue;
                foreach (var productKeyName in releaseKey.GetSubKeyNames())
                {
                    using var productKey = releaseKey.OpenSubKey(productKeyName);
                    var productName = productKey?.GetValue("ProductName") as string;
                    if (!string.IsNullOrWhiteSpace(productName))
                    {
                        product = productName;
                        return releaseName.TrimStart('R');
                    }
                }
            }
        }

        product = "Unknown AutoCAD host";
        return "Unknown";
    }

    private static string? ReadInstalledUpdate(string productName)
    {
        var roots = new[]
        {
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
        };
        try
        {
            foreach (var root in roots)
            {
                using (root)
                using (var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstall is null) continue;
                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        using var item = uninstall.OpenSubKey(name);
                        var displayName = item?.GetValue("DisplayName") as string;
                        if (displayName is not null && displayName.Contains("AutoCAD", StringComparison.OrdinalIgnoreCase) &&
                            displayName.Contains("Update", StringComparison.OrdinalIgnoreCase))
                            return displayName;
                    }
                }
            }
        }
        catch (SecurityException)
        {
            // An unreadable uninstall hive is evidence of an unresolved update, not permission to guess.
        }
        return null;
    }

    private static int? ReadRuntimeConfigMajor(string path)
    {
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var frameworks = document.RootElement.GetProperty("runtimeOptions").GetProperty("frameworks");
        foreach (var framework in frameworks.EnumerateArray())
        {
            if (!string.Equals(framework.GetProperty("name").GetString(), "Microsoft.NETCore.App", StringComparison.Ordinal)) continue;
            var version = framework.GetProperty("version").GetString();
            if (version is not null && int.TryParse(version.Split('.')[0], out var major)) return major;
        }
        return null;
    }

    private static IReadOnlyCollection<int> ReadInstalledRuntimeMajors()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Environment.GetEnvironmentVariable("DOTNET_ROOT(x64)"),
            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? string.Empty, "dotnet")
        };
        return roots.Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => Directory.Exists(Path.Combine(x!, "shared", "Microsoft.NETCore.App"))
                ? Directory.GetDirectories(Path.Combine(x!, "shared", "Microsoft.NETCore.App"))
                : Array.Empty<string>())
            .Select(x => Path.GetFileName(x).Split('.')[0])
            .Select(x => int.TryParse(x, out var major) ? (int?)major : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }
}
