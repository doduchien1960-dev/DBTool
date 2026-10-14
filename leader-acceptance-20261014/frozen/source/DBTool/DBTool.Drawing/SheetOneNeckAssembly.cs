using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>Bounded native-sheet assembly convention; never rewrites physical geometry or quantities.</summary>
public sealed class SheetOneNeckAssembly
{
    private SheetOneNeckAssembly(double bodyTopMm,double plateTopMm)
    { BodyTopMm=bodyTopMm;PlateTopMm=plateTopMm; }
    public double BodyTopMm {get;}
    public double PlateTopMm {get;}
    // Sourced frame depth 100 mm under rule OWNER-COVER-20261012-v2; uniform across neck thicknesses.
    public const double NapGaVSupportPlateHeightMm=100;
    public static SheetOneNeckAssembly? Resolve(GeometryModel geometry,PresentationRequest presentation)
    {
        if(!SheetOnePresentationPolicy.Enabled(presentation) || geometry.PurchasedCovers.Length!=1) return null;
        var cover=geometry.PurchasedCovers[0];
        if(cover.SectionOutlines is not null) return null; // Sourced domain outline owns this assembly's presentation.
        if(cover.CatalogId!="NapGaV.700" && !cover.CatalogId.StartsWith("NapGaCN.",StringComparison.OrdinalIgnoreCase) && !cover.CatalogId.StartsWith("VIN.",StringComparison.OrdinalIgnoreCase) && !cover.CatalogId.StartsWith("Nap",StringComparison.OrdinalIgnoreCase)) return null;
        var neck=geometry.Components.FirstOrDefault(c=>c.Id=="Neck");
        if(neck is null || neck.Footprint.InnerLoops.Length!=1 ||
            !Rectangle(neck.Footprint.Outer) || !Rectangle(neck.Footprint.InnerLoops[0])) return null;
        var outer=neck.Footprint.Outer.Vertices;var inner=neck.Footprint.InnerLoops[0].Vertices;
        double thick = inner.Min(p=>p.X)-outer.Min(p=>p.X);
        if(!Near(thick,125) && !Near(thick,150)) return null;
        if(!Near(outer.Max(p=>p.X)-inner.Max(p=>p.X),thick) ||
            !Near(inner.Min(p=>p.Y)-outer.Min(p=>p.Y),thick) ||
            !Near(outer.Max(p=>p.Y)-inner.Max(p=>p.Y),thick)) return null;

        var collar=geometry.Components.FirstOrDefault(c=>c.Id=="Collar");
        double bodyTop;
        if(collar is not null)
        {
            if(!Near(collar.ZRange.Length,50) || !Near(neck.ZRange.End,collar.ZRange.End) || collar.ZRange.Start<=neck.ZRange.Start)
                return null;
            bodyTop = collar.ZRange.Start;
        }
        else if(!geometry.Datums.IsDefaultOrEmpty && geometry.Datums.FirstOrDefault(d=>d.surface_id=="NeckTop") is { } neckDatum)
        {
            bodyTop = neckDatum.z_mm;
        }
        else
        {
            bodyTop = neck.ZRange.End;
        }
        double plateHeight = NapGaVSupportPlateHeightMm;
        double plateTop = (!geometry.Datums.IsDefaultOrEmpty && geometry.Datums.FirstOrDefault(d=>d.surface_id=="FrameTop") is { } frameDatum)
            ? frameDatum.z_mm
            : bodyTop + plateHeight;
        return new(bodyTop, plateTop);
    }
    public ViewProfile Section(ViewProfile source)
    {
        if(source.Id is not ("AA" or "BB") || source.Purpose!=ViewPurpose.Section) return source;
        var lower=source.Regions.Where(r=>r.SourceComponentId=="Neck" &&
            r.Footprint.Outer.Vertices.Max(p=>p.Y)<=BodyTopMm+1e-8).ToArray();
        if(lower.Length!=2 || lower.Any(r=>!Rectangle(r.Footprint.Outer) || r.Footprint.InnerLoops.Length!=0 ||
            !Near(r.Footprint.Outer.Vertices.Max(p=>p.Y),BodyTopMm))) return source;
        var plates = new List<MaterialRegion>();
        for (int i = 0; i < lower.Length; i++)
        {
            double wMin = lower[i].Footprint.Outer.Vertices.Min(p => p.X);
            double wMax = lower[i].Footprint.Outer.Vertices.Max(p => p.X);
            plates.Add(new MaterialRegion($"Neck/PresentationSupportPlate/{i + 1}", "Neck", "Presentation/NapGaVSupportPlate",
                new Footprint(new Polygon2Mm([new(wMin, BodyTopMm), new(wMax, BodyTopMm), new(wMax, PlateTopMm), new(wMin, PlateTopMm)]), []),
                lower[i].Sources));
        }
        var regions=new List<MaterialRegion>();bool inserted=false;
        foreach(var region in source.Regions)
        {
            if(region.SourceComponentId=="Collar" || region.SourceComponentId=="Neck" && !lower.Contains(region))
            {
                if(!inserted) { regions.AddRange(plates);inserted=true; }
            }
            else regions.Add(region);
        }
        if(!inserted) return source;
        return new(source.Id,source.Purpose,regions,source.Voids,source.Dimensions,source.AnalyticRegions,source.AnalyticVoids);
    }
    private static bool Near(double a,double b)=>Math.Abs(a-b)<=1e-8;
    private static bool Rectangle(Polygon2Mm polygon)=>polygon.Vertices.Length==4 &&
        polygon.Vertices.Select(p=>p.X).Distinct().Count()==2 && polygon.Vertices.Select(p=>p.Y).Distinct().Count()==2;
}
