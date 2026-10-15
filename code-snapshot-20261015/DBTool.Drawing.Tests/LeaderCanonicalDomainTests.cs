using System.Text.Json.Nodes;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class LeaderCanonicalDomainTests
{
    [Fact]
    public void RadiusPlaneAloneDoesNotProveArcEndpointDomain()
    {
        var contract=CanonicalR04Tests.Contract("Arc");
        var capture=CanonicalR04Tests.Snapshot(contract,"Arc");
        capture["entities"]![0]!["physical_payload"]!["end"]!["xyz_mm"]=new JsonArray(-5,0,0);
        var fault=Assert.Throws<InvalidOperationException>(()=>SemanticDiff.ComparePhysicalManifests(capture.ToJsonString(),capture.ToJsonString(),contract.ToJsonString()));
        Assert.Contains("SNAPSHOT_DOMAIN",fault.Message);
    }

    [Theory]
    [InlineData(-1.5707963267948966,0,-5)]
    [InlineData(6.283185307179586,5,0)]
    public void SignedAndFullCircleDomainsRemainAnalytic(double sweep,double endX,double endY)
    {
        var contract=CanonicalR04Tests.Contract("Arc");
        var capture=CanonicalR04Tests.Snapshot(contract,"Arc");
        var payload=capture["entities"]![0]!["physical_payload"]!;
        payload["sweep_rad"]=sweep;payload["end"]!["xyz_mm"]=new JsonArray(endX,endY,0);
        Assert.True(SemanticDiff.ComparePhysicalManifests(capture.ToJsonString(),capture.ToJsonString(),contract.ToJsonString()).NoDifference);
    }
}
