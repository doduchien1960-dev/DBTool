using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public enum SectionAxis { AlongY, AlongX }
public enum SectionMode { TrueSection, Conventional }
public enum CutMode { TrueSection, SteppedExplicit }

public sealed record SectionPlane
{
    public SectionPlane(string id, Point3Mm point, Vector3 unitNormal)
    {
        Id = ContractGuard.Text(id, nameof(id));
        Point = point;
        UnitNormal = ContractGuard.Required(unitNormal);
        double norm = unitNormal.X * unitNormal.X + unitNormal.Y * unitNormal.Y + unitNormal.Z * unitNormal.Z;
        if (Math.Abs(norm - 1) > 1e-9) throw new ArgumentException("Section normal must be unit length.", nameof(unitNormal));
    }
    public string Id { get; }
    public Point3Mm Point { get; }
    public Vector3 UnitNormal { get; }
}
public sealed record SteppedSectionDefinition
{
    public SteppedSectionDefinition(string id,IEnumerable<Point2Mm> cutterVertices)
    {
        Id=ContractGuard.Text(id,nameof(id));
        CutterVertices=ContractGuard.Copy(cutterVertices);
        if(CutterVertices.Length<2 || Enumerable.Range(1,CutterVertices.Length-1)
            .Any(i=>CutterVertices[i]==CutterVertices[i-1]))
            throw new ArgumentException("Stepped cutter needs nonzero ordered segments.",nameof(cutterVertices));
    }
    public string Id { get; }
    public string Version => "SteppedExplicit-v1";
    public ImmutableArray<Point2Mm> CutterVertices { get; }
}
public enum ProjectedVoidRole { RoofOpening, PipePlanSilhouette, Inner, PipeOuter, ConcreteCutout }

public sealed record SectionDefinition
{
    public SectionDefinition(string id, SectionAxis axis, double cutCoordinateMm, SectionMode mode)
    {
        Id = ContractGuard.Text(id,nameof(id));
        Axis = ContractGuard.EnumValue(axis);
        CutCoordinateMm = ContractGuard.Finite(cutCoordinateMm,nameof(cutCoordinateMm));
        Mode = ContractGuard.EnumValue(mode);
    }
    public string Id { get; }
    public SectionAxis Axis { get; }
    public double CutCoordinateMm { get; }
    public SectionMode Mode { get; }
}

/// <summary>A closed material region in unscaled local view millimetres.</summary>
public sealed record MaterialRegion
{
    public MaterialRegion(string id,string sourceComponentId,string materialId,Footprint footprint,IEnumerable<SourceRef> sources)
    {
        Id=ContractGuard.Text(id,nameof(id));
        SourceComponentId=ContractGuard.Text(sourceComponentId,nameof(sourceComponentId));
        MaterialId=ContractGuard.Text(materialId,nameof(materialId));
        Footprint=ContractGuard.Required(footprint);
        Sources=ContractGuard.Copy(sources);
    }
    public string Id { get; }
    public string SourceComponentId { get; }
    public string MaterialId { get; }
    public Footprint Footprint { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

/// <summary>Projected aperture/boundary. PipePlanSilhouette is an outline, not a horizontal material cut.</summary>
public sealed record ProjectedVoid
{
    public ProjectedVoid(string id,string sourceOpeningId,string ownerId,ProjectedVoidRole role,Polygon2Mm boundary,IEnumerable<SourceRef> sources)
    {
        Id=ContractGuard.Text(id,nameof(id));
        SourceOpeningId=ContractGuard.Text(sourceOpeningId,nameof(sourceOpeningId));
        OwnerId=ContractGuard.Text(ownerId,nameof(ownerId));
        Role=ContractGuard.EnumValue(role);
        Boundary=ContractGuard.Required(boundary);
        Sources=ContractGuard.Copy(sources);
    }
    public string Id { get; }
    public string SourceOpeningId { get; }
    public string OwnerId { get; }
    public ProjectedVoidRole Role { get; }
    public Polygon2Mm Boundary { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record ViewDimensionIntent
{
    public ViewDimensionIntent(string id,Point2Mm start,Point2Mm end,double measuredMm)
    {
        Id=ContractGuard.Text(id,nameof(id));
        Start=start; End=end;
        MeasuredMm=ContractGuard.NonNegative(measuredMm,nameof(measuredMm));
    }
    public string Id { get; }
    public Point2Mm Start { get; }
    public Point2Mm End { get; }
    public double MeasuredMm { get; }
}

public sealed record ViewProfile
{
    public ViewProfile(string id,ViewPurpose purpose,IEnumerable<MaterialRegion> regions,IEnumerable<ProjectedVoid> voids,IEnumerable<ViewDimensionIntent> dimensions)
        :this(id,purpose,regions,voids,dimensions,[],[]) { }

    public ViewProfile(string id,ViewPurpose purpose,IEnumerable<MaterialRegion> regions,
        IEnumerable<ProjectedVoid> voids,IEnumerable<ViewDimensionIntent> dimensions,
        IEnumerable<AnalyticSectionRegion> analyticRegions,
        IEnumerable<AnalyticSectionVoid> analyticVoids)
    {
        Id=ContractGuard.Text(id,nameof(id));
        Purpose=ContractGuard.EnumValue(purpose);
        Regions=ContractGuard.Copy(regions);
        Voids=ContractGuard.Copy(voids);
        Dimensions=ContractGuard.Copy(dimensions);
        AnalyticRegions=ContractGuard.Copy(analyticRegions);
        AnalyticVoids=ContractGuard.Copy(analyticVoids);
        if (Regions.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=Regions.Length ||
            Voids.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=Voids.Length ||
            Dimensions.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=Dimensions.Length ||
            AnalyticRegions.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=AnalyticRegions.Length ||
            AnalyticVoids.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=AnalyticVoids.Length)
            throw new ArgumentException("Duplicate view semantic identity.");
    }
    public string Id { get; }
    public ViewPurpose Purpose { get; }
    public ImmutableArray<MaterialRegion> Regions { get; }
    public ImmutableArray<ProjectedVoid> Voids { get; }
    public ImmutableArray<ViewDimensionIntent> Dimensions { get; }
    public ImmutableArray<AnalyticSectionRegion> AnalyticRegions { get; }
    public ImmutableArray<AnalyticSectionVoid> AnalyticVoids { get; }
}
