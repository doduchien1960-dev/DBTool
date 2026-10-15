using System.Collections;
using System.Reflection;
using DBTool.Application.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class LeaderAcceptanceIntegrityTests
{
    [Theory]
    [InlineData("mm")]
    [InlineData("mm2")]
    [InlineData("kg")]
    [InlineData("paper_mm")]
    [InlineData("pdf_point")]
    [InlineData("rad")]
    [InlineData("dimensionless")]
    [InlineData("1:S")]
    public void MissingToleranceDoesNotCreateNumericalAuthority(string unit)
    {
        var state=Fixture();
        var rules=state.rules with { geometry_tolerance_mm=null,length_tolerance_mm=null,area_tolerance_mm2=null,mass_tolerance_kg=null,paper_tolerance_mm=null,pdf_tolerance_points=null,angle_tolerance_rad=null,basis_tolerance=null };
        var method=typeof(AcceptanceEvidenceAssembler).GetMethod("GetUnitTolerance",BindingFlags.NonPublic|BindingFlags.Static)!;
        Assert.Null(method.Invoke(null,[unit,rules]));
    }

    [Fact]
    public void MatchingHandleNeverTurnsExpectedDimensionIntoMeasuredActual()
    {
        var state=Fixture();
        var native=new NativeV2("not-a-real-saved.dwg",new string('a',64),
            [new HandleV2("Effective.clear_x_mm","10A",null,[],[],state.context.manhole_id)],
            state.geometry.physical_hash,state.rebar.physical_hash,"synthetic-test-only","Committed","Passed",[],[],[],state.rebar.fabrication_geometry_hash,state.sheet.sheet_hash,"Reopened");
        var input=new S10StateV2(state.context,state.request,state.assets,state.rules,state.raw,state.effective,state.geometry,state.rebar,state.groups,state.projection,state.annotations,state.schedules,state.sheet,native);
        var method=typeof(AcceptanceEvidenceAssembler).GetMethod("BuildAcceptanceSubjects",BindingFlags.NonPublic|BindingFlags.Static)!;
        var subjects=((IEnumerable)method.Invoke(null,[input])!).Cast<object>().ToArray();
        Assert.NotEmpty(subjects);
        foreach(var subject in subjects)
        {
            Assert.Null(subject.GetType().GetProperty("Actual")!.GetValue(subject));
            Assert.Null(subject.GetType().GetProperty("ActualText")!.GetValue(subject));
        }
    }

    private static S9StateV2 Fixture()=>(S9StateV2)typeof(W13StageAdapterTests).GetMethod("CreateMockS9State",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;
}
