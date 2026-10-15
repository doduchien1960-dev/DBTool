using System.Reflection;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests.Contracts;

public class UnitsBehaviorTests
{
    private static Type Unit(string name) => Assert.IsAssignableFrom<Type>(typeof(Point2Mm).Assembly.GetType("DBTool.Domain." + name));
    private static object Value(string name, double value) => Activator.CreateInstance(Unit(name), value)!;
    [Theory]
    [InlineData("LengthMm", "ToMetres", 1000, 1)]
    [InlineData("AreaMm2", "ToSquareMetres", 1000000, 1)]
    [InlineData("VolumeMm3", "ToCubicMetres", 1000000000, 1)]
    public void ConvertsUnits(string type, string method, double input, double expected)
        => Assert.Equal(expected, Unit("Units").GetMethod(method)!.Invoke(null, [Value(type, input)]));
    [Fact]
    public void AddsLocalMillimetresToElevationMetres()
    {
        var result = Unit("Units").GetMethod("ToAbsoluteElevation")!.Invoke(null, [Value("ElevationM", 10), Value("LengthMm", 500)]);
        Assert.Equal(10.5, result!.GetType().GetProperty("Value")!.GetValue(result));
    }
    [Theory]
    [InlineData("LengthMm")][InlineData("AreaMm2")][InlineData("VolumeMm3")][InlineData("ElevationM")][InlineData("MassKg")]
    public void RejectsEveryNonFiniteValue(string type)
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => Value(type, value)).InnerException);
    }
    [Fact]
    public void SignedLengthButNeverNegativeMass()
    {
        Assert.Equal(-100d, Unit("LengthMm").GetProperty("Value")!.GetValue(Value("LengthMm", -100)));
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<TargetInvocationException>(() => Value("MassKg", -1)).InnerException);
    }
}
