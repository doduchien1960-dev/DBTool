using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Orthographic line silhouettes and end-on diameter dots from physical occurrences.</summary>
public static class StructuralRebarProjector
{
    public static OperationResult<IReadOnlyList<DrawingPrimitive>> Build(RebarLayout layout,
        IReadOnlyDictionary<string, string> marks, ViewProfile? sectionAA = null, ViewProfile? sectionBB = null,
        bool continuousSheetOneWalls = false)
    {
        var primitives = new List<DrawingPrimitive>();
        double SectionPlane(string view,string component)
        {
            if(component!="Neck") return 0;
            var coordinates=layout.Occurrences.Where(b=>b.ComponentId==component)
                .SelectMany(b=>b.Path.Segments.OfType<LineSegment3Mm>().SelectMany(s=>new[]{s.Start,s.End}))
                .Select(p=>view=="SteelAA"?p.X:p.Y).ToArray();
            return coordinates.Length==0?0:(coordinates.Min()+coordinates.Max())/2;
        }
        // Parallel families use the nearest representative to the section plane; transverse bars are cut at the plane.
        var representatives = new Dictionary<(string View,string Component,string? Frame,RebarSurface? Surface,Layer Layer,RebarDirection Direction,Axis Axis,string? Family),double>();
        foreach(string view in new[]{"SteelAA","SteelBB"})
        foreach(var group in layout.Occurrences.GroupBy(b=>(b.ComponentId,b.FrameId,b.Surface,b.Layer,b.Direction,b.Axis,b.FamilyId)))
        {
            if (group.Key.ComponentId == "Neck")
            {
                if (view == "SteelAA" && group.Key.FrameId is not ("Neck/T1" or "Neck/T3")) continue;
                if (view == "SteelBB" && group.Key.FrameId is not ("Neck/T2" or "Neck/T4")) continue;
            }
            double Normal(Point3Mm p)=>view=="SteelAA"?p.X:p.Y;
            var candidates=group.Select(b=>(Bar:b,Leg:b.Path.Segments.OfType<LineSegment3Mm>().MaxBy(s=>
                Math.Abs(s.End.X-s.Start.X)+Math.Abs(s.End.Y-s.Start.Y)+Math.Abs(s.End.Z-s.Start.Z))))
                .Where(x=>x.Leg is not null && Math.Abs(Normal(x.Leg.Start)-Normal(x.Leg.End))<1e-9)
                .OrderBy(x=>Math.Abs(Normal(x.Leg!.Start)-SectionPlane(view,group.Key.ComponentId))).ThenBy(x=>x.Bar.Id,StringComparer.Ordinal).ToArray();
            if(candidates.Length>0)
                representatives[(view,group.Key.ComponentId,group.Key.FrameId,group.Key.Surface,group.Key.Layer,group.Key.Direction,group.Key.Axis,group.Key.FamilyId)]=Normal(candidates[0].Leg!.Start);
        }
        foreach (var bar in layout.Occurrences)
        {
            // Sheet 1 uses the explicit visible rung silhouette in its supporting section.
            if(continuousSheetOneWalls && bar.FamilyId=="Ladder") continue;
            foreach (var view in new[] { "WallMeshPlan", "SteelAA", "SteelBB" })
            {
                if (view == "WallMeshPlan" && bar.ComponentKind != ComponentKind.Wall) continue;
                if (bar.ComponentKind == ComponentKind.Neck)
                {
                    if (view == "SteelAA" && bar.FrameId is not ("Neck/T1" or "Neck/T3")) continue;
                    if (view == "SteelBB" && bar.FrameId is not ("Neck/T2" or "Neck/T4")) continue;
                }
                // The first-sheet wall-mesh plan depicts face meshes, not roof-joint
                // dowels. Dowels retain both section silhouettes and all physical/schedule data.
                if(continuousSheetOneWalls && view=="WallMeshPlan" && bar.FamilyId=="Dowel") continue;
                if(bar.FamilyId=="Tie" && bar.RuleId=="VHG-C-WRAP-R8-20261012")
                {
                    if(view=="WallMeshPlan")
                    {
                        var plan=PlanBarProjector.Build(bar,view,"Rebar/Main",marks[bar.Id]);
                        if(!plan.Success) return plan;
                        foreach(var primitive in plan.Value!)
                            if(primitive is PolylinePrimitive line)
                                primitives.Add(new PolylinePrimitive(line.SemanticId,line.Role,view,bar.ComponentId,bar.Id,marks[bar.Id],
                                    line.Points.Select(WallMeshPresentation.GraphicPoint),false,materialKind:line.MaterialKind));
                            else if(primitive is CirclePrimitive dot)
                                primitives.Add(new CirclePrimitive(dot.SemanticId,dot.Role,view,bar.ComponentId,bar.Id,marks[bar.Id],
                                    WallMeshPresentation.GraphicPoint(dot.Center),dot.RadiusMm));
                        continue;
                    }
                    bool alongY=bar.FrameId is "Wall/T2" or "Wall/T4";
                    if(view!=(alongY?"SteelBB":"SteelAA")) continue;
                    var section=view=="SteelAA"?sectionAA:sectionBB;
                    var key=(view,bar.ComponentId,bar.FrameId,bar.Surface,bar.Layer,bar.Direction,bar.Axis,bar.FamilyId);
                    double normal=view=="SteelAA"?bar.Path.Segments[0].Start.X:bar.Path.Segments[0].Start.Y;
                    if(section is not null && (!representatives.TryGetValue(key,out var chosen) || Math.Abs(normal-chosen)>1e-9)) continue;
                    var material=section?.Regions.Where(r=>r.SourceComponentId==bar.ComponentId).Select(r=>r.Footprint).ToArray();
                    if(material is {Length:0}) continue;
                    Point2Mm ProjectC(Point3Mm p)=>view=="SteelAA"?new(p.Y,p.Z):new(p.X,p.Z);
                    int fragment=0;
                    foreach(var segment in bar.Path.Segments)
                    {
                        string id=$"{view}/{bar.Id}/C/{fragment++}";
                        if(segment is LineSegment3Mm leg)
                        {
                            var a=ProjectC(leg.Start);var b=ProjectC(leg.End);
                            var cuts=material is null?new[]{(Start:a,End:b)}:SectionRebarClipper.Clip(a,b,material);
                            int index=0;
                            foreach(var cut in cuts) primitives.Add(new PolylinePrimitive(id+"/"+index++,"Rebar/Main",view,bar.ComponentId,bar.Id,
                                marks[bar.Id],[cut.Start,cut.End],false,materialKind:DrawingMaterialKind.RoundSteel));
                        }
                        else if(segment is ArcSegment3Mm arc)
                        {
                            var center=ProjectC(arc.Center);var start=ProjectC(arc.Start);
                            double orientation=view=="SteelAA"?arc.Normal.X:-arc.Normal.Y;
                            if(Math.Abs(orientation)<1-1e-9) return new(false,null,[new Diagnostic("C_TIE_SECTION_FRAME_UNSUPPORTED",
                                DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,bar.Sources.FirstOrDefault(),"C path parallel to supporting section",bar.Id,"No curved occurrence was omitted.")]);
                            var projected=new ArcPathSegment(center,arc.CenterlineRadiusMm,Math.Atan2(start.Y-center.Y,start.X-center.X),arc.SweepRadians*Math.Sign(orientation));
                            var cuts=material is null?new[]{projected}:SectionRebarClipper.ClipArc(projected,material);
                            int index=0;
                            foreach(var cut in cuts) primitives.Add(new PathPrimitive(id+"/"+index++,"Rebar/Main",view,bar.ComponentId,bar.Id,marks[bar.Id],
                                [cut],false,ViewPurpose.Section,bar.ScheduleLengthMm,DrawingMaterialKind.RoundSteel));
                        }
                    }
                    continue;
                }
                for (int i = 0; i < bar.Path.Segments.Length; i++)
                {
                    if (bar.Path.Segments[i] is not LineSegment3Mm line)
                        return new(false, null, [new Diagnostic("STRUCTURAL_REBAR_PROJECTION_UNSUPPORTED",
                            DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                            bar.Sources.FirstOrDefault(), "verified line silhouette", bar.Id,
                            "Curved structural bar projection requires a verified projection frame; occurrence was not omitted.")]);
                    Point2Mm Project(Point3Mm point) => view switch
                    {
                        "SteelAA" => new(point.Y, point.Z),
                        "SteelBB" => new(point.X, point.Z),
                        _ => WallMeshPresentation.GraphicPoint(new(point.X, point.Y))
                    };
                    string role = view == "WallMeshPlan" && bar.Direction == RebarDirection.Horizontal ? "Rebar/Hidden" : "Rebar/Main";
                    var start = Project(line.Start); var end = Project(line.End);
                    string id = $"{view}/{bar.Id}/{i}";
                    var section = view == "SteelAA" ? sectionAA : view == "SteelBB" ? sectionBB : null;
                    if (section is not null)
                    {
                        var key=(view,bar.ComponentId,bar.FrameId,bar.Surface,bar.Layer,bar.Direction,bar.Axis,bar.FamilyId);
                        double n0=view=="SteelAA"?line.Start.X:line.Start.Y,n1=view=="SteelAA"?line.End.X:line.End.Y;
                        // Opening clipping splits one physical lane into several occurrences.
                        // Retain every parallel fragment on that lane, not only one occurrence ID.
                        var principal=bar.Path.Segments.OfType<LineSegment3Mm>().MaxBy(s=>
                            Math.Abs(s.End.X-s.Start.X)+Math.Abs(s.End.Y-s.Start.Y)+Math.Abs(s.End.Z-s.Start.Z));
                        double principalStart=view=="SteelAA"?principal!.Start.X:principal!.Start.Y;
                        double principalEnd=view=="SteelAA"?principal.End.X:principal.End.Y;
                        bool representative=representatives.TryGetValue(key,out var chosen) &&
                            Math.Abs(principalStart-chosen)<1e-9 && Math.Abs(principalEnd-chosen)<1e-9;
                        if(!representative)
                        {
                            double plane=SectionPlane(view,bar.ComponentId);
                            n0-=plane;n1-=plane;
                            if(Math.Abs(n1-n0)<1e-9 || n0*n1>0) continue;
                            double t=-n0/(n1-n0);
                            start=end=new Point2Mm(start.X+t*(end.X-start.X),start.Y+t*(end.Y-start.Y));
                        }
                        var ownerRegions=section.Regions.Where(r => r.SourceComponentId == bar.ComponentId).ToArray();
                        // A wall absent from this section remains absent. For a wall that is cut,
                        // keep its physical anchorage legs where they enter the base/roof concrete.
                        string haunchSlab=bar.ComponentKind==ComponentKind.Roof?"Roof":"Base";
                        var material=bar.FamilyId=="Haunch"
                            ? section.Regions.Where(r=>r.SourceComponentId==haunchSlab || r.SourceComponentId==haunchSlab+"/Haunch" ||
                                r.SourceComponentId==bar.FrameId).Select(r=>r.Footprint)
                            : ownerRegions.Select(r=>r.Footprint).Concat(
                                (bar.ComponentKind==ComponentKind.Wall || bar.ComponentKind==ComponentKind.Neck) && ownerRegions.Length>0
                                    ? section.Regions.Where(r=>r.SourceComponentId is "Base" or "Roof" ||
                                        bar.FamilyId=="Dowel" && r.SourceComponentId=="Wall/Mortar").Select(r=>r.Footprint)
                                    : []);
                        int fragment = 0;
                        var cuts=SectionRebarClipper.Clip(start,end,material);
                        if(bar.FamilyId=="Dowel")
                        {
                            // Join only touching material intervals of the same straight
                            // physical dowel. A real void remains a gap; no extrapolation.
                            var joined=new List<(Point2Mm Start,Point2Mm End)>();
                            foreach(var cut in cuts)
                                if(joined.Count>0 && Math.Abs(joined[^1].End.X-cut.Start.X)<1e-9 &&
                                    Math.Abs(joined[^1].End.Y-cut.Start.Y)<1e-9)
                                    joined[^1]=(joined[^1].Start,cut.End);
                                else joined.Add(cut);
                            cuts=joined;
                        }
                        foreach (var cut in cuts)
                        {
                            if (start != end && Math.Abs(cut.End.X - cut.Start.X) < 1e-4 && Math.Abs(cut.End.Y - cut.Start.Y) < 1e-4) continue;
                            string cutId = $"{id}/Cut/{fragment++}";
                            primitives.Add(cut.Start == cut.End
                                ? new CirclePrimitive(cutId, "Rebar/Main", view, bar.ComponentId, bar.Id, marks[bar.Id], cut.Start, bar.DiameterMm / 2)
                                : new PolylinePrimitive(cutId, "Rebar/Main", view, bar.ComponentId, bar.Id, marks[bar.Id], [cut.Start, cut.End], false, materialKind: DrawingMaterialKind.RoundSteel));
                        }
                        continue;
                    }
                    if (start == end)
                        primitives.Add(new CirclePrimitive(id, role, view, bar.ComponentId,
                            bar.Id, marks[bar.Id], start, bar.DiameterMm / 2));
                    else
                        primitives.Add(new PolylinePrimitive(id, role, view, bar.ComponentId,
                            bar.Id, marks[bar.Id], [start, end], false, materialKind: DrawingMaterialKind.RoundSteel));
                }
            }
        }
        // Preserve physical opening gaps in every presentation, including Sheet1.
        return new(true, primitives, []);
    }
}
