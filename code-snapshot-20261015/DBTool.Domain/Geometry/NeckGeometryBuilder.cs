using System.Collections.Immutable;

namespace DBTool.Domain.Geometry;

public sealed record NeckSeatCutSolid(Rectangle2Mm Opening,double ExpansionMm,NeckSeatKind Kind,Interval1D ZRange) : SolidDescriptor;
public sealed record PurchasedCoverPlacement
{
    public PurchasedCoverPlacement(string catalogId,Point3Mm anchor,Dimensions2Mm physicalSize,double scale,
        double rotationDegrees,Rectangle2Mm localBounds,IEnumerable<SourceRef> sources,Profile2Mm? localClearProfile=null,
        CoverBlockPlacements? nativeViewPlacement=null,CoverSectionOutlines? sectionOutlines=null)
    {
        CatalogId=ContractGuard.Text(catalogId,nameof(catalogId));Anchor=anchor;PhysicalSize=ContractGuard.Required(physicalSize);
        Scale=ContractGuard.Positive(scale,nameof(scale));RotationDegrees=ContractGuard.Finite(rotationDegrees,nameof(rotationDegrees));
        LocalBounds=ContractGuard.Required(localBounds);Sources=ContractGuard.Copy(sources);
        LocalClearProfile=localClearProfile;
        NativeViewPlacement=nativeViewPlacement;
        SectionOutlines=sectionOutlines;
        WorldClearProfile=localClearProfile switch
        {
            null=>null,
            CircleProfile circle=>new CircleProfile(Transform(circle.Center),circle.RadiusMm*Scale),
            RoundedRectangleProfile rounded=>new RoundedRectangleProfile(Transform(rounded.Center),
                new(rounded.Size.Width*Scale,rounded.Size.Height*Scale),rounded.CornerRadiusMm*Scale,
                rounded.RotationDegrees+RotationDegrees),
            RectangleProfile rectangle=>new PolygonProfile(new Polygon2Mm([
                Transform(new(rectangle.Rectangle.MinX,rectangle.Rectangle.MinY)),
                Transform(new(rectangle.Rectangle.MaxX,rectangle.Rectangle.MinY)),
                Transform(new(rectangle.Rectangle.MaxX,rectangle.Rectangle.MaxY)),
                Transform(new(rectangle.Rectangle.MinX,rectangle.Rectangle.MaxY))])),
            _=>throw new ArgumentException("Unsupported physical cover aperture.",nameof(localClearProfile))
        };
    }
    private Point2Mm Transform(Point2Mm point)
    {
        double radians=RotationDegrees*Math.PI/180;
        return new(Anchor.X+Scale*(Math.Cos(radians)*point.X-Math.Sin(radians)*point.Y),
            Anchor.Y+Scale*(Math.Sin(radians)*point.X+Math.Cos(radians)*point.Y));
    }
    public string CatalogId {get;} public Point3Mm Anchor {get;} public Dimensions2Mm PhysicalSize {get;}
    public double Scale {get;} public double RotationDegrees {get;} public Rectangle2Mm LocalBounds {get;}
    public ImmutableArray<SourceRef> Sources {get;}
    /// <summary>Actual catalog aperture in native block coordinates, independent of inspection bounds.</summary>
    public Profile2Mm? LocalClearProfile {get;}
    /// <summary>Actual aperture in plan coordinates. Rectangles retain rotated edges, not their bounding box.</summary>
    public Profile2Mm? WorldClearProfile {get;}
    public CoverBlockPlacements? NativeViewPlacement {get;}
    public CoverSectionOutlines? SectionOutlines {get;}
}
public sealed record NeckGeometry(ImmutableArray<ComponentGeometry> Components,OpeningProfile RoofOpening,NeckSeatProfile SeatProfile,
    PurchasedCoverPlacement? PurchasedCoverPlacement,Point2Mm Center,double HoleAreaMm2,double GrossVolumeMm3,double SeatRemovedMm3)
{
    public double NetVolumeMm3 => GrossVolumeMm3-SeatRemovedMm3;
}

public static class NeckGeometryBuilder
{
    private static readonly Vector3 X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    private static readonly Frame3Mm PlanFrame=new(new Point3Mm(0,0,0),X,Y,Z);
    public static OperationResult<NeckGeometry> Build(ValidatedManholeSpec spec,NeckSpec neck,NeckSeatSpec seat,CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(neck); ArgumentNullException.ThrowIfNull(seat); ArgumentNullException.ThrowIfNull(policy);
        var i=spec.Input;
        OperationResult<NeckGeometry> Fail(string code,string expected,string actual,SourceRef? source=null) =>
            new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Manhole,i.Id,null,source ?? neck.Sources.FirstOrDefault(),expected,actual,code)]);
        if(policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || spec.RuleVersion!=policy.Version)
            return Fail("NECK_POLICY_INVALID","S2Geometry-1/v1 or S2Rebar-1/v1",policy.Version);
        if(neck.InnerWidthMm>i.L || neck.InnerLengthMm>i.B)
            return Fail("NECK_OPENING_OUTSIDE",$"<= {i.L} x {i.B}",$"{neck.InnerWidthMm} x {neck.InnerLengthMm}");
        if(seat.Kind!=NeckSeatKind.None && (neck.HeightMm<=0 || neck.ThicknessMm-seat.ExpansionMm<=0 || seat.HeightMm>neck.HeightMm))
            return Fail("NECK_SEAT_WALL_INVALID","0 < seat height <= neck height; expansion < wall thickness",$"{seat.HeightMm}; {seat.ExpansionMm}",seat.Sources.FirstOrDefault());
        double outerWidth=neck.InnerWidthMm+2*neck.ThicknessMm,outerLength=neck.InnerLengthMm+2*neck.ThicknessMm;
        double supportWidth=neck.InnerWidthMm+2*seat.ExpansionMm,supportLength=neck.InnerLengthMm+2*seat.ExpansionMm;
        double prospectiveTop=i.H+i.RoofThicknessMm+neck.HeightMm;
        if(!double.IsFinite(outerWidth) || !double.IsFinite(outerLength) || !double.IsFinite(supportWidth) ||
            !double.IsFinite(supportLength) || !double.IsFinite(prospectiveTop))
            return Fail("NECK_OVERFLOW","finite derived dimensions and elevations","overflow");
        var (cx,cy)=NeckPositionMap.Center(neck.Position,i.L,i.B,neck.InnerWidthMm,neck.InnerLengthMm);
        var center=new Point2Mm(cx,cy);
        var inner=Rect(cx,cy,neck.InnerWidthMm,neck.InnerLengthMm);
        var outer=Rect(cx,cy,outerWidth,outerLength);
        double roofHalfX=i.L/2+i.WallThicknessMm,roofHalfY=i.B/2+i.WallThicknessMm;
        if(outer.MinX < -roofHalfX || outer.MaxX>roofHalfX || outer.MinY < -roofHalfY || outer.MaxY>roofHalfY)
            return Fail("NECK_SUPPORT_OUTSIDE","outer neck footprint inside roof",$"{outer}");
        double roofBottom=i.H,roofTop=i.H+i.RoofThicknessMm,neckTop=roofTop+neck.HeightMm;
        if(seat.Kind!=NeckSeatKind.None && Math.Abs(seat.SeatElevationMm-(neckTop-seat.HeightMm))>policy.TolerancePolicy.GeometryMm)
            return Fail("NECK_SEAT_DATUM_MISMATCH","seat elevation equals bottom of specified top cut",seat.SeatElevationMm.ToString(System.Globalization.CultureInfo.InvariantCulture),seat.Sources.FirstOrDefault());
        var holeProfile=new RectangleProfile(inner);
        var hole=new OpeningProfile("Roof/Hole","Roof",OpeningKind.Box,holeProfile,null,holeProfile,null,
            new Frame3Mm(new Point3Mm(0,0,roofBottom),X,Y,Z),new Interval1D(roofBottom,roofTop),new Interval1D(0,i.RoofThicknessMm),neck.Sources);
        var supportInner=seat.Kind==NeckSeatKind.None?inner:Rect(cx,cy,supportWidth,supportLength);
        var profile=new NeckSeatProfile(supportInner,outer,seat.SeatElevationMm,neckTop,seat.HeightMm,neck.Sources.Concat(seat.Sources));
        double gross=((neck.InnerWidthMm+2*neck.ThicknessMm)*(neck.InnerLengthMm+2*neck.ThicknessMm)-neck.InnerWidthMm*neck.InnerLengthMm)*neck.HeightMm;
        double w=seat.ExpansionMm;
        double removed=seat.Kind switch
        {
            NeckSeatKind.None=>0,
            NeckSeatKind.Step=>((neck.InnerWidthMm+2*w)*(neck.InnerLengthMm+2*w)-neck.InnerWidthMm*neck.InnerLengthMm)*seat.HeightMm,
            NeckSeatKind.Slope=>((neck.InnerWidthMm+neck.InnerLengthMm)*w+4*w*w/3)*seat.HeightMm,
            _=>throw new ArgumentOutOfRangeException(nameof(seat))
        };
        if(!double.IsFinite(gross) || !double.IsFinite(removed) || removed>gross)
            return Fail("NECK_VOLUME_INVALID","finite net volume >= 0",$"gross={gross}; removed={removed}");
        var components=ImmutableArray<ComponentGeometry>.Empty;
        if(neck.HeightMm>0)
        {
            var footprint=Ring(outer,inner);
            var range=new Interval1D(roofTop,neckTop);
            SolidDescriptor occupied=new ExtrusionSolid(footprint,PlanFrame,range);
            if(seat.Kind!=NeckSeatKind.None)
                occupied=new DifferenceSolid(occupied,[new NeckSeatCutSolid(inner,w,seat.Kind,new Interval1D(neckTop-seat.HeightMm,neckTop))]);
            var patches=HorizontalRingPatches("Neck",outer,supportInner,neckTop,FaceRole.Top,null,neck.Sources)
                .Concat(HorizontalRingPatches("Neck",outer,inner,roofTop,FaceRole.Bottom,"Roof",neck.Sources)).ToArray();
            components=[new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",footprint,range,occupied,[],patches,neck.Sources)];
        }
        PurchasedCoverPlacement? purchase=null;
        if(i.PurchasedCoverSpec is not null)
        {
            var built=PurchasedCoverGeometryBuilder.Build(spec,center,policy,neck);
            if(!built.Success) return new(false,null,built.Diagnostics);
            purchase=built.Value!;
        }
        return new(true,new NeckGeometry(components,hole,profile,purchase,center,neck.InnerWidthMm*neck.InnerLengthMm,gross,removed),[]);
    }
    internal static Rectangle2Mm Rect(double cx,double cy,double width,double length) => new(cx-width/2,cy-length/2,cx+width/2,cy+length/2);
    internal static Footprint Ring(Rectangle2Mm outer,Rectangle2Mm inner) => new(
        new Polygon2Mm([new(outer.MinX,outer.MinY),new(outer.MaxX,outer.MinY),new(outer.MaxX,outer.MaxY),new(outer.MinX,outer.MaxY)]),
        [new Polygon2Mm([new(inner.MinX,inner.MaxY),new(inner.MaxX,inner.MaxY),new(inner.MaxX,inner.MinY),new(inner.MinX,inner.MinY)])]);
    internal static IEnumerable<FacePatch> HorizontalRingPatches(string owner,Rectangle2Mm outer,Rectangle2Mm inner,double z,FaceRole role,string? adjacent,IEnumerable<SourceRef> sources)
    {
        var rectangles=new[]
        {
            new Rectangle2Mm(outer.MinX,outer.MinY,outer.MaxX,inner.MinY),
            new Rectangle2Mm(outer.MinX,inner.MaxY,outer.MaxX,outer.MaxY),
            new Rectangle2Mm(outer.MinX,inner.MinY,inner.MinX,inner.MaxY),
            new Rectangle2Mm(inner.MaxX,inner.MinY,outer.MaxX,inner.MaxY)
        };
        var normal=role==FaceRole.Top?Z:new Vector3(0,0,-1);
        for(int index=0;index<rectangles.Length;index++)
        {
            var rect=rectangles[index];
            yield return new FacePatch($"{owner}/{role}/{index+1}",owner,role,new RectangleProfile(rect),
                new Frame3Mm(new Point3Mm(0,0,z),X,Y,normal),new AreaMm2((rect.MaxX-rect.MinX)*(rect.MaxY-rect.MinY)),normal,adjacent,sources);
        }
    }
}
