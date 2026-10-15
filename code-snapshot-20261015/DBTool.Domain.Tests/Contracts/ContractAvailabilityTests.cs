using Xunit;
using DBTool.Domain.Geometry;

namespace DBTool.Domain.Tests.Contracts;

public class ContractAvailabilityTests
{
    [Theory]
    [InlineData("LengthMm")]
    [InlineData("AreaMm2")]
    [InlineData("VolumeMm3")]
    [InlineData("ElevationM")]
    [InlineData("MassKg")]
    [InlineData("SourceRef")]
    [InlineData("Diagnostic")]
    [InlineData("OperationResult`1")]
    [InlineData("CalculationPolicy")]
    [InlineData("ManholeInput")]
    [InlineData("ValidatedManholeSpec")]
    [InlineData("GeometryModel")]
    [InlineData("BarOccurrence")]
    [InlineData("SteelSchedule")]
    public void ContractTypeIsAvailable(string name)
    {
        Assert.NotNull(typeof(Point2Mm).Assembly.GetType("DBTool.Domain." + name));
    }
}
