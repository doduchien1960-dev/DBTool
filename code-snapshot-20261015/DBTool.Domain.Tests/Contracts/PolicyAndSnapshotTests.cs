using Xunit;
namespace DBTool.Domain.Tests.Contracts;
public class PolicyAndSnapshotTests
{
    private static SourceRef Source => new(SourceKind.SyntheticFixture,"fixture.json",new string('a',64),null,null,null,"");
    [Fact] public void ExperimentalPolicyRequiresExplicitConsent()
    {
        var denied=CalculationPolicy.Select("S1-Analytic-1",false);
        Assert.False(denied.Success); Assert.Null(denied.Value);
        Assert.Equal("POLICY_EXPERIMENTAL_DISABLED",Assert.Single(denied.Diagnostics).Code);
        var allowed=CalculationPolicy.Select("S1-Analytic-1",true);
        Assert.True(allowed.Success); Assert.Equal(EvidenceStatus.ExperimentalSoftwareFixture,allowed.Value!.EvidenceStatus);
        Assert.Null(typeof(CalculationPolicy).GetProperty("Scale"));
        Assert.Null(typeof(CalculationPolicy).GetProperty("ScaleDenominator"));
    }
    [Fact] public void RawNestedCollectionsAreSnapshots()
    {
        var cells=new Dictionary<string,RawCell>{{"Invert",new(RawCellKind.Number,null,300,null,null,null,Source)}};
        var row=new RawManholeRow(4,cells);
        var rows=new List<RawManholeRow>{row};
        var catalogs=new Dictionary<string,IEnumerable<RawManholeRow>>{{"CF.Cong",rows}};
        var project=new RawProjectInput("S1",new string('a',64),Profile.GaThamLienKhoi,"GaThamLienKhoi",rows,new Dictionary<string,RawCell>(),catalogs,Array.Empty<Diagnostic>());
        cells["Invert"]=new(RawCellKind.Number,null,900,null,null,null,Source); rows.Clear(); catalogs.Clear();
        Assert.Equal(300,Assert.Single(project.Rows).Cells["Invert"].Number);
        Assert.Single(project.CatalogCells["CF.Cong"]);
    }
    [Fact] public void BlankNeverBecomesZeroAndUnionRejectsConflicts()
    {
        Assert.Null(new RawCell(RawCellKind.Blank,null,null,null,null,null,Source).Number);
        Assert.Throws<ArgumentException>(()=>new RawCell(RawCellKind.Blank,null,0,null,null,null,Source));
        Assert.Throws<ArgumentException>(()=>new ParsedRebar(RebarKind.Mesh,new MeshSpec(10,150),null,new AnchorSpec(6,150,100),Source));
    }
    [Fact] public void NumericPolicyDisallowsThousands()
    {
        Assert.Equal(",",new NumericPolicy("vi-VN",false,false).DecimalSeparator);
        Assert.Throws<ArgumentException>(()=>new NumericPolicy("en-US",true,false));
    }
    [Fact] public void ResultTruthTableAndSnapshot()
    {
        var review=new Diagnostic("REVIEW",DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,null,null,null,"review");
        var block=new Diagnostic("BLOCK",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,"block");
        Assert.Throws<ArgumentException>(()=>new OperationResult<string>(true,null,[]));
        Assert.Throws<ArgumentException>(()=>new OperationResult<string>(true,"data",[block]));
        Assert.Throws<ArgumentException>(()=>new OperationResult<string>(false,"data",[block]));
        Assert.Throws<ArgumentException>(()=>new OperationResult<string>(false,null,[]));
        Assert.Throws<ArgumentException>(()=>new OperationResult<string>(false,null,[review]));
        var diagnostics=new List<Diagnostic>{review};
        var result=new OperationResult<string>(true,"data",diagnostics); diagnostics.Add(block);
        Assert.True(result.Success); Assert.Single(result.Diagnostics);
        Assert.False(new OperationResult<string>(false,null,[block]).Success);
    }
}
