using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Published composite sections: chamber on its axis, roof and neck through the access opening.</summary>
public static class SheetOneSectionViews
{
    public static OperationResult<ViewProfile> ThroughNeck(GeometryModel geometry,ViewProfile chamber)
    {
        var neck=geometry.Components.FirstOrDefault(c=>c.Id=="Neck");
        if(neck is null || neck.Footprint.InnerLoops.Length!=1 || chamber.Id is not ("AA" or "BB"))
            return new(true,chamber,[]);
        var hole=neck.Footprint.InnerLoops[0].Vertices;
        bool aa=chamber.Id=="AA";
        double coordinate=aa?(hole.Min(p=>p.X)+hole.Max(p=>p.X))/2:(hole.Min(p=>p.Y)+hole.Max(p=>p.Y))/2;
        if(Math.Abs(coordinate)<1e-8) return new(true,chamber,[]);
        var access=SectionProjector.BuildSection(geometry,new SectionDefinition(chamber.Id,
            aa?SectionAxis.AlongY:SectionAxis.AlongX,coordinate,SectionMode.TrueSection));
        if(!access.Success)return access;
        bool AccessOwner(string owner)=>owner is "Neck" or "Collar" or "Roof" || owner.StartsWith("Roof/Haunch", StringComparison.Ordinal);
        var result=new ViewProfile(chamber.Id,chamber.Purpose,
            chamber.Regions.Where(r=>!AccessOwner(r.SourceComponentId)).Concat(access.Value!.Regions.Where(r=>AccessOwner(r.SourceComponentId))),
            chamber.Voids.Where(v=>!AccessOwner(v.OwnerId)).Concat(access.Value.Voids.Where(v=>AccessOwner(v.OwnerId))),
            chamber.Dimensions,chamber.AnalyticRegions,chamber.AnalyticVoids);
        return new(true,result,access.Diagnostics);
    }
}
