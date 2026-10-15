using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using System.Text.Json.Serialization;

namespace DBTool.Drawing;

public enum BoundaryWinding { CounterClockwise, Clockwise }

[JsonPolymorphic(TypeDiscriminatorPropertyName="$type")]
[JsonDerivedType(typeof(SectionLineEdge),"line")]
[JsonDerivedType(typeof(SectionEllipseEdge),"ellipse")]
public abstract record SectionBoundaryEdge
{
    private protected SectionBoundaryEdge() { }
    public abstract Point2Mm Start { get; }
    public abstract Point2Mm End { get; }
}

public sealed record SectionLineEdge : SectionBoundaryEdge
{
    public SectionLineEdge(Point2Mm start,Point2Mm end)
    {
        if(start==end) throw new ArgumentException("Zero-length boundary edge.");
        Start=start;End=end;
    }
    public override Point2Mm Start { get; }
    public override Point2Mm End { get; }
}

public sealed record SectionEllipseEdge : SectionBoundaryEdge
{
    public SectionEllipseEdge(Point2Mm center,double stationRadiusMm,double heightRadiusMm,
        double startRadians,double endRadians)
    {
        Center=center;
        StationRadiusMm=ContractGuard.Positive(stationRadiusMm,nameof(stationRadiusMm));
        HeightRadiusMm=ContractGuard.Positive(heightRadiusMm,nameof(heightRadiusMm));
        StartRadians=ContractGuard.Finite(startRadians,nameof(startRadians));
        EndRadians=ContractGuard.Finite(endRadians,nameof(endRadians));
        if(startRadians==endRadians) throw new ArgumentException("Zero ellipse arc.");
        Start=At(startRadians);End=At(endRadians);
    }
    public Point2Mm Center { get; }
    public double StationRadiusMm { get; }
    public double HeightRadiusMm { get; }
    public double StartRadians { get; }
    public double EndRadians { get; }
    public override Point2Mm Start { get; }
    public override Point2Mm End { get; }
    public Point2Mm At(double radians)=>new(Center.X+StationRadiusMm*Math.Cos(radians),
        Center.Y+HeightRadiusMm*Math.Sin(radians));
}

public sealed record SectionBoundaryLoop
{
    public SectionBoundaryLoop(IEnumerable<SectionBoundaryEdge> edges,BoundaryWinding winding)
    {
        Edges=ContractGuard.Copy(edges);
        Winding=ContractGuard.EnumValue(winding);
        if(Edges.Length<2) throw new ArgumentException("Closed boundary needs edges.");
        for(int i=0;i<Edges.Length;i++)
        {
            var a=Edges[i].End;var b=Edges[(i+1)%Edges.Length].Start;
            if(Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2))>1e-6)
                throw new ArgumentException("Boundary edges are disconnected.");
        }
    }
    public ImmutableArray<SectionBoundaryEdge> Edges { get; }
    public BoundaryWinding Winding { get; }
    public bool Closed => true;
}

/// <summary>Exact section of a circular cylinder: an ellipse clipped at two stations.</summary>
public sealed record EllipticSectionBand
{
    public EllipticSectionBand(double stationStartMm,double stationEndMm,
        double centerStationMm,double centerHeightMm,double stationRadiusMm,double heightRadiusMm)
    {
        StationStartMm=ContractGuard.Finite(stationStartMm,nameof(stationStartMm));
        StationEndMm=ContractGuard.Finite(stationEndMm,nameof(stationEndMm));
        CenterStationMm=ContractGuard.Finite(centerStationMm,nameof(centerStationMm));
        CenterHeightMm=ContractGuard.Finite(centerHeightMm,nameof(centerHeightMm));
        StationRadiusMm=ContractGuard.Positive(stationRadiusMm,nameof(stationRadiusMm));
        HeightRadiusMm=ContractGuard.Positive(heightRadiusMm,nameof(heightRadiusMm));
        if(stationEndMm<=stationStartMm ||
            stationStartMm<centerStationMm-stationRadiusMm-1e-6 ||
            stationEndMm>centerStationMm+stationRadiusMm+1e-6)
            throw new ArgumentException("Station span must lie inside the ellipse.");
    }
    public double StationStartMm { get; }
    public double StationEndMm { get; }
    public double CenterStationMm { get; }
    public double CenterHeightMm { get; }
    public double StationRadiusMm { get; }
    public double HeightRadiusMm { get; }
    public double Upper(double stationMm)=>CenterHeightMm+HalfHeight(stationMm);
    public double Lower(double stationMm)=>CenterHeightMm-HalfHeight(stationMm);
    public double AreaMm2
    {
        get
        {
            double x0=Math.Clamp((StationStartMm-CenterStationMm)/StationRadiusMm,-1,1);
            double x1=Math.Clamp((StationEndMm-CenterStationMm)/StationRadiusMm,-1,1);
            static double Primitive(double x)=>x*Math.Sqrt(Math.Max(0,1-x*x))+Math.Asin(x);
            return StationRadiusMm*HeightRadiusMm*(Primitive(x1)-Primitive(x0));
        }
    }
    public SectionEllipseEdge UpperEdge(double startMm,double endMm)=>new(
        new Point2Mm(CenterStationMm,CenterHeightMm),StationRadiusMm,HeightRadiusMm,
        Math.Acos(Math.Clamp((startMm-CenterStationMm)/StationRadiusMm,-1,1)),
        Math.Acos(Math.Clamp((endMm-CenterStationMm)/StationRadiusMm,-1,1)));
    public SectionEllipseEdge LowerEdge(double startMm,double endMm)=>new(
        new Point2Mm(CenterStationMm,CenterHeightMm),StationRadiusMm,HeightRadiusMm,
        2*Math.PI-Math.Acos(Math.Clamp((startMm-CenterStationMm)/StationRadiusMm,-1,1)),
        2*Math.PI-Math.Acos(Math.Clamp((endMm-CenterStationMm)/StationRadiusMm,-1,1)));
    private double HalfHeight(double stationMm)
    {
        double x=(stationMm-CenterStationMm)/StationRadiusMm;
        return HeightRadiusMm*Math.Sqrt(Math.Max(0,1-x*x));
    }
}

public sealed record AnalyticSectionVoid
{
    public AnalyticSectionVoid(string id,string sourceOpeningId,string ownerId,
        ProjectedVoidRole role,EllipticSectionBand band,IEnumerable<SourceRef> sources)
        :this(id,sourceOpeningId,ownerId,role,band,null,null,sources) { }

    public AnalyticSectionVoid(string id,string sourceOpeningId,string ownerId,
        ProjectedVoidRole role,EllipticSectionBand band,double? lowerClipMm,
        double? upperClipMm,IEnumerable<SourceRef> sources)
    {
        Id=ContractGuard.Text(id,nameof(id));
        SourceOpeningId=ContractGuard.Text(sourceOpeningId,nameof(sourceOpeningId));
        OwnerId=ContractGuard.Text(ownerId,nameof(ownerId));
        Role=ContractGuard.EnumValue(role);
        Band=ContractGuard.Required(band);
        LowerClipMm=lowerClipMm.HasValue?ContractGuard.Finite(lowerClipMm.Value,nameof(lowerClipMm)):null;
        UpperClipMm=upperClipMm.HasValue?ContractGuard.Finite(upperClipMm.Value,nameof(upperClipMm)):null;
        Sources=ContractGuard.Copy(sources);
        double s0=band.StationStartMm,s1=band.StationEndMm;
        var ul=new Point2Mm(s0,UpperClipMm??band.Upper(s0));
        var ur=new Point2Mm(s1,UpperClipMm??band.Upper(s1));
        var lr=new Point2Mm(s1,LowerClipMm??band.Lower(s1));
        var ll=new Point2Mm(s0,LowerClipMm??band.Lower(s0));
        var edges=new List<SectionBoundaryEdge>{UpperClipMm.HasValue
            ?new SectionLineEdge(ul,ur):band.UpperEdge(s0,s1)};
        if(Distance(ur,lr)>1e-6) edges.Add(new SectionLineEdge(ur,lr));
        edges.Add(LowerClipMm.HasValue
            ?new SectionLineEdge(lr,ll):band.LowerEdge(s1,s0));
        if(Distance(ll,ul)>1e-6) edges.Add(new SectionLineEdge(ll,ul));
        Boundary=new SectionBoundaryLoop(edges,BoundaryWinding.Clockwise);
        if(AreaMm2<=0) throw new ArgumentException("Clipped ellipse has no area.");
    }
    public string Id { get; }
    public string SourceOpeningId { get; }
    public string OwnerId { get; }
    public ProjectedVoidRole Role { get; }
    public EllipticSectionBand Band { get; }
    public double? LowerClipMm { get; }
    public double? UpperClipMm { get; }
    public SectionBoundaryLoop Boundary { get; }
    public double AreaMm2
    {
        get
        {
            double width=Band.StationEndMm-Band.StationStartMm;
            double half=Band.AreaMm2/2;
            double upper=UpperClipMm.HasValue?UpperClipMm.Value*width:
                Band.CenterHeightMm*width+half;
            double lower=LowerClipMm.HasValue?LowerClipMm.Value*width:
                Band.CenterHeightMm*width-half;
            return upper-lower;
        }
    }
    public ImmutableArray<SourceRef> Sources { get; }
    private static double Distance(Point2Mm a,Point2Mm b)=>Math.Sqrt(
        (a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
}

public enum AnalyticMaterialSide { Above, Below }

public sealed record AnalyticSectionRegion
{
    public AnalyticSectionRegion(string id,string sourceComponentId,string materialId,
        AnalyticMaterialSide side,EllipticSectionBand cut,double hostBottomMm,
        double hostTopMm,IEnumerable<SourceRef> sources)
    {
        Id=ContractGuard.Text(id,nameof(id));
        SourceComponentId=ContractGuard.Text(sourceComponentId,nameof(sourceComponentId));
        MaterialId=ContractGuard.Text(materialId,nameof(materialId));
        Side=ContractGuard.EnumValue(side);
        Cut=ContractGuard.Required(cut);
        HostBottomMm=ContractGuard.Finite(hostBottomMm,nameof(hostBottomMm));
        HostTopMm=ContractGuard.Finite(hostTopMm,nameof(hostTopMm));
        if(hostBottomMm>=hostTopMm) throw new ArgumentException("Invalid host height.");
        Sources=ContractGuard.Copy(sources);
        double s0=cut.StationStartMm,s1=cut.StationEndMm;
        if(side==AnalyticMaterialSide.Above)
        {
            var ur=new Point2Mm(s1,cut.Upper(s1));
            var ul=new Point2Mm(s0,cut.Upper(s0));
            var tr=new Point2Mm(s1,hostTopMm);
            var tl=new Point2Mm(s0,hostTopMm);
            var edges=new List<SectionBoundaryEdge>{cut.UpperEdge(s0,s1)};
            if(Distance(ur,tr)>1e-6) edges.Add(new SectionLineEdge(ur,tr));
            edges.Add(new SectionLineEdge(tr,tl));
            if(Distance(tl,ul)>1e-6) edges.Add(new SectionLineEdge(tl,ul));
            Boundary=new SectionBoundaryLoop(edges,BoundaryWinding.CounterClockwise);
        }
        else
        {
            var bl=new Point2Mm(s0,hostBottomMm);
            var br=new Point2Mm(s1,hostBottomMm);
            var lr=new Point2Mm(s1,cut.Lower(s1));
            var ll=new Point2Mm(s0,cut.Lower(s0));
            var edges=new List<SectionBoundaryEdge>{new SectionLineEdge(bl,br)};
            if(Distance(br,lr)>1e-6) edges.Add(new SectionLineEdge(br,lr));
            edges.Add(cut.LowerEdge(s1,s0));
            if(Distance(ll,bl)>1e-6) edges.Add(new SectionLineEdge(ll,bl));
            Boundary=new SectionBoundaryLoop(edges,BoundaryWinding.CounterClockwise);
        }
        if(AreaMm2<=0) throw new ArgumentException("Curved material has no area.");
    }
    public string Id { get; }
    public string SourceComponentId { get; }
    public string MaterialId { get; }
    public AnalyticMaterialSide Side { get; }
    public EllipticSectionBand Cut { get; }
    public double HostBottomMm { get; }
    public double HostTopMm { get; }
    public SectionBoundaryLoop Boundary { get; }
    public double AreaMm2=>Side==AnalyticMaterialSide.Above
        ? (HostTopMm-Cut.CenterHeightMm)*(Cut.StationEndMm-Cut.StationStartMm)-Cut.AreaMm2/2
        : (Cut.CenterHeightMm-HostBottomMm)*(Cut.StationEndMm-Cut.StationStartMm)-Cut.AreaMm2/2;
    public ImmutableArray<SourceRef> Sources { get; }
    private static double Distance(Point2Mm a,Point2Mm b)=>Math.Sqrt(
        (a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
}
