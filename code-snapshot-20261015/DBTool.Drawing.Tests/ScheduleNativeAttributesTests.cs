using DBTool.Domain;
using Xunit;

namespace DBTool.Drawing.Tests;
public sealed class ScheduleNativeAttributesTests
{
    [Fact]
    public void SummaryHasNineTagsIncludingTongWithoutInventingABlockNamedTong()
    {
        var attrs=ScheduleNativeAttributes.Summary(new Dictionary<string,double>{["DAY1"]=12,["CO2"]=3},15);
        Assert.Equal(new[]{"DAY1","DAY2","TUONG1","TUONG2","TRAN1","TRAN2","CO1","CO2","TONG"},attrs.Select(a=>a.Key));
        Assert.Equal("15",attrs.Single(a=>a.Key=="TONG").Value);
        Assert.Throws<ArgumentException>(()=>ScheduleNativeAttributes.Summary(new Dictionary<string,double>{["BAD"]=1},1));
    }
    [Fact]
    public void SummaryBucketsUseDiameterCutoffRatherThanPhysicalLayer()
    {
        SteelScheduleRow Row(string mark,int diameter,Layer layer,double mass)=>new(mark,"line",ComponentKind.Base,layer,Axis.X,diameter,1,1000,1000,mass,new MassKg(mass),[mark],"test");
        var rows=new[]{Row("a",10,Layer.Top,1),Row("b",12,Layer.Bottom,2)};
        var schedule=new SteelSchedule(rows,[new KeyValuePair<ComponentKind,MassKg>(ComponentKind.Base,new MassKg(3))],new MassKg(3),"test","v1");
        var attrs=ScheduleNativeAttributes.Summary(schedule,null).ToDictionary(a=>a.Key,a=>a.Value);
        Assert.Equal("1",attrs["DAY1"]);Assert.Equal("2",attrs["DAY2"]);Assert.Equal("3",attrs["TONG"]);
    }
    [Fact]
    public void AngleUsesCatalogCodeAndMassPerMetreWithMetreTotalLength()
    {
        var row=new AngleScheduleRow("A001","V50","Neck",2,1500,3000,2,new MassKg(6),["a","b"]);
        var attrs=ScheduleNativeAttributes.Angle(row).ToDictionary(a=>a.Key,a=>a.Value);
        Assert.Equal("TK.TG",ScheduleNativeAttributes.AngleBlockName);
        Assert.Equal("V50",attrs["L"]); Assert.Equal("2kg/m",attrs["D"]);
        Assert.Equal("3",attrs["TDAI"]); Assert.Equal("6",attrs["KL"]);
    }
}
