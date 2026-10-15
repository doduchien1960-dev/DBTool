using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DBTool.Excel.Tests;

// P1/P2 contract tests. The source workbook is copied; no production fixture is edited.
// Geometry expectations come from explicit millimetre inputs and rectangle geometry.
// Relationship tests do not approve the engineering rules of either output.
public sealed class P012NeckContractTests
{
    private static readonly CalculationPolicy Policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
    private sealed class Case : IDisposable
    {
        public string Path {get;}
        public Case(string size="700x700",string mesh="D10A150",string name="G500-2",int thickness=125,bool neck=true)
        {
            Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"p012-"+Guid.NewGuid().ToString("N")+".xlsx");
            File.Copy(System.IO.Path.Combine(TestWorkbookFactory.WorkspaceRoot(),"outputs","P6-candidate","GaThamNapRoi-G500-2-Golden.xlsx"),Path);
            using var b=new XLWorkbook(Path);
            var s=b.Worksheet("GaThamNapRoi");
            int row=s.RowsUsed().Single(r=>r.Cell(2).GetString()=="G500-2").RowNumber();
            // Rename every other row first to avoid collision when testing a known code.
            foreach(var r in s.RowsUsed().Where(r=>r.RowNumber()!=row && r.Cell(2).GetString()==name)) r.Cell(2).Value="unused-"+r.RowNumber();
            s.Cell(row,2).Value=name; s.Cell(row,25).Value=mesh; s.Cell(row,26).Value=mesh;
            s.Cell(row,24).Value=thickness;
            var c=b.Worksheet("CF.Ga"); c.Cell("E11").Value=size;
            if(!neck) {s.Cell(row,23).Value=0;c.Cell("F11").Value="Không";c.Cell("F13").Value="Không";c.Cell("C13").Value="Không";c.Cell("H11").Value="";c.Cell("I11").Value=0;}
            var pair=size.Split('x');
            TestWorkbookFactory.DeclareSyntheticCover(b,double.Parse(pair[0],System.Globalization.CultureInfo.InvariantCulture),double.Parse(pair[1],System.Globalization.CultureInfo.InvariantCulture));
            b.Save();
        }
        public void Dispose()=>File.Delete(Path);
    }
    private static ValidatedManholeSpec Import(Case c,string name="G500-2")
    {
        var raw=new WorkbookImporter().Read(new("P012",c.Path,Profile.GaThamNapRoi,"GaThamNapRoi"));
        Assert.True(raw.Success,string.Join(";",raw.Diagnostics.Select(d=>d.Code)));
        var numeric=new NumericPolicy("en-US",false,false);
        var catalogs=new CatalogReader().Read(raw.Value!,numeric); Assert.True(catalogs.Success);
        var result=new ManholeValidator(new NumericCellReader(),numeric).Validate(raw.Value!,catalogs.Value!,
            new("P012",[ManholeIdentity.Create("P012",Profile.GaThamNapRoi,name)],CalculationScope.FullRebar,Policy.PolicyId,true),Policy);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
        return Assert.Single(result.Value!.SelectedManholes);
    }
    private static (ValidatedManholeSpec Spec,GeometryModel Geometry,RebarLayout Bars,SteelSchedule Schedule,DrawingPackage Drawing) Build(Case c,string name="G500-2")
    {
        var spec=Import(c,name); var g=new GeometryStageAdapter().Build(spec,Policy);
        Assert.True(g.Success,string.Join(";",g.Diagnostics.Select(d=>d.Code)));
        var bars=new FullRebarStage().Plan(spec,g.Value!,Policy);
        Assert.True(bars.Success,string.Join(";",bars.Diagnostics.Select(d=>d.Code)));
        var schedule=new SteelScheduleStageAdapter().Calculate(bars.Value!,Policy);Assert.True(schedule.Success);
        var angles=AngleScheduleCalculator.Calculate(bars.Value!.Angles);Assert.True(angles.Success);
        var inputs=new DrawingInputs(g.Value!,bars.Value!,schedule.Value!,angles.Value!,DrawingQuantitySnapshot.NotCalculated);
        var drawing=new DrawingStageAdapter().Build(spec,inputs,new(20,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true),
            new(new string('A',64),Policy.Version,CalculationScope.FullRebar));
        Assert.True(drawing.Success,string.Join(";",drawing.Diagnostics.Select(d=>d.Code)));
        return(spec,g.Value!,bars.Value!,schedule.Value!,drawing.Value!);
    }
    [Theory]
    [InlineData("820x570",820,570)]
    [InlineData("500x700",500,700)]
    [InlineData("700x500",700,500)]
    [InlineData("713x587",713,587)]
    public void ExplicitInputDimensionsReachSpecAndPhysicalOpening(string size,double x,double y)
    {
        using var c=new Case(size);var spec=Import(c);
        Assert.Equal(x,spec.NeckSpec!.InnerWidthMm);Assert.Equal(y,spec.NeckSpec.InnerLengthMm);
        Assert.Contains(spec.NeckSpec.Sources,s=>s.CellAddress=="E11");
        var g=NeckGeometryBuilder.Build(spec,spec.NeckSpec,spec.Input.NeckSeatSpec!,Policy);Assert.True(g.Success);
        var hole=Assert.IsType<RectangleProfile>(g.Value!.RoofOpening.Inner);
        Assert.Equal(x,hole.Rectangle.MaxX-hole.Rectangle.MinX,6);Assert.Equal(y,hole.Rectangle.MaxY-hole.Rectangle.MinY,6);
    }
    [Fact]
    public void FlagAndDimensionsHaveDistinctSourcesAndHeights()
    {
        using var c=new Case();var s=Import(c);
        Assert.Equal(75,s.Input.NeckSeatSpec!.ExpansionMm);Assert.Equal(50,s.Input.NeckSeatSpec.HeightMm);
        Assert.Equal(s.Input.NeckHeight+50,s.NeckSpec!.HeightMm);
        Assert.Equal(s.Input.H+s.Input.RoofThicknessMm+s.Input.NeckHeight,s.Input.NeckSeatSpec.SeatElevationMm);
        Assert.Contains(s.NeckSpec.Sources,r=>r.CellAddress=="F11");Assert.Contains(s.NeckSpec.Sources,r=>r.CellAddress=="G11");
    }
    [Theory]
    [InlineData("713x587")]
    [InlineData("820x570")]
    [Trait("Phase","P2-RED")]
    public void DrawingNeckPlanMustContainTheInputOpening(string size)
    {
        using var c=new Case(size);var a=Build(c);var n=a.Spec.NeckSpec!;
        Assert.Contains(a.Drawing.Primitives.OfType<PolylinePrimitive>(),p=>p.ViewId=="Reinforcement/Neck/Plan" && p.Closed && p.Points.Length>=4 &&
            Math.Abs(p.Points.Max(q=>q.X)-p.Points.Min(q=>q.X)-n.InnerWidthMm)<0.1 &&
            Math.Abs(p.Points.Max(q=>q.Y)-p.Points.Min(q=>q.Y)-n.InnerLengthMm)<0.1);
    }
    [Theory]
    [InlineData("Plan")][InlineData("AA")][InlineData("BB")]
    [Trait("Phase","P2-RED")]
    public void EachNeckRebarViewMustReferencePhysicalBars(string view)
    {
        using var c=new Case();var a=Build(c);var ids=a.Bars.Occurrences.Where(b=>b.ComponentId=="Neck").Select(b=>b.Id).ToHashSet();
        Assert.NotEmpty(ids);
        Assert.Contains(a.Drawing.Primitives,p=>p.ViewId=="Reinforcement/Neck/"+view && p.OccurrenceId is not null && ids.Contains(p.OccurrenceId));
    }
    [Fact]
    [Trait("Phase","P2-RED")]
    public void DrawingScheduleMustRetainAuthoritativeNeckMarks()
    {
        using var c=new Case();var a=Build(c);var ids=a.Bars.Occurrences.Where(b=>b.ComponentId=="Neck").Select(b=>b.Id).ToHashSet();
        var marks=a.Schedule.Rows.Where(r=>r.OccurrenceIds.Any(ids.Contains)).Select(r=>r.Mark).ToArray();Assert.NotEmpty(marks);
        foreach(var mark in marks) Assert.Contains(a.Drawing.Primitives,p=>p.ViewId.StartsWith("SteelSchedule") && p.ScheduleMark==mark);
    }
    private static string Physical(RebarLayout b)=>JsonSerializer.Serialize(b.Occurrences.Where(x=>x.ComponentId=="Neck").Select(x=>new {x.DiameterMm,x.Surface,x.Direction,x.ScheduleLengthMm,Segments=x.Path.Segments.Select(s=>new{s.Start,s.End})}));
    private static string NeckDrawing(DrawingPackage d)
    {
        using var json=JsonDocument.Parse(DrawingPackageJson.Serialize(d));
        var shapes=json.RootElement.GetProperty("Primitives").EnumerateArray()
            .Where(p=>p.GetProperty("ViewId").GetString()!.StartsWith("Reinforcement/Neck/"))
            .Select(p=>{var shape=JsonNode.Parse(p.GetRawText())!.AsObject();
                foreach(var key in new[]{"SemanticId","OccurrenceId","SourceOccurrenceId","ScheduleMark"}) shape.Remove(key);
                return shape.ToJsonString();}).Order().ToArray();
        return JsonSerializer.Serialize(shapes);
    }
    [Fact]
    [Trait("Phase","P2-RED")]
    public void ChangingOnlyTheIdentifierMustNotChangeNeckDrawing()
    {
        using var ca=new Case(name:"G500-2");using var cb=new Case(name:"G800-1");var a=Build(ca);var b=Build(cb,"G800-1");
        Assert.Equal(Physical(a.Bars),Physical(b.Bars));
        Assert.Equal(NeckDrawing(a.Drawing),NeckDrawing(b.Drawing));
    }
    [Theory]
    [InlineData("D10A125")][InlineData("D12A150")]
    [Trait("Phase","P2-RED")]
    public void ChangingMeshMustChangeNeckPhysicalBarsAndDrawing(string mesh)
    {
        using var ca=new Case();using var cb=new Case(mesh:mesh);var a=Build(ca);var b=Build(cb);
        Assert.NotEqual(Physical(a.Bars),Physical(b.Bars));Assert.NotEqual(NeckDrawing(a.Drawing),NeckDrawing(b.Drawing));
    }
    [Fact]
    public void NeckScheduleBijectionAndNominalLengthsAreConsistent()
    {
        using var c=new Case("713x587");var a=Build(c);var bars=a.Bars.Occurrences.Where(b=>b.ComponentId=="Neck").ToArray();
        var ids=bars.Select(b=>b.Id).ToHashSet();var rows=a.Schedule.Rows.Where(r=>r.OccurrenceIds.Any(ids.Contains)).ToArray();
        Assert.Equal(ids.Order(),rows.SelectMany(r=>r.OccurrenceIds).Order());
        foreach(var r in rows) {Assert.Equal(r.OccurrenceIds.Length,r.Quantity);Assert.Equal(r.SingleLengthMm*r.Quantity,r.TotalLengthMm,6);}
        Assert.All(bars,b=>{Assert.NotEmpty(b.Sources);Assert.False(string.IsNullOrWhiteSpace(b.RuleId));
            double sum=b.Path.Segments.Sum(s=>Math.Sqrt(Math.Pow(s.End.X-s.Start.X,2)+Math.Pow(s.End.Y-s.Start.Y,2)+Math.Pow(s.End.Z-s.Start.Z,2)));
            Assert.Equal(sum,b.ScheduleLengthMm,6);});
    }
    [Fact]
    public void IntersectingFaceStacksAreBlocked()
    {
        using var c=new Case(thickness:90);var s=Import(c);var g=new GeometryStageAdapter().Build(s,Policy);Assert.True(g.Success);
        var b=new FullRebarStage().Plan(s,g.Value!,Policy);Assert.False(b.Success);Assert.Contains(b.Diagnostics,d=>d.Code=="MEMBER_LAYER_COLLISION");
    }
    [Fact]
    public void DisabledNeckDoesNotProducePhysicalNeckBars()
    {
        using var c=new Case(neck:false);var s=Import(c);var g=new GeometryStageAdapter().Build(s,Policy);Assert.True(g.Success);
        var b=new FullRebarStage().Plan(s,g.Value!,Policy);Assert.True(b.Success);Assert.DoesNotContain(b.Value!.Occurrences,x=>x.ComponentId=="Neck");
    }
    [Theory]
    [InlineData(1059.9,8)][InlineData(1060,8)][InlineData(1060.1,9)]
    public void EqualizedSpacingCrossesOneBarThreshold(double span,int count)
    {
        // Hand calculation: 30 mm centre inset, usable span 999.9/1000/1000.1;
        // seven 142.857142857 mm intervals hold at most 1000 mm.
        var r=BarDistributor.DistributeSegment(span,25,10,1000d/7,DistributionMode.Equalized);
        Assert.True(r.Success);Assert.Equal(count,r.Value!.PositionsMm.Length);
        Assert.Equal(30,r.Value.PositionsMm[0],6);Assert.Equal(span-30,r.Value.PositionsMm[^1],6);
        Assert.All(r.Value.ActualGapsMm,g=>Assert.True(g<=1000d/7+1e-6));
    }
}
