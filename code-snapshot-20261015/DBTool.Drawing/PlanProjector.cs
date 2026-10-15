using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Logical plan projection from one geometry snapshot, in millimetres before scale.</summary>
public static class PlanProjector
{
    public static OperationResult<ViewProfile> BuildPlanAtElevation(GeometryModel model,
        double zMm,CatchBasinGeometry? catchBasin = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        OperationResult<ViewProfile> Fail(string code,string actual,SourceRef? source=null) =>
            new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,
                null,null,source,"axis-aligned rectangular horizontal slice",actual,
                "Net plan slice cannot be built.")]);
        if(!double.IsFinite(zMm)) return Fail("PLAN_ELEVATION_INVALID",zMm.ToString());
        var cuts=new List<(string Id,string OwnerId,Rectangle2Mm XY,
            IEnumerable<SourceRef> Sources)>();
        foreach(var opening in model.Openings)
        {
            if(zMm<=opening.ZRange.Start || zMm>=opening.ZRange.End) continue;
            Rectangle2Mm? xy=null;
            if(opening.WallFrame is null && opening.ConcreteCutout is RectangleProfile roof)
                xy=roof.Rectangle;
            else if(opening.WallFrame is { } wall)
            {
                double u0,u1;
                if(opening.ConcreteCutout is CircleProfile circle)
                {
                    double delta=zMm-circle.Center.Y;
                    if(Math.Abs(delta)>=circle.RadiusMm) continue;
                    double half=Math.Sqrt((circle.RadiusMm-Math.Abs(delta))*
                        (circle.RadiusMm+Math.Abs(delta)));
                    u0=circle.Center.X-half;u1=circle.Center.X+half;
                }
                else if(opening.ConcreteCutout is RectangleProfile box)
                {
                    if(zMm<=box.Rectangle.MinY || zMm>=box.Rectangle.MaxY) continue;
                    u0=box.Rectangle.MinX;u1=box.Rectangle.MaxX;
                }
                else if(opening.ConcreteCutout is OpenTopProfile openTop &&
                    TryRectangle(openTop.Polygon,out var openRect))
                {
                    if(zMm<=openRect.MinY || zMm>=openRect.MaxY) continue;
                    u0=openRect.MinX;u1=openRect.MaxX;
                }
                else return Fail("PLAN_OPENING_UNSUPPORTED",opening.Id,opening.Sources.FirstOrDefault());
                var points=new[]{wall.ToGlobal(u0,opening.ExtrusionRange.Start,zMm),
                    wall.ToGlobal(u0,opening.ExtrusionRange.End,zMm),
                    wall.ToGlobal(u1,opening.ExtrusionRange.Start,zMm),
                    wall.ToGlobal(u1,opening.ExtrusionRange.End,zMm)};
                xy=new Rectangle2Mm(points.Min(p=>p.X),points.Min(p=>p.Y),
                    points.Max(p=>p.X),points.Max(p=>p.Y));
            }
            else return Fail("PLAN_OPENING_UNSUPPORTED",opening.Id,opening.Sources.FirstOrDefault());
            cuts.Add((opening.Id,opening.OwnerId,xy!,opening.Sources));
        }
        if(catchBasin is not null)
        {
            foreach(var child in catchBasin.OpeningOwnedChildren)
            foreach(var cell in child.Cells)
                if(zMm>cell.Z.Start && zMm<cell.Z.End)
                    cuts.Add((child.ParentId,child.OwnerId,cell.XY,child.Sources));
        }
        var regions=new List<MaterialRegion>();
        var voids=new List<ProjectedVoid>();
        foreach(var (id,owner,xy,sources) in cuts.OrderBy(c=>c.Id,StringComparer.Ordinal))
            voids.Add(new ProjectedVoid($"{id}/PlanCut/{voids.Count}",id,owner,
                ProjectedVoidRole.ConcreteCutout,Rect(xy.MinX,xy.MinY,xy.MaxX,xy.MaxY),sources));
        foreach(var component in model.Components)
        {
            if(zMm<=component.ZRange.Start || zMm>=component.ZRange.End) continue;
            if(component.Occupied is not ExtrusionSolid solid ||
                solid.Frame.Origin!=new Point3Mm(0,0,0) ||
                solid.Frame.U!=new Vector3(1,0,0) || solid.Frame.V!=new Vector3(0,1,0) ||
                solid.Frame.N!=new Vector3(0,0,1) ||
                !TryRectangle(component.Footprint.Outer,out var outer) ||
                component.Footprint.InnerLoops.Any(h=>!TryRectangle(h,out _)))
                return Fail("PLAN_COMPONENT_UNSUPPORTED",component.Id,component.Sources.FirstOrDefault());
            var holes=component.Footprint.InnerLoops.Select(h=>
            { TryRectangle(h,out var r);return r; }).ToArray();
            var relevant=cuts.Where(c=>c.OwnerId==component.Id).Select(c=>c.XY).ToArray();
            double[] xs=new[]{outer.MinX,outer.MaxX}.Concat(holes.SelectMany(h=>new[]{h.MinX,h.MaxX}))
                .Concat(relevant.SelectMany(r=>new[]{r.MinX,r.MaxX})).Distinct().Order().ToArray();
            double[] ys=new[]{outer.MinY,outer.MaxY}.Concat(holes.SelectMany(h=>new[]{h.MinY,h.MaxY}))
                .Concat(relevant.SelectMany(r=>new[]{r.MinY,r.MaxY})).Distinct().Order().ToArray();
            for(int i=0;i<xs.Length-1;i++)
            for(int j=0;j<ys.Length-1;j++)
            {
                double x0=xs[i],x1=xs[i+1],y0=ys[j],y1=ys[j+1];
                if(x1<=x0 || y1<=y0) continue;
                double x=(x0+x1)/2,y=(y0+y1)/2;
                if(!Inside(outer,x,y) || holes.Any(h=>Inside(h,x,y)) ||
                    relevant.Any(r=>Inside(r,x,y))) continue;
                regions.Add(new MaterialRegion($"{component.Id}/PlanSlice/{i}/{j}",
                    component.Id,component.MaterialId,
                    new Footprint(Rect(x0,y0,x1,y1),[]),component.Sources));
            }
        }
        if(catchBasin is not null)
        {
            var parts=new List<Rectangle2Mm>();
            foreach(var descriptor in catchBasin.OccupiedUnion.Solids)
            {
                if(descriptor is not ExtrusionSolid solid ||
                    !TryRectangle(solid.Footprint.Outer,out var rect) ||
                    !solid.Footprint.InnerLoops.IsEmpty)
                    return Fail("PLAN_CATCH_UNSUPPORTED",descriptor.GetType().Name);
                if(zMm>solid.Depth.Start && zMm<solid.Depth.End) parts.Add(rect);
            }
            if(parts.Count>0)
            {
                double[] xs=parts.SelectMany(p=>new[]{p.MinX,p.MaxX}).Distinct().Order().ToArray();
                double[] ys=parts.SelectMany(p=>new[]{p.MinY,p.MaxY}).Distinct().Order().ToArray();
                var sources=catchBasin.ExteriorFaces.SelectMany(f=>f.Sources).Distinct();
                for(int i=0;i<xs.Length-1;i++)
                for(int j=0;j<ys.Length-1;j++)
                {
                    double x=(xs[i]+xs[i+1])/2,y=(ys[j]+ys[j+1])/2;
                    if(!parts.Any(p=>Inside(p,x,y))) continue;
                    regions.Add(new MaterialRegion($"Catch/PlanSlice/{i}/{j}","Catch","Concrete",
                        new Footprint(Rect(xs[i],ys[j],xs[i+1],ys[j+1]),[]),sources));
                }
            }
        }
        return new(true,new ViewProfile($"PlanSlice/{zMm:R}",ViewPurpose.Plan,regions,voids,[]),[]);
    }

    private static bool Inside(Rectangle2Mm r,double x,double y) =>
        x>r.MinX && x<r.MaxX && y>r.MinY && y<r.MaxY;
    private static bool TryRectangle(Polygon2Mm polygon,out Rectangle2Mm rectangle)
    {
        var points=polygon.Vertices;
        double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X);
        double y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
        if(points.Length!=4 || points.Any(p=>(p.X!=x0 && p.X!=x1)||
            (p.Y!=y0 && p.Y!=y1)))
        { rectangle=null!;return false; }
        rectangle=new Rectangle2Mm(x0,y0,x1,y1);return true;
    }

    public static OperationResult<ViewProfile> BuildPlan(GeometryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var regions=model.Components.Select(c=>new MaterialRegion(c.Id,c.Id,c.MaterialId,c.Footprint,c.Sources)).ToArray();
        var dimensions=new List<ViewDimensionIntent>();
        foreach(var c in model.Components)
        {
            var vertices=c.Footprint.Outer.Vertices;
            double x0=vertices.Min(v=>v.X),x1=vertices.Max(v=>v.X);
            double y0=vertices.Min(v=>v.Y),y1=vertices.Max(v=>v.Y);
            dimensions.Add(new ViewDimensionIntent($"{c.Id}/WidthX",new(x0,y0),new(x1,y0),x1-x0));
            dimensions.Add(new ViewDimensionIntent($"{c.Id}/WidthY",new(x0,y0),new(x0,y1),y1-y0));
        }
        var voids=new List<ProjectedVoid>();
        foreach(var opening in model.Openings)
        {
            if (opening.OwnerId == "Roof" && opening.ConcreteCutout is RectangleProfile rectangle)
            {
                var rect=rectangle.Rectangle;
                var boundary=Rect(rect.MinX,rect.MinY,rect.MaxX,rect.MaxY);
                voids.Add(new ProjectedVoid(opening.Id,opening.Id,opening.OwnerId,ProjectedVoidRole.RoofOpening,boundary,opening.Sources));
                dimensions.Add(new ViewDimensionIntent($"{opening.Id}/Width",new(rect.MinX,rect.MinY),new(rect.MaxX,rect.MinY),rect.MaxX-rect.MinX));
                dimensions.Add(new ViewDimensionIntent($"{opening.Id}/Height",new(rect.MinX,rect.MinY),new(rect.MinX,rect.MaxY),rect.MaxY-rect.MinY));
            }
            else if (opening.WallFrame is { } wall &&
                (opening.ConcreteCutout is CircleProfile && opening.Kind==OpeningKind.Round ||
                 opening.ConcreteCutout is RectangleProfile && opening.Kind==OpeningKind.Box))
            {
                double u0, u1, z;
                if (opening.ConcreteCutout is CircleProfile circle)
                { u0=circle.Center.X-circle.RadiusMm; u1=circle.Center.X+circle.RadiusMm; z=circle.Center.Y; }
                else
                { var boxRect=((RectangleProfile)opening.ConcreteCutout).Rectangle;
                  u0=boxRect.MinX; u1=boxRect.MaxX; z=(boxRect.MinY+boxRect.MaxY)/2; }
                // Orthographic silhouette across the wall depth; material is retained in plan because concrete remains above the tunnel.
                var xy=new[]
                {
                    wall.ToGlobal(u0,opening.ExtrusionRange.Start,z),
                    wall.ToGlobal(u1,opening.ExtrusionRange.Start,z),
                    wall.ToGlobal(u0,opening.ExtrusionRange.End,z),
                    wall.ToGlobal(u1,opening.ExtrusionRange.End,z)
                };
                var boundary=Rect(xy.Min(p=>p.X),xy.Min(p=>p.Y),xy.Max(p=>p.X),xy.Max(p=>p.Y));
                voids.Add(new ProjectedVoid($"{opening.Id}/PlanSilhouette",opening.Id,opening.OwnerId,
                    ProjectedVoidRole.PipePlanSilhouette,boundary,opening.Sources));
            }
            else return Fail("PLAN_OPENING_UNSUPPORTED",opening,$"{opening.Kind}");
        }
        return new(true,new ViewProfile("Plan",ViewPurpose.Plan,regions,voids,dimensions),[]);
    }

    private static Polygon2Mm Rect(double x0,double y0,double x1,double y1) => new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);

    private static OperationResult<ViewProfile> Fail(string code,OpeningProfile opening,string actual) =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,
            opening.Sources.FirstOrDefault(),"rectangular roof or round wall opening",actual,"Plan opening projection is unsupported.")]);
}
