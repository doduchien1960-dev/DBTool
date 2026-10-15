using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Preserves exact analytic section loops and their hatch winding for S2 consumers.</summary>
public static class SectionProfilePrimitiveBuilder
{
    public static ImmutableArray<DrawingPrimitive> Build(ViewProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if(profile.Purpose!=ViewPurpose.Section)
            throw new ArgumentException("A section profile is required.",nameof(profile));
        var result=ImmutableArray.CreateBuilder<DrawingPrimitive>();
        foreach(var region in profile.Regions)
        {
            AddPolygon(region.Id,"Material",region.SourceComponentId,
                region.Footprint.Outer);
            int hole=0;
            foreach(var loop in region.Footprint.InnerLoops)
                AddPolygon($"{region.Id}/Hole/{++hole}","MaterialHole",
                    region.SourceComponentId,loop);
        }
        foreach(var item in profile.Voids)
            AddPolygon(item.Id,item.Role.ToString(),item.OwnerId,item.Boundary);
        foreach(var item in profile.AnalyticRegions)
            result.Add(new SectionBoundaryPrimitive(item.Id,"Material",profile.Id,
                item.SourceComponentId,null,item.Boundary));
        foreach(var item in profile.AnalyticVoids)
            result.Add(new SectionBoundaryPrimitive(item.Id,item.Role.ToString(),profile.Id,
                item.OwnerId,item.SourceOpeningId,item.Boundary));
        foreach(var dimension in profile.Dimensions)
            result.Add(new DimensionIntentPrimitive(dimension.Id,"Dimension",profile.Id,
                null,null,null,dimension.Start,dimension.End,dimension.MeasuredMm,
                dimension.MeasuredMm.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture),0));
        return result.ToImmutable();

        void AddPolygon(string id,string role,string componentId,Polygon2Mm polygon)=>
            result.Add(new PolylinePrimitive(id,role,profile.Id,componentId,null,null,
                polygon.Vertices,true));
    }
}
