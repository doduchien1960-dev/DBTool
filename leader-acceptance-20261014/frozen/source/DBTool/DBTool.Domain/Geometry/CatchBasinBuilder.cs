using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record CatchBasinSurface
{
    public CatchBasinSurface(string id, double areaMm2, ImmutableArray<SourceRef> sources)
    { Id = id; AreaMm2 = areaMm2; Sources = sources; }
    public CatchBasinSurface(FacePatch patch)
    { Patch = ContractGuard.Required(patch); Id = patch.Id;
      AreaMm2 = patch.AreaMm2.Value; Sources = patch.Sources; }
    public string Id { get; }
    public double AreaMm2 { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public FacePatch? Patch { get; }
}
public sealed record CatchBasinGeometry
{
    public CatchBasinGeometry(UnionSolid occupiedUnion,
        ImmutableArray<OwnedOpeningChild> openingOwnedChildren,
        ImmutableArray<CatchBasinSurface> exteriorFaces,
        ImmutableArray<CatchBasinSurface> contactPatches,
        Interval1D openingZRange, double chamberBottomZmm, double chamberStartVMm,
        double openingRevealAreaMm2, double totalOccupiedVolumeMm3,
        ImmutableArray<FacePatch> openingRevealPatches = default)
    { OccupiedUnion = occupiedUnion; OpeningOwnedChildren = openingOwnedChildren;
      ExteriorFaces = exteriorFaces; ContactPatches = contactPatches;
      OpeningZRange = openingZRange; ChamberBottomZmm = chamberBottomZmm;
      ChamberStartVMm = chamberStartVMm; OpeningRevealAreaMm2 = openingRevealAreaMm2;
      TotalOccupiedVolumeMm3 = totalOccupiedVolumeMm3;
      OpeningRevealPatches = openingRevealPatches.IsDefault ? [] : openingRevealPatches; }
    public UnionSolid OccupiedUnion { get; }
    public ImmutableArray<OwnedOpeningChild> OpeningOwnedChildren { get; }
    public ImmutableArray<CatchBasinSurface> ExteriorFaces { get; }
    public ImmutableArray<CatchBasinSurface> ContactPatches { get; }
    public Interval1D OpeningZRange { get; }
    public double ChamberBottomZmm { get; }
    public double ChamberStartVMm { get; }
    public double OpeningRevealAreaMm2 { get; }
    public ImmutableArray<FacePatch> OpeningRevealPatches { get; }
    public double TotalOccupiedVolumeMm3 { get; }
}

public static class CatchBasinBuilder
{
    public static OperationResult<CatchBasinGeometry> Build(ValidatedManholeSpec validatedSpec,
        GeometryModel geometry, CatchBasinSpec basin, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(validatedSpec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(basin);
        ArgumentNullException.ThrowIfNull(policy);
        var input = validatedSpec.Input;
        var source = basin.SourceRefs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value).FirstOrDefault();
        OperationResult<CatchBasinGeometry> Fail(string code, string expected, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Component, input.Id,
                basin.Owner == CatchBasinOwner.Body ? $"Wall/{basin.Wall}" : "Neck",
                source, expected, actual, "Catch basin geometry cannot be built.")]);
        if (policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || policy.Version != validatedSpec.RuleVersion)
            return Fail("CATCH_POLICY_UNSUPPORTED", "S2Geometry-1/v1", policy.Version);
        if (basin.Owner == CatchBasinOwner.Neck && basin.Position != HorizontalPosition.Center)
            return Fail("CATCH_NECK_POSITION_UNSUPPORTED", "Center", basin.Position.ToString());
        if (basin.ChamberClearHeightMm < basin.OpeningHeightMm)
            return Fail("CATCH_CHAMBER_TOO_SHORT", "chamber height >= opening height", basin.ChamberClearHeightMm.ToString());
        var hostId = basin.Owner == CatchBasinOwner.Body ? $"Wall/{basin.Wall}" : "Neck";
        var host = geometry.Components.FirstOrDefault(c => c.Id == hostId);
        if (host is null) return Fail("CATCH_HOST_MISSING", "existing host", hostId);
        double hostTop = host.ZRange.End;
        double hostThickness = input.WallThicknessMm;
        double clearWidth = basin.Wall is WallId.T1 or WallId.T3 ? input.L : input.B;
        WallFrame frame;
        if (basin.Owner == CatchBasinOwner.Neck)
        {
            if (host.Footprint.InnerLoops.Length != 1 ||
                !TryRectangle(host.Footprint.Outer, out var outer) ||
                !TryRectangle(host.Footprint.InnerLoops[0], out var inner))
                return Fail("CATCH_NECK_HOST_UNSUPPORTED", "rectangular neck ring", host.Id);
            double width = inner.MaxX-inner.MinX, depth = inner.MaxY-inner.MinY;
            clearWidth = basin.Wall is WallId.T1 or WallId.T3 ? width : depth;
            hostThickness = basin.Wall switch
            {
                WallId.T1 => outer.MaxY-inner.MaxY,
                WallId.T2 => outer.MaxX-inner.MaxX,
                WallId.T3 => inner.MinY-outer.MinY,
                _ => inner.MinX-outer.MinX
            };
            if (hostThickness <= 0)
                return Fail("CATCH_NECK_HOST_UNSUPPORTED", "positive neck wall thickness", hostThickness.ToString());
            var centered = WallFrame.Create(basin.Wall,width,depth);
            frame = new WallFrame(basin.Wall,
                new Point3Mm(centered.Origin.X+(inner.MinX+inner.MaxX)/2,
                    centered.Origin.Y+(inner.MinY+inner.MaxY)/2,0),
                centered.Left,centered.Outward,centered.Up);
        }
        else frame = WallFrame.Create(basin.Wall,input.L,input.B);
        double centerU = basin.Position switch
        {
            HorizontalPosition.Left => -clearWidth / 2 + basin.OpeningWidthMm / 2 + basin.OffsetMm,
            HorizontalPosition.Right => clearWidth / 2 - basin.OpeningWidthMm / 2 + basin.OffsetMm,
            _ => basin.OffsetMm
        };
        double z0 = hostTop - basin.OpeningHeightMm;
        double bottom = hostTop - basin.ChamberClearHeightMm;
        double u0 = centerU - basin.OpeningWidthMm / 2, u1 = centerU + basin.OpeningWidthMm / 2;
        if (u0 < -clearWidth / 2 || u1 > clearWidth / 2 || z0 < host.ZRange.Start ||
            !new[] { hostTop, hostThickness, centerU, z0, bottom, u0, u1 }.All(double.IsFinite))
            return Fail("CATCH_OPENING_OUTSIDE_HOST", "opening within host", $"u={u0}..{u1};z={z0}..{hostTop}");
        var openingCut = LocalBox(frame, u0, u1, 0, hostThickness, z0, hostTop);
        var partition = OpeningOwnershipPartitioner.Partition(geometry,
            new OpeningSolid($"{hostId}/Catch", [openingCut], basin.SourceRefs.Values), new OpeningOwnershipPolicy());
        if (!partition.Success)
            return new(false, null, partition.Diagnostics);
        var owned = partition.Value!.Children;
        if (owned.IsEmpty || owned.Sum(c => c.VolumeMm3) <= 0)
            return Fail("CATCH_OPENING_NO_OWNER", "owned host cut", "empty");
        double half = basin.ChamberClearWidthMm / 2, t = basin.WallThicknessMm;
        double v0 = hostThickness, v1 = v0 + basin.ChamberClearDepthMm;
        double floorTop = bottom + basin.BaseThicknessMm;
        var partsList = new List<AxisAlignedCut>
        {
            LocalBox(frame, centerU-half-t, centerU+half+t, v0, v1+t, bottom, floorTop),
            LocalBox(frame, centerU-half-t, centerU-half, v0, v1, floorTop, hostTop),
            LocalBox(frame, centerU+half, centerU+half+t, v0, v1, floorTop, hostTop),
            LocalBox(frame, centerU-half-t, centerU+half+t, v1, v1+t, floorTop, hostTop)
        };
        if (basin.PadThicknessMm > 0)
        {
            double padExp = basin.PadExpansionMm;
            partsList.Add(LocalBox(frame, centerU - half - t - padExp, centerU + half + t + padExp,
                v0, v1 + t + padExp, bottom - basin.PadThicknessMm, bottom));
        }
        var parts = partsList.ToArray();
        if (parts.Any(p => p.Z.Length <= 0))
            return Fail("CATCH_DIMENSION_INVALID", "base thickness < chamber height", basin.BaseThicknessMm.ToString());
        var occupied = new UnionSolid(parts.Select(p => ToSolid(p)));
        var sources = basin.SourceRefs.Values.ToImmutableArray();
        FacePatch Reveal(string side, Point3Mm origin, Vector3 axisU, Vector3 axisV,
            Vector3 normal, double width, double height) =>
            new($"{hostId}/Catch/Reveal/{side}",hostId,FaceRole.OpeningReveal,
                new RectangleProfile(new Rectangle2Mm(0,0,width,height)),
                new Frame3Mm(origin,axisU,axisV,normal),new AreaMm2(width*height),
                normal,null,sources);
        var left=frame.Left;var outward=frame.Outward;var up=frame.Up;
        var revealPatches=ImmutableArray.Create(
            Reveal("Left",frame.ToGlobal(u0,0,z0),outward,up,left,
                hostThickness,basin.OpeningHeightMm),
            Reveal("Right",frame.ToGlobal(u1,0,z0),up,outward,
                new Vector3(-left.X,-left.Y,-left.Z),
                basin.OpeningHeightMm,hostThickness),
            Reveal("Sill",frame.ToGlobal(u0,0,z0),left,outward,up,
                basin.OpeningWidthMm,hostThickness));
        double reveal=revealPatches.Sum(p=>p.AreaMm2.Value);
        var measured=RectangularSolidUnion.Measure(occupied);
        if(!measured.Success) return new(false,null,measured.Diagnostics);
        double totalVolume=measured.Value!.Value;
        var (exterior,contact)=ExposedSurfaces(parts,frame,v0,hostId,sources);
        if (!new[] { reveal, totalVolume, exterior.Sum(f=>f.AreaMm2),
            contact.Sum(f=>f.AreaMm2) }.All(double.IsFinite))
            return Fail("CATCH_DIMENSION_INVALID", "finite surface and volume", "overflow");
        return new(true, new CatchBasinGeometry(occupied, owned, exterior, contact,
            new Interval1D(z0, hostTop), bottom, v0, reveal, totalVolume,revealPatches), []);
    }

    private static (ImmutableArray<CatchBasinSurface> Exterior,
        ImmutableArray<CatchBasinSurface> Contact) ExposedSurfaces(
            AxisAlignedCut[] parts, WallFrame wall, double hostOuterV, string hostId,
            ImmutableArray<SourceRef> sources)
    {
        double[] xs=parts.SelectMany(p=>new[]{p.XY.MinX,p.XY.MaxX}).Distinct().Order().ToArray();
        double[] ys=parts.SelectMany(p=>new[]{p.XY.MinY,p.XY.MaxY}).Distinct().Order().ToArray();
        double[] zs=parts.SelectMany(p=>new[]{p.Z.Start,p.Z.End}).Distinct().Order().ToArray();
        bool Occupied(int i,int j,int k)
        {
            if(i<0 || i>=xs.Length-1 || j<0 || j>=ys.Length-1 || k<0 || k>=zs.Length-1)
                return false;
            double x=(xs[i]+xs[i+1])/2,y=(ys[j]+ys[j+1])/2,z=(zs[k]+zs[k+1])/2;
            return parts.Any(p=>x>p.XY.MinX && x<p.XY.MaxX &&
                y>p.XY.MinY && y<p.XY.MaxY && z>p.Z.Start && z<p.Z.End);
        }
        var exterior=ImmutableArray.CreateBuilder<CatchBasinSurface>();
        var contact=ImmutableArray.CreateBuilder<CatchBasinSurface>();
        var hostPoint=wall.ToGlobal(0,hostOuterV,0);
        int faceIndex=0;
        for(int i=0;i<xs.Length-1;i++)
        for(int j=0;j<ys.Length-1;j++)
        for(int k=0;k<zs.Length-1;k++)
        {
            if(!Occupied(i,j,k)) continue;
            double x0=xs[i],x1=xs[i+1],y0=ys[j],y1=ys[j+1],z0=zs[k],z1=zs[k+1];
            foreach(var side in new[]{(-1,0,0),(1,0,0),(0,-1,0),(0,1,0),(0,0,-1),(0,0,1)})
            {
                if(Occupied(i+side.Item1,j+side.Item2,k+side.Item3)) continue;
                Point3Mm origin;
                Vector3 u,v,normal;
                double width,height;
                switch(side)
                {
                    case (-1,0,0):
                        origin=new(x0,y0,z0);u=new(0,0,1);v=new(0,1,0);
                        normal=new(-1,0,0);width=z1-z0;height=y1-y0;break;
                    case (1,0,0):
                        origin=new(x1,y0,z0);u=new(0,1,0);v=new(0,0,1);
                        normal=new(1,0,0);width=y1-y0;height=z1-z0;break;
                    case (0,-1,0):
                        origin=new(x0,y0,z0);u=new(1,0,0);v=new(0,0,1);
                        normal=new(0,-1,0);width=x1-x0;height=z1-z0;break;
                    case (0,1,0):
                        origin=new(x0,y1,z0);u=new(0,0,1);v=new(1,0,0);
                        normal=new(0,1,0);width=z1-z0;height=x1-x0;break;
                    case (0,0,-1):
                        origin=new(x0,y0,z0);u=new(0,1,0);v=new(1,0,0);
                        normal=new(0,0,-1);width=y1-y0;height=x1-x0;break;
                    default:
                        origin=new(x0,y0,z1);u=new(1,0,0);v=new(0,1,0);
                        normal=new(0,0,1);width=x1-x0;height=y1-y0;break;
                }
                bool hostContact=normal.X==-wall.Outward.X &&
                    normal.Y==-wall.Outward.Y && normal.Z==-wall.Outward.Z &&
                    Math.Abs((origin.X-hostPoint.X)*wall.Outward.X+
                        (origin.Y-hostPoint.Y)*wall.Outward.Y)<=1e-6;
                var patch=new FacePatch($"Catch/{hostId}/Face/{faceIndex++}",$"Catch/{hostId}",
                    hostContact?FaceRole.Contact:FaceRole.Outer,
                    new RectangleProfile(new Rectangle2Mm(0,0,width,height)),
                    new Frame3Mm(origin,u,v,normal),new AreaMm2(width*height),normal,
                    hostContact?hostId:null,sources);
                (hostContact?contact:exterior).Add(new CatchBasinSurface(patch));
            }
        }
        return(exterior.ToImmutable(),contact.ToImmutable());
    }

    private static AxisAlignedCut LocalBox(WallFrame frame, double u0, double u1,
        double v0, double v1, double z0, double z1)
    {
        var points = new[] { frame.ToGlobal(u0, v0, z0), frame.ToGlobal(u0, v1, z0),
            frame.ToGlobal(u1, v0, z0), frame.ToGlobal(u1, v1, z0) };
        return new AxisAlignedCut(new Rectangle2Mm(points.Min(p => p.X), points.Min(p => p.Y),
            points.Max(p => p.X), points.Max(p => p.Y)), new Interval1D(z0, z1));
    }

    private static ExtrusionSolid ToSolid(AxisAlignedCut cut)
    {
        var r = cut.XY;
        var footprint = new Footprint(new Polygon2Mm([new(r.MinX, r.MinY), new(r.MaxX, r.MinY),
            new(r.MaxX, r.MaxY), new(r.MinX, r.MaxY)]), []);
        var frame = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        return new ExtrusionSolid(footprint, frame, cut.Z);
    }

    private static bool TryRectangle(Polygon2Mm polygon, out Rectangle2Mm rectangle)
    {
        var points=polygon.Vertices;
        double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X);
        double y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
        if(points.Length!=4 || points.Any(p=>(p.X!=x0 && p.X!=x1)||(p.Y!=y0 && p.Y!=y1)))
        { rectangle=null!; return false; }
        rectangle=new Rectangle2Mm(x0,y0,x1,y1);
        return true;
    }
}
