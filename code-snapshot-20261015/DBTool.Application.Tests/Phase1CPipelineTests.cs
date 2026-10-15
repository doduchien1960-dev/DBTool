using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class Phase1CPipelineTests
{
    [Fact]
    public void FullPipelinePassesAnglesAndCalculatedQuantitiesToRealDrawingAdapter()
    {
        var raw=ManholeValidatorTests.Raw();
        var cells=raw.Rows[0].Cells.SetItem("PipeCodeT1",ManholeValidatorTests.Cell(null,"O4"))
            .SetItem("InvertT1",ManholeValidatorTests.Cell(null,"P4"));
        raw=new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[new RawManholeRow(4,cells)],raw.ConfigCells,new Dictionary<string,IEnumerable<RawManholeRow>>(),raw.Diagnostics);
        var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
        var request=new CalculationRequest(raw.ProjectId,[ManholeValidatorTests.Id],CalculationScope.Full,policy.PolicyId,true);
        var input=new ManholeValidator(new NumericCellReader(),new("en-US",false,false))
            .Validate(raw,ManholeValidatorTests.Catalogs(),request,policy);
        Assert.True(input.Success,string.Join(";",input.Diagnostics.Select(d=>d.Code)));
        var result=new CalculationPipeline(new GeometryStageAdapter(),new WithSyntheticAngle(),new SteelScheduleStageAdapter(),
            new DrawingStageAdapter(),new ConcreteQuantityStageAdapter(),new FormworkQuantityStageAdapter())
            .Calculate(input.Value!,policy,request,new(30,ElevationMode.Symbolic,null,"Default",true),default);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var package=result.Value!;
        Assert.Equal(CalculationStatus.Calculated,package.ConcreteStatus);
        Assert.Equal(CalculationStatus.Calculated,package.FormworkStatus);
        var angle=Assert.Single(package.DrawingPackage!.Primitives.OfType<TableRowPrimitive>().Where(r=>r.Role=="Schedule/AngleRow"));
        Assert.Equal(1000,angle.Cells.Single(c=>c.ColumnKey=="TotalLengthMm").Number);
        Assert.Equal(package.AngleSchedule!.TotalMassKg.Value,angle.Cells.Single(c=>c.ColumnKey=="MassKg").Number);
        Assert.Equal(package.ConcreteLines!.Value.Length+package.FormworkLines!.Value.Length,
            package.DrawingPackage.Primitives.OfType<TableRowPrimitive>().Count(r=>r.Role=="Quantity/Row"));
    }

    private sealed class WithSyntheticAngle : IBaseRebarStage
    {
        public OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec,GeometryModel geometry,CalculationPolicy policy)
        {
            var result=new FullRebarStage().Plan(spec,geometry,policy);
            if(!result.Success)return result;
            // Explicit synthetic angle isolates scheduling order from neck section rendering (P3).
            var angle=new AngleOccurrence("synthetic-angle","Base","V30",result.Value!.Occurrences[0].Path,1000,1.36,spec.Sources);
            return new(true,new RebarLayout(result.Value.Occurrences,result.Diagnostics,result.Value.Scope,result.Value.Status,[angle]),result.Diagnostics);
        }
    }
}
