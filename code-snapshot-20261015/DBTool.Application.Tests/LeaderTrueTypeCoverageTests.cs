using DBTool.Application.Templates;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class LeaderTrueTypeCoverageTests
{
    private static string Font=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");
    [Fact]
    public void ActualArialMapsVietnameseAndDigitsWithoutChangingFontBytes()
    {
        var hash=NativeContractHash.File(Font);
        var result=TrueTypeTextCoverage.Verify(Font,hash,"Hố ga 123.456");
        Assert.True(result.HasInk);
        Assert.Equal("Hố ga 123.456".EnumerateRunes().Count(),result.GlyphCount);
        Assert.Equal(hash,result.FontSha256);
    }
    [Fact]
    public void SpaceOnlyHasMappedGlyphsAndNoOutlineInk()
    {
        var result=TrueTypeTextCoverage.Verify(Font,NativeContractHash.File(Font),"   ");
        Assert.False(result.HasInk);
        Assert.Equal(3,result.GlyphCount);
    }
    [Fact]
    public void AbsentUnicodeScalarCannotSilentlyUseAnotherFont()
    {
        var fault=Assert.Throws<NativeAssetContractException>(()=>TrueTypeTextCoverage.Verify(Font,NativeContractHash.File(Font),char.ConvertFromUtf32(0x10ffff)));
        Assert.Equal("FONT_GLYPH_MISSING",fault.Code);
    }
    [Fact]
    public void MismatchedBytesAndMalformedUtf16HaveSpecificFaults()
    {
        Assert.Equal("FONT_SOURCE_MISMATCH",Assert.Throws<NativeAssetContractException>(()=>TrueTypeTextCoverage.Verify(Font,new string('a',64),"A")).Code);
        Assert.Equal("TEXT_ENCODING_INVALID",Assert.Throws<NativeAssetContractException>(()=>TrueTypeTextCoverage.Verify(Font,NativeContractHash.File(Font),"\ud800")).Code);
    }
}
