namespace DBTool.Domain;

public sealed record HookLengths
{
    internal HookLengths(double startMm, double endMm) { StartMm = startMm; EndMm = endMm; }
    public double StartMm { get; }
    public double EndMm { get; }
}

public static class HookResolver
{
    public static OperationResult<HookLengths> Resolve(HookPattern pattern, HookMode mode, double memberThicknessMm,
        double startCoverMm, double endCoverMm, double diameterMm, int? multiple, SourceRef? source = null)
    {
        if (!Enum.IsDefined(pattern) || !Enum.IsDefined(mode) ||
            !double.IsFinite(memberThicknessMm) || memberThicknessMm <= 0 ||
            !double.IsFinite(startCoverMm) || startCoverMm < 0 ||
            !double.IsFinite(endCoverMm) || endCoverMm < 0 || !double.IsFinite(diameterMm) || diameterMm <= 0 ||
            (mode == HookMode.DiameterMultiple ? multiple is not > 0 : multiple is not null)) return Invalid();
        if (pattern == HookPattern.Hook0M) return new(true, new HookLengths(0, 0), []);
        double hook = mode == HookMode.ClearThickness
            ? memberThicknessMm - startCoverMm - endCoverMm : multiple!.Value * diameterMm;
        if (!double.IsFinite(hook) || hook <= 0) return Invalid();
        return new(true, new HookLengths(
            pattern is HookPattern.Hook2M or HookPattern.HookStartOnly ? hook : 0,
            pattern is HookPattern.Hook2M or HookPattern.Hook1M ? hook : 0), []);

        OperationResult<HookLengths> Invalid() => new(false, null,
            [new Diagnostic("HOOK_INVALID", DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source,
                "finite positive member/diameter; nonnegative covers; positive active hook; valid mode/multiple", null,
                "Invalid hook inputs or no positive length available. No clamping was applied.")]);
    }
}
