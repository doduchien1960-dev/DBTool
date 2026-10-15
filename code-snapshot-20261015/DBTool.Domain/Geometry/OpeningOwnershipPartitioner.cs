using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record AxisAlignedCut
{
    public AxisAlignedCut(Rectangle2Mm xy, Interval1D z) { XY = xy; Z = z; }
    public Rectangle2Mm XY { get; }
    public Interval1D Z { get; }
}
public sealed record RoundOpeningCut
{
    public RoundOpeningCut(WallFrame frame, CircleProfile crossSection, Interval1D depth)
    { Frame = ContractGuard.Required(frame); CrossSection = ContractGuard.Required(crossSection); Depth = depth; }
    public WallFrame Frame { get; }
    public CircleProfile CrossSection { get; }
    public Interval1D Depth { get; }
}
public sealed record OpeningOwnershipPolicy
{
    public OpeningOwnershipPolicy(IEnumerable<string>? ownerPriority = null)
    {
        OwnerPriority = ContractGuard.Copy(ownerPriority ?? []);
        if (OwnerPriority.Distinct(StringComparer.Ordinal).Count() != OwnerPriority.Length)
            throw new ArgumentException("Owner priority must be unique.", nameof(ownerPriority));
    }
    public ImmutableArray<string> OwnerPriority { get; }
}

public sealed record OpeningSolid
{
    public OpeningSolid(string id, IEnumerable<AxisAlignedCut> cuts, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        Cuts = ContractGuard.Copy(cuts);
        RoundCuts = [];
        Sources = ContractGuard.Copy(sources);
        if (Cuts.IsEmpty) throw new ArgumentException("An opening requires at least one cut.", nameof(cuts));
    }
    public OpeningSolid(string id, IEnumerable<RoundOpeningCut> roundCuts, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        RoundCuts = ContractGuard.Copy(roundCuts);
        Cuts = [];
        Sources = ContractGuard.Copy(sources);
        if (RoundCuts.IsEmpty) throw new ArgumentException("An opening requires at least one cut.", nameof(roundCuts));
    }
    public OpeningSolid(string id, IEnumerable<AxisAlignedCut> cuts,
        IEnumerable<RoundOpeningCut> roundCuts, IEnumerable<SourceRef> sources)
    {
        Id = ContractGuard.Text(id, nameof(id));
        Cuts = ContractGuard.Copy(cuts);
        RoundCuts = ContractGuard.Copy(roundCuts);
        Sources = ContractGuard.Copy(sources);
        if (Cuts.IsEmpty && RoundCuts.IsEmpty)
            throw new ArgumentException("An opening requires at least one cut.",nameof(cuts));
    }
    public string Id { get; }
    public ImmutableArray<AxisAlignedCut> Cuts { get; }
    public ImmutableArray<RoundOpeningCut> RoundCuts { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}

public sealed record OwnedOpeningChild
{
    public OwnedOpeningChild(string parentId, string ownerId, string traceId,
        ImmutableArray<AxisAlignedCut> cells, double volumeMm3, ImmutableArray<SourceRef> sources,
        ImmutableArray<RoundOwnedCell> roundCells = default,
        ImmutableArray<RoundUnionOwnedCell> roundUnionCells = default,
        ImmutableArray<CrossFrameRoundOwnedCell> crossFrameRoundCells = default)
    { ParentId = parentId; OwnerId = ownerId; TraceId = traceId; Cells = cells;
      VolumeMm3 = volumeMm3; Sources = sources;
      RoundCells = roundCells.IsDefault ? [] : roundCells;
      RoundUnionCells = roundUnionCells.IsDefault ? [] : roundUnionCells;
      CrossFrameRoundCells = crossFrameRoundCells.IsDefault ? [] : crossFrameRoundCells; }
    public string ParentId { get; }
    public string OwnerId { get; }
    public string TraceId { get; }
    public ImmutableArray<AxisAlignedCut> Cells { get; }
    public double VolumeMm3 { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public ImmutableArray<RoundOwnedCell> RoundCells { get; }
    public ImmutableArray<RoundUnionOwnedCell> RoundUnionCells { get; }
    public ImmutableArray<CrossFrameRoundOwnedCell> CrossFrameRoundCells { get; }
}
public sealed record CrossFrameRoundOwnedCell
{
    public CrossFrameRoundOwnedCell(IEnumerable<RoundOpeningCut> cuts,double volumeMm3)
    {
        Cuts=ContractGuard.Copy(cuts);
        if(Cuts.Length!=2) throw new ArgumentException("An orthogonal union requires two cuts.",nameof(cuts));
        VolumeMm3=ContractGuard.NonNegative(volumeMm3,nameof(volumeMm3));
    }
    public ImmutableArray<RoundOpeningCut> Cuts { get; }
    public double VolumeMm3 { get; }
}
public sealed record RoundUnionOwnedCell
{
    public RoundUnionOwnedCell(WallFrame frame, IEnumerable<CircleProfile> disks,
        Rectangle2Mm localUzBounds, Interval1D localDepth, double volumeMm3)
    {
        Frame = ContractGuard.Required(frame);
        Disks = ContractGuard.Copy(disks);
        if (Disks.IsEmpty) throw new ArgumentException("A union cell requires at least one disk.",nameof(disks));
        LocalUzBounds = ContractGuard.Required(localUzBounds);
        LocalDepth = localDepth;
        VolumeMm3 = ContractGuard.NonNegative(volumeMm3,nameof(volumeMm3));
    }
    public WallFrame Frame { get; }
    public ImmutableArray<CircleProfile> Disks { get; }
    public Rectangle2Mm LocalUzBounds { get; }
    public Interval1D LocalDepth { get; }
    public double VolumeMm3 { get; }
}
public sealed record RoundOwnedCell
{
    public RoundOwnedCell(RoundOpeningCut cut, Rectangle2Mm localUzBounds,
        Interval1D localDepth, double volumeMm3)
    { Cut = cut; LocalUzBounds = localUzBounds; LocalDepth = localDepth; VolumeMm3 = volumeMm3; }
    public RoundOpeningCut Cut { get; }
    public Rectangle2Mm LocalUzBounds { get; }
    public Interval1D LocalDepth { get; }
    public double VolumeMm3 { get; }
}
public sealed record OwnedOpeningSet
{
    public OwnedOpeningSet(ImmutableArray<OwnedOpeningChild> children) { Children = children; }
    public ImmutableArray<OwnedOpeningChild> Children { get; }
}

/// <summary>Exact finite rectangular-prism partition. Other solids return a Block diagnostic.</summary>
public static class OpeningOwnershipPartitioner
{
    public static OperationResult<OwnedOpeningSet> Partition(GeometryModel model, OpeningSolid opening,
        OpeningOwnershipPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(policy);
        if (!opening.RoundCuts.IsEmpty)
            return PartitionRound(model, opening, policy);
        OperationResult<OwnedOpeningSet> Fail(string code, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                opening.Sources.FirstOrDefault(), "axis-aligned rectangular extrusion with finite exact cuts",
                actual, "Opening ownership cannot be partitioned for this geometry.")]);

        var owners = new List<(ComponentGeometry Component, Rectangle2Mm Bounds)>();
        foreach (var component in model.Components)
        {
            if (component.Occupied is not ExtrusionSolid solid ||
                solid.Frame.Origin != new Point3Mm(0, 0, 0) ||
                solid.Frame.U != new Vector3(1, 0, 0) || solid.Frame.V != new Vector3(0, 1, 0) ||
                solid.Frame.N != new Vector3(0, 0, 1) || solid.Depth != component.ZRange ||
                !TryRectangle(solid.Footprint.Outer, out var bounds) ||
                solid.Footprint.InnerLoops.Any(h => !TryRectangle(h, out _)))
                return Fail("OPENING_KERNEL_UNSUPPORTED", component.Id);
            owners.Add((component, bounds));
        }
        var children = ImmutableArray.CreateBuilder<OwnedOpeningChild>();
        foreach (var (component, bounds) in owners.OrderBy(x => x.Component.Id, StringComparer.Ordinal))
        {
            var relevant = opening.Cuts.Where(c => Intersects(bounds, component.ZRange, c)).ToArray();
            if (relevant.Length == 0) continue;
            double[] xs = relevant.SelectMany(c => new[] { c.XY.MinX, c.XY.MaxX })
                .Concat(owners.SelectMany(o => new[] { o.Bounds.MinX, o.Bounds.MaxX }))
                .Concat(component.Occupied is ExtrusionSolid es ? es.Footprint.InnerLoops
                    .SelectMany(h => new[] { h.Vertices.Min(p => p.X), h.Vertices.Max(p => p.X) }) : [])
                .Concat([bounds.MinX, bounds.MaxX]).Where(x => x >= bounds.MinX && x <= bounds.MaxX).Distinct().Order().ToArray();
            double[] ys = relevant.SelectMany(c => new[] { c.XY.MinY, c.XY.MaxY })
                .Concat(owners.SelectMany(o => new[] { o.Bounds.MinY, o.Bounds.MaxY }))
                .Concat(component.Occupied is ExtrusionSolid es2 ? es2.Footprint.InnerLoops
                    .SelectMany(h => new[] { h.Vertices.Min(p => p.Y), h.Vertices.Max(p => p.Y) }) : [])
                .Concat([bounds.MinY, bounds.MaxY]).Where(y => y >= bounds.MinY && y <= bounds.MaxY).Distinct().Order().ToArray();
            double[] zs = relevant.SelectMany(c => new[] { c.Z.Start, c.Z.End })
                .Concat(owners.SelectMany(o => new[] { o.Component.ZRange.Start, o.Component.ZRange.End }))
                .Concat([component.ZRange.Start, component.ZRange.End])
                .Where(z => z >= component.ZRange.Start && z <= component.ZRange.End).Distinct().Order().ToArray();
            var cells = ImmutableArray.CreateBuilder<AxisAlignedCut>();
            double volume = 0;
            for (int ix = 0; ix < xs.Length - 1; ix++)
            for (int iy = 0; iy < ys.Length - 1; iy++)
            for (int iz = 0; iz < zs.Length - 1; iz++)
            {
                double x0 = xs[ix], x1 = xs[ix + 1], y0 = ys[iy], y1 = ys[iy + 1], z0 = zs[iz], z1 = zs[iz + 1];
                if (x1 <= x0 || y1 <= y0 || z1 <= z0) continue;
                double x = (x0 + x1) / 2, y = (y0 + y1) / 2, z = (z0 + z1) / 2;
                if (!relevant.Any(c => Inside(c.XY, x, y) && z > c.Z.Start && z < c.Z.End)) continue;
                if (solidHasHole(component.Occupied, x, y)) continue;
                var candidates = owners.Where(o => Inside(o.Bounds, x, y) &&
                    z > o.Component.ZRange.Start && z < o.Component.ZRange.End &&
                    !solidHasHole(o.Component.Occupied, x, y)).Select(o => o.Component.Id).ToArray();
                if (candidates.Length > 1)
                {
                    if (candidates.Any(id => !policy.OwnerPriority.Contains(id)))
                        return Fail("OPENING_OWNER_AMBIGUOUS", string.Join(",", candidates));
                    string chosen = candidates.OrderBy(id => policy.OwnerPriority.IndexOf(id)).First();
                    if (chosen != component.Id) continue;
                }
                double cellVolume = (x1 - x0) * (y1 - y0) * (z1 - z0);
                if (!double.IsFinite(cellVolume) || !double.IsFinite(volume + cellVolume))
                    return Fail("OPENING_VOLUME_OVERFLOW", component.Id);
                volume += cellVolume;
                cells.Add(new AxisAlignedCut(new Rectangle2Mm(x0, y0, x1, y1), new Interval1D(z0, z1)));
            }
            if (cells.Count > 0)
                children.Add(new OwnedOpeningChild(opening.Id, component.Id,
                    $"{opening.Id}/owner/{component.Id}", cells.ToImmutable(), volume, opening.Sources));
        }
        return new(true, new OwnedOpeningSet(children.ToImmutable()), []);

        static bool solidHasHole(SolidDescriptor descriptor, double x, double y)
        {
            var solid = (ExtrusionSolid)descriptor;
            return solid.Footprint.InnerLoops.Any(h => TryRectangle(h, out var r) && Inside(r, x, y));
        }
    }

    private static OperationResult<OwnedOpeningSet> PartitionRound(GeometryModel model,
        OpeningSolid opening, OpeningOwnershipPolicy policy)
    {
        OperationResult<OwnedOpeningSet> Fail(string code, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                opening.Sources.FirstOrDefault(), "one analytic round cut and rectangular occupied solids",
                actual, "Round ownership partition is unavailable.")]);
        var cuts = opening.RoundCuts.Distinct().ToArray();
        if (cuts.Length > 1 || !opening.Cuts.IsEmpty || HasPotentialOwnerOverlap(model))
            return PartitionRoundUnion(model, opening, cuts, policy);
        if (cuts.Length != 1 || !opening.Cuts.IsEmpty)
            return Fail("OPENING_ROUND_UNION_UNSUPPORTED", $"{cuts.Length} distinct round cuts");
        var cut = cuts[0];
        var result = ImmutableArray.CreateBuilder<OwnedOpeningChild>();
        foreach (var component in model.Components.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (component.Occupied is not ExtrusionSolid solid ||
                solid.Frame.Origin != new Point3Mm(0,0,0) ||
                solid.Frame.U != new Vector3(1,0,0) || solid.Frame.V != new Vector3(0,1,0) ||
                solid.Frame.N != new Vector3(0,0,1) || !TryRectangle(component.Footprint.Outer,out var bounds) ||
                component.Footprint.InnerLoops.Any(h=>!TryRectangle(h,out _)))
                return Fail("OPENING_KERNEL_UNSUPPORTED", component.Id);
            var local = Corners(bounds).Select(p=>cut.Frame.ToLocal(new Point3Mm(p.X,p.Y,0))).ToArray();
            double u0=local.Min(p=>p.U),u1=local.Max(p=>p.U);
            double v0=Math.Max(cut.Depth.Start,local.Min(p=>p.V));
            double v1=Math.Min(cut.Depth.End,local.Max(p=>p.V));
            if(v1<=v0) continue;
            double area=DiskRectangleArea(cut.CrossSection.Center.X,cut.CrossSection.Center.Y,
                cut.CrossSection.RadiusMm,u0,u1,component.ZRange.Start,component.ZRange.End);
            double volume=area*(v1-v0);
            foreach(var hole in component.Footprint.InnerLoops)
            {
                TryRectangle(hole,out var inner);
                var innerLocal=Corners(inner).Select(p=>cut.Frame.ToLocal(new Point3Mm(p.X,p.Y,0))).ToArray();
                double hV0=Math.Max(cut.Depth.Start,innerLocal.Min(p=>p.V));
                double hV1=Math.Min(cut.Depth.End,innerLocal.Max(p=>p.V));
                if(hV1<=hV0) continue;
                volume-=DiskRectangleArea(cut.CrossSection.Center.X,cut.CrossSection.Center.Y,
                    cut.CrossSection.RadiusMm,innerLocal.Min(p=>p.U),innerLocal.Max(p=>p.U),
                    component.ZRange.Start,component.ZRange.End)*(hV1-hV0);
            }
            if (!double.IsFinite(volume) || volume < -1e-6)
                return Fail("OPENING_VOLUME_INVALID", component.Id);
            if (volume<=1e-6) continue;
            var z0=Math.Max(component.ZRange.Start,cut.CrossSection.Center.Y-cut.CrossSection.RadiusMm);
            var z1=Math.Min(component.ZRange.End,cut.CrossSection.Center.Y+cut.CrossSection.RadiusMm);
            var roundCell=new RoundOwnedCell(cut,new Rectangle2Mm(u0,z0,u1,z1),
                new Interval1D(v0,v1),volume);
            result.Add(new OwnedOpeningChild(opening.Id,component.Id,$"{opening.Id}/owner/{component.Id}",
                [],volume,opening.Sources,[roundCell]));
        }
        return new(true,new OwnedOpeningSet(result.ToImmutable()),[]);
    }

    private static bool HasPotentialOwnerOverlap(GeometryModel model)
    {
        var components=model.Components.ToArray();
        for (int i=0;i<components.Length;i++)
        for (int j=i+1;j<components.Length;j++)
        {
            if (!TryRectangle(components[i].Footprint.Outer,out var a) ||
                !TryRectangle(components[j].Footprint.Outer,out var b)) continue;
            if (a.MinX<b.MaxX && a.MaxX>b.MinX && a.MinY<b.MaxY && a.MaxY>b.MinY &&
                components[i].ZRange.Start<components[j].ZRange.End &&
                components[i].ZRange.End>components[j].ZRange.Start)
                return true;
        }
        return false;
    }

    private static OperationResult<OwnedOpeningSet> PartitionRoundUnion(GeometryModel model,
        OpeningSolid opening, RoundOpeningCut[] cuts, OpeningOwnershipPolicy policy)
    {
        OperationResult<OwnedOpeningSet> Fail(string code, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                opening.Sources.FirstOrDefault(), "co-planar circles and rectangular occupied owners",
                actual, "Round union ownership requires a supported exact partition.")]);
        var first = cuts[0];
        if (cuts.Any(c => c.Frame != first.Frame))
        {
            if(TryOrthogonalFullRoundUnion(model,opening,cuts,out var orthogonal))
                return new(true,orthogonal,[]);
            return Fail("OPENING_ROUND_UNION_UNSUPPORTED", "different wall frames outside the exact orthogonal kernel");
        }
        var owners = new List<(ComponentGeometry Component, Rectangle2Mm Bounds)>();
        foreach (var component in model.Components)
        {
            if (component.Occupied is not ExtrusionSolid solid ||
                solid.Frame.Origin != new Point3Mm(0,0,0) ||
                solid.Frame.U != new Vector3(1,0,0) || solid.Frame.V != new Vector3(0,1,0) ||
                solid.Frame.N != new Vector3(0,0,1) ||
                !TryRectangle(component.Footprint.Outer, out var bounds) ||
                component.Footprint.InnerLoops.Any(h=>!TryRectangle(h,out _)))
                return Fail("OPENING_KERNEL_UNSUPPORTED", component.Id);
            owners.Add((component, bounds));
        }
        double[] xs=owners.SelectMany(o=>new[]{o.Bounds.MinX,o.Bounds.MaxX})
            .Concat(owners.SelectMany(o=>o.Component.Footprint.InnerLoops.SelectMany(h=>
                new[]{h.Vertices.Min(p=>p.X),h.Vertices.Max(p=>p.X)})))
            .Concat(opening.Cuts.SelectMany(c=>new[]{c.XY.MinX,c.XY.MaxX}))
            .Distinct().Order().ToArray();
        double[] ys=owners.SelectMany(o=>new[]{o.Bounds.MinY,o.Bounds.MaxY})
            .Concat(owners.SelectMany(o=>o.Component.Footprint.InnerLoops.SelectMany(h=>
                new[]{h.Vertices.Min(p=>p.Y),h.Vertices.Max(p=>p.Y)})))
            .Concat(opening.Cuts.SelectMany(c=>new[]{c.XY.MinY,c.XY.MaxY}))
            .Distinct().Order().ToArray();
        double[] zs=owners.SelectMany(o=>new[]{o.Component.ZRange.Start,o.Component.ZRange.End})
            .Concat(opening.Cuts.SelectMany(c=>new[]{c.Z.Start,c.Z.End}))
            .Distinct().Order().ToArray();
        var volumes=owners.ToDictionary(o=>o.Component.Id,_=>0d,StringComparer.Ordinal);
        var ownedCells=owners.ToDictionary(o=>o.Component.Id,_=>new List<RoundUnionOwnedCell>(),StringComparer.Ordinal);
        var rectangularCells=owners.ToDictionary(o=>o.Component.Id,_=>new List<AxisAlignedCut>(),StringComparer.Ordinal);
        for (int ix=0;ix<xs.Length-1;ix++)
        for (int iy=0;iy<ys.Length-1;iy++)
        for (int iz=0;iz<zs.Length-1;iz++)
        {
            double x0=xs[ix],x1=xs[ix+1],y0=ys[iy],y1=ys[iy+1],z0=zs[iz],z1=zs[iz+1];
            if (x1<=x0 || y1<=y0 || z1<=z0) continue;
            double x=(x0+x1)/2,y=(y0+y1)/2,z=(z0+z1)/2;
            var candidates=owners.Where(o=>Inside(o.Bounds,x,y) &&
                z>o.Component.ZRange.Start && z<o.Component.ZRange.End &&
                !o.Component.Footprint.InnerLoops.Any(h=>TryRectangle(h,out var r) && Inside(r,x,y)))
                .Select(o=>o.Component.Id).ToArray();
            if (candidates.Length==0) continue;
            bool rectangular=opening.Cuts.Any(c=>Inside(c.XY,x,y) && z>c.Z.Start && z<c.Z.End);
            List<RoundUnionOwnedCell> pieces;
            double volume;
            if (rectangular)
            {
                pieces=[];
                volume=(x1-x0)*(y1-y0)*(z1-z0);
            }
            else
            {
                var local=Corners(new Rectangle2Mm(x0,y0,x1,y1))
                    .Select(p=>first.Frame.ToLocal(new Point3Mm(p.X,p.Y,0))).ToArray();
                pieces=UnionCells(local.Min(p=>p.U),local.Max(p=>p.U),
                    local.Min(p=>p.V),local.Max(p=>p.V),z0,z1);
                volume=pieces.Sum(p=>p.VolumeMm3);
            }
            if (volume<=1e-6) continue;
            string ownerId;
            if (candidates.Length==1) ownerId=candidates[0];
            else
            {
                if (candidates.Any(id=>!policy.OwnerPriority.Contains(id)))
                    return Fail("OPENING_OWNER_AMBIGUOUS",string.Join(",",candidates));
                ownerId=candidates.OrderBy(id=>policy.OwnerPriority.IndexOf(id)).First();
            }
            if (!double.IsFinite(volume) || !double.IsFinite(volumes[ownerId]+volume))
                return Fail("OPENING_VOLUME_INVALID",ownerId);
            volumes[ownerId]+=volume;
            ownedCells[ownerId].AddRange(pieces);
            if (rectangular) rectangularCells[ownerId].Add(
                new AxisAlignedCut(new Rectangle2Mm(x0,y0,x1,y1),new Interval1D(z0,z1)));
        }
        var children = ImmutableArray.CreateBuilder<OwnedOpeningChild>();
        foreach (var (ownerId,volume) in volumes.OrderBy(o=>o.Key,StringComparer.Ordinal))
        {
            if (volume<=1e-6) continue;
            children.Add(new OwnedOpeningChild(opening.Id,ownerId,
                $"{opening.Id}/owner/{ownerId}",rectangularCells[ownerId].ToImmutableArray(),volume,opening.Sources,
                roundUnionCells:ownedCells[ownerId].ToImmutableArray()));
        }
        return new(true,new OwnedOpeningSet(children.ToImmutable()),[]);

        List<RoundUnionOwnedCell> UnionCells(double u0,double u1,double v0,double v1,double z0,double z1)
        {
            var pieces=new List<RoundUnionOwnedCell>();
            if (u1<=u0 || v1<=v0 || z1<=z0) return pieces;
            var depths=cuts.SelectMany(c=>new[]{c.Depth.Start,c.Depth.End})
                .Concat([v0,v1]).Where(v=>v>=v0 && v<=v1).Distinct().Order().ToArray();
            for (int i=0;i<depths.Length-1;i++)
            {
                double start=depths[i],end=depths[i+1];
                if (end<=start) continue;
                double mid=(start+end)/2;
                var circles=cuts.Where(c=>mid>c.Depth.Start && mid<c.Depth.End)
                    .Select(c=>c.CrossSection).ToArray();
                if (circles.Length==0) continue;
                double volume=DiskUnionRectangleArea(circles,u0,u1,z0,z1)*(end-start);
                if (volume>1e-6)
                    pieces.Add(new RoundUnionOwnedCell(first.Frame,circles,
                        new Rectangle2Mm(u0,z0,u1,z1),new Interval1D(start,end),volume));
            }
            return pieces;
        }
    }

    // For two complete perpendicular cylinders, the common Steinmetz volume is 16R^3/3.
    // All constraints here are exact; partial owners or shifted cylinders use the general Block path.
    private static bool TryOrthogonalFullRoundUnion(GeometryModel model,OpeningSolid opening,
        RoundOpeningCut[] cuts,out OwnedOpeningSet owned)
    {
        owned=null!;
        if(cuts.Length!=2 || !opening.Cuts.IsEmpty || model.Components.Length!=1)
            return false;
        var a=cuts[0];var b=cuts[1];
        if(a.CrossSection.RadiusMm!=b.CrossSection.RadiusMm ||
            a.Frame.Up!=new Vector3(0,0,1) || b.Frame.Up!=new Vector3(0,0,1) ||
            !HorizontalAxis(a.Frame.Outward) || !HorizontalAxis(b.Frame.Outward) ||
            a.Frame.Outward.X*b.Frame.Outward.X+
                a.Frame.Outward.Y*b.Frame.Outward.Y!=0)
            return false;
        var centerA=a.Frame.ToGlobal(a.CrossSection.Center.X,0,a.CrossSection.Center.Y);
        var centerB=b.Frame.ToGlobal(b.CrossSection.Center.X,0,b.CrossSection.Center.Y);
        if(centerA!=centerB) return false;
        double radius=a.CrossSection.RadiusMm;
        if(a.Depth.Start>-radius || a.Depth.End<radius ||
            b.Depth.Start>-radius || b.Depth.End<radius)
            return false;
        var component=model.Components[0];
        if(component.Occupied is not ExtrusionSolid solid ||
            solid.Frame.Origin!=new Point3Mm(0,0,0) ||
            solid.Frame.U!=new Vector3(1,0,0) ||
            solid.Frame.V!=new Vector3(0,1,0) ||
            solid.Frame.N!=new Vector3(0,0,1) ||
            solid.Depth!=component.ZRange ||
            !TryRectangle(component.Footprint.Outer,out var bounds) ||
            !component.Footprint.InnerLoops.IsEmpty ||
            !ContainsCut(a) || !ContainsCut(b))
            return false;
        double intersection=16d/3*radius*radius*radius;
        double volume=Math.PI*radius*radius*(a.Depth.Length+b.Depth.Length)-intersection;
        if(!double.IsFinite(volume) || volume<=0) return false;
        var cell=new CrossFrameRoundOwnedCell(cuts,volume);
        owned=new OwnedOpeningSet([new OwnedOpeningChild(opening.Id,component.Id,
            $"{opening.Id}/owner/{component.Id}",[],volume,opening.Sources,
            crossFrameRoundCells:[cell])]);
        return true;

        bool ContainsCut(RoundOpeningCut cut)
        {
            foreach(double u in new[]{cut.CrossSection.Center.X-radius,
                cut.CrossSection.Center.X+radius})
            foreach(double v in new[]{cut.Depth.Start,cut.Depth.End})
            foreach(double z in new[]{cut.CrossSection.Center.Y-radius,
                cut.CrossSection.Center.Y+radius})
            {
                var p=cut.Frame.ToGlobal(u,v,z);
                if(p.X<bounds.MinX || p.X>bounds.MaxX ||
                    p.Y<bounds.MinY || p.Y>bounds.MaxY ||
                    p.Z<component.ZRange.Start || p.Z>component.ZRange.End)
                    return false;
            }
            return true;
        }
        static bool HorizontalAxis(Vector3 direction)=>
            (Math.Abs(direction.X)==1 && direction.Y==0 && direction.Z==0) ||
            (Math.Abs(direction.Y)==1 && direction.X==0 && direction.Z==0);
    }

    // The vertical union envelope changes only at circle endpoints, intersections,
    // or a circle's crossing of the rectangular top/bottom. Integrate each arc exactly.
    private static double DiskUnionRectangleArea(CircleProfile[] circles,
        double u0,double u1,double z0,double z1)
    {
        var events=new List<double>{u0,u1};
        foreach (var circle in circles)
        {
            double cx=circle.Center.X,cz=circle.Center.Y,r=circle.RadiusMm;
            events.Add(cx-r);events.Add(cx+r);
            foreach(double z in new[]{z0,z1})
            {
                double d=z-cz;
                if (Math.Abs(d)<r)
                {
                    double h=Math.Sqrt(r*r-d*d);
                    events.Add(cx-h);events.Add(cx+h);
                }
            }
        }
        for (int i=0;i<circles.Length;i++)
        for (int j=i+1;j<circles.Length;j++)
        {
            var a=circles[i];var b=circles[j];
            double dx=b.Center.X-a.Center.X,dz=b.Center.Y-a.Center.Y;
            double d=Math.Sqrt(dx*dx+dz*dz),ra=a.RadiusMm,rb=b.RadiusMm;
            if (d==0 || d>ra+rb || d<Math.Abs(ra-rb)) continue;
            double along=(ra*ra-rb*rb+d*d)/(2*d);
            double h=Math.Sqrt(Math.Max(0,ra*ra-along*along));
            double x=a.Center.X+along*dx/d;
            events.Add(x-h*dz/d);events.Add(x+h*dz/d);
        }
        var ordered=events.Where(x=>x>=u0 && x<=u1).Distinct().Order().ToArray();
        double total=0;
        for (int i=0;i<ordered.Length-1;i++)
        {
            double a=ordered[i],b=ordered[i+1],mid=(a+b)/2;
            if (b<=a) continue;
            var bands=new List<(double Low,double High,int LowCircle,int HighCircle)>();
            for (int c=0;c<circles.Length;c++)
            {
                var circle=circles[c];double r=circle.RadiusMm,dx=mid-circle.Center.X;
                if (Math.Abs(dx)>=r) continue;
                double h=Math.Sqrt(r*r-dx*dx);
                double low=circle.Center.Y-h,high=circle.Center.Y+h;
                if (high<=z0 || low>=z1) continue;
                bands.Add((Math.Max(z0,low),Math.Min(z1,high),low<z0?-1:c,high>z1?-1:c));
            }
            var sorted=bands.OrderBy(v=>v.Low).ThenBy(v=>v.High).ToArray();
            if (sorted.Length==0) continue;
            var current=sorted[0];
            for (int k=1;k<=sorted.Length;k++)
            {
                if (k<sorted.Length && sorted[k].Low<=current.High)
                {
                    if (sorted[k].High>current.High)
                        current=(current.Low,sorted[k].High,current.LowCircle,sorted[k].HighCircle);
                    continue;
                }
                total+=Integrate(current.HighCircle,true)-Integrate(current.LowCircle,false);
                if (k<sorted.Length) current=sorted[k];
            }

            double Integrate(int index,bool upper)
            {
                if (index<0) return (upper?z1:z0)*(b-a);
                var circle=circles[index];double r=circle.RadiusMm,cx=circle.Center.X;
                double Primitive(double u)
                {
                    double x=u-cx,s=Math.Sqrt(Math.Max(0,r*r-x*x));
                    return (x*s+r*r*Math.Asin(Math.Clamp(x/r,-1,1)))/2;
                }
                return circle.Center.Y*(b-a)+(upper?1:-1)*(Primitive(b)-Primitive(a));
            }
        }
        return Math.Max(0,total);
    }

    private static Point2Mm[] Corners(Rectangle2Mm r) =>
        [new(r.MinX,r.MinY),new(r.MinX,r.MaxY),new(r.MaxX,r.MinY),new(r.MaxX,r.MaxY)];

    private static double DiskRectangleArea(double cx,double cz,double radius,
        double u0,double u1,double z0,double z1)
    {
        u0=Math.Max(u0,cx-radius);u1=Math.Min(u1,cx+radius);
        if(u1<=u0 || z1<=cz-radius || z0>=cz+radius) return 0;
        var events=new List<double>{u0,u1,cx};
        foreach(double z in new[]{z0,z1})
        {
            double d=z-cz;
            if(Math.Abs(d)<radius)
            {
                double x=Math.Sqrt(radius*radius-d*d);
                events.Add(cx-x);events.Add(cx+x);
            }
        }
        var ordered=events.Where(x=>x>=u0 && x<=u1).Distinct().Order().ToArray();
        double total=0;
        for(int i=0;i<ordered.Length-1;i++)
        {
            double a=ordered[i],b=ordered[i+1],mid=(a+b)/2;
            if(b<=a) continue;
            double half=Math.Sqrt(Math.Max(0,radius*radius-(mid-cx)*(mid-cx)));
            double upper=Math.Min(z1,cz+half),lower=Math.Max(z0,cz-half);
            if(upper<=lower) continue;
            bool topArc=cz+half<z1, bottomArc=cz-half>z0;
            double constant=(topArc?cz:z1)-(bottomArc?cz:z0);
            int coefficient=(topArc?1:0)+(bottomArc?1:0);
            double Primitive(double u)
            {
                double x=u-cx,s=Math.Sqrt(Math.Max(0,radius*radius-x*x));
                return (x*s+radius*radius*Math.Asin(Math.Clamp(x/radius,-1,1)))/2;
            }
            total+=constant*(b-a)+coefficient*(Primitive(b)-Primitive(a));
        }
        return Math.Max(0,total);
    }

    private static bool Intersects(Rectangle2Mm bounds, Interval1D z, AxisAlignedCut cut) =>
        bounds.MinX < cut.XY.MaxX && bounds.MaxX > cut.XY.MinX &&
        bounds.MinY < cut.XY.MaxY && bounds.MaxY > cut.XY.MinY &&
        z.Start < cut.Z.End && z.End > cut.Z.Start;
    private static bool Inside(Rectangle2Mm r, double x, double y) =>
        x > r.MinX && x < r.MaxX && y > r.MinY && y < r.MaxY;
    private static bool TryRectangle(Polygon2Mm polygon, out Rectangle2Mm rectangle)
    {
        var points = polygon.Vertices;
        double x0 = points.Min(p => p.X), x1 = points.Max(p => p.X);
        double y0 = points.Min(p => p.Y), y1 = points.Max(p => p.Y);
        if (points.Length != 4 || points.Any(p => (p.X != x0 && p.X != x1) || (p.Y != y0 && p.Y != y1)))
        {
            rectangle = null!;
            return false;
        }
        rectangle = new Rectangle2Mm(x0, y0, x1, y1);
        return true;
    }
}
