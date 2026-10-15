using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class ScheduleTablePlannerTests
{
    [Fact]
    public void TwoHundredRowsHaveRepeatedHeadersGridAndOneFinalFooter()
    {
        var rows = Enumerable.Range(0,200).Select(i => new TableRowPrimitive($"r{i}","Schedule/Row","table",
            i < 100 ? "Base" : "Wall",null,null,$"D{i}",
            [new("Mark",TableCellKind.Text,$"D{i}",null),new("MassKg",TableCellKind.Number,null,i+1)])).ToArray();
        var pages = ScheduleTablePlanner.Plan(rows,new(0,0),30,"Steel",["Mark","Mass (kg)"],"Total 20100 kg");
        Assert.True(pages.Count > 1);
        Assert.Equal(200,pages.Sum(p=>p.Primitives.OfType<TableRowPrimitive>().Count()));
        Assert.Single(pages.SelectMany(p=>p.Primitives).Where(p=>p.Role=="Schedule/TotalMass"));
        Assert.All(pages,p=> {
            Assert.Contains(p.Primitives,x=>x.Role=="Schedule/Header");
            Assert.Contains(p.Primitives,x=>x.Role=="Schedule/Grid");
            Assert.True(p.View.Bounds!.MaxY <= 7200);
            foreach(var row in p.Primitives.OfType<TableRowPrimitive>())
                Assert.All(row.Placements,c=>Assert.InRange(c.Position.Y,0,p.View.Bounds.MaxY-c.HeightMm));
        });
        Assert.Equal(2,pages.SelectMany(p=>p.Primitives).OfType<TableRowPrimitive>().Select(r=>r.ComponentId).Distinct().Count());
    }

    [Fact]
    public void OversizedTitleIsRejectedRatherThanDrawnOutsideBounds()
    {
        var row=new TableRowPrimitive("r","Schedule/Row","t",null,null,null,"1",[new("Mark",TableCellKind.Text,"1",null)]);
        Assert.Throws<ArgumentException>(()=>ScheduleTablePlanner.Plan([row],new(0,0),30,new string('W',1000),["Mark"],null));
    }
}
