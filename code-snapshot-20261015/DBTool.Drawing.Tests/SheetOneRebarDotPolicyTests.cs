using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneRebarDotPolicyTests
{
    [Theory]
    [InlineData("SteelAA")]
    [InlineData("SteelBB")]
    [InlineData("WallMeshPlan")]
    public void Family21DotPreservesPhysicalIdentityAndPosition(string view)
    {
        var physical = new CirclePrimitive("bar/dot", "Rebar/Main", view, "Wall/T3", "bar", "T3", new(120,300), 6);
        var request = new PresentationRequest(30, ElevationMode.Symbolic, null, SheetOnePresentationPolicy.Family21Theme, true);
        var dot = Assert.IsType<CirclePrimitive>(SheetOneRebarDotPolicy.Apply(physical, request));
        Assert.Equal(SheetOneRebarDotPolicy.Role, dot.Role);
        Assert.Equal(physical.SemanticId, dot.SemanticId);
        Assert.Equal(physical.ComponentId, dot.ComponentId);
        Assert.Equal(physical.OccurrenceId, dot.OccurrenceId);
        Assert.Equal(physical.ScheduleMark, dot.ScheduleMark);
        Assert.Equal(physical.Center, dot.Center);
        Assert.Equal(physical.RadiusMm, dot.RadiusMm);
        Assert.Equal("VHG-Thep", SemanticRoleRegistry.Resolve(dot.Role)!.Layer);
    }

    [Fact]
    public void OtherProfilesViewsAndNonRebarCirclesRetainOriginalNativeContract()
    {
        var request = new PresentationRequest(30, ElevationMode.Symbolic, null, SheetOnePresentationPolicy.Family21Theme, true);
        var physical = new CirclePrimitive("dot", "Rebar/Main", "SteelAA", "Wall/T3", "bar", "T3", new(0,0), 6);
        Assert.Same(physical, SheetOneRebarDotPolicy.Apply(physical, new(30,ElevationMode.Symbolic,null,"S2",true)));
        var otherView = new CirclePrimitive("dot", "Rebar/Main", "Reinforcement/Wall/T3/Inner", "Wall/T3", "bar", "T3", new(0,0), 6);
        Assert.Same(otherView, SheetOneRebarDotPolicy.Apply(otherView, request));
        var geometry = new CirclePrimitive("pipe", "Geometry/PipeInner", "SteelAA", "Wall/T3", null, null, new(0,0), 200);
        Assert.Same(geometry, SheetOneRebarDotPolicy.Apply(geometry, request));
    }
}
