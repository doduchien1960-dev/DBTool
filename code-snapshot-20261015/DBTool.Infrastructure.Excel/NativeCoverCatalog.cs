using System.Collections.Immutable;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Infrastructure.Excel;

/// <summary>Source-bound measurements of current native assets; never approves unresolved assembly fields.</summary>
internal static class NativeCoverCatalog
{
    internal static ImmutableArray<CoverCatalogEntry> Read()
    {
        using var stream=typeof(NativeCoverCatalog).Assembly.GetManifestResourceStream("DBTool.NativeCoverCatalog.json")
            ?? throw new InvalidOperationException("Native cover catalog resource is missing.");
        using var document=JsonDocument.Parse(stream);
        var root=document.RootElement;
        var dwg=new SourceRef(SourceKind.Dwg,root.GetProperty("sourceDwg").GetString()!,root.GetProperty("sourceDwgSha256").GetString()!,
            null,null,null,"Native mm at1:1; current DWG exported read-only with AutoCAD2026; MB/AA/BB measurements.");
        var dxf=new SourceRef(SourceKind.Dxf,root.GetProperty("sourceDxf").GetString()!,root.GetProperty("sourceDxfSha256").GetString()!,
            null,null,null,"Audited DXF exported from the current native DWG; extents are not aperture/support contours.");
        var entries=ImmutableArray.CreateBuilder<CoverCatalogEntry>();
        foreach(var item in root.GetProperty("covers").EnumerateArray())
        {
            string code=item.GetProperty("code").GetString()!;
            var views=item.GetProperty("views").EnumerateArray().Select(v=>ReadView(v,dxf)).ToImmutableArray();
            var sources=new List<SourceRef>{dwg,dxf};
            Profile2Mm? aperture=null;
            if(item.TryGetProperty("confirmedAperture",out var confirmed))
            {
                if(confirmed.GetProperty("shape").GetString()!="Circle")
                    throw new InvalidOperationException("Unknown native aperture evidence shape.");
                aperture=new CircleProfile(new(confirmed.GetProperty("center")[0].GetDouble(),confirmed.GetProperty("center")[1].GetDouble()),confirmed.GetProperty("diameterMm").GetDouble()/2);
                sources.Add(new(SourceKind.RuleDocument,root.GetProperty("ownerConfirmations").GetString()!,
                    root.GetProperty("ownerConfirmationsSha256").GetString()!,null,null,null,confirmed.GetProperty("evidence").GetString()!));
            }
            else if(item.TryGetProperty("measuredAperture",out var measured))
            {
                aperture=measured.GetProperty("shape").GetString() switch
                {
                    "Rectangle"=>new RectangleProfile(Rect(measured.GetProperty("bounds"))),
                    "RoundedRectangle"=>new RoundedRectangleProfile(
                        new(measured.GetProperty("center")[0].GetDouble(),measured.GetProperty("center")[1].GetDouble()),
                        new(measured.GetProperty("size")[0].GetDouble(),measured.GetProperty("size")[1].GetDouble()),
                        measured.GetProperty("cornerRadiusMm").GetDouble()),
                    "Circle"=>new CircleProfile(new(measured.GetProperty("center")[0].GetDouble(),
                        measured.GetProperty("center")[1].GetDouble()),measured.GetProperty("diameterMm").GetDouble()/2),
                    _=>throw new InvalidOperationException("Unknown measured native aperture shape.")
                };
                sources.Add(new(SourceKind.Dxf,dxf.RelativePath,dxf.Sha256,null,null,null,
                    measured.GetProperty("evidence").GetString()!));
            }
            var unresolved=item.GetProperty("unresolvedFields").EnumerateArray().Select(v=>v.GetString()!).ToImmutableArray();
            var contacts=item.TryGetProperty("measuredContactSections",out var contactData)
                ? contactData.EnumerateArray().Select(c=>new CoverContactSection(c.GetProperty("view").GetString()!,
                    c.GetProperty("segments").EnumerateArray().Select(s=>new CoverContactSegment(
                        new(s.GetProperty("start")[0].GetDouble(),s.GetProperty("start")[1].GetDouble()),
                        new(s.GetProperty("end")[0].GetDouble(),s.GetProperty("end")[1].GetDouble()))),
                    [new(SourceKind.Dxf,dxf.RelativePath,dxf.Sha256,null,null,null,c.GetProperty("evidence").GetString()!)]))
                : [];
            CoverSectionOutlineRule? outlineRule=null;
            if(root.TryGetProperty("sectionOutlineRule",out var outline) &&
                outline.GetProperty("catalogIds").EnumerateArray().Any(id=>id.GetString()==code))
            {
                if(outline.GetProperty("reference").GetString()!="TopEdgeMidpoint")
                    throw new InvalidOperationException("Unknown cover section outline reference.");
                var evidence=new SourceRef(SourceKind.RuleDocument,root.GetProperty("ownerConfirmations").GetString()!,
                    root.GetProperty("ownerConfirmationsSha256").GetString()!,null,null,null,outline.GetProperty("source").GetString()!);
                outlineRule=new(outline.GetProperty("sideExtensionMm").GetDouble(),outline.GetProperty("depthMm").GetDouble(),[evidence]);
                sources.Add(evidence);
            }
            CoverNominalOpening? nominalOpening=null;
            if(root.TryGetProperty("nameSuffixDimensionPolicy",out var nominalPolicy))
            {
                var match=Regex.Match(code,@"(?<width>[0-9]+(?:\.[0-9]+)?)x(?<height>[0-9]+(?:\.[0-9]+)?)$",RegexOptions.CultureInvariant);
                if(!match.Success) throw new InvalidOperationException($"Owner suffix policy cannot resolve cover dimensions: {code}");
                double width=double.Parse(match.Groups["width"].Value,CultureInfo.InvariantCulture);
                double height=double.Parse(match.Groups["height"].Value,CultureInfo.InvariantCulture);
                if(nominalPolicy.TryGetProperty("overrides",out var overrides) && overrides.TryGetProperty(code,out var size))
                {width=size[0].GetDouble();height=size[1].GetDouble();}
                var evidence=new SourceRef(SourceKind.RuleDocument,root.GetProperty("ownerConfirmations").GetString()!,
                    root.GetProperty("ownerConfirmationsSha256").GetString()!,null,null,null,nominalPolicy.GetProperty("source").GetString()!);
                nominalOpening=new(new(width,height),[evidence]);
                sources.Add(evidence);
                if(nominalPolicy.TryGetProperty("registration",out var registration))
                {
                    var anchorSource=new SourceRef(SourceKind.RuleDocument,evidence.RelativePath,evidence.Sha256,
                        null,null,null,registration.GetString()!);
                    views=views.Select(v=>v.AlignmentPoint.HasValue?v:new CoverBlockView(v.Name,v.View,v.BasePoint,
                        v.Bounds,new(0,0),[anchorSource])).ToImmutableArray();
                    sources.Add(anchorSource);
                }
            }
            var set=new CoverBlockSet(views,aperture,unresolved,sources,contacts,outlineRule,nominalOpening);
            var bounds=views.Single(v=>v.View=="MB").Bounds;
            var planReference=views.Single(v=>v.View=="MB").AlignmentPoint ?? new Point2Mm(0,0);
            entries.Add(new(code,new(bounds.MaxX-bounds.MinX,bounds.MaxY-bounds.MinY),new(-planReference.X,-planReference.Y,0),0,
                CoverPlacementDatum.NeckTop,bounds,sources,blockSet:set));
        }
        if(entries.Select(e=>e.Code).Distinct(StringComparer.Ordinal).Count()!=entries.Count)
            throw new InvalidOperationException("Native catalog has duplicate product IDs.");
        return entries.ToImmutable();
    }
    private static Rectangle2Mm Rect(JsonElement value)=>new(value[0].GetDouble(),value[1].GetDouble(),value[2].GetDouble(),value[3].GetDouble());
    private static CoverBlockView ReadView(JsonElement value,SourceRef source)
    {
        Point2Mm? point=null;
        SourceRef[] evidence=[];
        if(value.TryGetProperty("alignmentPoint",out var alignment))
        {
            point=new(alignment[0].GetDouble(),alignment[1].GetDouble());
            evidence=[new(SourceKind.Dxf,source.RelativePath,source.Sha256,null,null,null,
                value.GetProperty("alignmentEvidence").GetString()!)];
        }
        var basePoint=value.GetProperty("basePoint");
        return new(value.GetProperty("name").GetString()!,value.GetProperty("view").GetString()!,
            new(basePoint[0].GetDouble(),basePoint[1].GetDouble(),basePoint[2].GetDouble()),
            Rect(value.GetProperty("bounds")),point,evidence);
    }
}
