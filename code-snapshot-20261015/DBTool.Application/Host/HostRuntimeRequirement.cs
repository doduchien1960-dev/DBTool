using DBTool.Domain;

namespace DBTool.Application.Host;

public enum PreflightStatus { Compatible, MissingRuntime, UnknownUpdate, SdkMismatch, TemplateMissing }

public sealed record HostDescriptor(string Product, string MajorRelease, string? UpdateName,
    string FileVersion, int? TargetRuntimeMajor, string Source);

public sealed record RuntimeInventory(IReadOnlyCollection<int> InstalledMajorVersions);

public sealed record CompatibilityMatrix(IReadOnlyDictionary<string, int> RuntimeByUpdate);

public sealed record PreflightResult(PreflightStatus Status, int? RequiredRuntimeMajor,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record HostSnapshot(
    string Product,
    string ReleaseNumber,
    string ExecutablePath,
    string ProductVersion,
    string? UpdateName,
    int? RuntimeConfigMajor,
    IReadOnlyCollection<int> InstalledRuntimeMajors,
    IReadOnlyDictionary<string, string> ManagedAssemblyVersions,
    IReadOnlyDictionary<string, bool> ResourcePresence,
    string Source,
    IReadOnlyDictionary<string, string>? ManagedAssemblyPaths = null,
    IReadOnlyDictionary<string, string>? ManagedAssemblyHashes = null,
    string? RuntimeConfigPath = null);

public sealed record HostRequirement(
    string Product,
    string ReleaseNumber,
    IReadOnlyDictionary<string, int> RuntimeByUpdate,
    string RequiredManagedSdkVersion,
    IReadOnlyCollection<string> RequiredManagedAssemblies,
    IReadOnlyCollection<string> RequiredResources);

public sealed record HostCompatibilityReport(
    PreflightStatus Status,
    string Product,
    string ReleaseNumber,
    string? UpdateName,
    int RequiredRuntimeMajor,
    string TargetFramework,
    string ExecutablePath,
    string ProductVersion,
    IReadOnlyDictionary<string, string> ManagedAssemblyVersions);

public static class HostRuntimeRequirement
{
    public static OperationResult<HostCompatibilityReport> Evaluate(HostSnapshot snapshot, HostRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(requirement);

        var diagnostics = new List<Diagnostic>();
        void Block(string code, string message, string? expected = null, string? actual = null) =>
            diagnostics.Add(new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, null, expected, actual, message));

        if (!string.Equals(snapshot.Product, requirement.Product, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ReleaseNumber, requirement.ReleaseNumber, StringComparison.Ordinal))
            Block("HOST_PRODUCT_MISMATCH", "Observed host does not match the requested product release.",
                $"{requirement.Product}/{requirement.ReleaseNumber}", $"{snapshot.Product}/{snapshot.ReleaseNumber}");

        int? requiredRuntime = null;
        if (snapshot.UpdateName is not null && requirement.RuntimeByUpdate.TryGetValue(snapshot.UpdateName, out var documentedRuntime))
            requiredRuntime = documentedRuntime;
        else
            Block("HOST_UPDATE_UNKNOWN", "No verified update-to-runtime requirement is available.", actual: snapshot.UpdateName ?? "<unknown>");

        if (requiredRuntime.HasValue && !snapshot.InstalledRuntimeMajors.Contains(requiredRuntime.Value))
            Block("HOST_RUNTIME_MISSING", $".NET runtime {requiredRuntime.Value} is not installed.",
                expected: requiredRuntime.Value.ToString(), actual: string.Join(",", snapshot.InstalledRuntimeMajors.OrderBy(x => x)));

        if (requiredRuntime.HasValue && snapshot.RuntimeConfigMajor.HasValue && snapshot.RuntimeConfigMajor != requiredRuntime)
            Block("HOST_RUNTIME_CONFIG_MISMATCH", "Runtimeconfig evidence conflicts with the compatibility matrix.",
                expected: requiredRuntime.Value.ToString(), actual: snapshot.RuntimeConfigMajor.Value.ToString());

        foreach (var assembly in requirement.RequiredManagedAssemblies)
        {
            if (!snapshot.ManagedAssemblyVersions.TryGetValue(assembly, out var actualVersion))
                Block("HOST_SDK_MISSING", $"Required managed SDK assembly {assembly} was not observed.", actual: assembly);
            else if (!string.Equals(actualVersion, requirement.RequiredManagedSdkVersion, StringComparison.OrdinalIgnoreCase))
                Block("HOST_SDK_VERSION_MISMATCH", $"Managed SDK assembly {assembly} has an unexpected version.",
                    expected: requirement.RequiredManagedSdkVersion, actual: actualVersion);
        }

        foreach (var resource in requirement.RequiredResources)
            if (!snapshot.ResourcePresence.TryGetValue(resource, out var present) || !present)
                Block("TEMPLATE_MISSING", $"Required host resource {resource} is missing.", actual: resource);

        if (diagnostics.Count > 0)
            return new(false, null, diagnostics);

        var runtime = requiredRuntime!.Value;
        return new(true,
            new HostCompatibilityReport(PreflightStatus.Compatible, snapshot.Product, snapshot.ReleaseNumber,
                snapshot.UpdateName, runtime, $"net{runtime}.0-windows", snapshot.ExecutablePath,
                snapshot.ProductVersion, snapshot.ManagedAssemblyVersions),
            Array.Empty<Diagnostic>());
    }

    public static PreflightResult Evaluate(HostDescriptor host, RuntimeInventory inventory, CompatibilityMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(matrix);

        int? documentedRuntime = host.UpdateName is not null &&
            matrix.RuntimeByUpdate.TryGetValue(host.UpdateName, out int runtime) ? runtime : null;
        if (host.TargetRuntimeMajor.HasValue && documentedRuntime.HasValue &&
            host.TargetRuntimeMajor != documentedRuntime)
            return new(PreflightStatus.UnknownUpdate, null, new[] { Issue("HOST_RUNTIME_CONFLICT", "Host runtime evidence conflicts with the compatibility matrix.") });

        int? required = host.TargetRuntimeMajor ?? documentedRuntime;
        if (!required.HasValue)
            return new(PreflightStatus.UnknownUpdate, null, new[] { Issue("HOST_UPDATE_UNKNOWN", "No verified update-to-runtime requirement is available.") });
        if (!inventory.InstalledMajorVersions.Contains(required.Value))
            return new(PreflightStatus.MissingRuntime, required, new[] { Issue("HOST_RUNTIME_MISSING", $".NET runtime {required} is not installed.") });
        return new(PreflightStatus.Compatible, required, Array.Empty<Diagnostic>());
    }

    private static Diagnostic Issue(string code, string message) =>
        new(code, DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, null, null, null, message);
}
