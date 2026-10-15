using DBTool.Core;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class WallFrameTests
{
    [Theory]
    [InlineData(WallDirection.T1, -100)]
    [InlineData(WallDirection.T2, 100)]
    [InlineData(WallDirection.T3, 100)]
    [InlineData(WallDirection.T4, -100)]
    [Trait("Category", "LegacyGlobalAxisConvention")]
    [Trait("Migration", "MigratedCharacterization")]
    public void LegacyGlobalAxisOffset_MatchesCurrentConvention(WallDirection direction, double expected)
    {
        Assert.Equal(expected, WallViewTransform.ToLocalOffset(direction, 100), 6);
    }
}
