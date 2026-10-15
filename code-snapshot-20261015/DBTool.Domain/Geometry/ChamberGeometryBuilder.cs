using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Basic S1 chamber geometry in local millimetres; no CAD or quantity calculation.</summary>
public static class ChamberGeometryBuilder
{
    private static readonly Vector3 X = new(1,0,0);
    private static readonly Vector3 Y = new(0,1,0);
    private static readonly Vector3 Z = new(0,0,1);
    private static readonly Frame3Mm PlanFrame = new(new Point3Mm(0,0,0),X,Y,Z);

    public static OperationResult<GeometryModel> Build(ValidatedManholeSpec spec, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);
        var input = spec.Input;
        var diagnostics = new List<Diagnostic>();
        var purchasedCovers = new List<PurchasedCoverPlacement>();

        bool s2=policy.PolicyId is "S2Geometry-1" or "S2Rebar-1";
        if (policy.PolicyId is not ("S1-Analytic-1" or "S2Geometry-1" or "S2Rebar-1") || policy.Version != spec.RuleVersion)
            diagnostics.Add(Block("GEOMETRY_POLICY_UNSUPPORTED",DiagnosticScope.Manhole,null,"matching S1 or S2 policy",policy.Version,"Geometry policy/version mismatch."));
        if (input.Profile == Profile.GaThamThuNapRoi && !s2 || !s2 && (input.BaseHaunch > 0 || input.TopHaunch > 0 || input.NeckHeight > 0))
            diagnostics.Add(Block("GEOMETRY_UNSUPPORTED",DiagnosticScope.Manhole,null,"basic chamber without haunch, neck or catch basin",input.Profile.ToString(),"Requested geometry is outside T11."));
        if(s2 && (input.BaseHaunch>0 && input.BaseHaunchSpec is null || input.TopHaunch>0 && input.TopHaunchSpec is null))
            diagnostics.Add(Block("HAUNCH_SPEC_REQUIRED",DiagnosticScope.Manhole,null,"typed haunch spec",$"{input.BaseHaunch}/{input.TopHaunch}","Active haunch must have a typed spec."));
        if(s2 && input.NeckHeight>0 && (input.NeckSpec is null || input.NeckSeatSpec is null))
            diagnostics.Add(Block("U_NECK_SEAT",DiagnosticScope.Manhole,null,"explicit neck dimensions and seat datum",input.NeckHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),"Neck seat mapping is unresolved."));
        if (input.BeddingMortarMm >= input.H)
            diagnostics.Add(Block("GEOMETRY_HEIGHT_INVALID",DiagnosticScope.Manhole,null,"0 <= bedding mortar < H",input.BeddingMortarMm.ToString(System.Globalization.CultureInfo.InvariantCulture),"Mortar would remove the wall height."));
        if (input.RoofOpening.MinX < -input.L/2 || input.RoofOpening.MaxX > input.L/2 ||
            input.RoofOpening.MinY < -input.B/2 || input.RoofOpening.MaxY > input.B/2)
            diagnostics.Add(Block("GEOMETRY_CONTAINMENT",DiagnosticScope.Component,"Roof","opening inside chamber",$"[{input.RoofOpening.MinX},{input.RoofOpening.MinY},{input.RoofOpening.MaxX},{input.RoofOpening.MaxY}]","Roof opening exceeds chamber clear bounds."));
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block)) return new(false,null,diagnostics);

        double halfX=input.L/2, halfY=input.B/2, t=input.WallThicknessMm;
        double outerX=halfX+t, outerY=halfY+t;
        double baseX=outerX+input.BaseExpansionMm, baseY=outerY+input.BaseExpansionMm;
        double padX=baseX+input.PadExpansionMm, padY=baseY+input.PadExpansionMm;
        double f=input.BaseThicknessMm, p=input.PadThicknessMm, h=input.H, r=input.RoofThicknessMm;
        double wallTop=input.Profile == Profile.GaThamLienKhoi ? h : h-input.BeddingMortarMm;
        var components=new List<ComponentGeometry>();
        // PadThickness=0 is the explicitly absent bedding feature (L06/G10),
        // not a zero-depth solid. The concrete base datum remains [-f, 0].
        if (p > 0)
            components.Add(Slab("Pad",ComponentKind.Pad,"Bedding",-padX,-padY,padX,padY,-f-p,-f,spec.Sources));
        components.Add(Slab("Base",ComponentKind.Base,"Concrete",-baseX,-baseY,baseX,baseY,-f,0,spec.Sources));

        // T1/T3 own the corners. T2/T4 terminate at the inner Y faces.
        var strips = new (string Id,double X0,double Y0,double X1,double Y1)[]
        {
            ("Wall/T1",-outerX,halfY,outerX,outerY),
            ("Wall/T2",halfX,-halfY,outerX,halfY),
            ("Wall/T3",-outerX,-outerY,outerX,-halfY),
            ("Wall/T4",-outerX,-halfY,-halfX,halfY)
        };
        foreach(var strip in strips)
            components.Add(Wall(strip.Id,"Concrete",strip.X0,strip.Y0,strip.X1,strip.Y1,0,wallTop,spec.Sources));

        if (input.Profile != Profile.GaThamLienKhoi && input.BeddingMortarMm > 0)
        {
            // A separate ring owns the mortar band; concrete walls stop at H-m_b.
            var mortarFootprint=Ring(-outerX,-outerY,outerX,outerY,-halfX,-halfY,halfX,halfY);
            var mortarZ=new Interval1D(wallTop,h);
            components.Add(new ComponentGeometry("Wall/Mortar",ComponentKind.Wall,"BeddingMortar",mortarFootprint,mortarZ,
                new ExtrusionSolid(mortarFootprint,PlanFrame,mortarZ),[],HorizontalPatches("Wall/Mortar",mortarFootprint,mortarZ,spec.Sources),spec.Sources));
        }

        var roofRect=input.RoofOpening;
        var roofFootprint=Ring(-outerX,-outerY,outerX,outerY,roofRect.MinX,roofRect.MinY,roofRect.MaxX,roofRect.MaxY);
        var roofZ=new Interval1D(h,h+r);
        var holeFrame=new Frame3Mm(new Point3Mm(0,0,h),X,Y,Z);
        var holeProfile=new RectangleProfile(roofRect);
        var hole=new OpeningProfile("Roof/Hole","Roof",OpeningKind.Box,holeProfile,null,holeProfile,null,holeFrame,roofZ,new Interval1D(0,r),spec.Sources);
        // Occupied remains the full blank; the registered void removes the hole exactly once.
        var roofBlank=Rect(-outerX,-outerY,outerX,outerY);
        components.Add(new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",roofFootprint,roofZ,
            new ExtrusionSolid(roofBlank,PlanFrame,roofZ),[hole],HorizontalPatches("Roof",roofFootprint,roofZ,spec.Sources),spec.Sources));

        if(s2)
        {
            foreach(var haunch in new[]{input.BaseHaunchSpec,input.TopHaunchSpec}.Where(x=>x is not null))
            {
                var built=HaunchProfileBuilder.Build(spec,haunch!,policy);
                if(!built.Success) return new(false,null,built.Diagnostics);
                components.AddRange(built.Value!.Components);
            }
            if(input.NeckSpec is not null)
            {
                if(input.NeckSeatSpec is null)
                    return new(false,null,[Block("U_NECK_SEAT",DiagnosticScope.Manhole,null,"explicit seat mapping","missing","Neck seat rule is unresolved.")]);
                var neck=NeckGeometryBuilder.Build(spec,input.NeckSpec,input.NeckSeatSpec,policy);
                if(!neck.Success) return new(false,null,neck.Diagnostics);
                var calculated=((RectangleProfile)neck.Value!.RoofOpening.Inner).Rectangle;
                if(calculated!=input.RoofOpening)
                    return new(false,null,[Block("NECK_ROOF_OPENING_MISMATCH",DiagnosticScope.Component,"Roof","matching typed neck hole",calculated.ToString(),"Typed neck and roof opening differ.")]);
                components.AddRange(neck.Value.Components);
                if(neck.Value.PurchasedCoverPlacement is { } purchase) purchasedCovers.Add(purchase);
                if(input.CoverPanelSpec is not null)
                {
                    var layout=CoverPanelBuilder.Build(neck.Value.SeatProfile,input.CoverPanelSpec,policy);
                    if(!layout.Success) return new(false,null,layout.Diagnostics);
                    foreach(var panel in layout.Value!.Panels)
                    {
                        var panelBounds=panel.Bounds;
                        var footprint=Rect(panelBounds.MinX,panelBounds.MinY,panelBounds.MaxX,panelBounds.MaxY);
                        var patch=new FacePatch($"{panel.Id}/Top",panel.Id,FaceRole.Top,new RectangleProfile(panelBounds),
                            new Frame3Mm(new Point3Mm(0,0,panel.ZRange.End),X,Y,Z),new AreaMm2((panelBounds.MaxX-panelBounds.MinX)*(panelBounds.MaxY-panelBounds.MinY)),Z,null,panel.Sources);
                        components.Add(new ComponentGeometry(panel.Id,ComponentKind.Panel,"Concrete",footprint,panel.ZRange,
                            new ExtrusionSolid(footprint,PlanFrame,panel.ZRange),[],[patch],panel.Sources));
                    }
                }
            }
            else if(input.CoverPanelSpec is not null)
                return new(false,null,[Block("PANEL_SEAT_REQUIRED",DiagnosticScope.Manhole,null,"typed seat profile","missing","Panel layout requires explicit support geometry.")]);
            else if(input.PurchasedCoverSpec is not null)
            {
                var centre=new Point2Mm((roofRect.MinX+roofRect.MaxX)/2,(roofRect.MinY+roofRect.MaxY)/2);
                var cover=PurchasedCoverGeometryBuilder.Build(spec,centre,policy);
                if(!cover.Success) return new(false,null,cover.Diagnostics);
                purchasedCovers.Add(cover.Value!);
            }
            if(input.CollarSpec is not null)
            {
                if(input.NeckSeatSpec is null)
                    return new(false,null,[Block("U_NECK_SEAT",DiagnosticScope.Manhole,null,"explicit seat mapping","missing","Collar needs seat mapping.")]);
                var collar=CollarGeometryBuilder.Build(spec,input.NeckSeatSpec,input.CollarSpec,policy);
                if(!collar.Success) return new(false,null,collar.Diagnostics);
                components.Add(collar.Value!);
            }
        }

        foreach(var pipe in input.PipeConnections)
            diagnostics.Add(new Diagnostic("GEOMETRY_PIPE_DEFERRED",DiagnosticSeverity.Review,DiagnosticScope.Component,input.Id,$"Wall/{pipe.WallId}",
                pipe.SourceRefs.Values.FirstOrDefault(),"T14.a pipe cutout",pipe.CatalogCode,"Pipe opening is not calculated by the T11 basic chamber builder."));
        return new(true,new GeometryModel(components,[hole],purchasedCovers),diagnostics);

        Diagnostic Block(string code,DiagnosticScope scope,string? componentId,string expected,string actual,string message) =>
            new(code,DiagnosticSeverity.Block,scope,input.Id,componentId,
                input.SourceRefs.Values.FirstOrDefault() ?? spec.Sources.FirstOrDefault(),expected,actual,message);
    }

    private static ComponentGeometry Slab(string id,ComponentKind kind,string material,double x0,double y0,double x1,double y1,double z0,double z1,IEnumerable<SourceRef> sources)
    {
        var footprint=Rect(x0,y0,x1,y1);
        var range=new Interval1D(z0,z1);
        return new(id,kind,material,footprint,range,new ExtrusionSolid(footprint,PlanFrame,range),[],HorizontalPatches(id,footprint,range,sources),sources);
    }

    private static ComponentGeometry Wall(string id,string material,double x0,double y0,double x1,double y1,double z0,double z1,IEnumerable<SourceRef> sources)
    {
        var footprint=Rect(x0,y0,x1,y1);
        var range=new Interval1D(z0,z1);
        var patches=new List<FacePatch>();
        bool alongX=id is "Wall/T1" or "Wall/T3";
        double length=alongX?x1-x0:y1-y0, thickness=alongX?y1-y0:x1-x0;
        Vector3 outward=id switch {"Wall/T1"=>Y,"Wall/T2"=>X,"Wall/T3"=>new(0,-1,0),_=>new(-1,0,0)};
        Vector3 inward=new(-outward.X,-outward.Y,0);
        var tangent=alongX?X:Y;
        var verticalBoundary=new RectangleProfile(new Rectangle2Mm(0,0,length,range.Length));
        var innerOrigin=id switch {"Wall/T1"=>new Point3Mm(x0,y0,z0),"Wall/T2"=>new Point3Mm(x0,y0,z0),
            "Wall/T3"=>new Point3Mm(x0,y1,z0),_=>new Point3Mm(x1,y0,z0)};
        var outerOrigin=id switch {"Wall/T1"=>new Point3Mm(x0,y1,z0),"Wall/T2"=>new Point3Mm(x1,y0,z0),
            "Wall/T3"=>new Point3Mm(x0,y0,z0),_=>new Point3Mm(x0,y0,z0)};
        patches.Add(new FacePatch($"{id}/Inner",id,FaceRole.Inner,verticalBoundary,new Frame3Mm(innerOrigin,tangent,Z,inward),new AreaMm2(length*range.Length),inward,null,sources));
        patches.Add(new FacePatch($"{id}/Outer",id,FaceRole.Outer,verticalBoundary,new Frame3Mm(outerOrigin,tangent,Z,outward),new AreaMm2(length*range.Length),outward,null,sources));
        var horizontalBoundary=new RectangleProfile(new Rectangle2Mm(0,0,length,thickness));
        var longAxis=alongX?X:Y;
        var shortAxis=alongX?Y:X;
        patches.Add(new FacePatch($"{id}/Top",id,FaceRole.Top,horizontalBoundary,new Frame3Mm(new(x0,y0,z1),longAxis,shortAxis,Z),new AreaMm2(length*thickness),Z,null,sources));
        patches.Add(new FacePatch($"{id}/Bottom",id,FaceRole.Bottom,horizontalBoundary,new Frame3Mm(new(x0,y0,z0),longAxis,shortAxis,new(0,0,-1)),new AreaMm2(length*thickness),new(0,0,-1),"Base",sources));
        return new(id,ComponentKind.Wall,material,footprint,range,new ExtrusionSolid(footprint,PlanFrame,range),[],patches,sources);
    }

    private static FacePatch[] HorizontalPatches(string id,Footprint footprint,Interval1D range,IEnumerable<SourceRef> sources)
    {
        var points=footprint.Outer.Vertices;
        double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X),y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
        var rectangles=new List<Rectangle2Mm>();
        if (footprint.InnerLoops.IsEmpty) rectangles.Add(new Rectangle2Mm(x0,y0,x1,y1));
        else
        {
            var hole=footprint.InnerLoops[0].Vertices;
            double hx0=hole.Min(p=>p.X),hx1=hole.Max(p=>p.X),hy0=hole.Min(p=>p.Y),hy1=hole.Max(p=>p.Y);
            if (hy0>y0) rectangles.Add(new Rectangle2Mm(x0,y0,x1,hy0));
            if (hy1<y1) rectangles.Add(new Rectangle2Mm(x0,hy1,x1,y1));
            if (hx0>x0) rectangles.Add(new Rectangle2Mm(x0,hy0,hx0,hy1));
            if (hx1<x1) rectangles.Add(new Rectangle2Mm(hx1,hy0,x1,hy1));
        }
        var patches=new List<FacePatch>();
        for(int index=0;index<rectangles.Count;index++)
        {
            var rect=rectangles[index];
            var area=new AreaMm2((rect.MaxX-rect.MinX)*(rect.MaxY-rect.MinY));
            string suffix=rectangles.Count==1?"":$"/{index+1}";
            patches.Add(new FacePatch($"{id}/Top{suffix}",id,FaceRole.Top,new RectangleProfile(rect),new Frame3Mm(new(0,0,range.End),X,Y,Z),area,Z,null,sources));
            patches.Add(new FacePatch($"{id}/Bottom{suffix}",id,FaceRole.Bottom,new RectangleProfile(rect),new Frame3Mm(new(0,0,range.Start),X,Y,new(0,0,-1)),area,new(0,0,-1),null,sources));
        }
        return patches.ToArray();
    }

    private static Footprint Rect(double x0,double y0,double x1,double y1) => new(Outer(x0,y0,x1,y1),[]);
    private static Footprint Ring(double x0,double y0,double x1,double y1,double hx0,double hy0,double hx1,double hy1) =>
        new(Outer(x0,y0,x1,y1),[new Polygon2Mm([new(hx0,hy1),new(hx1,hy1),new(hx1,hy0),new(hx0,hy0)])]);
    private static Polygon2Mm Outer(double x0,double y0,double x1,double y1) =>
        new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
}
