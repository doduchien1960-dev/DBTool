using System.Globalization;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Presentation labels and dimension intents; all measured values remain logical millimetres.</summary>
public static class AnnotationPlanner
{
    public static IReadOnlyList<DrawingPrimitive>? NativeSectionElevations(GeometryModel geometry, PresentationRequest presentation,double? assembledTopMm=null)
    {
        var neck = geometry.Components.FirstOrDefault(c=>c.Id=="Neck");
        if (neck is null || geometry.Components.Count(c=>c.Kind==ComponentKind.Wall && c.Id!="Wall/Mortar")!=4) return null;
        var floor = geometry.Components.Single(c=>c.Id=="Base").ZRange.End;
        // Sourced finished cover top surface under rule OWNER-COVER-20261012-v2:
        double seat;
        var cover = geometry.PurchasedCovers.FirstOrDefault();
        if (cover?.SectionOutlines is not null)
            seat = cover.SectionOutlines.AA.MaxY;
        else if (cover is not null)
            seat = cover.Anchor.Z;
        else if (assembledTopMm.HasValue)
            seat = assembledTopMm.Value;
        else
            seat = (geometry.Components.FirstOrDefault(c=>c.Id=="Collar")?.ZRange.Start ?? neck.ZRange.End) + 100.0;
        var vertices = neck.Footprint.Outer.Vertices;
        double cx=(vertices.Min(p=>p.X)+vertices.Max(p=>p.X))/2, cy=(vertices.Min(p=>p.Y)+vertices.Max(p=>p.Y))/2;
        string Label(double z,string symbolic) => presentation.ElevationMode==ElevationMode.Absolute
            ? (presentation.BaseElevationM!.Value.Value+z/1000).ToString("0.00",CultureInfo.InvariantCulture) : symbolic;
        var result = new List<DrawingPrimitive>();
        bool sheetOne=SheetOnePresentationPolicy.Enabled(presentation);
        string floorRole=sheetOne?"Elevation/SheetOneFloor":"Elevation/Native";
        string topRole=sheetOne?"Elevation/SheetOneCap":"Elevation/Native";
        double ctScale = presentation.ScaleDenominator / 100.0;
        foreach (string view in new[] {"AA","BB","SteelAA","SteelBB"})
        {
            double center = view is "AA" or "SteelAA" ? cy : cx;
            result.Add(new BlockPrimitive($"{view}/Elevation/Floor",floorRole,view,"Base","Block:CT",
                new(0,floor),ctScale,[new("CT",Label(floor,"Z1"))]));
            result.Add(new BlockPrimitive($"{view}/Elevation/NeckSeat",topRole,view,"Neck","Block:CT",
                new(center,seat),ctScale,[new("CT",Label(seat,"Z2"))]));
        }
        return result;
    }

    public static IEnumerable<DrawingPrimitive> Dimensions(ViewProfile profile, PresentationRequest presentation)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(presentation);
        return profile.Dimensions.Select(d => (DrawingPrimitive)new DimensionIntentPrimitive(
            $"{profile.Id}/{d.Id}","Dimension/Linear",profile.Id,null,null,null,d.Start,d.End,d.MeasuredMm,
            FormatMm(d.MeasuredMm),3*presentation.ScaleDenominator)).ToArray();
    }

    public static IEnumerable<DrawingPrimitive> Elevations(GeometryModel geometry, PresentationRequest presentation)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(presentation);
        var baseComponent=geometry.Components.Single(c=>c.Id=="Base");
        var roof=geometry.Components.Single(c=>c.Id=="Roof");
        var pad=geometry.Components.SingleOrDefault(c=>c.Id=="Pad");
        double floor=baseComponent.ZRange.End, roofTop=roof.ZRange.End;
        var reference=geometry.Components.SelectMany(c=>c.Footprint.Outer.Vertices).Max(p=>p.Y)-100;
        var absolute=presentation.ElevationMode==ElevationMode.Absolute;
        string role=absolute?"Elevation/Absolute":"Elevation/Symbolic";
        double textHeight=2.5*presentation.ScaleDenominator;
        string Label(double z,string symbolic) => absolute
            ? FormatMetres(presentation.BaseElevationM!.Value.Value+z/1000d)+" m"
            : symbolic;
        var labels = new List<DrawingPrimitive>
        {
            new TextPrimitive("Elevation/Floor",role,"AA","Base",null,null,new(reference,floor),Label(floor,"Z1"),textHeight,0),
            new TextPrimitive("Elevation/RoofTop",role,"AA","Roof",null,null,new(reference,roofTop),Label(roofTop,"Z2"),textHeight,0)
        };
        // L06/G10: the annotation belongs to the actual bedding component.
        // An explicitly absent Pad has no PadBottom datum or label.
        if (pad is not null)
        {
            double padBottom=pad.ZRange.Start;
            labels.Add(new TextPrimitive("Elevation/PadBottom",role,"AA","Pad",null,null,new(reference,padBottom),
                Label(padBottom,$"Z1 - {FormatMetres((floor-padBottom)/1000d)} m"),textHeight,0));
        }
        return labels;
    }

    public static TextPrimitive ScheduleTotal(SteelSchedule schedule, PresentationRequest presentation) =>
        new("SteelSchedule/TotalMassDisplay","Schedule/TotalMass","SteelSchedule",null,null,null,
            new Point2Mm(0,230),$"Total {FormatMetres(schedule.TotalMassKg.Value)} kg",
            2.5*presentation.ScaleDenominator,0);

    private static string FormatMm(double value) => Math.Round(value,3,MidpointRounding.AwayFromZero)
        .ToString("0.###",CultureInfo.InvariantCulture);

    private static string FormatMetres(double value) => Math.Round(value,3,MidpointRounding.AwayFromZero)
        .ToString("F3",CultureInfo.InvariantCulture);
}
