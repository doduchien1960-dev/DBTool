using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Physical true and explicit sections through the supported rectangular owners and typed openings.</summary>
public static class SectionProjector
{
    public static OperationResult<ViewProfile> BuildSection(GeometryModel model,
        SteppedSectionDefinition cutter,CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(cutter);
        ArgumentNullException.ThrowIfNull(policy);
        if(policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1"))
            return Fail("SECTION_MODE_UNSUPPORTED",null,"S2Geometry-1 or S2Rebar-1 stepped cutter",policy.PolicyId);
        var regions=new List<MaterialRegion>();
        var voids=new List<ProjectedVoid>();
        var analyticRegions=new List<AnalyticSectionRegion>();
        var analyticVoids=new List<AnalyticSectionVoid>();
        foreach(var component in model.Components)
        {
            if(component.Occupied is not ExtrusionSolid solid ||
                solid.Frame.Origin!=new Point3Mm(0,0,0) ||
                solid.Frame.U!=new Vector3(1,0,0) || solid.Frame.V!=new Vector3(0,1,0) ||
                solid.Frame.N!=new Vector3(0,0,1) || solid.Depth!=component.ZRange ||
                !TryRectangle(component.Footprint.Outer,out var outer) ||
                component.Footprint.InnerLoops.Any(h=>!TryRectangle(h,out _)))
                return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),
                    "axis-aligned rectangular extrusion",component.Id);
            var ownerOpenings=model.Openings.Where(o=>o.OwnerId==component.Id).ToArray();
            OpeningProfile? roundOpening=ownerOpenings.FirstOrDefault(o=>o.Kind==OpeningKind.Round);
            if(roundOpening is not null)
            {
                if(ownerOpenings.Length!=1 || component.Footprint.InnerLoops.Length!=0 ||
                    roundOpening.WallFrame is null ||
                    roundOpening.Inner is not CircleProfile ||
                    roundOpening.PipeOuter is not CircleProfile ||
                    roundOpening.ConcreteCutout is not CircleProfile)
                    return Fail("SECTION_OPENING_UNSUPPORTED",roundOpening.Sources.FirstOrDefault(),
                        "single round wall cut in rectangular owner",roundOpening.Id);
            }
            else foreach(var opening in ownerOpenings)
                if(!TryOpeningPlanRectangle(opening,out _,out _))
                    return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),
                        "axis-aligned rectangular physical opening cut on explicit cutter",opening.Id);
            double station=0;
            for(int i=0;i<cutter.CutterVertices.Length-1;i++)
            {
                var start=cutter.CutterVertices[i];var end=cutter.CutterVertices[i+1];
                double dx=end.X-start.X,dy=end.Y-start.Y;
                double length=Math.Sqrt(dx*dx+dy*dy);
                var outerSpan=SegmentRectangle(outer,start,dx,dy);
                if(outerSpan is { } span)
                {
                    if(roundOpening is not null)
                    {
                        var opening=roundOpening;
                        var wall=opening.WallFrame!;
                        var local=wall.ToLocal(new Point3Mm(start.X,start.Y,0));
                        double du=dx*wall.Left.X+dy*wall.Left.Y;
                        double dv=dx*wall.Outward.X+dy*wall.Outward.Y;
                        double min=span.Min,max=span.Max;
                        var concrete=(CircleProfile)opening.ConcreteCutout;
                        bool intersectsDepth=Slab(local.V,dv,opening.ExtrusionRange.Start,
                            opening.ExtrusionRange.End,ref min,ref max);
                        bool intersectsCircle=intersectsDepth && Slab(
                            local.U-concrete.Center.X,du,-concrete.RadiusMm,
                            concrete.RadiusMm,ref min,ref max);
                        if(!intersectsCircle)
                        {
                            regions.Add(new MaterialRegion($"{component.Id}/Step/{i}/Whole",
                                component.Id,component.MaterialId,
                                new Footprint(Rect(station+span.Min*length,
                                    component.ZRange.Start,station+span.Max*length,
                                    component.ZRange.End),[]),component.Sources));
                            station+=length;
                            continue;
                        }
                        if(Math.Abs(du)<=1e-12)
                            return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),
                                "round cutter with varying wall-local U",opening.Id);
                        EllipticSectionBand? Band(CircleProfile circle)
                        {
                            double t0=min,t1=max;
                            if(!Slab(local.U-circle.Center.X,du,-circle.RadiusMm,
                                circle.RadiusMm,ref t0,ref t1)) return null;
                            return new EllipticSectionBand(station+t0*length,
                                station+t1*length,
                                station+(circle.Center.X-local.U)*length/du,
                                circle.Center.Y,circle.RadiusMm*length/Math.Abs(du),
                                circle.RadiusMm);
                        }
                        foreach(var (role,circle) in new[]{
                            (ProjectedVoidRole.Inner,(CircleProfile)opening.Inner),
                            (ProjectedVoidRole.PipeOuter,(CircleProfile)opening.PipeOuter!),
                            (ProjectedVoidRole.ConcreteCutout,concrete)})
                            if(Band(circle) is { } band)
                                AddRoundVoids($"{opening.Id}/{role}/Step/{i}",opening,
                                    role,band,component.ZRange.Start,component.ZRange.End,
                                    analyticVoids);
                        var concreteBand=Band(concrete)!;
                        if(min-span.Min>1e-6)
                            regions.Add(new MaterialRegion($"{component.Id}/Step/{i}/SideStart",
                                component.Id,component.MaterialId,
                                new Footprint(Rect(station+span.Min*length,
                                    component.ZRange.Start,station+min*length,
                                    component.ZRange.End),[]),component.Sources));
                        if(span.Max-max>1e-6)
                            regions.Add(new MaterialRegion($"{component.Id}/Step/{i}/SideEnd",
                                component.Id,component.MaterialId,
                                new Footprint(Rect(station+max*length,
                                    component.ZRange.Start,station+span.Max*length,
                                    component.ZRange.End),[]),component.Sources));
                        AddRoundMaterial($"{component.Id}/Step/{i}",component,
                            concreteBand,regions,analyticRegions);
                        station+=length;
                        continue;
                    }
                    var intervals=new List<(double Min,double Max)>{span};
                    foreach(var hole in component.Footprint.InnerLoops)
                    {
                        TryRectangle(hole,out var inner);
                        if(SegmentRectangle(inner,start,dx,dy) is not { } voidSpan) continue;
                        var next=new List<(double Min,double Max)>();
                        foreach(var part in intervals)
                        {
                            if(voidSpan.Max<=part.Min || voidSpan.Min>=part.Max)
                            { next.Add(part);continue; }
                            if(voidSpan.Min>part.Min) next.Add((part.Min,voidSpan.Min));
                            if(voidSpan.Max<part.Max) next.Add((voidSpan.Max,part.Max));
                        }
                        intervals=next;
                    }
                    var cutRegions=new List<(double Min,double Max,double Z0,double Z1)>();
                    foreach(var opening in ownerOpenings)
                    {
                        TryOpeningPlanRectangle(opening,out var cut,out var role);
                        if(SegmentRectangle(cut,start,dx,dy) is not { } cutSpan) continue;
                        double lo=Math.Max(cutSpan.Min,span.Min);
                        double hi=Math.Min(cutSpan.Max,span.Max);
                        double z0=Math.Max(opening.ZRange.Start,component.ZRange.Start);
                        double z1=Math.Min(opening.ZRange.End,component.ZRange.End);
                        if(hi-lo<=1e-6 || z1-z0<=1e-6) continue;
                        cutRegions.Add((lo,hi,z0,z1));
                        voids.Add(new ProjectedVoid($"{opening.Id}/StepCut/{i}",opening.Id,
                            opening.OwnerId,role,
                            Rect(station+lo*length,z0,station+hi*length,z1),opening.Sources));
                    }
                    int piece=0;
                    foreach(var (min,max) in intervals)
                    {
                        double[] xs=new[]{min,max}.Concat(cutRegions.SelectMany(c=>
                            new[]{Math.Clamp(c.Min,min,max),Math.Clamp(c.Max,min,max)}))
                            .Distinct().Order().ToArray();
                        double[] zs=new[]{component.ZRange.Start,component.ZRange.End}
                            .Concat(cutRegions.SelectMany(c=>new[]{c.Z0,c.Z1}))
                            .Distinct().Order().ToArray();
                        for(int x=0;x<xs.Length-1;x++)
                        for(int z=0;z<zs.Length-1;z++)
                        {
                            double x0=xs[x],x1=xs[x+1],z0=zs[z],z1=zs[z+1];
                            if((x1-x0)*length<=1e-6 || z1-z0<=1e-6) continue;
                            double midX=(x0+x1)/2,midZ=(z0+z1)/2;
                            if(cutRegions.Any(c=>midX>c.Min && midX<c.Max &&
                                midZ>c.Z0 && midZ<c.Z1)) continue;
                            regions.Add(new MaterialRegion($"{component.Id}/Step/{i}/{++piece}",
                                component.Id,component.MaterialId,
                                new Footprint(Rect(station+x0*length,z0,
                                    station+x1*length,z1),[]),component.Sources));
                        }
                    }
                }
                station+=length;
            }
        }
        return new(true,new ViewProfile(cutter.Id,ViewPurpose.Section,regions,voids,[],
            analyticRegions,analyticVoids),[]);

        static (double Min,double Max)? SegmentRectangle(Rectangle2Mm r,Point2Mm p,double dx,double dy)
        {
            double min=0,max=1;
            if(!Slab(p.X,dx,r.MinX,r.MaxX,ref min,ref max) ||
                !Slab(p.Y,dy,r.MinY,r.MaxY,ref min,ref max) || max<=min)
                return null;
            return(min,max);
        }
        static bool Slab(double point,double direction,double lo,double hi,ref double min,ref double max)
        {
            if(direction==0) return point>lo && point<hi;
            double a=(lo-point)/direction,b=(hi-point)/direction;
            if(a>b)(a,b)=(b,a);
            min=Math.Max(min,a);max=Math.Min(max,b);
            return max>min;
        }
    }

    public static OperationResult<ViewProfile> BuildSection(GeometryModel model, SectionPlane plane,
        CutMode mode, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(plane);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PolicyId is not ("S1-Analytic-1" or "S2Geometry-1" or "S2Rebar-1") ||
            !Enum.IsDefined(mode) || mode != CutMode.TrueSection)
            return Fail("SECTION_MODE_UNSUPPORTED", null, "supported true section policy", $"{policy.Version}/{mode}");
        SectionAxis axis;
        double coordinate;
        if (Math.Abs(plane.UnitNormal.X) == 1 && plane.UnitNormal.Y == 0 && plane.UnitNormal.Z == 0)
        { axis = SectionAxis.AlongY; coordinate = plane.Point.X; }
        else if (Math.Abs(plane.UnitNormal.Y) == 1 && plane.UnitNormal.X == 0 && plane.UnitNormal.Z == 0)
        { axis = SectionAxis.AlongX; coordinate = plane.Point.Y; }
        else if (plane.UnitNormal.Z == 0)
            return BuildObliqueSection(model,plane);
        else return Fail("SECTION_PLANE_UNSUPPORTED", null, "vertical section plane", plane.UnitNormal.ToString());
        return BuildSection(model, new SectionDefinition(plane.Id, axis, coordinate, SectionMode.TrueSection));
    }

    private static OperationResult<ViewProfile> BuildObliqueSection(GeometryModel model,SectionPlane plane)
    {
        double tx=-plane.UnitNormal.Y,ty=plane.UnitNormal.X;
        var regions=new List<MaterialRegion>();
        var voids=new List<ProjectedVoid>();
        var analyticRegions=new List<AnalyticSectionRegion>();
        var analyticVoids=new List<AnalyticSectionVoid>();
        var dimensions=new List<ViewDimensionIntent>();
        foreach(var component in model.Components)
        {
            if(component.Occupied is not ExtrusionSolid solid ||
                solid.Frame.Origin!=new Point3Mm(0,0,0) ||
                solid.Frame.U!=new Vector3(1,0,0) || solid.Frame.V!=new Vector3(0,1,0) ||
                solid.Frame.N!=new Vector3(0,0,1) || solid.Depth!=component.ZRange ||
                !TryRectangle(component.Footprint.Outer,out var outer) ||
                component.Footprint.InnerLoops.Any(h=>!TryRectangle(h,out _)))
                return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),
                    "axis-aligned rectangular extrusion",component.Id);
            var outerInterval=LineRectangle(outer);
            if(outerInterval is null) continue;
            var ownerOpenings=model.Openings.Where(o=>o.OwnerId==component.Id).ToArray();
            if(ownerOpenings.Any(o=>o.Kind==OpeningKind.Round))
            {
                if(ownerOpenings.Length!=1 || component.Footprint.InnerLoops.Length!=0 ||
                    ownerOpenings[0].WallFrame is not { } wall ||
                    ownerOpenings[0].Inner is not CircleProfile inner ||
                    ownerOpenings[0].PipeOuter is not CircleProfile pipeOuter ||
                    ownerOpenings[0].ConcreteCutout is not CircleProfile concrete)
                    return Fail("SECTION_OPENING_UNSUPPORTED",ownerOpenings[0].Sources.FirstOrDefault(),
                        "single round wall cut in rectangular owner",ownerOpenings[0].Id);
                var opening=ownerOpenings[0];
                var origin=wall.ToLocal(plane.Point);
                double du=tx*wall.Left.X+ty*wall.Left.Y;
                double dv=tx*wall.Outward.X+ty*wall.Outward.Y;
                double min=outerInterval.Value.Min,max=outerInterval.Value.Max;
                bool intersectsDepth=Slab(origin.V,dv,opening.ExtrusionRange.Start,
                    opening.ExtrusionRange.End,ref min,ref max);
                bool intersectsCircle=intersectsDepth && Slab(
                    origin.U-concrete.Center.X,du,-concrete.RadiusMm,
                    concrete.RadiusMm,ref min,ref max);
                if(!intersectsCircle)
                {
                    regions.Add(new MaterialRegion($"{component.Id}/ObliqueWhole",
                        component.Id,component.MaterialId,
                        new Footprint(Rect(outerInterval.Value.Min,
                            component.ZRange.Start,outerInterval.Value.Max,
                            component.ZRange.End),[]),component.Sources));
                    dimensions.Add(new ViewDimensionIntent($"{component.Id}/ObliqueWidth",
                        new Point2Mm(outerInterval.Value.Min,component.ZRange.Start),
                        new Point2Mm(outerInterval.Value.Max,component.ZRange.Start),
                        outerInterval.Value.Max-outerInterval.Value.Min));
                    continue;
                }
                if(Math.Abs(du)<=1e-12)
                    return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),
                        "round section with varying wall-local U",opening.Id);
                EllipticSectionBand? Band(CircleProfile circle)
                {
                    double s0=min,s1=max;
                    if(!Slab(origin.U-circle.Center.X,du,-circle.RadiusMm,
                        circle.RadiusMm,ref s0,ref s1)) return null;
                    return new EllipticSectionBand(s0,s1,
                        (circle.Center.X-origin.U)/du,circle.Center.Y,
                        circle.RadiusMm/Math.Abs(du),circle.RadiusMm);
                }
                foreach(var (role,circle) in new[]{
                    (ProjectedVoidRole.Inner,inner),
                    (ProjectedVoidRole.PipeOuter,pipeOuter),
                    (ProjectedVoidRole.ConcreteCutout,concrete)})
                    if(Band(circle) is { } band)
                        AddRoundVoids($"{opening.Id}/{role}/Oblique",opening,role,band,
                            component.ZRange.Start,component.ZRange.End,analyticVoids);
                var concreteBand=Band(concrete)!;
                if(min-outerInterval.Value.Min>1e-6)
                    regions.Add(new MaterialRegion($"{component.Id}/ObliqueSideStart",
                        component.Id,component.MaterialId,
                        new Footprint(Rect(outerInterval.Value.Min,
                            component.ZRange.Start,min,component.ZRange.End),[]),component.Sources));
                if(outerInterval.Value.Max-max>1e-6)
                    regions.Add(new MaterialRegion($"{component.Id}/ObliqueSideEnd",
                        component.Id,component.MaterialId,
                        new Footprint(Rect(max,component.ZRange.Start,
                            outerInterval.Value.Max,component.ZRange.End),[]),component.Sources));
                AddRoundMaterial($"{component.Id}/Oblique",component,concreteBand,
                    regions,analyticRegions);
                dimensions.Add(new ViewDimensionIntent($"{component.Id}/ObliqueWidth",
                    new Point2Mm(outerInterval.Value.Min,component.ZRange.Start),
                    new Point2Mm(outerInterval.Value.Max,component.ZRange.Start),
                    outerInterval.Value.Max-outerInterval.Value.Min));
                continue;
            }
            var spans=new List<(double Min,double Max)>{outerInterval.Value};
            foreach(var hole in component.Footprint.InnerLoops)
            {
                TryRectangle(hole,out var inner);
                if(LineRectangle(inner) is not { } voidSpan) continue;
                var next=new List<(double Min,double Max)>();
                foreach(var span in spans)
                {
                    if(voidSpan.Max<=span.Min || voidSpan.Min>=span.Max)
                    { next.Add(span);continue; }
                    if(voidSpan.Min>span.Min) next.Add((span.Min,voidSpan.Min));
                    if(voidSpan.Max<span.Max) next.Add((voidSpan.Max,span.Max));
                }
                spans=next;
            }
            var cutRegions=new List<(double Min,double Max,double Z0,double Z1)>();
            foreach(var opening in model.Openings.Where(o=>o.OwnerId==component.Id))
            {
                if(!TryOpeningPlanRectangle(opening,out var cut,out var role))
                    return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),
                        "axis-aligned rectangular physical opening cut on oblique plane",opening.Id);
                if(LineRectangle(cut) is not { } cutSpan) continue;
                double lo=Math.Max(cutSpan.Min,outerInterval.Value.Min);
                double hi=Math.Min(cutSpan.Max,outerInterval.Value.Max);
                double z0=Math.Max(opening.ZRange.Start,component.ZRange.Start);
                double z1=Math.Min(opening.ZRange.End,component.ZRange.End);
                if(hi-lo<=1e-6 || z1-z0<=1e-6) continue;
                cutRegions.Add((lo,hi,z0,z1));
                voids.Add(new ProjectedVoid($"{opening.Id}/ObliqueCut",opening.Id,
                    opening.OwnerId,role,
                    Rect(lo,z0,hi,z1),opening.Sources));
            }
            int index=0;
            foreach(var (min,max) in spans)
            {
                if(max-min<=1e-6) continue;
                double[] xs=new[]{min,max}.Concat(cutRegions.SelectMany(c=>
                    new[]{Math.Clamp(c.Min,min,max),Math.Clamp(c.Max,min,max)}))
                    .Distinct().Order().ToArray();
                double[] zs=new[]{component.ZRange.Start,component.ZRange.End}
                    .Concat(cutRegions.SelectMany(c=>new[]{c.Z0,c.Z1}))
                    .Distinct().Order().ToArray();
                for(int i=0;i<xs.Length-1;i++)
                for(int j=0;j<zs.Length-1;j++)
                {
                    double x0=xs[i],x1=xs[i+1],z0=zs[j],z1=zs[j+1];
                    if(x1-x0<=1e-6 || z1-z0<=1e-6) continue;
                    double x=(x0+x1)/2,z=(z0+z1)/2;
                    if(cutRegions.Any(c=>x>c.Min && x<c.Max && z>c.Z0 && z<c.Z1))
                        continue;
                    regions.Add(new MaterialRegion($"{component.Id}/ObliqueSection/{++index}",
                        component.Id,component.MaterialId,
                        new Footprint(Rect(x0,z0,x1,z1),[]),component.Sources));
                }
            }
            dimensions.Add(new ViewDimensionIntent($"{component.Id}/ObliqueWidth",
                new Point2Mm(outerInterval.Value.Min,component.ZRange.Start),
                new Point2Mm(outerInterval.Value.Max,component.ZRange.Start),
                outerInterval.Value.Max-outerInterval.Value.Min));
        }
        return new(true,new ViewProfile(plane.Id,ViewPurpose.Section,regions,voids,dimensions,
            analyticRegions,analyticVoids),[]);

        (double Min,double Max)? LineRectangle(Rectangle2Mm r)
        {
            double min=double.NegativeInfinity,max=double.PositiveInfinity;
            if(!Slab(plane.Point.X,tx,r.MinX,r.MaxX,ref min,ref max) ||
                !Slab(plane.Point.Y,ty,r.MinY,r.MaxY,ref min,ref max) || max<=min)
                return null;
            return(min,max);
        }
        static bool Slab(double point,double direction,double lo,double hi,
            ref double min,ref double max)
        {
            if(direction==0) return point>lo && point<hi;
            double a=(lo-point)/direction,b=(hi-point)/direction;
            if(a>b)(a,b)=(b,a);
            min=Math.Max(min,a);max=Math.Min(max,b);
            return max>min;
        }
    }

    public static OperationResult<ViewProfile> BuildSection(GeometryModel model,SectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Mode != SectionMode.TrueSection)
            return Fail("SECTION_MODE_UNSUPPORTED",null,"TrueSection",definition.Mode.ToString());
        var regions=new List<MaterialRegion>();
        var voids=new List<ProjectedVoid>();
        var dimensions=new List<ViewDimensionIntent>();

        foreach(var component in model.Components)
        {
            if (component.Occupied is not ExtrusionSolid)
            {
                if(component.Occupied is DifferenceSolid difference && difference.Occupied is ExtrusionSolid extrusion &&
                    difference.Cuts.Length==1 && difference.Cuts[0] is NeckSeatCutSolid seat && seat.Kind==NeckSeatKind.Step &&
                    seat.ZRange.End==component.ZRange.End && seat.ZRange.Start>component.ZRange.Start &&
                    TryRectangle(component.Footprint.Outer,out var seatOuter))
                {
                    var expanded=new Rectangle2Mm(seat.Opening.MinX-seat.ExpansionMm,seat.Opening.MinY-seat.ExpansionMm,
                        seat.Opening.MaxX+seat.ExpansionMm,seat.Opening.MaxY+seat.ExpansionMm);
                    if(expanded.MinX<=seatOuter.MinX || expanded.MaxX>=seatOuter.MaxX ||
                        expanded.MinY<=seatOuter.MinY || expanded.MaxY>=seatOuter.MaxY)
                        return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),"seat inside neck outer boundary",component.Id);
                    var upper=new Footprint(component.Footprint.Outer,[new Polygon2Mm(
                        Rect(expanded.MinX,expanded.MinY,expanded.MaxX,expanded.MaxY).Vertices.Reverse())]);
                    var lowerZ=new Interval1D(component.ZRange.Start,seat.ZRange.Start);
                    ComponentGeometry Part(string suffix,Footprint fp,Interval1D range)=>new(component.Id+suffix,component.Kind,
                        component.MaterialId,fp,range,new ExtrusionSolid(fp,extrusion.Frame,range),[],[],component.Sources);
                    var projected=BuildSection(new GeometryModel([Part("/Lower",component.Footprint,lowerZ),
                        Part("/Seat",upper,seat.ZRange)],[]),definition);
                    if(!projected.Success)return projected;
                    regions.AddRange(projected.Value!.Regions.Select(r=>new MaterialRegion(r.Id,component.Id,
                        r.MaterialId,r.Footprint,r.Sources)));
                    continue;
                }
                if (component.Occupied is HaunchSolid haunch)
                {
                    double horizontalHalf=(definition.Axis==SectionAxis.AlongY?haunch.ChamberLengthMm:haunch.ChamberWidthMm)/2;
                    double transverseHalf=(definition.Axis==SectionAxis.AlongY?haunch.ChamberWidthMm:haunch.ChamberLengthMm)/2;
                    double haunchCut=Math.Abs(definition.CutCoordinateMm);
                    if(haunchCut>=transverseHalf) continue;
                    if(haunch.WidthMm<=0 || haunch.HeightMm<=0 || haunch.WidthMm>Math.Min(horizontalHalf,transverseHalf))
                        return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),"valid physical haunch",component.Id);
                    double z0=haunch.ZRange.Start,z1=haunch.ZRange.End;
                    double Width(double z)=>haunch.WidthMm*(haunch.Owner==HaunchOwner.Base
                        ? (z1-z)/(z1-z0) : (z-z0)/(z1-z0));
                    var levels=new List<double>{z0,z1};
                    double remaining=transverseHalf-haunchCut;
                    if(remaining<haunch.WidthMm)
                        levels.Add(haunch.Owner==HaunchOwner.Base
                            ? z1-remaining/haunch.WidthMm*(z1-z0) : z0+remaining/haunch.WidthMm*(z1-z0));
                    levels.Sort();
                    bool suppressLeft = false, suppressRight = false;
                    if (haunch.Owner == HaunchOwner.Roof)
                    {
                        var roofComp = model.Components.FirstOrDefault(c => c.Id == "Roof");
                        var neckComp = model.Components.FirstOrDefault(c => c.Id == "Neck");
                        Rectangle2Mm? neckOuterBounds = neckComp != null && TryRectangle(neckComp.Footprint.Outer, out var no)
                            ? no : null;
                        var holes = (roofComp?.Footprint.InnerLoops ?? [])
                            .Concat(neckComp?.Footprint.InnerLoops ?? [])
                            .Select(l => (MinX: l.Vertices.Min(p => p.X), MaxX: l.Vertices.Max(p => p.X),
                                          MinY: l.Vertices.Min(p => p.Y), MaxY: l.Vertices.Max(p => p.Y)));
                        foreach (var (hx0, hx1, hy0, hy1) in holes)
                        {
                            double cutTrans = definition.CutCoordinateMm;
                            if (definition.Axis == SectionAxis.AlongY)
                            {
                                double testMin = neckOuterBounds != null ? neckOuterBounds.MinX : hx0;
                                double testMax = neckOuterBounds != null ? neckOuterBounds.MaxX : hx1;
                                if (cutTrans >= testMin - 1e-6 && cutTrans <= testMax + 1e-6)
                                {
                                    if (hy0 <= -horizontalHalf + haunch.WidthMm + 1e-6) suppressLeft = true;
                                    if (hy1 >= horizontalHalf - haunch.WidthMm - 1e-6) suppressRight = true;
                                }
                            }
                            else
                            {
                                double testMin = neckOuterBounds != null ? neckOuterBounds.MinY : hy0;
                                double testMax = neckOuterBounds != null ? neckOuterBounds.MaxY : hy1;
                                if (cutTrans >= testMin - 1e-6 && cutTrans <= testMax + 1e-6)
                                {
                                    if (hx0 <= -horizontalHalf + haunch.WidthMm + 1e-6) suppressLeft = true;
                                    if (hx1 >= horizontalHalf - haunch.WidthMm - 1e-6) suppressRight = true;
                                }
                            }
                        }
                    }
                    int piece=0;
                    for(int i=0;i<levels.Count-1;i++)
                    {
                        double lo=levels[i],hi=levels[i+1];
                        if(Width((lo+hi)/2)>=remaining)
                        {
                            if (suppressLeft && suppressRight) { }
                            else if (suppressLeft) Add(Rect(0,lo,horizontalHalf,hi));
                            else if (suppressRight) Add(Rect(-horizontalHalf,lo,0,hi));
                            else Add(Rect(-horizontalHalf,lo,horizontalHalf,hi));
                        }
                        else
                        {
                            double w0=Width(lo),w1=Width(hi);
                            if (!suppressLeft)
                                Add(new Polygon2Mm(new Point2Mm[]{new(-horizontalHalf,lo),new(-horizontalHalf+w0,lo),
                                    new(-horizontalHalf+w1,hi),new(-horizontalHalf,hi)}.Distinct()));
                            if (!suppressRight)
                                Add(new Polygon2Mm(new Point2Mm[]{new(horizontalHalf-w0,lo),new(horizontalHalf,lo),
                                    new(horizontalHalf,hi),new(horizontalHalf-w1,hi)}.Distinct()));
                        }
                    }
                    void Add(Polygon2Mm polygon)=>regions.Add(new MaterialRegion($"{component.Id}/HaunchSection/{++piece}",
                        component.Id,component.MaterialId,new Footprint(polygon,[]),component.Sources));
                    continue;
                }
                return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),"rectangular extrusion",component.Id);
            }
            if (!TryRectangle(component.Footprint.Outer,out var outer))
                return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),"axis-aligned rectangle",component.Id);
            if (component.Footprint.InnerLoops.Length>1 || component.Footprint.InnerLoops.Any(h=>!TryRectangle(h,out _)))
                return Fail("SECTION_COMPONENT_UNSUPPORTED",component.Sources.FirstOrDefault(),"zero or one rectangular inner loop",component.Id);

            double transverseMin=definition.Axis==SectionAxis.AlongY?outer.MinX:outer.MinY;
            double transverseMax=definition.Axis==SectionAxis.AlongY?outer.MaxX:outer.MaxY;
            double cut=definition.CutCoordinateMm;
            if (cut<=transverseMin || cut>=transverseMax) continue; // Tangency has zero section area.
            double horizontalMin=definition.Axis==SectionAxis.AlongY?outer.MinY:outer.MinX;
            double horizontalMax=definition.Axis==SectionAxis.AlongY?outer.MaxY:outer.MaxX;
            var horizontal=new List<(double Min,double Max)>{(horizontalMin,horizontalMax)};
            if (component.Footprint.InnerLoops.Length==1)
            {
                TryRectangle(component.Footprint.InnerLoops[0],out var hole);
                double holeTransverseMin=definition.Axis==SectionAxis.AlongY?hole.MinX:hole.MinY;
                double holeTransverseMax=definition.Axis==SectionAxis.AlongY?hole.MaxX:hole.MaxY;
                if (cut >= holeTransverseMin - 1e-6 && cut <= holeTransverseMax + 1e-6)
                {
                    double holeHorizontalMin=definition.Axis==SectionAxis.AlongY?hole.MinY:hole.MinX;
                    double holeHorizontalMax=definition.Axis==SectionAxis.AlongY?hole.MaxY:hole.MaxX;
                    horizontal.Clear();
                    if (holeHorizontalMin>horizontalMin) horizontal.Add((horizontalMin,holeHorizontalMin));
                    if (holeHorizontalMax<horizontalMax) horizontal.Add((holeHorizontalMax,horizontalMax));
                }
            }

            var ownerOpenings=model.Openings.Where(o=>o.OwnerId==component.Id).ToArray();
            var roundOpenings=ownerOpenings.Where(o=>o.Kind==OpeningKind.Round).ToArray();
            var boxOpenings=ownerOpenings.Where(o=>o.Kind==OpeningKind.Box && o.WallFrame is not null).ToArray();
            var openTopOpenings=ownerOpenings.Where(o=>o.Kind==OpeningKind.OpenTop).ToArray();
            var unsupported=ownerOpenings.FirstOrDefault(o=>o.Kind is not (OpeningKind.Round or OpeningKind.Box or OpeningKind.OpenTop));
            if(unsupported is not null)
                return Fail("SECTION_OPENING_UNSUPPORTED",unsupported.Sources.FirstOrDefault(),
                    "round, box, or open-top wall opening",unsupported.Id);
            var concreteCuts=new List<(double Min,double Max)>();
            foreach(var opening in roundOpenings)
            {
                if (opening.WallFrame is null || opening.Inner is not CircleProfile inner ||
                    opening.PipeOuter is not CircleProfile pipeOuter || opening.ConcreteCutout is not CircleProfile cutout)
                    return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),"T14.a round wall opening",opening.Id);
                bool longitudinal=definition.Axis==SectionAxis.AlongY
                    ? opening.WallFrame.WallId is WallId.T1 or WallId.T3
                    : opening.WallFrame.WallId is WallId.T2 or WallId.T4;
                if (!longitudinal)
                    return Fail("SECTION_MODE_UNSUPPORTED",opening.Sources.FirstOrDefault(),"longitudinal section through pipe axis",definition.Id);
                var center=opening.WallFrame.ToGlobal(cutout.Center.X,0,cutout.Center.Y);
                double delta=definition.Axis==SectionAxis.AlongY?cut-center.X:cut-center.Y;
                AddBand(opening,ProjectedVoidRole.Inner,inner,delta);
                AddBand(opening,ProjectedVoidRole.PipeOuter,pipeOuter,delta);
                AddBand(opening,ProjectedVoidRole.ConcreteCutout,cutout,delta);
                double? q=HalfChord(cutout.RadiusMm,delta);
                if (q.HasValue) concreteCuts.Add((cutout.Center.Y-q.Value,cutout.Center.Y+q.Value));
                // Pipe material is the outer chord minus the inner chord, independently of mortar.
                double? outerChord=HalfChord(pipeOuter.RadiusMm,delta);
                double? innerChord=HalfChord(inner.RadiusMm,delta);
                if (outerChord.HasValue)
                {
                    if (innerChord.HasValue)
                    {
                        PipeBand(pipeOuter.Center.Y-outerChord.Value,inner.Center.Y-innerChord.Value,"Lower");
                        PipeBand(inner.Center.Y+innerChord.Value,pipeOuter.Center.Y+outerChord.Value,"Upper");
                    }
                    else PipeBand(pipeOuter.Center.Y-outerChord.Value,pipeOuter.Center.Y+outerChord.Value,"Whole");
                }
                void PipeBand(double bottom,double top,string part)
                {
                    if (top<=bottom) return;
                    regions.Add(new MaterialRegion($"{opening.Id}/PipeSection/{part}",opening.OwnerId,"Pipe",
                        new Footprint(Rect(horizontalMin,bottom,horizontalMax,top),[]),opening.Sources));
                }
            }
            foreach(var opening in boxOpenings)
            {
                if (opening.WallFrame is null || opening.Inner is not MultiCellProfile inner ||
                    opening.PipeOuter is not RectangleProfile pipeOuter ||
                    opening.ConcreteCutout is not RectangleProfile cutout)
                    return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),"typed box wall opening",opening.Id);
                bool longitudinal=definition.Axis==SectionAxis.AlongY
                    ? opening.WallFrame.WallId is WallId.T1 or WallId.T3
                    : opening.WallFrame.WallId is WallId.T2 or WallId.T4;
                if (!longitudinal)
                    return Fail("SECTION_MODE_UNSUPPORTED",opening.Sources.FirstOrDefault(),"longitudinal section through box pipe",definition.Id);
                var sample=definition.Axis==SectionAxis.AlongY
                    ? new Point3Mm(definition.CutCoordinateMm,opening.WallFrame.Origin.Y,0)
                    : new Point3Mm(opening.WallFrame.Origin.X,definition.CutCoordinateMm,0);
                double u=opening.WallFrame.ToLocal(sample).U;
                var innerBands=new List<(double Min,double Max)>();
                foreach(var cell in inner.Cells)
                    if (PolygonBand(cell.Polygon,u) is { } band)
                    {
                        AddBoxBand(opening,ProjectedVoidRole.Inner,band);
                        innerBands.Add(band);
                    }
                if (u>pipeOuter.Rectangle.MinX && u<pipeOuter.Rectangle.MaxX)
                {
                    AddBoxBand(opening,ProjectedVoidRole.PipeOuter,(pipeOuter.Rectangle.MinY,pipeOuter.Rectangle.MaxY));
                    double pipeStart=pipeOuter.Rectangle.MinY, pipeEnd=pipeOuter.Rectangle.MaxY;
                    var owner=model.Components.Single(c=>c.Id==opening.OwnerId);
                    TryRectangle(owner.Footprint.Outer,out var bounds);
                    double pipeHorizontalMin=definition.Axis==SectionAxis.AlongY?bounds.MinY:bounds.MinX;
                    double pipeHorizontalMax=definition.Axis==SectionAxis.AlongY?bounds.MaxY:bounds.MaxX;
                    int bandIndex=0;
                    void PipeMaterial(double z0,double z1)
                    {
                        if(z1<=z0) return;
                        regions.Add(new MaterialRegion($"{opening.Id}/PipeSection/{++bandIndex}",opening.Id,
                            "Pipe",new Footprint(Rect(pipeHorizontalMin,z0,pipeHorizontalMax,z1),[]),opening.Sources));
                    }
                    foreach(var band in innerBands.OrderBy(b=>b.Min))
                    { PipeMaterial(pipeStart,Math.Min(band.Min,pipeEnd)); pipeStart=Math.Max(pipeStart,band.Max); }
                    PipeMaterial(pipeStart,pipeEnd);
                }
                if (u>cutout.Rectangle.MinX && u<cutout.Rectangle.MaxX)
                {
                    var band=(cutout.Rectangle.MinY,cutout.Rectangle.MaxY);
                    concreteCuts.Add(band);
                    AddBoxBand(opening,ProjectedVoidRole.ConcreteCutout,band);
                }
            }
            foreach(var opening in openTopOpenings)
            {
                if(opening.WallFrame is null || opening.ConcreteCutout is not OpenTopProfile cutout)
                    return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),
                        "open-top wall opening",opening.Id);
                bool longitudinal=definition.Axis==SectionAxis.AlongY
                    ? opening.WallFrame.WallId is WallId.T1 or WallId.T3
                    : opening.WallFrame.WallId is WallId.T2 or WallId.T4;
                if(!longitudinal)
                    return Fail("SECTION_MODE_UNSUPPORTED",opening.Sources.FirstOrDefault(),
                        "longitudinal section through open-top opening",definition.Id);
                var sample=definition.Axis==SectionAxis.AlongY
                    ? new Point3Mm(definition.CutCoordinateMm,opening.WallFrame.Origin.Y,0)
                    : new Point3Mm(opening.WallFrame.Origin.X,definition.CutCoordinateMm,0);
                double u=opening.WallFrame.ToLocal(sample).U;
                if(PolygonBand(cutout.Polygon,u) is not { } band) continue;
                concreteCuts.Add(band);
                var owner=model.Components.Single(c=>c.Id==opening.OwnerId);
                TryRectangle(owner.Footprint.Outer,out var bounds);
                double min=definition.Axis==SectionAxis.AlongY?bounds.MinY:bounds.MinX;
                double max=definition.Axis==SectionAxis.AlongY?bounds.MaxY:bounds.MaxX;
                voids.Add(new ProjectedVoid($"{opening.Id}/ConcreteCutout",opening.Id,opening.OwnerId,
                    ProjectedVoidRole.ConcreteCutout,Rect(min,band.Min,max,band.Max),opening.Sources));
            }

            int index=0;
            var mergedCuts=new List<(double Min,double Max)>();
            foreach(var band in concreteCuts.OrderBy(b=>b.Min).ThenBy(b=>b.Max))
            {
                double z0=Math.Max(component.ZRange.Start,band.Min);
                double z1=Math.Min(component.ZRange.End,band.Max);
                if(z1<=z0) continue;
                if(mergedCuts.Count>0 && z0<=mergedCuts[^1].Max)
                    mergedCuts[^1]=(mergedCuts[^1].Min,Math.Max(mergedCuts[^1].Max,z1));
                else mergedCuts.Add((z0,z1));
            }
            foreach(var (min,max) in horizontal)
            {
                double cursor=component.ZRange.Start;
                foreach(var band in mergedCuts)
                { AddMaterial(min,max,cursor,band.Min); cursor=Math.Max(cursor,band.Max); }
                AddMaterial(min,max,cursor,component.ZRange.End);
            }

            dimensions.Add(new ViewDimensionIntent($"{component.Id}/Width",new(horizontalMin,component.ZRange.Start),
                new(horizontalMax,component.ZRange.Start),horizontalMax-horizontalMin));
            dimensions.Add(new ViewDimensionIntent($"{component.Id}/Height",new(horizontalMin,component.ZRange.Start),
                new(horizontalMin,component.ZRange.End),component.ZRange.Length));
            if (component.Kind==ComponentKind.Wall && component.Id!="Wall/Mortar")
                dimensions.Add(new ViewDimensionIntent($"{component.Id}/Thickness",new(horizontalMin,component.ZRange.Start),
                    new(horizontalMax,component.ZRange.Start),horizontalMax-horizontalMin));

            void AddMaterial(double min,double max,double z0,double z1)
            {
                if (max<=min || z1<=z0) return;
                var fp=new Footprint(Rect(min,z0,max,z1),[]);
                regions.Add(new MaterialRegion($"{component.Id}/Section/{++index}",component.Id,component.MaterialId,fp,component.Sources));
            }
        }

        foreach(var opening in model.Openings.Where(o=>o.Kind==OpeningKind.Box && o.WallFrame is null))
        {
            if (opening.ConcreteCutout is not RectangleProfile rectangle)
                return Fail("SECTION_OPENING_UNSUPPORTED",opening.Sources.FirstOrDefault(),"rectangular roof opening",opening.Id);
            var rect=rectangle.Rectangle;
            double transverseMin=definition.Axis==SectionAxis.AlongY?rect.MinX:rect.MinY;
            double transverseMax=definition.Axis==SectionAxis.AlongY?rect.MaxX:rect.MaxY;
            if (definition.CutCoordinateMm<=transverseMin || definition.CutCoordinateMm>=transverseMax) continue;
            double min=definition.Axis==SectionAxis.AlongY?rect.MinY:rect.MinX;
            double max=definition.Axis==SectionAxis.AlongY?rect.MaxY:rect.MaxX;
            voids.Add(new ProjectedVoid(opening.Id,opening.Id,opening.OwnerId,ProjectedVoidRole.RoofOpening,
                Rect(min,opening.ZRange.Start,max,opening.ZRange.End),opening.Sources));
            dimensions.Add(new ViewDimensionIntent($"{opening.Id}/Width",new(min,opening.ZRange.Start),
                new(max,opening.ZRange.Start),max-min));
        }

        return new(true,new ViewProfile(definition.Id,ViewPurpose.Section,regions,voids,dimensions),[]);

        void AddBand(OpeningProfile opening,ProjectedVoidRole role,CircleProfile circle,double delta)
        {
            double? q=HalfChord(circle.RadiusMm,delta);
            if (!q.HasValue) return;
            var owner=model.Components.Single(c=>c.Id==opening.OwnerId);
            TryRectangle(owner.Footprint.Outer,out var bounds);
            double min=definition.Axis==SectionAxis.AlongY?bounds.MinY:bounds.MinX;
            double max=definition.Axis==SectionAxis.AlongY?bounds.MaxY:bounds.MaxX;
            voids.Add(new ProjectedVoid($"{opening.Id}/{role}",opening.Id,opening.OwnerId,role,
                Rect(min,circle.Center.Y-q.Value,max,circle.Center.Y+q.Value),opening.Sources));
        }
        void AddBoxBand(OpeningProfile opening,ProjectedVoidRole role,(double Min,double Max) band)
        {
            var owner=model.Components.Single(c=>c.Id==opening.OwnerId);
            TryRectangle(owner.Footprint.Outer,out var bounds);
            double min=definition.Axis==SectionAxis.AlongY?bounds.MinY:bounds.MinX;
            double max=definition.Axis==SectionAxis.AlongY?bounds.MaxY:bounds.MaxX;
            voids.Add(new ProjectedVoid($"{opening.Id}/{role}",opening.Id,opening.OwnerId,role,
                Rect(min,band.Min,max,band.Max),opening.Sources));
        }
    }

    private static (double Min,double Max)? PolygonBand(Polygon2Mm polygon,double u)
    {
        var vertices=polygon.Vertices;
        var crossings=new List<double>();
        for(int i=0;i<vertices.Length;i++)
        {
            var a=vertices[i]; var b=vertices[(i+1)%vertices.Length];
            if (a.X==b.X) continue;
            if (u>Math.Min(a.X,b.X) && u<Math.Max(a.X,b.X))
                crossings.Add(a.Y+(u-a.X)*(b.Y-a.Y)/(b.X-a.X));
        }
        if(crossings.Count<2) return null;
        return (crossings.Min(),crossings.Max());
    }

    private static double? HalfChord(double radius,double delta)
    {
        if (Math.Abs(delta)>=radius) return null;
        return Math.Sqrt((radius-Math.Abs(delta))*(radius+Math.Abs(delta)));
    }

    private static bool TryOpeningPlanRectangle(OpeningProfile opening,
        out Rectangle2Mm rectangle,out ProjectedVoidRole role)
    {
        rectangle=null!;
        role=ProjectedVoidRole.ConcreteCutout;
        if(opening.WallFrame is null)
        {
            if(opening.Kind!=OpeningKind.Box ||
                opening.ConcreteCutout is not RectangleProfile roof) return false;
            rectangle=roof.Rectangle;
            role=ProjectedVoidRole.RoofOpening;
            return true;
        }
        Rectangle2Mm local;
        if(opening.ConcreteCutout is RectangleProfile box) local=box.Rectangle;
        else if(opening.ConcreteCutout is OpenTopProfile openTop &&
            TryRectangle(openTop.Polygon,out var openRect)) local=openRect;
        else return false;
        var wall=opening.WallFrame;
        var points=new[]
        {
            wall.ToGlobal(local.MinX,opening.ExtrusionRange.Start,opening.ZRange.Start),
            wall.ToGlobal(local.MinX,opening.ExtrusionRange.End,opening.ZRange.Start),
            wall.ToGlobal(local.MaxX,opening.ExtrusionRange.Start,opening.ZRange.Start),
            wall.ToGlobal(local.MaxX,opening.ExtrusionRange.End,opening.ZRange.Start)
        };
        double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X);
        double y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
        if(x1-x0<=1e-6 || y1-y0<=1e-6 || points.Any(p=>
            (Math.Abs(p.X-x0)>1e-6 && Math.Abs(p.X-x1)>1e-6) ||
            (Math.Abs(p.Y-y0)>1e-6 && Math.Abs(p.Y-y1)>1e-6))) return false;
        rectangle=new Rectangle2Mm(x0,y0,x1,y1);
        return true;
    }

    private static bool TryRectangle(Polygon2Mm polygon,out Rectangle2Mm rect)
    {
        var points=polygon.Vertices;
        double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X),y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
        if (points.Length!=4 || points.Any(p=>(p.X!=x0 && p.X!=x1)||(p.Y!=y0 && p.Y!=y1)))
        {
            rect=null!;
            return false;
        }
        rect=new Rectangle2Mm(x0,y0,x1,y1);
        return true;
    }

    private static void AddRoundVoids(string id,OpeningProfile opening,ProjectedVoidRole role,
        EllipticSectionBand band,double hostBottom,double hostTop,
        List<AnalyticSectionVoid> voids)
    {
        var parts=SplitAtHostHeights(band,hostBottom,hostTop);
        for(int i=0;i<parts.Count;i++)
        {
            var part=parts[i];
            double mid=(part.StationStartMm+part.StationEndMm)/2;
            if(part.Lower(mid)>=hostTop || part.Upper(mid)<=hostBottom) continue;
            double? lower=part.Lower(mid)<hostBottom?hostBottom:null;
            double? upper=part.Upper(mid)>hostTop?hostTop:null;
            voids.Add(new AnalyticSectionVoid(parts.Count==1?id:$"{id}/{i}",
                opening.Id,opening.OwnerId,role,part,lower,upper,opening.Sources));
        }
    }

    private static void AddRoundMaterial(string id,ComponentGeometry component,
        EllipticSectionBand band,List<MaterialRegion> regions,
        List<AnalyticSectionRegion> analyticRegions)
    {
        double bottom=component.ZRange.Start,top=component.ZRange.End;
        var parts=SplitAtHostHeights(band,bottom,top);
        for(int i=0;i<parts.Count;i++)
        {
            var part=parts[i];
            string suffix=parts.Count==1?"":$"/{i}";
            double mid=(part.StationStartMm+part.StationEndMm)/2;
            if(part.Lower(mid)>=top || part.Upper(mid)<=bottom)
            {
                regions.Add(new MaterialRegion($"{id}/Whole{suffix}",component.Id,
                    component.MaterialId,new Footprint(Rect(part.StationStartMm,bottom,
                        part.StationEndMm,top),[]),component.Sources));
                continue;
            }
            if(part.Upper(mid)<top)
                analyticRegions.Add(new AnalyticSectionRegion($"{id}/Above{suffix}",
                    component.Id,component.MaterialId,AnalyticMaterialSide.Above,
                    part,bottom,top,component.Sources));
            if(part.Lower(mid)>bottom)
                analyticRegions.Add(new AnalyticSectionRegion($"{id}/Below{suffix}",
                    component.Id,component.MaterialId,AnalyticMaterialSide.Below,
                    part,bottom,top,component.Sources));
        }
    }

    private static List<EllipticSectionBand> SplitAtHostHeights(EllipticSectionBand band,
        double bottom,double top)
    {
        var stations=new List<double>{band.StationStartMm,band.StationEndMm};
        foreach(double height in new[]{bottom,top})
        {
            double normalized=(height-band.CenterHeightMm)/band.HeightRadiusMm;
            if(Math.Abs(normalized)>=1) continue;
            double offset=band.StationRadiusMm*Math.Sqrt(1-normalized*normalized);
            foreach(double station in new[]{band.CenterStationMm-offset,
                band.CenterStationMm+offset})
                if(station>band.StationStartMm+1e-8 &&
                    station<band.StationEndMm-1e-8)
                    stations.Add(station);
        }
        stations.Sort();
        var result=new List<EllipticSectionBand>();
        for(int i=1;i<stations.Count;i++)
            if(stations[i]-stations[i-1]>1e-8)
                result.Add(new EllipticSectionBand(stations[i-1],stations[i],
                    band.CenterStationMm,band.CenterHeightMm,
                    band.StationRadiusMm,band.HeightRadiusMm));
        return result;
    }

    private static Polygon2Mm Rect(double x0,double y0,double x1,double y1) =>
        new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);

    private static OperationResult<ViewProfile> Fail(string code,SourceRef? source,string expected,string actual) =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,source,expected,actual,
            "Section projection is unsupported for this model or mode.")]);
}
