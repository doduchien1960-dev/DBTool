using DBTool.Domain;
using DBTool.Drawing;
using Xunit;
namespace DBTool.Application.Tests;
public class ContractTests
{
    [Theory]
    [InlineData(CalculationScope.Full)]
    [InlineData(CalculationScope.ConcreteOnly)]
    [InlineData(CalculationScope.FormworkOnly)]
    public void ScopeCannotClaimQuantitiesThatWereNotCalculated(CalculationScope scope)
    {
        Assert.Throws<ArgumentException>(()=>new CalculationPackage(new string('a',64),new string('b',64),"test","contract-v1","v1","future-policy",scope,
            new GeometryModel([],[]),[],new SteelSchedule([],new Dictionary<ComponentKind,MassKg>(),new MassKg(0),"D2Over162","v1"),
            CalculationStatus.NotCalculated,null,CalculationStatus.NotCalculated,null,null,[]));
    }
    [Fact] public void SelectionIsSnapshotAndS1RejectsMultipleOrUnsupportedScope()
    {
        var ids=new List<string>{"S1/GaThamLienKhoi/A"};
        var request=new CalculationRequest("S1",ids,CalculationScope.BaseRebarOnly,"S1-Analytic-1",true);
        ids.Add("B"); Assert.Single(request.SelectedIds);
        Assert.True(request.CheckS1().Success);
        Assert.Equal("S1_SINGLE_MANHOLE_ONLY",Assert.Single(new CalculationRequest("S1",ids,CalculationScope.BaseRebarOnly,"S1-Analytic-1",true).CheckS1().Diagnostics).Code);
        Assert.Equal("S1_SCOPE_UNSUPPORTED",Assert.Single(new CalculationRequest("S1",["A"],CalculationScope.Full,"S1-Analytic-1",true).CheckS1().Diagnostics).Code);
    }
    private static CalculationPackage Package(CalculationStatus status,IEnumerable<QuantityLine>? lines) => new(new string('a',64),new string('b',64),"test","contract-v1","S1-Analytic-1/v1","S1-Analytic-1",CalculationScope.BaseRebarOnly,new GeometryModel([],[]),[],new SteelSchedule([],new Dictionary<ComponentKind,MassKg>(),new MassKg(0),"D2Over162","v1"),status,lines,CalculationStatus.NotCalculated,null,null,[]);
    [Fact] public void NotCalculatedRequiresNullNotZeroOrEmpty()
    {
        var p=Package(CalculationStatus.NotCalculated,null);
        Assert.Null(p.ConcreteLines); Assert.Null(p.FormworkLines);
        Assert.Throws<ArgumentException>(()=>Package(CalculationStatus.NotCalculated,[]));
        Assert.Throws<ArgumentException>(()=>Package(CalculationStatus.Calculated,null));
    }
    [Fact] public void ScaleIsPresentationOnly()
    {
        var p=Package(CalculationStatus.NotCalculated,null);
        var a=new PresentationRequest(20,ElevationMode.Absolute,new ElevationM(10),"default",true);
        var b=new PresentationRequest(30,ElevationMode.Absolute,new ElevationM(10),"default",true);
        Assert.NotEqual(a.ScaleDenominator,b.ScaleDenominator);
        Assert.Null(typeof(CalculationRequest).GetProperty("ScaleDenominator"));
        Assert.Null(typeof(CalculationPolicy).GetProperty("ScaleDenominator"));
        Assert.Equal(new string('A',64),p.InputHash); Assert.Equal(0,p.SteelSchedule!.TotalMassKg.Value);
    }
}
