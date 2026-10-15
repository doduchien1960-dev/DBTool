using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class WallCTieCompositionTests
{
    [Theory]
    [InlineData(150,10,false)]
    [InlineData(200,16,false)]
    [InlineData(150,10,true)]
    [InlineData(200,16,true)]
    public void ValidatedWorkbookWallMeshesComposeDynamicPhysicalCBeforeMixedShapeGate(int thickness,int diameter,bool pipe)
    {
        var raw=ManholeValidatorTests.Raw();
        var cells=raw.Rows[0].Cells.SetItem("PipeCodeT1",ManholeValidatorTests.Cell(pipe?"D400":null,"O4"))
            .SetItem("InvertT1",ManholeValidatorTests.Cell(pipe?300:null,"P4"))
            .SetItem("WallThickness",ManholeValidatorTests.Cell(thickness,"I4"))
            .SetItem("WallOuterMesh",ManholeValidatorTests.Cell($"D{diameter}A150","J4"))
            .SetItem("WallInnerMesh",ManholeValidatorTests.Cell($"D{diameter}A150","K4"));
        var config=raw.ConfigCells;
        foreach(var (key,value) in new (string,object)[]{("WallSpacingDelta",0),("WallDiaStepDown",0),("CoverWall",30),
            ("HookPattern.E","0M"),("HookPattern.F","0M"),("Ties.H","D6A300"),("Ties.C","Đều"),
            ("Ties.D","Chữ C"),("Ties.E","Chữ C"),("Ties.F","Chữ C"),("Ties.G","Chữ C"),
            ("Ties.J",50),("Ties.K",100),("Ties.L",200)})
            config=config.SetItem(key,ManholeValidatorTests.Cell(value,"fixture-"+key,"CF.Ga"));
        raw=new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[new(4,cells)],config,
            new Dictionary<string,IEnumerable<RawManholeRow>>(),raw.Diagnostics);
        var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
        var request=new CalculationRequest(raw.ProjectId,[ManholeValidatorTests.Id],CalculationScope.FullRebar,policy.PolicyId,true);
        var validated=new ManholeValidator(new NumericCellReader(),new("en-US",false,false))
            .Validate(raw,ManholeValidatorTests.Catalogs(),request,policy);
        Assert.True(validated.Success,string.Join(";",validated.Diagnostics.Select(d=>d.Code)));
        var spec=Assert.Single(validated.Value!.SelectedManholes);
        var geometry=new GeometryStageAdapter().Build(spec,policy);
        Assert.True(geometry.Success,string.Join(";",geometry.Diagnostics.Select(d=>d.Code)));
        var layout=new FullRebarStage().Plan(spec,geometry.Value!,policy);
        Assert.True(layout.Success,string.Join(";",layout.Diagnostics.Select(d=>d.Code)));
        var ties=layout.Value!.Occurrences.Where(b=>b.FamilyId=="Tie").ToArray();
        Assert.NotEmpty(ties);
        Assert.Equal(4,ties.Select(b=>b.ComponentId).Distinct().Count());
        if(pipe)
        {
            var wall=geometry.Value!.Components.Single(c=>c.Id=="Wall/T1");
            Assert.NotEmpty(wall.Voids);
            Assert.Contains(layout.Value.Occurrences,b=>b.ComponentId==wall.Id && b.FamilyId=="Wall" && b.ParentId is not null);
            Assert.True(ties.Count(b=>b.ComponentId=="Wall/T1")<ties.Count(b=>b.ComponentId=="Wall/T3"));
            Assert.All(ties.Where(b=>b.ComponentId==wall.Id),b=>
            {
                var collision=CWrapOpeningCheck.Intersects(b,wall);
                Assert.True(collision.Success);
                Assert.False(collision.Value!.Intersects);
            });
        }
        Assert.All(ties,b=>
        {
            Assert.Equal(4,b.Path.Segments.OfType<ArcSegment3Mm>().Count());
            Assert.All(b.Path.Segments.OfType<ArcSegment3Mm>(),a=>Assert.Equal(8,a.CenterlineRadiusMm));
            Assert.Equal(LengthStatus.Known,b.LengthStatus);
            Assert.Contains(b.Sources,s=>s.SheetName=="CF.Ga");
            var points=b.Path.Segments.SelectMany(s=>new[]{s.Start,s.End}).ToArray();
            bool depthX=b.ComponentId is "Wall/T2" or "Wall/T4";
            var depths=points.Select(p=>depthX?p.X:p.Y).ToArray();
            Assert.Equal(thickness-54-2*diameter,depths.Max()-depths.Min(),6);
            Assert.Equal(Math.Max(diameter+6,16),points.Max(p=>p.Z)-points.Min(p=>p.Z),6);
        });
        var marks=ties.ToDictionary(b=>b.Id,_=>"T001",StringComparer.Ordinal);
        foreach(var group in ties.GroupBy(b=>b.ComponentId))
        {
            var face=ComponentRebarProjector.Build(group,marks,"Reinforcement/"+group.Key+"/Outer/Top",
                group.Key,group.Key,RebarSurface.Outer,Layer.Top);
            Assert.True(face.Success,string.Join(";",face.Diagnostics.Select(d=>d.Code)));
            Assert.Equal(group.Count(),face.Value!.Select(p=>p.OccurrenceId).Distinct().Count());
        }
        // This workbook requests all four members. Wall C is composed, while
        // unimplemented member consumers must remain explicitly incomplete.
        Assert.Equal(LayoutStatus.Partial,layout.Value.Status);
        Assert.Contains(layout.Diagnostics,d=>d.Code=="TIE_TYPED_SHAPE_NEEDS_RULE");
        Assert.False(new FeatureSupportValidator().ValidateS2Rebar(spec,request.Scope,policy).Success);
    }
}

