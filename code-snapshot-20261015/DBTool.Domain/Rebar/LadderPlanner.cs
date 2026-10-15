using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public enum LadderOpeningPolicy { Reject, TruncateAtOpening, PublishedTruncateAtOpening }

public static class LadderPlanner
{
    public static OperationResult<RebarLayout> PlanApproved(LadderSpec spec,ValidatedManholeSpec input,
        GeometryModel geometry,CalculationPolicy policy,LadderOpeningPolicy openingPolicy,double openingCoverMm,
        double? topDatumMm=null)
    {
        if (openingPolicy==LadderOpeningPolicy.Reject) return PlanApproved(spec,input,geometry,policy);
        if (openingPolicy is not (LadderOpeningPolicy.TruncateAtOpening or LadderOpeningPolicy.PublishedTruncateAtOpening)) throw new ArgumentOutOfRangeException(nameof(openingPolicy));
        bool published=openingPolicy==LadderOpeningPolicy.PublishedTruncateAtOpening;
        if (!double.IsFinite(openingCoverMm) || openingCoverMm<0)
            return Block("LADDER_OPENING_COVER_REQUIRED","Explicit nonnegative opening cover is required.",spec.Sources.FirstOrDefault());
        if (!topDatumMm.HasValue || !double.IsFinite(topDatumMm.Value))
            return Block("LADDER_TOP_DATUM_REQUIRED","An explicit placement top datum is required for the first rung.",spec.Sources.FirstOrDefault());
        var legacy=LegacyWall(input);
        var neck=input.NeckSpec;
        if (neck is null) return Block("LADDER_NECK_REQUIRED","Ladder support must be derived from the neck opening.",spec.Sources.FirstOrDefault());
        var (cx,cy)=NeckPositionMap.Center(neck.Position,input.Input.L,input.Input.B,neck.InnerWidthMm,neck.InnerLengthMm);
        double tolerance=policy.TolerancePolicy.GeometryMm;
        var candidates=new List<WallId>();
        if (Math.Abs(cy+neck.InnerLengthMm/2-input.Input.B/2)<=tolerance) candidates.Add(WallId.T1);
        if (Math.Abs(cx+neck.InnerWidthMm/2-input.Input.L/2)<=tolerance) candidates.Add(WallId.T2);
        if (Math.Abs(cy-neck.InnerLengthMm/2+input.Input.B/2)<=tolerance) candidates.Add(WallId.T3);
        if (Math.Abs(cx-neck.InnerWidthMm/2+input.Input.L/2)<=tolerance) candidates.Add(WallId.T4);
        if (candidates.Count==0)
            candidates.Add(legacy);
        var ordered=candidates.OrderBy(w=>w==legacy?0:1).ThenBy(w=>w).ToArray();
        var withoutOpening=ordered.Where(w=>!geometry.Openings.Any(o=>o.OwnerId==$"Wall/{w}")).ToArray();
        if (withoutOpening.Length>0)
            return PlanApprovedOnWall(spec,input,geometry,policy,withoutOpening[0],true,openingCoverMm,topDatumMm,published);
        var results=ordered.Select(w=>PlanApprovedOnWall(spec,input,geometry,policy,w,true,openingCoverMm,topDatumMm,published)).ToArray();
        var failure=results.FirstOrDefault(r=>!r.Success);
        if (failure is not null) return failure;
        return results.OrderByDescending(r=>r.Value!.Occurrences.Length).First();
    }

    private static WallId LegacyWall(ValidatedManholeSpec input) => input.NeckSpec?.Position switch {
        NeckPosition.East or NeckPosition.NorthEast or NeckPosition.SouthEast=>WallId.T2,
        NeckPosition.South=>WallId.T3,NeckPosition.West or NeckPosition.NorthWest or NeckPosition.SouthWest=>WallId.T4,_=>WallId.T1};

    public static OperationResult<RebarLayout> PlanPlaced(LadderSpec spec,ValidatedManholeSpec input,GeometryModel geometry,CalculationPolicy policy)
    {
        var neck=input.NeckSpec;
        WallId wallId=neck?.Position switch {
            NeckPosition.East or NeckPosition.NorthEast or NeckPosition.SouthEast=>WallId.T2,
            NeckPosition.South=>WallId.T3,NeckPosition.West or NeckPosition.NorthWest or NeckPosition.SouthWest=>WallId.T4,
            _=>WallId.T1};
        var wall=geometry.Components.Single(x=>x.Id==$"Wall/{wallId}");
        var frame=WallFrame.Create(wallId,input.Input.L,input.Input.B);
        var (cx,cy)=neck is null?(0d,0d):NeckPositionMap.Center(neck.Position,input.Input.L,input.Input.B,neck.InnerWidthMm,neck.InnerLengthMm);
        double u=frame.ToLocal(new(cx,cy,0)).U;
        double first=input.Input.H+input.Input.NeckHeight-spec.TopOffsetMm;
        if(spec.PitchMm<=0) return Block("LADDER_PITCH_INVALID","Ladder pitch must be positive.",spec.Sources.FirstOrDefault());
        double ratio=(first-spec.MinZMm)/spec.PitchMm;
        if(!double.IsFinite(ratio) || ratio>100000) return Block("LADDER_COUNT_INVALID","Ladder count exceeds supported placement size.",spec.Sources.FirstOrDefault());
        int count=ratio<0?0:(int)Math.Floor(ratio+1e-9)+1;
        var bars=new List<BarOccurrence>();
        for(int index=0;index<count;index++)
        {
            double z=first-index*spec.PitchMm;
            Point3Mm At(double along,double normal,double height)=>frame.ToGlobal(u+along,normal,height);
            var points=new[]{At(-spec.WidthMm/2,0,z+spec.HookMm),At(-spec.WidthMm/2,0,z),
                At(-spec.WidthMm/2,-spec.LegHeightMm,z),At(spec.WidthMm/2,-spec.LegHeightMm,z),
                At(spec.WidthMm/2,0,z),At(spec.WidthMm/2,0,z+spec.HookMm)};
            var segments=points.Zip(points.Skip(1),(a,b)=>(PathSegment)new LineSegment3Mm(a,b)).ToArray();
            // Check every segment against all registered wall voids; an intersection is not silently skipped.
            foreach(var segment in segments)
            {
                double length=Length(segment);
                var straight=new BarOccurrence("ladder-collision-probe",null,wall.Id,wall.Kind,Layer.Bottom,Axis.X,spec.DiameterMm,
                    new BarPath([segment],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),length,length,LengthStatus.Known,
                    "LADDER-PLACEMENT-1B",spec.Sources);
                var collision=OpeningClipper.Clip(straight,geometry.Openings.Where(x=>x.OwnerId==wall.Id).ToArray(),new ClearancePolicy(0),geometry);
                if(!collision.Success) return new(false,null,collision.Diagnostics);
                if(collision.Value!.RemovedLengthMm>policy.TolerancePolicy.GeometryMm)
                    return Block("LADDER_OPENING_COLLISION","Selected ladder support intersects a registered pipe opening.",spec.Sources.FirstOrDefault());
            }
            var path=new BarPath(segments,false,HookPattern.Hook0M,0,false,MeasurementBasis.NominalPolyline);
            var fabrication=NominalFabricationLength.Calculate(path,spec.DiameterMm,smoothOrTie:true);
            bars.Add(new($"{input.Input.Id}/{wall.Id}/Ladder/{index:000}",null,wall.Id,wall.Kind,Layer.Bottom,
                wallId is WallId.T1 or WallId.T3?Axis.X:Axis.Y,spec.DiameterMm,path,segments.Sum(Length),fabrication,
                fabrication.HasValue?LengthStatus.Known:LengthStatus.NeedsRule,"LADDER-PLACEMENT-1B",spec.Sources.Concat(wall.Sources),RebarDirection.Supplementary,wall.Id,
                RebarSurface.Inner,"Ladder",MeasurementBasisKind.Nominal));
        }
        var review=new Diagnostic("LADDER_FABRICATION_FALLBACK",DiagnosticSeverity.Review,DiagnosticScope.Component,
            input.Input.Id,wall.Id,spec.Sources.FirstOrDefault(),"bend radius",null,
            "VHG nominal ladder dimensions remain the schedule basis. Internal fabrication uses owner-approved inner radius 1.25d; infeasible bends remain unresolved for review.");
        return new(true,new RebarLayout(bars,[review],CalculationScope.FullRebar,count==0?LayoutStatus.Disabled:bars.Any(b=>b.LengthStatus==LengthStatus.NeedsRule)?LayoutStatus.Partial:LayoutStatus.Complete),[review]);
    }

    public static OperationResult<RebarLayout> PlanApproved(LadderSpec spec,ValidatedManholeSpec input,GeometryModel geometry,CalculationPolicy policy) =>
        PlanApprovedOnWall(spec,input,geometry,policy,LegacyWall(input),false,0);

    private static OperationResult<RebarLayout> PlanApprovedOnWall(LadderSpec spec,ValidatedManholeSpec input,
        GeometryModel geometry,CalculationPolicy policy,WallId wallId,bool truncate,double openingCoverMm,
        double? topDatumMm=null,bool published=false)
    {
        var neck=input.NeckSpec;
        var wall=geometry.Components.Single(c=>c.Id==$"Wall/{wallId}");var frame=WallFrame.Create(wallId,input.Input.L,input.Input.B);
        var (cx,cy)=neck is null?(0d,0d):NeckPositionMap.Center(neck.Position,input.Input.L,input.Input.B,neck.InnerWidthMm,neck.InnerLengthMm);
        double u=frame.ToLocal(new(cx,cy,0)).U;
        double embed=100,projection=spec.LegHeightMm-embed;
        // Concrete support cover exists independently of whether either mesh is enabled.
        if(input.Input.MainRebar?.WallSupportCoverMm is not { } supportCover)
            return Block("LADDER_SUPPORT_COVER_REQUIRED","Wall support cover must be resolved independently of active meshes.",spec.Sources.FirstOrDefault());
        if(spec.PitchMm<=0) return Block("LADDER_PITCH_INVALID","Ladder pitch must be positive.",spec.Sources.FirstOrDefault());
        if(projection<=0 || embed+spec.DiameterMm/2d>input.Input.WallThicknessMm-supportCover)
            return Block("LADDER_SUPPORT_INVALID","Approved ladder embed does not fit wall thickness/cover.",spec.Sources.FirstOrDefault());
        var bars=new List<BarOccurrence>();var diagnostics=new List<Diagnostic>();
        int index=0;
        double first=(truncate?topDatumMm!.Value:input.Input.H)-(published?spec.TopOffsetMm:300);
        double minimum=published?Math.Max(spec.MinZMm,spec.PitchMm):spec.MinZMm;
        for(double z=first;z>=minimum;z-=spec.PitchMm,index++)
        {
            if(index>100000)return Block("LADDER_COUNT_INVALID","Excessive ladder count.",spec.Sources.FirstOrDefault());
            Point3Mm At(double along,double v,double height)=>frame.ToGlobal(u+along,v,height);
            var points=new[]{At(-spec.WidthMm/2,embed,z+spec.HookMm),At(-spec.WidthMm/2,embed,z),At(-spec.WidthMm/2,-projection,z),
                At(spec.WidthMm/2,-projection,z),At(spec.WidthMm/2,embed,z),At(spec.WidthMm/2,embed,z+spec.HookMm)};
            var segments=points.Zip(points.Skip(1),(a,b)=>(PathSegment)new LineSegment3Mm(a,b)).ToArray();
            var supports=new List<ComponentGeometry>();
            if(published)
            {
                // A wall-supported ladder can continue into the aligned neck. Verify its
                // two embedded hooks against the real concrete/mortar union at every Z break.
                var heights=geometry.Components.SelectMany(c=>new[]{c.ZRange.Start,c.ZRange.End})
                    .Where(h=>h>z && h<z+spec.HookMm).Append(z).Append(z+spec.HookMm).Distinct().Order().ToArray();
                var samples=heights.Concat(heights.Zip(heights.Skip(1),(a,b)=>(a+b)/2));
                foreach(double height in samples)
                foreach(double along in new[]{-spec.WidthMm/2,spec.WidthMm/2})
                {
                    var p=At(along,embed,height);var xy=new Point2Mm(p.X,p.Y);
                    var contact=geometry.Components.FirstOrDefault(c=>c.MaterialId is "Concrete" or "BeddingMortar" &&
                        height>=c.ZRange.Start-policy.TolerancePolicy.GeometryMm && height<=c.ZRange.End+policy.TolerancePolicy.GeometryMm &&
                        c.Footprint.Outer.Contains(xy) && !c.Footprint.InnerLoops.Any(h=>h.Contains(xy)));
                    if(contact is null)return Block("LADDER_EMBED_SUPPORT_MISSING","Published ladder hook has no physical supporting material.",spec.Sources.FirstOrDefault());
                    supports.Add(contact);
                }
            }
            bool hit=false;string? hitOpening=null;
            foreach(var segment in segments)
            {
                double length=Length(segment);
                var probe=new BarOccurrence("ladder-probe",null,wall.Id,wall.Kind,Layer.Bottom,Axis.X,spec.DiameterMm,
                    new BarPath([segment],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),length,length,LengthStatus.Known,"LADDER-APPROVED-1C",spec.Sources);
                if (!truncate)
                {
                    var clip=OpeningClipper.Clip(probe,geometry.Openings.Where(o=>o.OwnerId==wall.Id).ToArray(),new ClearancePolicy(0),geometry);
                    if(!clip.Success)return new(false,null,clip.Diagnostics);
                    hit|=clip.Value!.RemovedLengthMm>policy.TolerancePolicy.GeometryMm;
                    continue;
                }
                foreach (var opening in geometry.Openings.Where(o=>o.OwnerId==wall.Id))
                {
                    // OpeningClipper adds half the probe diameter itself; do not add it twice.
                    var clip=OpeningClipper.Clip(probe,[opening],new ClearancePolicy(truncate?openingCoverMm:0),geometry);
                    if(!clip.Success)return new(false,null,clip.Diagnostics);
                    if (clip.Value!.RemovedLengthMm>policy.TolerancePolicy.GeometryMm) { hit=true;hitOpening=opening.Id;break; }
                }
                if(hit) break;
            }
            if(hit)
            {
                if(!truncate) return Block("LADDER_OPENING_COLLISION","Selected ladder support intersects a registered pipe opening; placement adjustment is unresolved.",spec.Sources.FirstOrDefault());
                diagnostics.Add(new(bars.Count==0?"LADDER_TRUNCATED_ALL":"LADDER_TRUNCATED_AT_OPENING",DiagnosticSeverity.Review,
                    DiagnosticScope.Component,input.Input.Id,wall.Id,spec.Sources.FirstOrDefault(),"contiguous ladder above opening",null,
                    FormattableString.Invariant($"Wall={wall.Id}; placed={bars.Count}; datumZ={topDatumMm}; firstZ={first}; stopZ={z}; opening={hitOpening}; openingCover={openingCoverMm}; radius={spec.DiameterMm/2d}. No lower rungs placed.")));
                break;
            }
            var path=new BarPath(segments,false,HookPattern.Hook0M,0,false,MeasurementBasis.NominalPolyline);
            double nominal=2*spec.HookMm+2*spec.LegHeightMm+spec.WidthMm;
            double? fabrication=NominalFabricationLength.Calculate(path,spec.DiameterMm,true);
            bars.Add(new($"{input.Input.Id}/{wall.Id}/Ladder/{index:000}",null,wall.Id,wall.Kind,Layer.Bottom,wallId is WallId.T1 or WallId.T3?Axis.X:Axis.Y,
                spec.DiameterMm,path,nominal,fabrication,fabrication.HasValue?LengthStatus.Known:LengthStatus.NeedsRule,
                published?"LADDER-PUBLISHED-1":"LADDER-APPROVED-1C",spec.Sources.Concat(wall.Sources).Concat(supports.SelectMany(c=>c.Sources)).Distinct(),RebarDirection.Supplementary,wall.Id,RebarSurface.Inner,"Ladder",MeasurementBasisKind.Nominal));
        }
        diagnostics.Add(new("LADDER_FABRICATION_FALLBACK",DiagnosticSeverity.Review,DiagnosticScope.Component,input.Input.Id,wall.Id,
            spec.Sources.FirstOrDefault(),"nominal schedule / separate fabrication",null,"Approved 100 mm embed, vertical hooks and r=1.25d fallback."));
        return new(true,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,diagnostics.Any(d=>d.Code.StartsWith("LADDER_TRUNCATED_",StringComparison.Ordinal)) || bars.Any(b=>b.LengthStatus==LengthStatus.NeedsRule)?LayoutStatus.Partial:LayoutStatus.Complete),diagnostics);
    }

    public static OperationResult<RebarLayout> Plan(LadderSpec spec, ComponentGeometry geometry, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(geometry); ArgumentNullException.ThrowIfNull(policy);
        if (spec.PitchMm <= 0)
            return Block("LADDER_PITCH_INVALID", "Ladder pitch must be positive.", spec.Sources.FirstOrDefault());
        double first = geometry.ZRange.End - spec.TopOffsetMm;
        var heights = new List<double>();
        for (double z = first; z >= spec.MinZMm - policy.TolerancePolicy.GeometryMm; z -= spec.PitchMm)
            heights.Add(z);
        if (heights.Count == 0)
            return new(true, new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Disabled), []);

        double centerX = geometry.Footprint.Outer.Vertices.Average(x => x.X);
        double centerY = geometry.Footprint.Outer.Vertices.Average(x => x.Y);
        var bars = new List<BarOccurrence>();
        var diagnostics = new List<Diagnostic>();
        for (int i = 0; i < heights.Count; i++)
        {
            double z = heights[i];
            var start = new Point3Mm(centerX - spec.WidthMm / 2 - spec.HookMm, centerY, z);
            var left = new Point3Mm(centerX - spec.WidthMm / 2, centerY, z);
            var leftTop = new Point3Mm(left.X, left.Y, z + spec.LegHeightMm);
            var rightTop = new Point3Mm(centerX + spec.WidthMm / 2, centerY, z + spec.LegHeightMm);
            var right = new Point3Mm(rightTop.X, rightTop.Y, z);
            var end = new Point3Mm(right.X + spec.HookMm, right.Y, right.Z);
            if (IntersectsOpening(geometry.Voids, start, end, leftTop, rightTop, z, spec.OpeningBehavior))
                return Block("LADDER_OPENING_COLLISION", "Ladder rung collides with an opening under the selected policy.", spec.Sources.FirstOrDefault());
            var path = new BarPath([new LineSegment3Mm(start, left), new LineSegment3Mm(left, leftTop),
                new LineSegment3Mm(leftTop, rightTop), new LineSegment3Mm(rightTop, right), new LineSegment3Mm(right, end)],
                false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            double length = path.Segments.Sum(Length);
            bars.Add(new BarOccurrence($"{geometry.Id}/Ladder/{i:000}", null, geometry.Id, geometry.Kind, Layer.Bottom, Axis.X,
                spec.DiameterMm, path, length, null, LengthStatus.NeedsRule, "LADDER-S2-001",
                geometry.Sources.Concat(spec.Sources), RebarDirection.Supplementary, geometry.Id, null, "Ladder",
                MeasurementBasisKind.Nominal));
        }
        diagnostics.Add(new Diagnostic("LADDER_FABRICATION_NEEDS_RULE", DiagnosticSeverity.Review, DiagnosticScope.Component,
            geometry.Id, geometry.Id, spec.Sources.FirstOrDefault(), "bend radius and fabrication basis", null,
            "Ladder schedule length is measured from placed coordinates; fabrication length remains unresolved."));
        return new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar, LayoutStatus.Partial), diagnostics);
    }

    public static OperationResult<RebarLayout> Plan(LadderSpec spec, GeometryModel geometry, CalculationPolicy policy) =>
        Plan(spec, geometry.Components.FirstOrDefault() ?? throw new ArgumentException("Geometry has no component."), policy);

    private static bool IntersectsOpening(IEnumerable<OpeningProfile> openings, Point3Mm start, Point3Mm end, Point3Mm leftTop, Point3Mm rightTop,
        double z, LadderOpeningBehavior behavior)
    {
        foreach (var opening in openings)
        {
            if (z < opening.ZRange.Start || z > opening.ZRange.End) continue;
            if (opening.ConcreteCutout is RectangleProfile rectangle)
            {
                bool hit = new[] { start, end, leftTop, rightTop }.Any(p => p.X >= rectangle.Rectangle.MinX && p.X <= rectangle.Rectangle.MaxX && p.Y >= rectangle.Rectangle.MinY && p.Y <= rectangle.Rectangle.MaxY);
                if (hit) return behavior == LadderOpeningBehavior.Reject;
            }
        }
        return false;
    }

    private static double Length(PathSegment segment) => segment switch
    {
        LineSegment3Mm line => Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2) + Math.Pow(line.End.Y - line.Start.Y, 2) + Math.Pow(line.End.Z - line.Start.Z, 2)),
        ArcSegment3Mm arc => Math.Abs(arc.SweepRadians) * arc.CenterlineRadiusMm,
        _ => 0
    };

    private static OperationResult<RebarLayout> Block(string code, string message, SourceRef? source) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source, null, null, message)]);
}
