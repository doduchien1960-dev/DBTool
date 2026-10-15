using System.Buffers;
using System.Buffers.Binary;
using System.Text;
namespace DBTool.Application.Templates;

public sealed record TrueTypeTextCoverageResult(string FontSha256, int GlyphCount, bool HasInk);

public static class TrueTypeTextCoverage
{
    // Binary offsets, tags and cmap formats follow the OpenType specification:
    // https://learn.microsoft.com/en-us/typography/opentype/spec/cmap
    // https://learn.microsoft.com/en-us/typography/opentype/spec/glyf
    // https://learn.microsoft.com/en-us/typography/opentype/spec/loca
    // This proves coverage from exact font bytes; it does not approximate text extents.
    public static TrueTypeTextCoverageResult Verify(string path, string expectedSha256, string text)
    {
        if(!File.Exists(path))throw Error("FONT_SOURCE_MISSING",path);
        var bytes=File.ReadAllBytes(path);
        var actualHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        if(!string.Equals(actualHash,expectedSha256,StringComparison.OrdinalIgnoreCase))throw Error("FONT_SOURCE_MISMATCH",path);
        try
        {
            void Range(int offset,int length)
            {
                if(offset<0||length<0||offset>bytes.Length-length)throw Error("FONT_DATA_INVALID",path+":"+offset+"+"+length);
            }
            ushort U16(int offset){Range(offset,2);return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset,2));}
            short I16(int offset){Range(offset,2);return BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(offset,2));}
            uint U32(int offset){Range(offset,4);return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));}
            int Offset(uint value){if(value>int.MaxValue)throw Error("FONT_DATA_INVALID",path+":offset overflow");return (int)value;}
            if(U32(0)!=0x00010000)throw Error("FONT_OUTLINE_UNSUPPORTED",path+": requires a TrueType sfnt; no SHX/CFF/TTC substitution");
            var tables=new Dictionary<string,(int Offset,int Length)>(StringComparer.Ordinal);
            for(int i=0;i<U16(4);i++)
            {
                int record=checked(12+16*i);Range(record,16);
                var tag=Encoding.ASCII.GetString(bytes,record,4);var offset=Offset(U32(record+8));var length=Offset(U32(record+12));Range(offset,length);
                if(!tables.TryAdd(tag,(offset,length)))throw Error("FONT_DATA_INVALID",path+":duplicate table "+tag);
            }
            (int Offset,int Length) Table(string tag)=>tables.TryGetValue(tag,out var table)?table:throw Error("FONT_OUTLINE_UNSUPPORTED",path+":missing "+tag);
            var cmap=Table("cmap");var head=Table("head");var loca=Table("loca");var glyf=Table("glyf");var maxp=Table("maxp");
            if(head.Length<54||maxp.Length<6||cmap.Length<4)throw Error("FONT_DATA_INVALID",path+":truncated table header");
            int glyphs=U16(maxp.Offset+4);int locationFormat=I16(head.Offset+50);
            if(glyphs==0||locationFormat is not(0 or 1))throw Error("FONT_DATA_INVALID",path+":glyph count/loca format");
            if(loca.Length<checked((glyphs+1)*(locationFormat==0?2:4)))throw Error("FONT_DATA_INVALID",path+":truncated loca");
            var maps=new List<(int Offset,int Length,int Format)>();
            for(int i=0;i<U16(cmap.Offset+2);i++)
            {
                int record=checked(cmap.Offset+4+i*8);
                if(record>cmap.Offset+cmap.Length-8)throw Error("FONT_DATA_INVALID",path+":truncated cmap encoding record");
                int platform=U16(record),encoding=U16(record+2);
                if(platform!=0 && !(platform==3 && encoding is 1 or 10))continue;
                int offset=checked(cmap.Offset+Offset(U32(record+4)));int format=U16(offset);
                if(format is not(4 or 12))continue;
                int length=format==4?U16(offset+2):Offset(U32(offset+4));
                if(offset<cmap.Offset||length<16||offset>cmap.Offset+cmap.Length-length)throw Error("FONT_DATA_INVALID",path+":cmap subtable bounds");
                maps.Add((offset,length,format));
            }
            if(maps.Count==0)throw Error("FONT_GLYPH_COVERAGE_UNVERIFIED",path+":no supported Unicode cmap");
            // The full-repertoire cmap is authoritative when present; no per-character fallback.
            var map=maps.OrderByDescending(value=>value.Format).First();
            int Glyph(int codepoint)
            {
                int start=map.Offset,end=checked(start+map.Length);
                if(map.Format==12)
                {
                    uint count=U32(start+12);if(count>(uint)((map.Length-16)/12))throw Error("FONT_DATA_INVALID",path+":cmap12 group bounds");
                    uint? previous=null;uint found=0;
                    for(int i=0;i<count;i++)
                    {
                        int row=checked(start+16+i*12);uint first=U32(row),last=U32(row+4),baseGlyph=U32(row+8);
                        if(first>last||last>0x10ffff||previous.HasValue&&first<=previous.Value)throw Error("FONT_DATA_INVALID",path+":cmap12 group order");
                        previous=last;
                        if(baseGlyph+(ulong)(last-first)>=(ulong)glyphs)throw Error("FONT_DATA_INVALID",path+":cmap12 glyph range");
                        if(codepoint>=first&&codepoint<=last)found=baseGlyph+(uint)codepoint-first;
                    }
                    return checked((int)found);
                }
                if(codepoint>0xffff)return 0;
                int twice=U16(start+6);if(twice==0||(twice&1)!=0)throw Error("FONT_DATA_INVALID",path+":cmap4 segment count");
                int count4=twice/2,ends=start+14,starts=checked(ends+2*count4+2),deltas=checked(starts+2*count4),ranges=checked(deltas+2*count4);
                if(ranges>end-2*count4)throw Error("FONT_DATA_INVALID",path+":cmap4 arrays");
                int previousEnd=-1;
                for(int i=0;i<count4;i++)
                {
                    int last=U16(ends+2*i),first=U16(starts+2*i);
                    if(first>last||last<=previousEnd)throw Error("FONT_DATA_INVALID",path+":cmap4 segment order");previousEnd=last;
                    if(codepoint<first||codepoint>last)continue;
                    int delta=I16(deltas+2*i),relative=U16(ranges+2*i);
                    if(relative==0)return (codepoint+delta)&0xffff;
                    int position=checked(ranges+2*i+relative+2*(codepoint-first));
                    if(position>end-2)throw Error("FONT_DATA_INVALID",path+":cmap4 glyph bounds");
                    int glyph=U16(position);return glyph==0?0:(glyph+delta)&0xffff;
                }
                return 0;
            }
            bool Ink(int glyph)
            {
                int begin=locationFormat==0?U16(loca.Offset+glyph*2)*2:Offset(U32(loca.Offset+glyph*4));
                int end=locationFormat==0?U16(loca.Offset+(glyph+1)*2)*2:Offset(U32(loca.Offset+(glyph+1)*4));
                if(begin<0||end<begin||end>glyf.Length)throw Error("FONT_DATA_INVALID",path+":glyph outline bounds");
                if(begin==end)return false;
                if(end-begin<10)throw Error("FONT_DATA_INVALID",path+":truncated glyph header");
                return I16(glyf.Offset+begin)!=0;
            }
            bool hasInk=false;int consumed=0,glyphCount=0;
            while(consumed<text.Length)
            {
                var status=Rune.DecodeFromUtf16(text.AsSpan(consumed),out var rune,out int chars);
                if(status!=OperationStatus.Done)throw Error("TEXT_ENCODING_INVALID","UTF16 index "+consumed);
                consumed+=chars;int glyph=Glyph(rune.Value);
                if(glyph==0)throw Error("FONT_GLYPH_MISSING",path+":U+"+rune.Value.ToString("X"));
                if(glyph>=glyphs)throw Error("FONT_DATA_INVALID",path+":glyph ID outside maxp");
                hasInk|=Ink(glyph);glyphCount++;
            }
            return new(actualHash,glyphCount,hasInk);
        }
        catch(OverflowException e){throw Error("FONT_DATA_INVALID",path+":"+e.Message);}
    }
    private static NativeAssetContractException Error(string code,string field)=>new(code,field,"Exact Unicode cmap/TrueType outline evidence required; no fallback font or metric estimate.");
}
