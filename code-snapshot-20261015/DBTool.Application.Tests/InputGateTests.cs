using Xunit;
using Legacy=DBTool.Core;
namespace DBTool.Application.Tests;
public class InputGateTests
{
    [Theory]
    [InlineData("ui")][InlineData("batch")][InlineData("export")]
    public void ThreeActualAdaptersUseSameGlobalAndSubsetGate(string entry)
    {
        var selected=new[]{new Legacy.NormalizedManholeSpec{Name="A",Type=Legacy.ManholeType.GaThamLienKhoi}};
        var global=new Legacy.Diagnostic(Legacy.DiagnosticSeverity.Block,"CF.Ga",new("input.xlsx","CF.Ga","C3","bad"),"bad");
        var other=new Legacy.Diagnostic(Legacy.DiagnosticSeverity.Block,"B",new("input.xlsx","GaThamLienKhoi","G5","bad"),"other ga");
        var review=new Legacy.Diagnostic(Legacy.DiagnosticSeverity.Review,"CF.Ga",global.Source,"review");
        var diagnostics=new[]{global,other,review};
        var result=entry switch {"ui"=>Legacy.DrawingInputGate.ForUi(selected,diagnostics),"batch"=>Legacy.DrawingInputGate.ForBatch(selected,diagnostics),_=>Legacy.DrawingInputGate.ForExport(selected,diagnostics)};
        Assert.Same(global,Assert.Single(result));
    }
    [Fact] public void DomainGateUsesIdsAndComponentOwner()
    {
        var errors=new[]{
            new Domain.Diagnostic("GLOBAL",Domain.DiagnosticSeverity.Block,Domain.DiagnosticScope.Global,null,null,null,null,null,"global"),
            new Domain.Diagnostic("OTHER",Domain.DiagnosticSeverity.Block,Domain.DiagnosticScope.Manhole,"B",null,null,null,null,"other"),
            new Domain.Diagnostic("COMP",Domain.DiagnosticSeverity.Block,Domain.DiagnosticScope.Component,"A","Base",null,null,null,"selected component"),
            new Domain.Diagnostic("REVIEW",Domain.DiagnosticSeverity.Review,Domain.DiagnosticScope.Global,null,null,null,null,null,"review")};
        Assert.Equal(new[]{"GLOBAL","COMP"},DrawingInputGate.GetBlockingDiagnostics(["A"],errors).Select(d=>d.Code));
    }
    [Theory]
    [InlineData("CF.Ga")][InlineData("CF.Cong")][InlineData("CF.Data")]
    public void LegacyConfigDiagnosticAlwaysBlocks(string sheet)
    {
        var selected=new[]{new Legacy.NormalizedManholeSpec { Name="A",Type=Legacy.ManholeType.GaThamLienKhoi }};
        var error=new Legacy.Diagnostic(Legacy.DiagnosticSeverity.Block,sheet,new("input.xlsx",sheet,"C3","bad"),"invalid config");
        Assert.Single(Legacy.DrawingInputGate.GetBlockingDiagnostics(selected,[error]));
    }
}
