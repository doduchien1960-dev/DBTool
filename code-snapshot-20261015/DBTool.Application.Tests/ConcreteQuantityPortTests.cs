using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class ConcreteQuantityPortTests
{
    [Fact]
    public void ConcreteQuantityStageExposesTheAdditiveS2Signature()
    {
        var method = typeof(IConcreteQuantityStage).GetMethod(nameof(IConcreteQuantityStage.Calculate));

        Assert.NotNull(method);
        Assert.Equal(typeof(OperationResult<ConcreteQuantityResult>), method!.ReturnType);
        Assert.Equal(
            [typeof(ValidatedManholeSpec), typeof(GeometryModel), typeof(QuantityPolicy)],
            method.GetParameters().Select(x => x.ParameterType).ToArray());
    }
}
