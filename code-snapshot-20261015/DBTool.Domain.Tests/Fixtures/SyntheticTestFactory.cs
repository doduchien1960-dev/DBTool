using System.Security.Cryptography;
using DBTool.Domain;

namespace DBTool.Domain.Tests.Fixtures;

/// <summary>SyntheticTest only. Not a VHG-verified or construction-approved input.</summary>
internal static class SyntheticTestFactory
{
    internal static ValidatedManholeSpec Create(IEnumerable<PipeConnectionSpec>? pipes = null, CatalogSet? catalogs = null)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("SyntheticTest: S1 contract fixture");
        var source = new SourceRef(SourceKind.SyntheticFixture, "SyntheticTest.inline", Convert.ToHexString(SHA256.HashData(bytes)), null, null, null, System.Text.Encoding.UTF8.GetString(bytes));
        var sources = new Dictionary<string,SourceRef> { ["fixture"] = source };
        var input = new ManholeInput("S1/GaThamLienKhoi/S1-MONO-001", "S1-MONO-001", Profile.GaThamLienKhoi,
            1000,1250,1400,150,150,150,100,100,100,0,0,0,0,new Rectangle2Mm(-350,-350,350,350),
            pipes ?? new[] { new PipeConnectionSpec(WallId.T1,"D400",300,0,50,sources) },
            new MeshSpec(10,150),new MeshSpec(10,150),30,ElevationMode.Absolute,new ElevationM(10),PipeInvertReference.InternalInvert,
            HookPattern.Hook0M,HookPattern.Hook0M,DistributionMode.Equalized,MeshJunctionPolicy.UniformBaseTestOnly,
            LayerStackPolicy.TwoDirectionsTouchingTestOnly,[],sources,new Dictionary<string,RawCell>());
        var catalog = new CatalogSet(new Dictionary<string,PipeCatalogEntry> { ["D400"] = new("D400",PipeKind.Round,400,null,null,55,null,0) },
            new Dictionary<string,SteelAngleEntry>(),new Dictionary<string,HatchEntry>(),new Dictionary<string,string>());
        return new ValidatedManholeSpec(input,catalogs ?? catalog,"S1-Analytic-1/v1",[source],[]);
    }
}
