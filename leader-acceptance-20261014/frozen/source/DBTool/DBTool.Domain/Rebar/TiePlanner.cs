using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class TiePlanner
{
    public static OperationResult<RebarLayout> PlanVhgWallC(ComponentGeometry wall,RebarLayout main,VhgTieInput input,
        double verticalPitchMm,double horizontalPitchMm)
    {
        var source=input.Sources.FirstOrDefault() ?? wall.Sources.FirstOrDefault();
        OperationResult<RebarLayout> Rule(string code,string actual)=>new(true,
            new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.Partial),
            [new Diagnostic(code,DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,source,
                "C wall ties on every second actual mesh station in both directions",wall.Id+":"+actual,"No replacement spacing or shape was invented.")]);
        if(!input.Shapes.TryGetValue(ComponentKind.Wall,out var requestedShape))
            return new(true,new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.NotApplicable),[]);
        if(wall.Kind!=ComponentKind.Wall || requestedShape!=VhgTieShape.C)
            return Rule("TIE_TYPED_SHAPE_NEEDS_RULE",wall.Kind.ToString());
        if(Math.Abs(input.Mesh.SpacingMm-2*verticalPitchMm)>1e-6 || Math.Abs(input.Mesh.SpacingMm-2*horizontalPitchMm)>1e-6)
            return Rule("TIE_DISTRIBUTION_NEEDS_RULE",$"tie={input.Mesh.SpacingMm};main={verticalPitchMm}/{horizontalPitchMm}");
        bool alongX=wall.Id.EndsWith("T1",StringComparison.Ordinal)||wall.Id.EndsWith("T3",StringComparison.Ordinal);
        double Tangent(Point3Mm p)=>alongX?p.X:p.Y;
        var bars=main.Occurrences.Where(b=>b.ComponentId==wall.Id && b.FamilyId=="Wall").ToArray();
        if(!bars.Any(b=>b.Surface==RebarSurface.Inner)||!bars.Any(b=>b.Surface==RebarSurface.Outer))
            return new(true,new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.Disabled),[]);
        var stations=bars.Where(b=>b.Direction==RebarDirection.Vertical && b.Surface==RebarSurface.Inner)
            .SelectMany(b=>b.Path.Segments.OfType<LineSegment3Mm>()).Where(l=>Math.Abs(l.End.Z-l.Start.Z)>1e-6)
            .Select(l=>Tangent(l.Start)).Distinct().Order().ToArray();
        var horizontal=bars.Where(b=>b.Direction==RebarDirection.Horizontal).SelectMany(b=>b.Path.Segments.OfType<LineSegment3Mm>()
            .Where(l=>Math.Abs(l.Start.Z-l.End.Z)<1e-6 && Math.Abs(Tangent(l.End)-Tangent(l.Start))>1e-6).Select(l=>(Bar:b,Line:l))).ToArray();
        var levels=horizontal.Where(p=>p.Bar.Surface==RebarSurface.Inner).Select(p=>p.Line.Start.Z).Distinct().Order().ToArray();
        if(levels.Length==0 || !horizontal.Any(p=>p.Bar.Surface==RebarSurface.Outer && levels.Any(z=>Math.Abs(z-p.Line.Start.Z)<1e-6)))
            return Rule("TIE_SUPPORT_GRID_NEEDS_RULE","two active face meshes have no common horizontal support level");
        var output=new List<BarOccurrence>();
        var diagnostics=new List<Diagnostic>();
        void Excluded(string code,double station,double z)=>diagnostics.Add(new Diagnostic(code,DiagnosticSeverity.Info,
            DiagnosticScope.Global,null,null,source,"whole supported C placement inside concrete and outside cutouts",
            FormattableString.Invariant($"{wall.Id}; station={station}; z={z}"),"Candidate omitted as a whole; no truncated tie or substitute station was created."));
        for(int row=0;row<levels.Length;row+=2)
        for(int column=input.Staggered && row/2%2==1?1:0;column<stations.Length;column+=2)
        {
            double station=stations[column],z=levels[row];
            var support=horizontal.Where(p=>Math.Abs(p.Line.Start.Z-z)<1e-6 &&
                station>=Math.Min(Tangent(p.Line.Start),Tangent(p.Line.End))-1e-6 &&
                station<=Math.Max(Tangent(p.Line.Start),Tangent(p.Line.End))+1e-6).ToArray();
            var inner=support.Where(p=>p.Bar.Surface==RebarSurface.Inner).OrderBy(p=>p.Bar.Id,StringComparer.Ordinal).ToArray();
            var outer=support.Where(p=>p.Bar.Surface==RebarSurface.Outer).OrderBy(p=>p.Bar.Id,StringComparer.Ordinal).ToArray();
            if(inner.Length==0 || outer.Length==0) {Excluded("TIE_C_SUPPORT_ABSENT",station,z);continue;}
            Point3Mm At(LineSegment3Mm line)=>alongX?new(station,line.Start.Y,z):new(line.Start.X,station,z);
            var path=BuildCWrap(At(inner[0].Line),inner[0].Bar.DiameterMm,At(outer[0].Line),outer[0].Bar.DiameterMm,
                input.Mesh.DiameterMm,input.HookC,source!);
            if(!path.Success) return new(false,null,path.Diagnostics);
            var length=BendLengthCalculator.Calculate(path.Value!,0,source);
            if(!length.Success) return new(false,null,length.Diagnostics);
            var points=path.Value!.Segments.SelectMany(s=>new[]{s.Start,s.End}).ToArray();double radius=input.Mesh.DiameterMm/2d;
            var vertices=wall.Footprint.Outer.Vertices;
            if(points.Any(p=>p.X-radius<vertices.Min(v=>v.X)-1e-6 || p.X+radius>vertices.Max(v=>v.X)+1e-6 ||
                p.Y-radius<vertices.Min(v=>v.Y)-1e-6 || p.Y+radius>vertices.Max(v=>v.Y)+1e-6 ||
                p.Z-radius<wall.ZRange.Start-1e-6 || p.Z+radius>wall.ZRange.End+1e-6))
            {Excluded("TIE_C_OUTSIDE_WALL",station,z);continue;}
            var bar=new BarOccurrence($"{wall.Id}/Tie/C/{row:000}/{column:000}",null,wall.Id,wall.Kind,Layer.Top,
                alongX?Axis.X:Axis.Y,input.Mesh.DiameterMm,path.Value!,length.Value!.Value,length.Value.Value,LengthStatus.Known,
                "VHG-C-WRAP-R8-20261012",input.Sources.Concat(inner[0].Bar.Sources).Concat(outer[0].Bar.Sources),
                RebarDirection.Supplementary,wall.Id,RebarSurface.Outer,"Tie",MeasurementBasisKind.Centerline,null,
                row * stations.Length + column, station, wall.Id, null, "CWrap", null, length.Value.Value,
                "VHG-C-WRAP-R8-20261012", "Tie", $"{wall.Id}:{row}:{column}", "Centerline");
            if(wall.Voids.Length>0)
            {
                var clash=CWrapOpeningCheck.Intersects(bar,wall);
                if(!clash.Success) return new(false,null,clash.Diagnostics);
                if(clash.Value!.Intersects) {Excluded("TIE_C_OPENING_EXCLUDED",station,z);continue;}
            }
            output.Add(bar);
        }
        return new(true,new RebarLayout(output,diagnostics,CalculationScope.FullRebar,output.Count>0?LayoutStatus.Complete:LayoutStatus.Disabled),diagnostics);
    }

    /// <summary>Encloses the two actual parallel wall bars. No nominal 90/30 dimensions are retained.</summary>
    public static OperationResult<BarPath> BuildCWrap(Point3Mm innerCenter,int innerDiameterMm,
        Point3Mm outerCenter,int outerDiameterMm,int tieDiameterMm,double hookMm,SourceRef source)
    {
        double dx=outerCenter.X-innerCenter.X,dy=outerCenter.Y-innerCenter.Y,distance=Math.Sqrt(dx*dx+dy*dy);
        if(innerDiameterMm<=0 || outerDiameterMm<=0 || tieDiameterMm<=0 ||
            Math.Abs(innerCenter.Z-outerCenter.Z)>1e-6 || distance<=(innerDiameterMm+outerDiameterMm)/2d)
            return new(false,null,[new Diagnostic("TIE_C_SUPPORT_INVALID",DiagnosticSeverity.Block,DiagnosticScope.Global,
                null,null,source,"two separated parallel wall bars at a common level",null,"Actual support bars cannot define a C wrap.")]);
        double leftRadius=(innerDiameterMm+tieDiameterMm)/2d,rightRadius=(outerDiameterMm+tieDiameterMm)/2d;
        double span=distance+leftRadius+rightRadius;
        // Four R8 fillets require at least 16 mm between top and bottom tangents.
        // The loop may grow to fit its fillets; support-bar or tie diameters are never reduced.
        double height=Math.Max(16,Math.Max(innerDiameterMm,outerDiameterMm)+tieDiameterMm);
        var u=new Vector3(dx/distance,dy/distance,0);var up=new Vector3(0,0,1);
        var normal=new Vector3(u.Y,-u.X,0);
        var origin=new Point3Mm(innerCenter.X-u.X*leftRadius,innerCenter.Y-u.Y*leftRadius,innerCenter.Z-height/2);
        return BuildCPath(span,height,hookMm,new Frame3Mm(origin,u,up,normal),source);
    }

    /// <summary>Owner instruction 2026-10-12: CAD FILLET R8 on the four corners of the upright C path.
    /// Dimensions are theoretical corner dimensions; radius is on the drawn path, not an inner radius.</summary>
    public static OperationResult<BarPath> BuildCPath(double layerSpanMm,double crossLegMm,double hookMm,Frame3Mm frame,SourceRef source)
    {
        const double radius=8;
        if(!double.IsFinite(layerSpanMm)||!double.IsFinite(crossLegMm)||!double.IsFinite(hookMm)||
            layerSpanMm<=2*radius || crossLegMm<2*radius || hookMm<=radius || hookMm>=layerSpanMm)
            return new(false,null,[new Diagnostic("TIE_C_FILLET_SPACE_INVALID",DiagnosticSeverity.Block,DiagnosticScope.Global,
                null,null,source,"R8 fits four corners without reducing requested dimensions",null,"C tie cannot fit its configured hook/span/leg.")]);
        Point2Mm[] corners=[new(hookMm,crossLegMm),new(0,crossLegMm),new(0,0),new(layerSpanMm,0),
            new(layerSpanMm,crossLegMm),new(layerSpanMm-hookMm,crossLegMm)];
        Point3Mm World(Point2Mm p)=>new(frame.Origin.X+frame.U.X*p.X+frame.V.X*p.Y,
            frame.Origin.Y+frame.U.Y*p.X+frame.V.Y*p.Y,frame.Origin.Z+frame.U.Z*p.X+frame.V.Z*p.Y);
        Vector3 Direction(Point2Mm p)=>new(frame.U.X*p.X+frame.V.X*p.Y,frame.U.Y*p.X+frame.V.Y*p.Y,frame.U.Z*p.X+frame.V.Z*p.Y);
        var normal=new Vector3(frame.U.Y*frame.V.Z-frame.U.Z*frame.V.Y,frame.U.Z*frame.V.X-frame.U.X*frame.V.Z,
            frame.U.X*frame.V.Y-frame.U.Y*frame.V.X);
        var segments=new List<PathSegment>();var previous=corners[0];
        for(int i=1;i<corners.Length-1;i++)
        {
            var a=corners[i-1];var corner=corners[i];var b=corners[i+1];
            double before=Math.Sqrt(Math.Pow(corner.X-a.X,2)+Math.Pow(corner.Y-a.Y,2));
            double after=Math.Sqrt(Math.Pow(b.X-corner.X,2)+Math.Pow(b.Y-corner.Y,2));
            var incoming=new Point2Mm((corner.X-a.X)/before,(corner.Y-a.Y)/before);
            var outgoing=new Point2Mm((b.X-corner.X)/after,(b.Y-corner.Y)/after);
            var start=new Point2Mm(corner.X-radius*incoming.X,corner.Y-radius*incoming.Y);
            var end=new Point2Mm(corner.X+radius*outgoing.X,corner.Y+radius*outgoing.Y);
            var center=new Point2Mm(start.X+radius*outgoing.X,start.Y+radius*outgoing.Y);
            if(BendLengthCalculator.Distance(World(previous),World(start))>1e-8)
                segments.Add(new LineSegment3Mm(World(previous),World(start)));
            segments.Add(new ArcSegment3Mm(World(center),radius,normal,
                Direction(new((start.X-center.X)/radius,(start.Y-center.Y)/radius)),Math.PI/2));
            previous=end;
        }
        segments.Add(new LineSegment3Mm(World(previous),World(corners[^1])));
        return new(true,new BarPath(segments,false,HookPattern.Hook2M,0,false,MeasurementBasis.TangentPath),[]);
    }

    /// <summary>5-leg U-shape tie between two active parallel mesh layers with actual stack gap.</summary>
    public static OperationResult<RebarLayout> PlanVhgSlabU(ComponentGeometry slab,RebarLayout main,VhgTieInput input,
        double pitchXMm,double pitchYMm)
    {
        var source=input.Sources.FirstOrDefault() ?? slab.Sources.FirstOrDefault();
        OperationResult<RebarLayout> Rule(string code,string actual)=>new(true,
            new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.Partial),
            [new Diagnostic(code,DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,source,
                "U slab ties on every second actual mesh station in both directions",slab.Id+":"+actual,"No replacement spacing or shape was invented.")]);

        if(!input.Shapes.TryGetValue(slab.Kind,out var requestedShape))
            return new(true,new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.NotApplicable),[]);

        if(requestedShape!=VhgTieShape.U)
            return Rule("TIE_TYPED_SHAPE_NEEDS_RULE",slab.Kind+":"+requestedShape);

        if(pitchXMm>0 && Math.Abs(input.Mesh.SpacingMm-2*pitchXMm)>1e-6 ||
            pitchYMm>0 && Math.Abs(input.Mesh.SpacingMm-2*pitchYMm)>1e-6)
            return Rule("TIE_DISTRIBUTION_NEEDS_RULE",$"tie={input.Mesh.SpacingMm};main={pitchXMm}/{pitchYMm}");

        var slabBars=main.Occurrences.Where(b=>b.ComponentId==slab.Id).ToArray();
        var bottomBars=slabBars.Where(b=>b.Layer==Layer.Bottom).ToArray();
        var topBars=slabBars.Where(b=>b.Layer==Layer.Top).ToArray();

        if(bottomBars.Length==0 || topBars.Length==0)
            return new(true,new RebarLayout([],[],CalculationScope.FullRebar,LayoutStatus.Disabled),[]);

        double zBottom=bottomBars.SelectMany(b=>b.Path.Segments).SelectMany(s=>new[]{s.Start.Z,s.End.Z}).Average();
        double zTop=topBars.SelectMany(b=>b.Path.Segments).SelectMany(s=>new[]{s.Start.Z,s.End.Z}).Average();
        double gapHeight=Math.Abs(zTop-zBottom);

        if(gapHeight<=0)
            return Rule("TIE_FACE_CLEARANCE_INVALID","no vertical clearance between top and bottom mesh");

        double hookZU=input.HookZU;
        double middleU=input.MiddleU;
        if(hookZU<=0 || middleU<=0)
            return Rule("TIE_U_DIMENSIONS_NEEDS_RULE",$"hookZU={hookZU};middleU={middleU}");

        var stationsX=bottomBars.SelectMany(b=>b.Path.Segments.OfType<LineSegment3Mm>())
            .Where(l=>Math.Abs(l.Start.X-l.End.X)<1e-6 && Math.Abs(l.Start.Y-l.End.Y)>1e-6)
            .Select(l=>l.Start.X).Distinct().Order().ToArray();

        var stationsY=bottomBars.SelectMany(b=>b.Path.Segments.OfType<LineSegment3Mm>())
            .Where(l=>Math.Abs(l.Start.Y-l.End.Y)<1e-6 && Math.Abs(l.Start.X-l.End.X)>1e-6)
            .Select(l=>l.Start.Y).Distinct().Order().ToArray();

        if(stationsX.Length==0 || stationsY.Length==0)
            return Rule("TIE_SUPPORT_GRID_NEEDS_RULE","no orthogonal mesh grid stations found in slab");

        var output=new List<BarOccurrence>();
        var diagnostics=new List<Diagnostic>();
        var outerVerts=slab.Footprint.Outer.Vertices;
        double minX=outerVerts.Min(v=>v.X),maxX=outerVerts.Max(v=>v.X);
        double minY=outerVerts.Min(v=>v.Y),maxY=outerVerts.Max(v=>v.Y);

        for(int row=0;row<stationsY.Length;row+=2)
        {
            int startCol=input.Staggered && (row/2%2==1)?1:0;
            for(int col=startCol;col<stationsX.Length;col+=2)
            {
                double sx=stationsX[col];
                double sy=stationsY[row];

                double x0=sx-hookZU;
                double x1=sx;
                double x2=sx+middleU;
                double x3=sx+middleU+hookZU;

                if(x0<minX || x3>maxX || sy<minY || sy>maxY)
                {
                    diagnostics.Add(new Diagnostic("TIE_U_OUTSIDE_SLAB",DiagnosticSeverity.Info,
                        DiagnosticScope.Global,null,null,source,
                        "whole supported U placement inside concrete",
                        FormattableString.Invariant($"{slab.Id}; stationX={sx}; stationY={sy}"),
                        "Candidate omitted as a whole."));
                    continue;
                }

                Point3Mm p0=new(x0,sy,zTop);
                Point3Mm p1=new(x1,sy,zTop);
                Point3Mm p2=new(x1,sy,zBottom);
                Point3Mm p3=new(x2,sy,zBottom);
                Point3Mm p4=new(x2,sy,zTop);
                Point3Mm p5=new(x3,sy,zTop);

                var segments=new PathSegment[]
                {
                    new LineSegment3Mm(p0,p1),
                    new LineSegment3Mm(p1,p2),
                    new LineSegment3Mm(p2,p3),
                    new LineSegment3Mm(p3,p4),
                    new LineSegment3Mm(p4,p5)
                };

                var path=new BarPath(segments,false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
                double length=2*hookZU+2*gapHeight+middleU;

                var bar=new BarOccurrence($"{slab.Id}/Tie/U/{row:000}/{col:000}",null,slab.Id,slab.Kind,
                    Layer.Bottom,Axis.X,input.Mesh.DiameterMm,path,length,length,LengthStatus.Known,
                    "VHG-U-SLAB-20261012",input.Sources.Concat(source!=null?[source]:[]),
                    RebarDirection.Supplementary,slab.Id,null,"Tie",MeasurementBasisKind.Centerline,null,
                    row * stationsX.Length + col, sx, slab.Id, null, "USlab", null, length,
                    "VHG-U-SLAB-20261012", "Tie", $"{slab.Id}:{row}:{col}", "Centerline");
                output.Add(bar);
            }
        }

        return new(true,new RebarLayout(output,diagnostics,CalculationScope.FullRebar,
            output.Count>0?LayoutStatus.Complete:LayoutStatus.Disabled),diagnostics);
    }

    public static OperationResult<RebarLayout> PlanVhgBaseU(ComponentGeometry slab,RebarLayout main,VhgTieInput input,
        double pitchXMm,double pitchYMm) => PlanVhgSlabU(slab,main,input,pitchXMm,pitchYMm);

    public static OperationResult<RebarLayout> PlanVhgRoofU(ComponentGeometry slab,RebarLayout main,VhgTieInput input,
        double pitchXMm,double pitchYMm) => PlanVhgSlabU(slab,main,input,pitchXMm,pitchYMm);

    public static OperationResult<RebarLayout> Plan(ComponentGeometry component,
        RebarLayout activeMainLayers, TieSpec spec, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(activeMainLayers);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);

        var componentBars = activeMainLayers.Occurrences
            .Where(x => string.Equals(x.ComponentId, component.Id, StringComparison.Ordinal))
            .ToArray();
        var activeLayers = componentBars.Select(x => x.Layer).Distinct().ToArray();
        if (component.MaterialId.Length == 0 || activeLayers.Length < 2 ||
            !activeLayers.Contains(Layer.Bottom) || !activeLayers.Contains(Layer.Top))
        {
            var diagnostic = Diagnostic(component, "TIE_ONE_ACTIVE_LAYER", DiagnosticSeverity.Info,
                "Both active steel layers are required before ties can be placed.");
            return Complete([], [diagnostic], LayoutStatus.Disabled);
        }

        var shape = spec.Shape;
        if (shape is null || shape.BendRadiusMm is null && shape.HookPattern != HookPattern.Hook0M)
        {
            var diagnostic = Diagnostic(component, "TIE_SHAPE_NEEDS_RULE", DiagnosticSeverity.Review,
                "Tie shape or bend-radius evidence is required; no fixed tie length was invented.");
            return Complete([], [diagnostic], LayoutStatus.Partial);
        }

        var bounds = Bounds(component.Footprint.Outer);
        double minU = bounds.MinX + spec.EdgeMarginMm;
        double maxU = bounds.MaxX - spec.EdgeMarginMm;
        double minV = bounds.MinY + spec.EdgeMarginMm;
        double maxV = bounds.MaxY - spec.EdgeMarginMm;
        if (minU > maxU || minV > maxV)
            return Complete([], [Diagnostic(component, "TIE_EDGE_MARGIN_EXCLUDES_DOMAIN", DiagnosticSeverity.Info,
                "Edge margin leaves no admissible lattice domain.")], LayoutStatus.Disabled);

        double spanU = maxU - minU;
        double spanV = maxV - minV;
        if (spec.SpacingUMm > spanU || spec.SpacingVMm > spanV)
        {
            if (spec.LargeSpacingBehavior == LargeSpacingBehavior.Block)
                return Block(component, "TIE_SPACING_INVALID", "Tie spacing exceeds the admissible component span.");
            if (spec.LargeSpacingBehavior == LargeSpacingBehavior.Disable)
                return Complete([], [Diagnostic(component, "TIE_LARGE_SPACING_DISABLED", DiagnosticSeverity.Info,
                    "Tie lattice is disabled because spacing exceeds the admissible span.")], LayoutStatus.Disabled);
        }

        var points = LatticePoints(minU, maxU, minV, maxV, spec);
        var bottom = componentBars.Where(x => x.Layer == Layer.Bottom).ToArray();
        var top = componentBars.Where(x => x.Layer == Layer.Top).ToArray();
        if (bottom.Length == 0 || top.Length == 0)
            return Complete([], [Diagnostic(component, "TIE_ONE_ACTIVE_LAYER", DiagnosticSeverity.Info,
                "Both active steel layers are required before ties can be placed.")], LayoutStatus.Disabled);

        double bottomZ = bottom.SelectMany(x => x.Path.Segments).SelectMany(x => new[] { x.Start.Z, x.End.Z }).Max();
        double topZ = top.SelectMany(x => x.Path.Segments).SelectMany(x => new[] { x.Start.Z, x.End.Z }).Min();
        double lower = Math.Min(bottomZ, topZ);
        double upper = Math.Max(bottomZ, topZ);
        double coverDiameter = shape!.DiameterMm;
        lower += coverDiameter / 2d;
        upper -= coverDiameter / 2d;
        if (upper <= lower)
            return Block(component, "TIE_FACE_CLEARANCE_INVALID", "Tie diameter does not fit between the supported steel faces.");

        var frame = Frame(component);
        var occurrences = new List<BarOccurrence>();
        int index = 0;
        foreach (var point in points)
        {
            if (!Inside(component.Footprint.Outer, point) || Forbidden(component.Voids, point, spec.OpeningClearanceMm))
                continue;
            var start = ToWorld(frame, point, lower);
            var end = ToWorld(frame, point, upper);
            var pathSegments = BuildPath(start, end, shape);
            var path = new BarPath(pathSegments, false, shape.HookPattern, 0, false, MeasurementBasis.TangentPath);
            double length = path.Segments.Sum(Length);
            occurrences.Add(new BarOccurrence($"{component.Id}/Tie/{index++:000}", null, component.Id,
                component.Kind, Layer.Bottom, Axis.X, shape.DiameterMm, path, length, length, LengthStatus.Known,
                "TIE-LATTICE-S2-001", component.Sources.Concat(spec.Sources), RebarDirection.Supplementary,
                frameId: component.Id, surface: null, familyId: "Tie", measurementBasis: MeasurementBasisKind.Centerline,
                innerRadiusMm: shape.BendRadiusMm));
        }

        return Complete(occurrences, [], occurrences.Count == 0 ? LayoutStatus.Disabled : LayoutStatus.Complete);
    }

    private static IEnumerable<Point2Mm> LatticePoints(double minU, double maxU, double minV, double maxV, TieSpec spec)
    {
        double firstU = FirstAtOrAfter(spec.Origin.X, minU, spec.SpacingUMm);
        double firstV = FirstAtOrAfter(spec.Origin.Y, minV, spec.SpacingVMm);
        for (int row = 0; ; row++)
        {
            double v = firstV + row * spec.SpacingVMm;
            if (v > maxV + 1e-6) yield break;
            double shift = spec.Mode == TieLatticeMode.Staggered && (row & 1) == 1 ? spec.SpacingUMm / 2d : 0;
            double rowStart = firstU + shift;
            if (shift > 0 && rowStart > maxU + 1e-6) continue;
            for (double u = rowStart; u <= maxU + 1e-6; u += spec.SpacingUMm)
                if (u >= minU - 1e-6) yield return new Point2Mm(u, v);
        }
    }

    private static double FirstAtOrAfter(double origin, double min, double spacing) =>
        origin + Math.Ceiling((min - origin) / spacing) * spacing;

    private static PathSegment[] BuildPath(Point3Mm start, Point3Mm end, TieShapeSpec shape)
    {
        if (shape.HookPattern == HookPattern.Hook0M) return [new LineSegment3Mm(start, end)];
        double hook = shape.HookLengthMm ?? (shape.BendRadiusMm ?? 0) + shape.DiameterMm;
        if (hook <= 0) return [new LineSegment3Mm(start, end)];
        var side = new Point3Mm(start.X + hook, start.Y, start.Z);
        var top = new Point3Mm(end.X + hook, end.Y, end.Z);
        return shape.HookPattern == HookPattern.Hook1M
            ? [new LineSegment3Mm(start, side), new LineSegment3Mm(side, end)]
            : [new LineSegment3Mm(start, side), new LineSegment3Mm(side, top), new LineSegment3Mm(top, end)];
    }

    private static double Length(PathSegment segment) => segment switch
    {
        LineSegment3Mm line => Distance(line.Start, line.End),
        ArcSegment3Mm arc => Math.Abs(arc.SweepRadians) * arc.CenterlineRadiusMm,
        _ => throw new ArgumentOutOfRangeException(nameof(segment))
    };

    private static double Distance(Point3Mm a, Point3Mm b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));

    private static Frame3Mm Frame(ComponentGeometry component) => component.Occupied is ExtrusionSolid solid
        ? solid.Frame
        : component.FacePatches.FirstOrDefault()?.Frame ??
          new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));

    private static Point3Mm ToWorld(Frame3Mm frame, Point2Mm point, double z)
    {
        double x = frame.Origin.X + frame.U.X * point.X + frame.V.X * point.Y + frame.N.X * z;
        double y = frame.Origin.Y + frame.U.Y * point.X + frame.V.Y * point.Y + frame.N.Y * z;
        double worldZ = frame.Origin.Z + frame.U.Z * point.X + frame.V.Z * point.Y + frame.N.Z * z;
        return new Point3Mm(x, y, worldZ);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(Polygon2Mm polygon) =>
        (polygon.Vertices.Min(x => x.X), polygon.Vertices.Min(x => x.Y), polygon.Vertices.Max(x => x.X), polygon.Vertices.Max(x => x.Y));

    private static bool Inside(Polygon2Mm polygon, Point2Mm point)
    {
        var bounds = Bounds(polygon);
        return point.X >= bounds.MinX - 1e-6 && point.X <= bounds.MaxX + 1e-6 &&
               point.Y >= bounds.MinY - 1e-6 && point.Y <= bounds.MaxY + 1e-6;
    }

    private static bool Forbidden(IEnumerable<OpeningProfile> openings, Point2Mm point, double clearance)
    {
        foreach (var opening in openings)
        {
            switch (opening.ConcreteCutout)
            {
                case CircleProfile circle:
                    if (Math.Pow(point.X - circle.Center.X, 2) + Math.Pow(point.Y - circle.Center.Y, 2) <= Math.Pow(circle.RadiusMm + clearance, 2)) return true;
                    break;
                case RectangleProfile rectangle:
                    if (point.X >= rectangle.Rectangle.MinX - clearance && point.X <= rectangle.Rectangle.MaxX + clearance &&
                        point.Y >= rectangle.Rectangle.MinY - clearance && point.Y <= rectangle.Rectangle.MaxY + clearance) return true;
                    break;
            }
        }
        return false;
    }

    private static OperationResult<RebarLayout> Complete(IEnumerable<BarOccurrence> bars, IEnumerable<Diagnostic> diagnostics, LayoutStatus status) =>
        new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar, status), diagnostics);

    private static OperationResult<RebarLayout> Block(ComponentGeometry component, string code, string message) =>
        new(false, null, [Diagnostic(component, code, DiagnosticSeverity.Block, message)]);

    private static Diagnostic Diagnostic(ComponentGeometry component, string code, DiagnosticSeverity severity, string message) =>
        new(code, severity, DiagnosticScope.Component, component.Id, component.Id, component.Sources.FirstOrDefault(), null, null, message);
}

