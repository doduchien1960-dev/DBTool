using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class MinimalDrawingPackageTests
{
    private static readonly PackageIdentity Identity = new(new string('b',64),"S1-Analytic-1/v1",CalculationScope.BaseRebarOnly);
    private const string SourceText="T36.a S1 synthetic fixture";
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture,"SyntheticTest.inline",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SourceText))),null,null,null,SourceText);

    [Fact]
    public void SixViewsUseSemanticGeometryAndOwnBounds()
    {
        var (geometry,layout,schedule)=Fixture();
        var result=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity);
        Assert.True(result.Success);
        var package=result.Value!;
        Assert.Equal(new[]{"Plan","AA","BB","BaseBottom","BaseTop","SteelSchedule"},package.Views.Select(v=>v.Id));
        Assert.Equal(new[]{ViewPurpose.Plan,ViewPurpose.Section,ViewPurpose.Section,ViewPurpose.BaseMesh,ViewPurpose.BaseMesh,ViewPurpose.SteelTable},
            package.Views.Select(v=>v.Purpose));
        Assert.All(package.Views,v=>Assert.NotNull(v.Bounds));
        Assert.Equal(1700,package.Views.Single(v=>v.Id=="Plan").Bounds!.MaxX-package.Views.Single(v=>v.Id=="Plan").Bounds!.MinX,6);
        Assert.Equal(1950,package.Views.Single(v=>v.Id=="Plan").Bounds!.MaxY-package.Views.Single(v=>v.Id=="Plan").Bounds!.MinY,6);
        var aa=package.Views.Single(v=>v.Id=="AA").Bounds!;
        Assert.Equal(-250,aa.MinY,6);
        Assert.Equal(1550,aa.MaxY,6);
        Assert.Equal(Identity.InputHash,package.InputHash);
        Assert.Equal(Identity.RuleVersion,package.RuleVersion);
        Assert.Equal(Identity.Scope,package.Scope);
        Assert.NotNull(package.Bounds);
        Assert.Contains(package.Primitives,p=>p.ViewId=="Plan" && p.Role=="Geometry/RoofOpening");
        Assert.Contains(package.Primitives,p=>p.ViewId=="AA" && p.Role=="Geometry/ConcreteCutout");
        Assert.DoesNotContain(package.Primitives,p=>p.ViewId=="BB" && p.Role=="Geometry/ConcreteCutout");
    }

    [Fact]
    public void FortyEightBarPrimitivesResolveToExactlyFourScheduleMarks()
    {
        var (geometry,layout,schedule)=Fixture();
        var package=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
        var bars=package.Primitives.Where(p=>p.OccurrenceId is not null).ToArray();
        Assert.Equal(48,bars.Length);
        Assert.Equal(48,bars.Select(p=>p.OccurrenceId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(24,bars.Count(p=>p.ViewId=="BaseBottom"));
        Assert.Equal(24,bars.Count(p=>p.ViewId=="BaseTop"));
        Assert.DoesNotContain(bars,p=>p.ViewId is "Plan" or "AA" or "BB" or "SteelSchedule");
        Assert.Equal(new[]{"D001","D002","D003","D004"},schedule.Rows.Select(r=>r.Mark));
        foreach(var bar in bars)
        {
            var row=Assert.Single(schedule.Rows.Where(r=>r.Mark==bar.ScheduleMark));
            Assert.Contains(bar.OccurrenceId!,row.OccurrenceIds);
            var original=Assert.Single(layout.Occurrences.Where(o=>o.Id==bar.OccurrenceId));
            var line=Assert.IsType<PolylinePrimitive>(bar);
            Assert.Equal(new Point2Mm(original.Path.Segments[0].Start.X,original.Path.Segments[0].Start.Y),line.Points[0]);
            Assert.Equal(new Point2Mm(original.Path.Segments[0].End.X,original.Path.Segments[0].End.Y),line.Points[1]);
        }
        var table=package.Primitives.OfType<TableRowPrimitive>().ToArray();
        Assert.Equal(4,table.Length);
        Assert.Equal(schedule.Rows.Select(r=>r.Mark),table.Select(t=>t.Mark));
        Assert.Equal(new[]{13d,11d,13d,11d},table.Select(t=>t.Cells.Single(c=>c.ColumnKey=="Quantity").Number!.Value));
        Assert.Equal(46.06172839506173,schedule.TotalMassKg.Value,9);
        Assert.Equal(schedule.Rows.Select(r=>r.MassKg.Value),table.Select(t=>t.Cells.Single(c=>c.ColumnKey=="MassKg").Number!.Value));
        Assert.Equal("Total 46.062 kg",package.Primitives.OfType<TextPrimitive>().Single(t=>t.SemanticId=="SteelSchedule/TotalMassDisplay").Content);
    }

    [Fact]
    public void UnknownOrStaleMarkFailsWithBlockAndNullValue()
    {
        var (geometry,layout,schedule)=Fixture();
        var missing=new SteelSchedule(schedule.Rows.Skip(1),
            new[]{new KeyValuePair<ComponentKind,MassKg>(ComponentKind.Base,new MassKg(schedule.Rows.Skip(1).Sum(r=>r.MassKg.Value)))},
            new MassKg(schedule.Rows.Skip(1).Sum(r=>r.MassKg.Value)),schedule.MassPolicyId,schedule.MassPolicyVersion);
        var result=ViewBuilder.Build(geometry,layout,missing,Absolute(20),Identity);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="DRAWING_MARK_UNRESOLVED" && d.Severity==DiagnosticSeverity.Block &&
            d.Scope==DiagnosticScope.Global && d.Source is not null);
    }

    [Fact]
    public void AbsoluteAndSymbolicElevationsKeepTheirSemanticReferences()
    {
        var (geometry,layout,schedule)=Fixture();
        var absolute=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
        var labels=absolute.Primitives.OfType<TextPrimitive>().Where(t=>t.Role=="Elevation/Absolute").ToDictionary(t=>t.SemanticId,t=>t.Content);
        Assert.Equal("10.000 m",labels["Elevation/Floor"]);
        Assert.Equal("11.550 m",labels["Elevation/RoofTop"]);
        Assert.Equal("9.750 m",labels["Elevation/PadBottom"]);
        var symbolic=ViewBuilder.Build(geometry,layout,schedule,new PresentationRequest(20,ElevationMode.Symbolic,null,"default",true),Identity).Value!;
        var symbols=symbolic.Primitives.OfType<TextPrimitive>().Where(t=>t.Role=="Elevation/Symbolic").ToDictionary(t=>t.SemanticId,t=>t.Content);
        Assert.Equal("Z1",symbols["Elevation/Floor"]);
        Assert.Equal("Z2",symbols["Elevation/RoofTop"]);
        Assert.Equal("Z1 - 0.250 m",symbols["Elevation/PadBottom"]);
        Assert.DoesNotContain(symbolic.Primitives.OfType<TextPrimitive>(),t=>t.Role=="Elevation/Symbolic" && t.Content=="0");
    }

    [Fact]
    public void ScaleOnlyChangesPresentationNotLogicalPathsDimensionsOrSchedule()
    {
        var (geometry,layout,schedule)=Fixture();
        var a=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
        var b=ViewBuilder.Build(geometry,layout,schedule,Absolute(30),Identity).Value!;
        Assert.Equal(a.Primitives.OfType<PolylinePrimitive>().Where(p=>p.Role!="Schedule/Grid").Select(p=>(p.SemanticId,string.Join(";",p.Points))),
            b.Primitives.OfType<PolylinePrimitive>().Where(p=>p.Role!="Schedule/Grid").Select(p=>(p.SemanticId,string.Join(";",p.Points))));
        Assert.Equal(a.Primitives.OfType<DimensionIntentPrimitive>().Select(p=>(p.SemanticId,p.Start,p.End,p.LogicalMeasurementMm)),
            b.Primitives.OfType<DimensionIntentPrimitive>().Select(p=>(p.SemanticId,p.Start,p.End,p.LogicalMeasurementMm)));
        Assert.Equal(a.Primitives.OfType<TableRowPrimitive>().Select(p=>(p.Mark,string.Join(";",p.Cells.Select(c=>c.Number?.ToString()??c.Text)))),
            b.Primitives.OfType<TableRowPrimitive>().Select(p=>(p.Mark,string.Join(";",p.Cells.Select(c=>c.Number?.ToString()??c.Text)))));
        Assert.Equal(20,a.Views[0].ScaleDenominator);
        Assert.Equal(30,b.Views[0].ScaleDenominator);
        Assert.NotEqual(a.Primitives.OfType<TextPrimitive>().First().HeightMm,b.Primitives.OfType<TextPrimitive>().First().HeightMm);
    }

    [Fact]
    public void DimensionValueComesFromProfileNotLabelText()
    {
        var (geometry,layout,schedule)=Fixture();
        var package=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
        var dim=package.Primitives.OfType<DimensionIntentPrimitive>().Single(p=>p.SemanticId=="Plan/Base/WidthX");
        Assert.Equal(1500,dim.LogicalMeasurementMm,6);
        Assert.Equal(new Point2Mm(-750,-875),dim.Start);
        Assert.Equal(new Point2Mm(750,-875),dim.End);
        Assert.Equal("1500",dim.Label);
        Assert.Equal("Dimension/Linear",dim.Role);
    }

    [Fact]
    public void PresentationLabelsUseInvariantCultureAndAwayFromZero()
    {
        var original=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
            var (geometry,layout,schedule)=Fixture();
            var package=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
            Assert.Equal("11.550 m",package.Primitives.OfType<TextPrimitive>().Single(t=>t.SemanticId=="Elevation/RoofTop").Content);
            Assert.Equal("Total 46.062 kg",package.Primitives.OfType<TextPrimitive>().Single(t=>t.SemanticId=="SteelSchedule/TotalMassDisplay").Content);
            Assert.Equal("1500",package.Primitives.OfType<DimensionIntentPrimitive>().Single(d=>d.SemanticId=="Plan/Base/WidthX").Label);
        }
        finally { CultureInfo.CurrentCulture=original; }
    }

    [Fact]
    public void JsonRoundTripRetainsPrimitiveKindsIdentitiesAndMillimetres()
    {
        var (geometry,layout,schedule)=Fixture();
        var package=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
        var json=DrawingPackageJson.Serialize(package);
        var restored=DrawingPackageJson.Deserialize(json);
        Assert.Equal(package.Views.Select(v=>v.Id),restored.Views.Select(v=>v.Id));
        Assert.Equal(package.Primitives.Select(p=>p.SemanticId),restored.Primitives.Select(p=>p.SemanticId));
        Assert.Equal(48,restored.Primitives.OfType<PolylinePrimitive>().Count(p=>p.OccurrenceId is not null));
        Assert.Equal(1500,restored.Primitives.OfType<DimensionIntentPrimitive>().Single(p=>p.SemanticId=="Plan/Base/WidthX").LogicalMeasurementMm,6);
        Assert.Equal(4,restored.Primitives.OfType<TableRowPrimitive>().Count());
        var first=restored.Primitives.OfType<PolylinePrimitive>().Single(p=>p.OccurrenceId?.EndsWith("/Bottom/X/001",StringComparison.Ordinal)==true);
        Assert.Equal(new Point2Mm(-720,-840),first.Points[0]);
        Assert.Equal(new Point2Mm(720,-840),first.Points[1]);
        Assert.Equal("D001",first.ScheduleMark);
        Assert.Equal(11.55555555555556,restored.Primitives.OfType<TableRowPrimitive>().Single(t=>t.Mark=="D001")
            .Cells.Single(c=>c.ColumnKey=="MassKg").Number!.Value,9);
    }

    [Fact]
    public void WritesDrawingJsonArtifactWhenRequested()
    {
        var output=Environment.GetEnvironmentVariable("DBTOOL_T36A_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        var (geometry,layout,schedule)=Fixture();
        var package=ViewBuilder.Build(geometry,layout,schedule,Absolute(20),Identity).Value!;
        Directory.CreateDirectory(output);
        var path=Path.Combine(output,"drawing-package.json");
        File.WriteAllText(path,DrawingPackageJson.Serialize(package,true));
        Assert.True(File.Exists(path));
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(6,document.RootElement.GetProperty("Views").GetArrayLength());
    }

    private static PresentationRequest Absolute(double scale)=>new(scale,ElevationMode.Absolute,new ElevationM(10),"default",true);

    private static (GeometryModel Geometry,RebarLayout Layout,SteelSchedule Schedule) Fixture()
    {
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var components=new List<ComponentGeometry>
        {
            Component("Pad",ComponentKind.Pad,"Bedding",-850,-975,850,975,-250,-150,frame),
            Component("Base",ComponentKind.Base,"Concrete",-750,-875,750,875,-150,0,frame),
            Component("Wall/T1",ComponentKind.Wall,"Concrete",-650,625,650,775,0,1400,frame),
            Component("Wall/T2",ComponentKind.Wall,"Concrete",500,-625,650,625,0,1400,frame),
            Component("Wall/T3",ComponentKind.Wall,"Concrete",-650,-775,650,-625,0,1400,frame),
            Component("Wall/T4",ComponentKind.Wall,"Concrete",-650,-625,-500,625,0,1400,frame)
        };
        var roofBlank=Foot(-650,-775,650,775);
        var roofFoot=new Footprint(roofBlank.Outer,[new Polygon2Mm([new(-350,350),new(350,350),new(350,-350),new(-350,-350)])]);
        var roofHole=new OpeningProfile("Roof/Hole","Roof",OpeningKind.Box,new RectangleProfile(new(-350,-350,350,350)),null,
            new RectangleProfile(new(-350,-350,350,350)),null,new Frame3Mm(new(0,0,1400),new(1,0,0),new(0,1,0),new(0,0,1)),
            new(1400,1550),new(0,150),[Source]);
        components.Add(new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",roofFoot,new(1400,1550),
            new ExtrusionSolid(roofBlank,frame,new(1400,1550)),[roofHole],[],[Source]));
        var wall=WallFrame.Create(WallId.T1,1000,1250);
        var pipe=new OpeningProfile("Wall/T1/Pipe","Wall/T1",OpeningKind.Round,new CircleProfile(new(0,500),200),
            new CircleProfile(new(0,500),255),new CircleProfile(new(0,500),305),wall,
            new Frame3Mm(wall.Origin,wall.Left,wall.Up,wall.Outward),new(195,805),new(0,150),[Source]);
        var t1=components.Single(c=>c.Id=="Wall/T1");
        components[2]=new ComponentGeometry(t1.Id,t1.Kind,t1.MaterialId,t1.Footprint,t1.ZRange,t1.Occupied,[pipe],[],[Source]);
        var geometry=new GeometryModel(components,[roofHole,pipe]);

        var bars=new List<BarOccurrence>();
        double[] ys=[-840,-700,-560,-420,-280,-140,0,140,280,420,560,700,840];
        double[] xs=[-715,-572,-429,-286,-143,0,143,286,429,572,715];
        foreach(var (layer,zx,zy) in new[]{(Layer.Bottom,-115d,-105d),(Layer.Top,-35d,-45d)})
        {
            for(int i=0;i<ys.Length;i++) bars.Add(Bar(layer,Axis.X,i+1,new(-720,ys[i],zx),new(720,ys[i],zx),1440));
            for(int i=0;i<xs.Length;i++) bars.Add(Bar(layer,Axis.Y,i+1,new(xs[i],-845,zy),new(xs[i],845,zy),1690));
        }
        var layout=new RebarLayout(bars,[],CalculationScope.BaseRebarOnly);
        var schedule=SteelScheduleCalculator.Calculate(bars,CalculationPolicy.Select("S1-Analytic-1",true).Value!.MassPolicy,
            new MarkPolicy(CalculationScope.BaseRebarOnly)).Value!;
        return (geometry,layout,schedule);
    }

    private static BarOccurrence Bar(Layer layer,Axis axis,int index,Point3Mm start,Point3Mm end,double length)
    {
        var path=new BarPath([new LineSegment3Mm(start,end)],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        return new BarOccurrence($"S1/GaThamLienKhoi/S1-MONO-001/Base/{layer}/{axis}/{index:D3}",null,"Base",ComponentKind.Base,
            layer,axis,10,path,length,length,LengthStatus.Known,"MESH-UNIFORM-TEST-001",[Source]);
    }

    private static ComponentGeometry Component(string id,ComponentKind kind,string material,double x0,double y0,double x1,double y1,double z0,double z1,Frame3Mm frame)
    {
        var foot=Foot(x0,y0,x1,y1); var z=new Interval1D(z0,z1);
        return new ComponentGeometry(id,kind,material,foot,z,new ExtrusionSolid(foot,frame,z),[],[],[Source]);
    }
    private static Footprint Foot(double x0,double y0,double x1,double y1)=>new(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
}
