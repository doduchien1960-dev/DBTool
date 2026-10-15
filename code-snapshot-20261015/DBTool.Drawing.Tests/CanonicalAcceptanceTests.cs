using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class CanonicalAcceptanceTests
{
    [Fact] public void LegacySnapshotCannotSignAcceptanceEvenWhenBothSidesEqual()
    {
        var entity = new SemanticEntity("a", "Circle", null, null,
            new Dictionary<string,string>(), new Dictionary<string,double>{{"Radius",-2}});
        var snapshot = new SemanticSnapshot("drawing", "run", [entity]);
        var error = Assert.Throws<InvalidOperationException>(() => SemanticDiff.Compare(snapshot, snapshot, new()));
        Assert.Contains("SNAPSHOT_LEGACY_INCOMPATIBLE", error.Message);
    }

    [Fact] public void MissingSchemaCannotEnterPhysicalAcceptance()
    {
        const string malformed = "{\"Entities\":[{\"SemanticId\":\"a\",\"Type\":\"Line\",\"Numbers\":{\"Start.X\":1}}]}";
        var error = Assert.Throws<InvalidOperationException>(() => SemanticDiff.ComparePhysicalManifests(malformed, malformed));
        Assert.Contains("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED", error.Message);
    }
}
