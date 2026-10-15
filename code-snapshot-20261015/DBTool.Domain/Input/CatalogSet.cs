using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public enum PipeKind { Round, Box, DoubleBox }
public sealed record PipeCatalogEntry
{
    public PipeCatalogEntry(string code, PipeKind kind, double? innerDiameterMm, double? innerWidthMm, double? innerHeightMm, double wallThicknessMm, double? middleWallThicknessMm, double chamferMm)
    {
        Code = ContractGuard.Text(code, nameof(code));
        Kind = ContractGuard.EnumValue(kind);
        InnerDiameterMm = innerDiameterMm;
        InnerWidthMm = innerWidthMm;
        InnerHeightMm = innerHeightMm;
        WallThicknessMm = ContractGuard.NonNegative(wallThicknessMm, nameof(wallThicknessMm));
        MiddleWallThicknessMm = middleWallThicknessMm;
        ChamferMm = ContractGuard.NonNegative(chamferMm, nameof(chamferMm));
        foreach (var v in new[] { innerDiameterMm, innerWidthMm, innerHeightMm, middleWallThicknessMm })
            if (v.HasValue) ContractGuard.Positive(v.Value, "dimension");
        if (kind == PipeKind.Round)
        {
            if (innerDiameterMm == null || innerWidthMm != null || innerHeightMm != null || middleWallThicknessMm != null || chamferMm != 0) throw new ArgumentException("Round catalog mismatch.");
        }
        else
        {
            if (innerDiameterMm != null || innerWidthMm == null || innerHeightMm == null) throw new ArgumentException("Box catalog mismatch.");
            if (kind == PipeKind.Box && middleWallThicknessMm != null) throw new ArgumentException("Single box has no middle wall.");
            if (middleWallThicknessMm >= innerWidthMm) throw new ArgumentException("Middle wall consumes opening.");
            var cellWidth = kind == PipeKind.DoubleBox && middleWallThicknessMm.HasValue ? (innerWidthMm.Value - middleWallThicknessMm.Value) / 2 : innerWidthMm.Value;
            if (2 * chamferMm > Math.Min(cellWidth, innerHeightMm.Value)) throw new ArgumentException("Chamfer does not fit.");
        }
    }
    public string Code { get; }
    public PipeKind Kind { get; }
    public double? InnerDiameterMm { get; }
    public double? InnerWidthMm { get; }
    public double? InnerHeightMm { get; }
    public double WallThicknessMm { get; }
    public double? MiddleWallThicknessMm { get; }
    public double ChamferMm { get; }
}
public sealed record SteelAngleEntry
{
    public SteelAngleEntry(string code, double kgPerMetre)
    {
        Code = ContractGuard.Text(code, nameof(code));
        KgPerMetre = ContractGuard.Positive(kgPerMetre, nameof(kgPerMetre));

    }
    public string Code { get; }
    public double KgPerMetre { get; }
}
public sealed record HatchEntry
{
    public HatchEntry(string code, string patternName, double scale)
    {
        Code = ContractGuard.Text(code, nameof(code));
        PatternName = ContractGuard.Text(patternName, nameof(patternName));
        Scale = ContractGuard.Positive(scale, nameof(scale));

    }
    public string Code { get; }
    public string PatternName { get; }
    public double Scale { get; }
}
public sealed record CoverCatalogEntry
{
    public CoverCatalogEntry(string code,Dimensions2Mm physicalSize)
    { Code=ContractGuard.Text(code,nameof(code)); PhysicalSize=ContractGuard.Required(physicalSize); }
    public CoverCatalogEntry(string code,Dimensions2Mm physicalSize,Point3Mm insertionOffset,double rotationDegrees,
        CoverPlacementDatum placementDatum,Rectangle2Mm localBounds,IEnumerable<SourceRef> sources,
        CoverOpeningContract? openingContract = null, CoverBlockSet? blockSet = null) : this(code,physicalSize)
    {
        InsertionOffset=insertionOffset; RotationDegrees=ContractGuard.Finite(rotationDegrees,nameof(rotationDegrees));
        PlacementDatum=ContractGuard.EnumValue(placementDatum); LocalBounds=localBounds;
        Sources=ContractGuard.Copy(sources);
        OpeningContract=openingContract;
        BlockSet=blockSet;
        if(Sources.IsEmpty) throw new ArgumentException("Physical catalog requires provenance.");
        if(Math.Abs(localBounds.MaxX-localBounds.MinX-physicalSize.Width)>1e-6 ||
           Math.Abs(localBounds.MaxY-localBounds.MinY-physicalSize.Height)>1e-6)
            throw new ArgumentException("Physical size must match the declared local boundary.");
    }
    public string Code { get; }
    public Dimensions2Mm PhysicalSize { get; }
    public Point3Mm? InsertionOffset { get; }
    public double? RotationDegrees { get; }
    public CoverPlacementDatum? PlacementDatum { get; }
    public Rectangle2Mm? LocalBounds { get; }
    public ImmutableArray<SourceRef> Sources { get; }=ImmutableArray<SourceRef>.Empty;
    public CoverOpeningContract? OpeningContract { get; }
    public CoverBlockSet? BlockSet { get; }
}
public sealed record CatalogSet
{
    public CatalogSet(IEnumerable<KeyValuePair<string,PipeCatalogEntry>> pipes, IEnumerable<KeyValuePair<string,SteelAngleEntry>> angles, IEnumerable<KeyValuePair<string,HatchEntry>> hatches, IEnumerable<KeyValuePair<string,string>> aliases, IEnumerable<KeyValuePair<string,CoverCatalogEntry>>? covers = null)
    {
        Pipes = ContractGuard.Map(pipes);
        Angles = ContractGuard.Map(angles);
        Hatches = ContractGuard.Map(hatches);
        Aliases = ContractGuard.Map(aliases);
        Covers = ContractGuard.Map(covers ?? Array.Empty<KeyValuePair<string,CoverCatalogEntry>>());
        if (Pipes.Any(p => p.Key != p.Value.Code) || Angles.Any(p => p.Key != p.Value.Code) || Hatches.Any(p => p.Key != p.Value.Code) || Covers.Any(p=>p.Key!=p.Value.Code))
            throw new ArgumentException("Catalog key must match canonical code.");
        if (Aliases.Any(p => !Pipes.ContainsKey(p.Value) && !Angles.ContainsKey(p.Value) && !Hatches.ContainsKey(p.Value) && !Covers.ContainsKey(p.Value))) throw new ArgumentException("Unresolved alias.");
    }
    public ImmutableDictionary<string,PipeCatalogEntry> Pipes { get; }
    public ImmutableDictionary<string,SteelAngleEntry> Angles { get; }
    public ImmutableDictionary<string,HatchEntry> Hatches { get; }
    public ImmutableDictionary<string,CoverCatalogEntry> Covers { get; }
    public ImmutableDictionary<string,string> Aliases { get; }
}
