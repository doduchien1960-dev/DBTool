using System.Collections.Immutable;

namespace DBTool.Domain.Geometry;

public sealed record HaunchSection(double ZMm,(double Width,double Length) ClearSize);
public sealed record HaunchSolid(double ChamberWidthMm,double ChamberLengthMm,double WidthMm,double HeightMm,HaunchOwner Owner,Interval1D ZRange) : SolidDescriptor;
public sealed record HaunchGeometry(ComponentKind OwnerKind,ImmutableArray<ComponentGeometry> Components,
    ImmutableArray<HaunchSection> Sections,ImmutableArray<FacePatch> FacePatches,double VolumeMm3,double TriangleAreaMm2,double InclinedEdgeMm);

public static class HaunchProfileBuilder
{
    private static readonly Vector3 X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    public static OperationResult<HaunchGeometry> Build(ValidatedManholeSpec spec,HaunchSpec haunch,CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(haunch); ArgumentNullException.ThrowIfNull(policy);
        var input=spec.Input;
        OperationResult<HaunchGeometry> Fail(string code,string expected,string actual) => new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Manhole,input.Id,null,haunch.Sources.FirstOrDefault(),expected,actual,code)]);
        if(policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || spec.RuleVersion!=policy.Version)
            return Fail("HAUNCH_POLICY_INVALID","S2Geometry-1/v1 or S2Rebar-1/v1",policy.Version);
        double b=haunch.WidthMm,h=haunch.HeightMm;
        if(b>Math.Min(input.L,input.B)/2) return Fail("HAUNCH_WIDTH_INVALID",$"<= {Math.Min(input.L,input.B)/2}",b.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if(h>input.H) return Fail("HAUNCH_HEIGHT_INVALID",$"<= {input.H}",h.ToString(System.Globalization.CultureInfo.InvariantCulture));
        double other=haunch.Owner==HaunchOwner.Base
            ? input.TopHaunchSpec?.HeightMm ?? input.TopHaunch
            : input.BaseHaunchSpec?.HeightMm ?? input.BaseHaunch;
        if(h+other>input.H) return Fail("HAUNCH_TOPOLOGY_OVERLAP","base+roof heights <= chamber height",(h+other).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var kind=haunch.Owner==HaunchOwner.Base?ComponentKind.Base:ComponentKind.Roof;
        if(b==0) return new(true,new HaunchGeometry(kind,[],[],[],0,0,0),[]);
        double z0=haunch.Owner==HaunchOwner.Base?0:input.H-h,z1=z0+h;
        double volume=h*((input.L+input.B)*b-4*b*b/3);
        if(!double.IsFinite(volume) || volume<=0) return Fail("HAUNCH_OVERFLOW","finite positive volume",volume.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var sections=haunch.Owner==HaunchOwner.Base
            ? ImmutableArray.Create(new HaunchSection(z0,(input.L-2*b,input.B-2*b)),new HaunchSection(z1,(input.L,input.B)))
            : ImmutableArray.Create(new HaunchSection(z0,(input.L,input.B)),new HaunchSection(z1,(input.L-2*b,input.B-2*b)));
        Polygon2Mm Rect(double x0,double y0,double x1,double y1)=>new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
        var innerLoops=input.L-2*b>0 && input.B-2*b>0
            ? new[]{new Polygon2Mm([new(-input.L/2+b,input.B/2-b),new(input.L/2-b,input.B/2-b),new(input.L/2-b,-input.B/2+b),new(-input.L/2+b,-input.B/2+b)])}
            : Array.Empty<Polygon2Mm>();
        var footprint=new Footprint(Rect(-input.L/2,-input.B/2,input.L/2,input.B/2),innerLoops);
        var range=new Interval1D(z0,z1);
        var id=haunch.Owner==HaunchOwner.Base?"Base/Haunch":"Roof/Haunch";
        double edge=Math.Sqrt(b*b+h*h);
        var patches=new List<FacePatch>();
        double rise=haunch.Owner==HaunchOwner.Base?h:-h;
        foreach(var (suffix,length,tangent,upSlope,normal,origin) in new (string,double,Vector3,Vector3,Vector3,Point3Mm)[]
        {
            ("North",input.L,X,new(0,b/edge,rise/edge),new(0,rise/edge,-b/edge),new(-input.L/2,input.B/2-b,haunch.Owner==HaunchOwner.Base?z0:z1)),
            ("East",input.B,Y,new(b/edge,0,rise/edge),new(rise/edge,0,-b/edge),new(input.L/2-b,-input.B/2,haunch.Owner==HaunchOwner.Base?z0:z1)),
            ("South",input.L,X,new(0,-b/edge,rise/edge),new(0,-rise/edge,-b/edge),new(-input.L/2,-input.B/2+b,haunch.Owner==HaunchOwner.Base?z0:z1)),
            ("West",input.B,Y,new(-b/edge,0,rise/edge),new(-rise/edge,0,-b/edge),new(-input.L/2+b,-input.B/2,haunch.Owner==HaunchOwner.Base?z0:z1))
        })
        {
            var polygon=length-2*b>0
                ? new Polygon2Mm([new(0,0),new(length,0),new(length-b,edge),new(b,edge)])
                : new Polygon2Mm([new(0,0),new(length,0),new(length/2,edge)]);
            patches.Add(new FacePatch($"{id}/{suffix}",id,FaceRole.Inner,new PolygonProfile(polygon),
                new Frame3Mm(origin,tangent,upSlope,normal),new AreaMm2((length-b)*edge),normal,null,haunch.Sources));
        }
        var component=new ComponentGeometry(id,kind,"Concrete",footprint,range,
            new HaunchSolid(input.L,input.B,b,h,haunch.Owner,range),[],patches,haunch.Sources);
        return new(true,new HaunchGeometry(kind,[component],sections,patches.ToImmutableArray(),volume,b*h/2,edge),[]);
    }
}
