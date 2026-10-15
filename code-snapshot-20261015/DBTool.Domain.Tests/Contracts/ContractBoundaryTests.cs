using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;
namespace DBTool.Domain.Tests.Contracts;
public class ContractBoundaryTests
{
    [Theory]
    [InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(double.NegativeInfinity)]
    public void GeometryRejectsNonFinite(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Point2Mm(value,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Point3Mm(0,0,value));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Interval1D(0,value));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Rectangle2Mm(0,0,1,value));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new MeshSpec(10,value));
    }
    [Fact] public void IntervalMayBePointButNotReversed()
    {
        Assert.Equal(0,new Interval1D(1,1).Length);
        Assert.Throws<ArgumentException>(()=>new Interval1D(2,1));
    }
    [Fact] public void ManholeOwnsPipeSnapshot()
    {
        var source = new Dictionary<string,SourceRef>();
        var pipe = new PipeConnectionSpec(WallId.T1,"D400",300,0,50,source);
        var pipes = new List<PipeConnectionSpec>{pipe};
        var validated=SyntheticTestFactory.Create(pipes); pipes.Clear();
        Assert.Equal(300,Assert.Single(validated.Input.PipeConnections).InvertMm);
        Assert.Equal(1000,validated.Input.L);
        Assert.Equal(400,validated.Catalogs.Pipes["D400"].InnerDiameterMm);
    }
    [Fact] public void OnlyTwoAssembliesAreFriends()
    {
        var friends=typeof(ValidatedManholeSpec).Assembly.GetCustomAttributes(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute),false)
            .Cast<System.Runtime.CompilerServices.InternalsVisibleToAttribute>().Select(a=>a.AssemblyName).Order().ToArray();
        Assert.Equal(new[]{"DBTool.Application","DBTool.Domain.Tests"},friends);
    }
    [Fact] public void SourceAndDiagnosticKeepTheirScope()
    {
        Assert.Throws<ArgumentException>(()=>new SourceRef(SourceKind.WorkbookCell,"a.xlsx",new string('a',64),null,null,null,""));
        Assert.Throws<ArgumentException>(()=>new SourceRef(SourceKind.SourceCode,"../a.cs",new string('a',64),null,null,null,""));
        Assert.Throws<ArgumentException>(()=>new Diagnostic("X",DiagnosticSeverity.Block,DiagnosticScope.Component,"ga",null,null,null,null,"bad"));
    }
    [Fact] public void RowCountCannotReplaceRealOccurrences()
    {
        Assert.Throws<ArgumentException>(()=>new SteelScheduleRow("1","straight-v1",ComponentKind.Base,Layer.Bottom,Axis.X,10,2,1000,2000,100d/162,new MassKg(200d/162),["bar1"],"MASS-162-001"));
    }
}
