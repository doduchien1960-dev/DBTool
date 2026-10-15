using DBTool.Domain;
using DBTool.Domain.Geometry;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SectionProjectorTests
{
    [Fact]
    public void NeckStepProjectsLowerAndUpperRingsWithoutFillingSeatVoid()
    {
        var outer = Rect(-500,-500,500,500).Outer;
        var footprint = new Footprint(outer,[new Polygon2Mm(Rect(-300,-300,300,300).Outer.Vertices.Reverse())]);
        var range = new Interval1D(1500,2000);
        var solid = new DifferenceSolid(new ExtrusionSolid(footprint,PlanFrame,range),
            [new NeckSeatCutSolid(new(-300,-300,300,300),50,NeckSeatKind.Step,new(1900,2000))]);
        var component = new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",footprint,range,solid,[],[],[Source]);
        var result = SectionProjector.BuildSection(new GeometryModel([component],[]),
            new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection));
        Assert.True(result.Success);
        Assert.Equal(4,result.Value!.Regions.Length);
        Assert.All(result.Value.Regions,r=>Assert.Equal("Neck",r.SourceComponentId));
        var top = result.Value.Regions.Where(r=>MinY(r.Footprint.Outer)==1900).ToArray();
        Assert.Equal(2,top.Length);
        Assert.All(top,r=>Assert.Equal(150,r.Footprint.Outer.Vertices.Max(p=>p.X)-r.Footprint.Outer.Vertices.Min(p=>p.X)));
    }
    [Theory]
    [InlineData(HaunchOwner.Base)]
    [InlineData(HaunchOwner.Roof)]
    public void CenteredHaunchSectionRetainsTwoPhysicalTriangles(HaunchOwner owner)
    {
        var footprint = Rect(-500, -625, 500, 625);
        var range = owner == HaunchOwner.Base ? new Interval1D(0,100) : new Interval1D(1300,1400);
        var component = new ComponentGeometry("Haunch", ComponentKind.Base, "Concrete", footprint, range,
            new HaunchSolid(1000,1250,100,100,owner,range), [], [], [Source]);
        var result = SectionProjector.BuildSection(new GeometryModel([component], []),
            new SectionDefinition("AA", SectionAxis.AlongY, 0, SectionMode.TrueSection));
        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Regions.Length);
        Assert.All(result.Value.Regions, r => Assert.Equal(3, r.Footprint.Outer.Vertices.Length));
        Assert.Equal(10000, result.Value.Regions.Sum(r => Area(r.Footprint.Outer)), 6);
        Assert.Equal(-625, result.Value.Regions.Min(r => MinX(r.Footprint.Outer)), 6);
        Assert.Equal(100, result.Value.Regions.Max(r => MaxY(r.Footprint.Outer)) - result.Value.Regions.Min(r => MinY(r.Footprint.Outer)));
    }
    [Theory]
    [InlineData(HaunchOwner.Base)]
    [InlineData(HaunchOwner.Roof)]
    public void OffCentreHaunchSectionIncludesTransverseRingStrip(HaunchOwner owner)
    {
        var range = new Interval1D(0,100);
        var component = new ComponentGeometry("Haunch",ComponentKind.Base,"Concrete",Rect(-500,-625,500,625),range,
            new HaunchSolid(1000,1250,100,100,owner,range),[],[],[Source]);
        var result=SectionProjector.BuildSection(new GeometryModel([component],[]),
            new SectionDefinition("BB",SectionAxis.AlongX,600,SectionMode.TrueSection));
        Assert.True(result.Success);
        Assert.Equal(75625,result.Value!.Regions.Sum(r=>Area(r.Footprint.Outer)),6);
    }
    [Fact]
    public void TangentHaunchSectionProducesNoZeroAreaRegion()
    {
        var range = new Interval1D(0,100);
        var component = new ComponentGeometry("Haunch",ComponentKind.Base,"Concrete",Rect(-500,-625,500,625),range,
            new HaunchSolid(1000,1250,100,100,HaunchOwner.Base,range),[],[],[Source]);
        var result=SectionProjector.BuildSection(new GeometryModel([component],[]),
            new SectionDefinition("BB",SectionAxis.AlongX,625,SectionMode.TrueSection));
        Assert.True(result.Success);
        Assert.Empty(result.Value!.Regions);
    }
    private static double Area(Polygon2Mm polygon) => Math.Abs(polygon.Vertices.Select((p,i) =>
        p.X*polygon.Vertices[(i+1)%polygon.Vertices.Length].Y - p.Y*polygon.Vertices[(i+1)%polygon.Vertices.Length].X).Sum())/2;
    [Fact]
    public void RoundPipeSectionHasTwoMaterialBandsBetweenIndependentChords()
    {
        var result = SectionProjector.BuildSection(Model(pipeOffset: 100),
            new SectionDefinition("AA", SectionAxis.AlongY, 0, SectionMode.TrueSection));
        Assert.True(result.Success);
        var pipe = result.Value!.Regions.Where(r => r.Id.StartsWith("Wall/T1/Pipe/PipeSection/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, pipe.Length);
        Assert.All(pipe, r => { Assert.Equal("Pipe", r.MaterialId); Assert.Equal("Wall/T1", r.SourceComponentId); });
        var outer = result.Value.Voids.Single(v => v.SourceOpeningId == "Wall/T1/Pipe" && v.Role == ProjectedVoidRole.PipeOuter);
        var inner = result.Value.Voids.Single(v => v.SourceOpeningId == "Wall/T1/Pipe" && v.Role == ProjectedVoidRole.Inner);
        Assert.Equal(MinY(outer.Boundary), pipe.Min(r => MinY(r.Footprint.Outer)));
        Assert.Equal(MaxY(outer.Boundary), pipe.Max(r => MaxY(r.Footprint.Outer)));
        Assert.DoesNotContain(pipe, r => MinY(r.Footprint.Outer) < 500 && MaxY(r.Footprint.Outer) > 500);
        Assert.Equal((MaxY(outer.Boundary) - MinY(outer.Boundary)) -
            (MaxY(inner.Boundary) - MinY(inner.Boundary)),
            pipe.Sum(r => MaxY(r.Footprint.Outer) - MinY(r.Footprint.Outer)), 8);
    }
    private const string SourceText="S1 analytic geometry";
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture,"SyntheticTest.inline",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SourceText))),null,null,null,SourceText);

    [Fact]
    public void PlanAndBothTrueSectionsUseOneGeometrySnapshot()
    {
        var model=Model();
        var plan=PlanProjector.BuildPlan(model).Value!;
        var aa=SectionProjector.BuildSection(model,new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection)).Value!;
        var bb=SectionProjector.BuildSection(model,new SectionDefinition("BB",SectionAxis.AlongX,0,SectionMode.TrueSection)).Value!;

        Assert.Equal(ViewPurpose.Plan,plan.Purpose);
        Bounds(plan,"Base",-750,-875,750,875);
        Bounds(plan,"Pad",-850,-975,850,975);
        var roofPlan=Assert.Single(plan.Regions.Where(r=>r.SourceComponentId=="Roof"));
        Assert.Single(roofPlan.Footprint.InnerLoops);
        Bounds(plan,"Roof",-650,-775,650,775);
        Assert.Equal(700,plan.Dimensions.Single(d=>d.Id=="Roof/Hole/Width").MeasuredMm);
        Assert.Equal(1750,plan.Dimensions.Single(d=>d.Id=="Base/WidthY").MeasuredMm);
        Assert.All(plan.Regions,r=>Assert.Equal(Source,r.Sources.Single()));

        Bounds(aa,"Base",-875,-150,875,0);
        Bounds(aa,"Pad",-975,-250,975,-150);
        Bounds(bb,"Base",-750,-150,750,0);
        AssertRoofGap(aa);
        AssertRoofGap(bb);
        Assert.Contains(aa.Voids,v=>v.SourceOpeningId=="Roof/Hole");
        Assert.Contains(bb.Voids,v=>v.SourceOpeningId=="Roof/Hole");
        Assert.All(aa.Regions.Concat(bb.Regions),r=>Assert.True(r.Footprint.Outer.Closed));
    }

    [Fact]
    public void AaAtOffsetOneHundredUsesThreeIndependentChordsAndNoFaceCircle()
    {
        var view=SectionProjector.BuildSection(Model(pipeOffset:100),new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection)).Value!;
        var cut=view.Voids.Single(v=>v.SourceOpeningId=="Wall/T1/Pipe" && v.Role==ProjectedVoidRole.ConcreteCutout);
        Assert.Equal(-0+625,MinX(cut.Boundary),6);
        Assert.Equal(775,MaxX(cut.Boundary),6);
        Assert.Equal(211.8594093155218,MinY(cut.Boundary),6);
        Assert.Equal(788.1405906844782,MaxY(cut.Boundary),6);
        Assert.Equal(576.2811813689564,MaxY(cut.Boundary)-MinY(cut.Boundary),6);
        Assert.Equal(2,view.Regions.Count(r=>r.SourceComponentId=="Wall/T1" && r.MaterialId=="Concrete"));
        Assert.DoesNotContain(view.Regions.Where(r=>r.SourceComponentId=="Wall/T1" && r.MaterialId=="Concrete"),r=>
            MinY(r.Footprint.Outer)<500 && MaxY(r.Footprint.Outer)>500);
        Assert.All(view.Voids.Where(v=>v.SourceOpeningId=="Wall/T1/Pipe"),v=>Assert.True(v.Boundary.Closed));
        Assert.Equal(3,view.Voids.Count(v=>v.SourceOpeningId=="Wall/T1/Pipe"));
        var inner=view.Voids.Single(v=>v.SourceOpeningId=="Wall/T1/Pipe" && v.Role==ProjectedVoidRole.Inner);
        var outer=view.Voids.Single(v=>v.SourceOpeningId=="Wall/T1/Pipe" && v.Role==ProjectedVoidRole.PipeOuter);
        Assert.Equal(346.410161513775,MaxY(inner.Boundary)-MinY(inner.Boundary),6);
        Assert.Equal(469.148164229596,MaxY(outer.Boundary)-MinY(outer.Boundary),6);
        Assert.All(view.Voids.Where(v=>v.SourceOpeningId=="Wall/T1/Pipe"),v=>Assert.Equal(Source,v.Sources.Single()));
        Assert.Equal(150,view.Dimensions.Single(d=>d.Id=="Wall/T1/Thickness").MeasuredMm);
    }

    [Fact]
    public void BbAtYZeroDoesNotShowT1PipeOrItsWall()
    {
        var view=SectionProjector.BuildSection(Model(),new SectionDefinition("BB",SectionAxis.AlongX,0,SectionMode.TrueSection)).Value!;
        Assert.DoesNotContain(view.Voids,v=>v.SourceOpeningId=="Wall/T1/Pipe");
        Assert.DoesNotContain(view.Regions,r=>r.SourceComponentId=="Wall/T1");
        Assert.Contains(view.Regions,r=>r.SourceComponentId=="Wall/T2");
        Assert.Contains(view.Regions,r=>r.SourceComponentId=="Wall/T4");
    }

    [Fact]
    public void PlanMarksRoundPipeProjectionWithoutRemovingTheWholeWall()
    {
        var plan=PlanProjector.BuildPlan(Model(pipeOffset:100)).Value!;
        var pipe=plan.Voids.Single(v=>v.SourceOpeningId=="Wall/T1/Pipe");
        Assert.Equal(ProjectedVoidRole.PipePlanSilhouette,pipe.Role);
        Assert.Equal(-405,MinX(pipe.Boundary));
        Assert.Equal(205,MaxX(pipe.Boundary));
        Assert.Equal(625,MinY(pipe.Boundary));
        Assert.Equal(775,MaxY(pipe.Boundary));
        Assert.Equal(Source,pipe.Sources.Single());
        Assert.Empty(plan.Regions.Single(r=>r.SourceComponentId=="Wall/T1").Footprint.InnerLoops);
    }

    [Theory]
    [InlineData(3,8)]
    [InlineData(5,0)]
    [InlineData(6,0)]
    public void SmallCircleChordAndTangencyHaveCorrectCutArea(double offset,double expectedHeight)
    {
        var view=SectionProjector.BuildSection(Model(pipeOffset:offset,innerRadius:5,outerRadius:5,cutRadius:5),
            new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection)).Value!;
        var cuts=view.Voids.Where(v=>v.SourceOpeningId=="Wall/T1/Pipe").ToArray();
        if(expectedHeight==0) Assert.Empty(cuts);
        else Assert.Equal(expectedHeight,MaxY(cuts.Single(v=>v.Role==ProjectedVoidRole.ConcreteCutout).Boundary)-
            MinY(cuts.Single(v=>v.Role==ProjectedVoidRole.ConcreteCutout).Boundary),6);
    }

    [Fact]
    public void PlaneOutsideAllComponentsHasNoMaterialHatchLoop()
    {
        var view=SectionProjector.BuildSection(Model(),new SectionDefinition("far",SectionAxis.AlongY,5000,SectionMode.TrueSection)).Value!;
        Assert.Empty(view.Regions);
        Assert.Empty(view.Voids);
    }

    [Fact]
    public void PlaneAcrossPadOnlyDoesNotInventBaseOrWallHatch()
    {
        var view=SectionProjector.BuildSection(Model(),new SectionDefinition("pad-edge",SectionAxis.AlongY,800,SectionMode.TrueSection)).Value!;
        Assert.Single(view.Regions);
        Assert.Equal("Pad",view.Regions[0].SourceComponentId);
        Assert.Empty(view.Voids);
    }

    [Fact]
    public void ConventionalSectionReturnsBlockAndNoPartialValue()
    {
        var result=SectionProjector.BuildSection(Model(),new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.Conventional));
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="SECTION_MODE_UNSUPPORTED" && d.Severity==DiagnosticSeverity.Block && d.Scope==DiagnosticScope.Global);
    }

    [Fact]
    public void ScaleTwentyOrThirtyCannotChangeLogicalProfiles()
    {
        var model=Model();
        var logical=SectionProjector.BuildSection(model,new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection)).Value!;
        foreach(double scale in new[]{20d,30d})
        {
            var request=new PresentationRequest(scale,ElevationMode.Absolute,new ElevationM(10),"test",true);
            Assert.Equal(scale,request.ScaleDenominator);
            var repeated=SectionProjector.BuildSection(model,new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection)).Value!;
            Assert.Equal(logical.Regions.Select(r=>r.Footprint.Outer.Vertices.ToArray()),repeated.Regions.Select(r=>r.Footprint.Outer.Vertices.ToArray()),new PointArrayComparer());
            Assert.Equal(logical.Dimensions.Select(d=>d.MeasuredMm),repeated.Dimensions.Select(d=>d.MeasuredMm));
        }
    }

    [Fact]
    public void SaveThreeSemanticViewProfilesWhenOutputDirectoryIsProvided()
    {
        var output=Environment.GetEnvironmentVariable("DBTOOL_T17A_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        var model=Model(pipeOffset:100);
        var views=new (string Name,ViewProfile Profile)[]
        {
            ("plan",PlanProjector.BuildPlan(model).Value!),
            ("aa",SectionProjector.BuildSection(model,new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection)).Value!),
            ("bb",SectionProjector.BuildSection(model,new SectionDefinition("BB",SectionAxis.AlongX,0,SectionMode.TrueSection)).Value!)
        };
        Directory.CreateDirectory(output);
        var options=new JsonSerializerOptions {WriteIndented=true};
        options.Converters.Add(new JsonStringEnumConverter());
        foreach(var (name,profile) in views)
        {
            var path=Path.Combine(output,$"{name}.json");
            File.WriteAllText(path,JsonSerializer.Serialize(profile,options));
            Assert.True(File.Exists(path));
            using var document=JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(profile.Id,document.RootElement.GetProperty("Id").GetString());
        }
    }

    private static void AssertRoofGap(ViewProfile view)
    {
        var roof=view.Regions.Where(r=>r.SourceComponentId=="Roof").ToArray();
        Assert.Equal(2,roof.Length);
        Assert.All(roof,r=>Assert.True(MaxX(r.Footprint.Outer)<=-350 || MinX(r.Footprint.Outer)>=350));
        var hole=view.Voids.Single(v=>v.SourceOpeningId=="Roof/Hole");
        Assert.Equal(-350,MinX(hole.Boundary)); Assert.Equal(350,MaxX(hole.Boundary));
        Assert.Equal(1400,MinY(hole.Boundary)); Assert.Equal(1550,MaxY(hole.Boundary));
    }

    private static void Bounds(ViewProfile view,string id,double x0,double y0,double x1,double y1)
    {
        var regions=view.Regions.Where(r=>r.SourceComponentId==id).ToArray();
        Assert.NotEmpty(regions);
        Assert.Equal(x0,regions.Min(r=>MinX(r.Footprint.Outer)),6);
        Assert.Equal(y0,regions.Min(r=>MinY(r.Footprint.Outer)),6);
        Assert.Equal(x1,regions.Max(r=>MaxX(r.Footprint.Outer)),6);
        Assert.Equal(y1,regions.Max(r=>MaxY(r.Footprint.Outer)),6);
    }

    private static double MinX(Polygon2Mm p)=>p.Vertices.Min(v=>v.X);
    private static double MaxX(Polygon2Mm p)=>p.Vertices.Max(v=>v.X);
    private static double MinY(Polygon2Mm p)=>p.Vertices.Min(v=>v.Y);
    private static double MaxY(Polygon2Mm p)=>p.Vertices.Max(v=>v.Y);

    private static GeometryModel Model(double pipeOffset=0,double innerRadius=200,double outerRadius=255,double cutRadius=305)
    {
        var components=new List<ComponentGeometry>
        {
            Component("Pad","Bedding",-850,-975,850,975,-250,-150),
            Component("Base","Concrete",-750,-875,750,875,-150,0),
            Component("Wall/T1","Concrete",-650,625,650,775,0,1400),
            Component("Wall/T2","Concrete",500,-625,650,625,0,1400),
            Component("Wall/T3","Concrete",-650,-775,650,-625,0,1400),
            Component("Wall/T4","Concrete",-650,-625,-500,625,0,1400)
        };
        var roofOuter=Rect(-650,-775,650,775);
        var roofFootprint=new Footprint(roofOuter.Outer,[new Polygon2Mm([new(-350,350),new(350,350),new(350,-350),new(-350,-350)])]);
        var roofFrame=new Frame3Mm(new(0,0,1400),new(1,0,0),new(0,1,0),new(0,0,1));
        var roofHole=new OpeningProfile("Roof/Hole","Roof",OpeningKind.Box,new RectangleProfile(new(-350,-350,350,350)),null,
            new RectangleProfile(new(-350,-350,350,350)),null,roofFrame,new(1400,1550),new(0,150),[Source]);
        components.Add(new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",roofFootprint,new(1400,1550),
            new ExtrusionSolid(roofOuter,PlanFrame,new(1400,1550)),[roofHole],[],[Source]));

        var wall=WallFrame.Create(WallId.T1,1000,1250);
        var frame=new Frame3Mm(wall.Origin,wall.Left,wall.Up,wall.Outward);
        var inner=new CircleProfile(new(pipeOffset,500),innerRadius);
        var outer=new CircleProfile(new(pipeOffset,500),outerRadius);
        var cut=new CircleProfile(new(pipeOffset,500),cutRadius);
        var pipe=new OpeningProfile("Wall/T1/Pipe","Wall/T1",OpeningKind.Round,inner,outer,cut,wall,frame,
            new(500-cutRadius,500+cutRadius),new(0,150),[Source]);
        int index=components.FindIndex(c=>c.Id=="Wall/T1");
        var old=components[index];
        components[index]=new ComponentGeometry(old.Id,old.Kind,old.MaterialId,old.Footprint,old.ZRange,old.Occupied,[pipe],[],[Source]);
        return new GeometryModel(components,[roofHole,pipe]);
    }

    private static readonly Frame3Mm PlanFrame=new(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
    private static Footprint Rect(double x0,double y0,double x1,double y1)=>new(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
    private static ComponentGeometry Component(string id,string material,double x0,double y0,double x1,double y1,double z0,double z1)
    {
        var fp=Rect(x0,y0,x1,y1); var z=new Interval1D(z0,z1);
        var kind=id switch {"Pad"=>ComponentKind.Pad,"Base"=>ComponentKind.Base,_=>ComponentKind.Wall};
        return new ComponentGeometry(id,kind,material,fp,z,new ExtrusionSolid(fp,PlanFrame,z),[],[],[Source]);
    }

    private sealed class PointArrayComparer : IEqualityComparer<Point2Mm[]>
    {
        public bool Equals(Point2Mm[]? a,Point2Mm[]? b)=>a is not null && b is not null && a.SequenceEqual(b);
        public int GetHashCode(Point2Mm[] x)=>x.Length;
    }
}
