using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application.Host;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Host;
using DBTool.AutoCAD.Template;

namespace DBTool.AutoCAD.Tests;

public sealed class HostSmokeCommands
{
    [CommandMethod("DBTOOL_HOST_SMOKE", CommandFlags.Session)]
    public void Run()
    {
        var outputPath = RequireEnvironment("DBTOOL_HOST_SMOKE_OUTPUT");
        var templatePath = Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_PATH");
        var expectedHash = Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_SHA256");
        var manifestOutputPath = Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_MANIFEST_OUTPUT");

        var checker = new HostCompatibilityChecker();
        var snapshot = checker.ReadHostSnapshot(templatePath);
        var preflight = HostRuntimeRequirement.Evaluate(snapshot, checker.CreateRequirement());

        OperationResultEnvelope? template = null;
        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            var roles = TemplateRoleCatalog.RequiredRoles;
            var manifest = new TemplateManifestReader().Read(new TemplateReadRequest(templatePath, expectedHash, roles,
                "VHGpro 3.2/Template/VHG-Template.dwg"));
            var contract = manifest.Success
                ? new TemplateContractValidator().Validate(manifest.Value!, roles, TemplateValidationMode.ProductionRender)
                : null;
            if (manifest.Value is not null && !string.IsNullOrWhiteSpace(manifestOutputPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(manifestOutputPath))!);
                File.WriteAllText(manifestOutputPath, JsonSerializer.Serialize(manifest.Value, JsonOptions()));
            }
            var diagnostics = manifest.Diagnostics.Concat(contract?.Diagnostics ?? [])
                .Select(d => new
            {
                d.Code,
                Severity = d.Severity.ToString(),
                d.Message,
                d.Expected,
                d.Actual
            }).ToArray();
            template = new OperationResultEnvelope(manifest.Success && contract?.Success == true, diagnostics);
        }

        var payload = new
        {
            HostProduct = snapshot.Product,
            HostRelease = snapshot.ReleaseNumber,
            HostUpdate = snapshot.UpdateName,
            HostProductVersion = snapshot.ProductVersion,
            RuntimeConfigMajor = snapshot.RuntimeConfigMajor,
            RuntimeConfigPath = snapshot.RuntimeConfigPath,
            InstalledRuntimeMajors = snapshot.InstalledRuntimeMajors.OrderBy(x => x).ToArray(),
            AssemblyPaths = snapshot.ManagedAssemblyPaths,
            AssemblyVersions = snapshot.ManagedAssemblyVersions,
            AssemblyHashes = snapshot.ManagedAssemblyHashes,
            PreflightStatus = preflight.Value?.Status.ToString() ?? preflight.Diagnostics.FirstOrDefault()?.Code,
            PreflightDiagnostics = preflight.Diagnostics.Select(d => d.Code).ToArray(),
            Template = template,
            ExitStatus = preflight.Success && (template is null || template.Success) ? "Success" : "Block"
        };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, JsonOptions()));
        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
            $"\nDBTOOL_HOST_SMOKE {payload.ExitStatus}: {outputPath}\n");
    }

    private static string RequireEnvironment(string key) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Required environment variable {key} is missing.");

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record OperationResultEnvelope(bool Success, IReadOnlyList<object> Diagnostics);
}
