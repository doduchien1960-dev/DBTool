using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public enum FaceRole { Inner, Outer, Top, Bottom, OpeningReveal, Contact }
public sealed record FacePatch
{
    public FacePatch(string id, string ownerId, FaceRole role, Profile2Mm boundary, Frame3Mm frame, AreaMm2 areaMm2, Vector3 normal, string? adjacentComponentId, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        OwnerId = ContractGuard.Text(ownerId, nameof(ownerId));
        Role = ContractGuard.EnumValue(role);
        Boundary = ContractGuard.Required(boundary);
        Frame = ContractGuard.Required(frame);
        AreaMm2 = areaMm2;
        Normal = ContractGuard.Required(normal);
        AdjacentComponentId = adjacentComponentId;
        Sources = ContractGuard.Copy(sources);
        if (Math.Abs(FrameInvariant.Dot(normal,normal)-1)>1e-9) throw new ArgumentException("Face normal must be unit.");
    }
    public string Id { get; }
    public string OwnerId { get; }
    public FaceRole Role { get; }
    public Profile2Mm Boundary { get; }
    public Frame3Mm Frame { get; }
    public AreaMm2 AreaMm2 { get; }
    public Vector3 Normal { get; }
    public string? AdjacentComponentId { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}
public sealed record ComponentGeometry
{
    public ComponentGeometry(string id, ComponentKind kind, string materialId, Footprint footprint, Interval1D zRange, SolidDescriptor occupied, IEnumerable<OpeningProfile> voids, IEnumerable<FacePatch> facePatches, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        Kind = ContractGuard.EnumValue(kind);
        MaterialId = ContractGuard.Text(materialId, nameof(materialId));
        Footprint = ContractGuard.Required(footprint);
        ZRange = zRange;
        Occupied = ContractGuard.Required(occupied);
        Voids = ContractGuard.Copy(voids);
        FacePatches = ContractGuard.Copy(facePatches);
        Sources = ContractGuard.Copy(sources);
        if (Voids.Any(v=>v.OwnerId != id) || FacePatches.Any(f=>f.OwnerId != id)) throw new ArgumentException("Component ownership mismatch.");
        if (Voids.Select(v=>v.Id).Distinct(StringComparer.Ordinal).Count()!=Voids.Length || FacePatches.Select(f=>f.Id).Distinct(StringComparer.Ordinal).Count()!=FacePatches.Length)
            throw new ArgumentException("Duplicate component void or face identity.");
    }
    public string Id { get; }
    public ComponentKind Kind { get; }
    public string MaterialId { get; }
    public Footprint Footprint { get; }
    public Interval1D ZRange { get; }
    public SolidDescriptor Occupied { get; }
    public ImmutableArray<OpeningProfile> Voids { get; }
    public ImmutableArray<FacePatch> FacePatches { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}
