using DBTool.Domain;
using Xunit;

namespace DBTool.Domain.Tests;

public class RebarSpecParserTests
{
    private static SourceRef Source => new(SourceKind.WorkbookCell,"fixture.xlsx",new string('a',64),"GaThamLienKhoi","G4",null,"");

    [Theory]
    [InlineData("D12A150",12,150)]
    [InlineData("d10a150",10,150)]
    [InlineData("D10A150.5",10,150.5)]
    public void MeshRequiredParsesWholeInvariantToken(string text,int diameter,double spacing)
    {
        var result = RebarSpecParser.Parse(text,ParseContext.MeshRequired,Source);
        Assert.True(result.Success);
        Assert.Equal(RebarKind.Mesh,result.Value!.Kind);
        Assert.Equal(diameter,result.Value.Mesh!.DiameterMm);
        Assert.Equal(spacing,result.Value.Mesh.SpacingMm);
        Assert.Equal(Source.SourceKind,result.Value.Source.SourceKind);
    }

    [Fact] public void AnchorAndCountedRespectContext()
    {
        var anchor = RebarSpecParser.Parse("D6A150L100",ParseContext.AnchorOptional,Source);
        Assert.True(anchor.Success); Assert.Equal(new AnchorSpec(6,150,100),anchor.Value!.Anchor);
        var counted = RebarSpecParser.Parse("2D8",ParseContext.LiftingHook,Source);
        Assert.True(counted.Success); Assert.Equal(new CountedBarSpec(2,8),counted.Value!.Counted);
    }

    [Theory]
    [InlineData(null,ParseContext.MeshOptional)]
    [InlineData("0",ParseContext.AnchorOptional)]
    [InlineData("0D10",ParseContext.LiftingHook)]
    public void ExplicitOptionalOffBecomesDisabled(string? text,ParseContext context)
    {
        var result = RebarSpecParser.Parse(text,context,Source);
        Assert.True(result.Success); Assert.Equal(RebarKind.Disabled,result.Value!.Kind);
    }

    [Theory]
    [InlineData(null,ParseContext.MeshRequired,"REBAR_REQUIRED")]
    [InlineData("0",ParseContext.MeshRequired,"REBAR_CONTEXT_INVALID")]
    [InlineData("0D10",ParseContext.MeshRequired,"REBAR_CONTEXT_INVALID")]
    [InlineData("2D8",ParseContext.MeshRequired,"REBAR_CONTEXT_INVALID")]
    [InlineData("D10A150",ParseContext.LiftingHook,"REBAR_CONTEXT_INVALID")]
    [InlineData("D6A150",ParseContext.AnchorOptional,"REBAR_FORMAT")]
    [InlineData("0D0",ParseContext.LiftingHook,"REBAR_FORMAT")]
    [InlineData("D10A0",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("D0A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("D10 A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("P10A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("|10A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("Phi10A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("Φ10A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("D10A150xyz",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("D10A1e3",ParseContext.MeshRequired,"REBAR_FORMAT")]
    [InlineData("D999999999999999A150",ParseContext.MeshRequired,"REBAR_FORMAT")]
    public void InvalidRebarBlocksWithCellSource(string? text,ParseContext context,string code)
    {
        var result = RebarSpecParser.Parse(text,context,Source);
        Assert.False(result.Success); Assert.Null(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Block,diagnostic.Severity);
        Assert.Equal("G4",diagnostic.Source!.CellAddress);
    }
}
