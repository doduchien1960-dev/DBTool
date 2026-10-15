using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record PipeOpeningSet
{
    public PipeOpeningSet(IEnumerable<OpeningProfile> openings)
    { Openings=ContractGuard.Copy(openings); }
    public ImmutableArray<OpeningProfile> Openings { get; }
}

public static class PipeOpeningBuilder
{
    /// <summary>S2 permits vertical wall/base/roof crossings and registers one exact cut per affected owner.</summary>
    public static OperationResult<GeometryModel> AttachRoundFull(GeometryModel model,OpeningProfile opening)
    {
        var contained=AttachOpening(model,opening);
        if(contained.Success || !contained.Diagnostics.Any(d=>d.Code=="OPENING_CROSS_COMPONENT_UNSUPPORTED")) return contained;
        var owner=model.Components.Single(x=>x.Id==opening.OwnerId);
        if(opening.WallFrame is not { } frame || opening.ConcreteCutout is not CircleProfile circle) return contained;
        var points=owner.Footprint.Outer.Vertices;
        double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
        if(frame.WallId is WallId.T1 or WallId.T3) {double t=maxY-minY;minX+=t;maxX-=t;}
        foreach(double u in new[]{circle.Center.X-circle.RadiusMm,circle.Center.X+circle.RadiusMm})
        foreach(double v in new[]{opening.ExtrusionRange.Start,opening.ExtrusionRange.End})
        {
            var p=frame.ToGlobal(u,v,0);
            if(p.X<minX || p.X>maxX || p.Y<minY || p.Y>maxY) return contained;
        }
        var owners=model.Components.Where(c=>c.Occupied is ExtrusionSolid && c.Kind is ComponentKind.Base or ComponentKind.Wall or ComponentKind.Roof).ToArray();
        if(circle.Center.Y-circle.RadiusMm<owners.Min(c=>c.ZRange.Start) || circle.Center.Y+circle.RadiusMm>owners.Max(c=>c.ZRange.End)) return contained;
        var partition=OpeningOwnershipPartitioner.Partition(new GeometryModel(owners,owners.SelectMany(c=>c.Voids)),
            new OpeningSolid(opening.Id,[new RoundOpeningCut(frame,circle,opening.ExtrusionRange)],opening.Sources),new OpeningOwnershipPolicy());
        if(!partition.Success) return new(false,null,partition.Diagnostics);
        if(partition.Value!.Children.IsEmpty) return contained;
        var registry=model.Openings.ToList();var components=new List<ComponentGeometry>();
        foreach(var component in model.Components)
        {
            var child=partition.Value.Children.SingleOrDefault(c=>c.OwnerId==component.Id);
            if(child is null) {components.Add(component);continue;}
            var cut=new OpeningProfile(component.Id==opening.OwnerId?opening.Id:$"{opening.Id}/owner/{component.Id}",component.Id,
                opening.Kind,opening.Inner,opening.PipeOuter,opening.ConcreteCutout,frame,opening.Frame,opening.ZRange,opening.ExtrusionRange,opening.Sources);
            registry.Add(cut);
            components.Add(new(component.Id,component.Kind,component.MaterialId,component.Footprint,component.ZRange,component.Occupied,
                component.Voids.Add(cut),component.FacePatches,component.Sources));
        }
        var review=new Diagnostic("OPENING_CROSS_COMPONENT_REGISTERED",DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,
            opening.Sources.FirstOrDefault(),"owned round cut",string.Join(",",partition.Value.Children.Select(c=>c.OwnerId)),
            "Exact cylinder ownership registered for rebar clipping; face patches remain gross until quantity/face consumers resolve them.");
        return new(true,new GeometryModel(components,registry,model.PurchasedCovers),[review]);
    }

    public static OperationResult<PipeOpeningSet> BuildAll(ValidatedManholeSpec spec,
        CalculationPolicy policy, DoubleBoxPolicy doubleBoxPolicy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(doubleBoxPolicy);
        var pipes=spec.Input.PipeConnections;
        var repeated=pipes.Where(p=>p.Id is not null).GroupBy(p=>p.Id!,StringComparer.Ordinal)
            .FirstOrDefault(g=>g.Count()>1);
        if(repeated is not null)
            return new(false,null,[new Diagnostic("PIPE_ID_DUPLICATE",DiagnosticSeverity.Block,
                DiagnosticScope.Manhole,spec.Input.Id,null,repeated.First().SourceRefs.Values.FirstOrDefault(),
                "unique explicit physical pipe ID",repeated.Key,"Duplicate pipe identity.")]);
        var openings=new List<OpeningProfile>();
        foreach(var pipe in pipes)
        {
            if(!spec.Catalogs.Pipes.TryGetValue(pipe.CatalogCode,out var entry))
                return new(false,null,[new Diagnostic("PIPE_CATALOG_MISSING",DiagnosticSeverity.Block,
                    DiagnosticScope.Manhole,spec.Input.Id,null,pipe.SourceRefs.Values.FirstOrDefault(),
                    "resolved catalog",pipe.CatalogCode,"Pipe catalog entry is missing.")]);
            OperationResult<OpeningProfile> built=entry.Kind==PipeKind.Round
                ? BuildRoundFull(spec,pipe,spec.Catalogs,policy)
                : BuildBox(spec,pipe,spec.Catalogs,policy,doubleBoxPolicy);
            if(!built.Success) return new(false,null,built.Diagnostics);
            openings.Add(built.Value!);
        }
        var repeatedOpening=openings.GroupBy(o=>o.Id,StringComparer.Ordinal).FirstOrDefault(g=>g.Count()>1);
        if(repeatedOpening is not null)
            return new(false,null,[new Diagnostic("PIPE_ID_DUPLICATE",DiagnosticSeverity.Block,
                DiagnosticScope.Manhole,spec.Input.Id,null,repeatedOpening.First().Sources.FirstOrDefault(),
                "unique physical pipe ID",repeatedOpening.Key,"Opening identities collide.")]);
        return new(true,new PipeOpeningSet(openings),[]);
    }

    /// <summary>S2 descriptor permits a physical cut to cross component boundaries; ownership is partitioned separately.</summary>
    public static OperationResult<OpeningProfile> BuildRoundFull(ValidatedManholeSpec spec,
        PipeConnectionSpec pipe, CatalogSet catalogs, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(policy);
        var input = spec.Input;
        string owner = $"Wall/{pipe.WallId}";
        var sources = pipe.SourceRefs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value)
            .Concat(spec.Sources).Distinct().ToArray();
        OperationResult<OpeningProfile> Fail(string code, string expected, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Component, input.Id, owner,
                sources.FirstOrDefault(), expected, actual, "Round opening cannot be generated.")]);
        if (policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || policy.Version != spec.RuleVersion ||
            policy.PipeInvertReference != PipeInvertReference.InternalInvert ||
            input.PipeInvertReference != PipeInvertReference.InternalInvert)
            return Fail("PIPE_POLICY_UNSUPPORTED", "S2Geometry-1/v1 internal invert", policy.Version);
        if (!catalogs.Pipes.TryGetValue(pipe.CatalogCode, out var entry))
            return Fail("PIPE_CATALOG_MISSING", "resolved round pipe", pipe.CatalogCode);
        if (entry.Kind != PipeKind.Round)
            return Fail("PIPE_SHAPE_UNSUPPORTED", "round", entry.Kind.ToString());
        var section = PipeCrossSectionBuilder.BuildRound(entry.InnerDiameterMm!.Value,
            entry.WallThicknessMm, pipe.MortarMm, pipe.InvertMm, pipe.OffsetMm, sources.FirstOrDefault());
        if (!section.Success)
            return new(false, null, section.Diagnostics.Select(d => new Diagnostic(d.Code, d.Severity,
                DiagnosticScope.Component, input.Id, owner, d.Source, d.Expected, d.Actual, d.Message)));
        var rings = section.Value!;
        var frame = WallFrame.Create(pipe.WallId, input.L, input.B);
        var cut = rings.ConcreteCutoutCircle;
        var opening = new OpeningProfile(pipe.Id ?? $"{owner}/Pipe/{pipe.CatalogCode}", owner, OpeningKind.Round,
            rings.InnerCircle, rings.PipeOuterCircle, cut, frame,
            new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(cut.Center.Y-cut.RadiusMm, cut.Center.Y+cut.RadiusMm),
            new Interval1D(0,input.WallThicknessMm), sources);
        return new(true, opening, []);
    }

    public static OperationResult<OpeningProfile> BuildBox(ValidatedManholeSpec spec, PipeConnectionSpec pipe,
        CatalogSet catalogs, CalculationPolicy policy, DoubleBoxPolicy doubleBoxPolicy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(doubleBoxPolicy);
        var input = spec.Input;
        string owner = $"Wall/{pipe.WallId}";
        var sources = pipe.SourceRefs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value)
            .Concat(spec.Sources).Distinct().ToArray();
        OperationResult<OpeningProfile> Fail(string code, string expected, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Component, input.Id, owner,
                sources.FirstOrDefault(), expected, actual, "Box opening cannot be generated.")]);
        if (policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || policy.Version != spec.RuleVersion ||
            policy.PipeInvertReference != PipeInvertReference.InternalInvert)
            return Fail("PIPE_POLICY_UNSUPPORTED", "S2Geometry-1/v1 internal invert", policy.Version);
        if (!catalogs.Pipes.TryGetValue(pipe.CatalogCode, out var entry))
            return Fail("PIPE_CATALOG_MISSING", "resolved pipe code", pipe.CatalogCode);
        var section = PipeCrossSectionBuilder.BuildBox(entry, pipe, doubleBoxPolicy);
        if (!section.Success)
            return new(false, null, section.Diagnostics.Select(d => new Diagnostic(d.Code, d.Severity,
                DiagnosticScope.Component, input.Id, owner, d.Source ?? sources.FirstOrDefault(),
                d.Expected, d.Actual, d.Message)));
        var box = section.Value!;
        var frame = WallFrame.Create(pipe.WallId, input.L, input.B);
        var cut = box.ConcreteCutout.Rectangle;
        var opening = new OpeningProfile(pipe.Id ?? $"{owner}/Pipe/{pipe.CatalogCode}", owner, OpeningKind.Box,
            new MultiCellProfile(box.InnerCells.Select(c => c.Profile)), box.PipeOuter, box.ConcreteCutout,
            frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(cut.MinY, cut.MaxY), new Interval1D(0, input.WallThicknessMm),
            sources.Concat(box.SourceRefs).Distinct());
        return new(true, opening, []);
    }

    /// <summary>Builds a round opening descriptor without cutting a solid or rendering CAD.</summary>
    public static OperationResult<OpeningProfile> BuildRound(ValidatedManholeSpec spec, PipeConnectionSpec pipe,
        CatalogSet catalogs, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(policy);
        string owner = $"Wall/{pipe.WallId}";
        var sources = pipe.SourceRefs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value)
            .Concat(spec.Sources).Distinct().ToArray();
        var source = sources.FirstOrDefault();
        var input = spec.Input;
        if (policy.PolicyId != "S1-Analytic-1" || policy.Version != spec.RuleVersion ||
            policy.PipeInvertReference != PipeInvertReference.InternalInvert ||
            input.PipeInvertReference != PipeInvertReference.InternalInvert)
            return Fail("PIPE_POLICY_UNSUPPORTED", "S1 policy/version with internal invert", policy.Version);

        if (!catalogs.Pipes.TryGetValue(pipe.CatalogCode, out var entry))
            return Fail("PIPE_CATALOG_MISSING", "resolved canonical pipe code", pipe.CatalogCode);
        if (entry.Kind != PipeKind.Round)
            return Fail("PIPE_SHAPE_UNSUPPORTED", "Round", entry.Kind.ToString());

        var crossSection = PipeCrossSectionBuilder.BuildRound(entry.InnerDiameterMm!.Value,
            entry.WallThicknessMm, pipe.MortarMm, pipe.InvertMm, pipe.OffsetMm, source);
        if (!crossSection.Success)
            return new(false, null, crossSection.Diagnostics.Select(d => new Diagnostic(d.Code, d.Severity,
                DiagnosticScope.Component, input.Id, owner, d.Source, d.Expected, d.Actual, d.Message)));
        var section = crossSection.Value!;
        var cut = section.ConcreteCutoutCircle;
        double halfWidth = (pipe.WallId is WallId.T1 or WallId.T3 ? input.L : input.B) / 2;
        double minZ = cut.Center.Y - cut.RadiusMm, maxZ = cut.Center.Y + cut.RadiusMm;
        double wallTop = input.Profile == Profile.GaThamLienKhoi ? input.H : input.H - input.BeddingMortarMm;
        if (cut.Center.X - cut.RadiusMm < -halfWidth || cut.Center.X + cut.RadiusMm > halfWidth ||
            minZ < 0 || maxZ > wallTop)
            return Fail("OPENING_CROSS_COMPONENT_UNSUPPORTED", "cutout contained in one concrete wall face",
                FormattableString.Invariant($"u=[{cut.Center.X-cut.RadiusMm},{cut.Center.X+cut.RadiusMm}];z=[{minZ},{maxZ}]"));

        var wall = WallFrame.Create(pipe.WallId, input.L, input.B);
        var frame = new Frame3Mm(wall.Origin, wall.Left, wall.Up, wall.Outward);
        return new(true, new OpeningProfile($"{owner}/Pipe", owner, OpeningKind.Round,
            section.InnerCircle, section.PipeOuterCircle, cut, wall, frame,
            new Interval1D(minZ, maxZ), new Interval1D(0, input.WallThicknessMm), sources), []);

        OperationResult<OpeningProfile> Fail(string code, string expected, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Component, input.Id, owner,
                source, expected, actual, "Round pipe opening cannot be generated for this input.")]);
    }

    /// <summary>Registers a cut in a new immutable snapshot; Occupied stays the gross blank.</summary>
    public static OperationResult<GeometryModel> AttachOpening(GeometryModel model, OpeningProfile opening)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(opening);
        var source = opening.Sources.FirstOrDefault();
        if (model.Openings.Any(o => o.Id == opening.Id))
            return Fail("OPENING_DUPLICATE", "unique opening ID", opening.Id);
        var owner = model.Components.FirstOrDefault(c => c.Id == opening.OwnerId);
        if (owner is null) return Fail("OPENING_OWNER_MISSING", "existing owner", opening.OwnerId);
        if (owner.Kind != ComponentKind.Wall || opening.WallFrame is null)
            return Fail("PIPE_SHAPE_UNSUPPORTED", "pipe opening on a wall", opening.Kind.ToString());
        if (opening.Kind == OpeningKind.Round && (opening.Inner is not CircleProfile || opening.PipeOuter is not CircleProfile || opening.ConcreteCutout is not CircleProfile))
            return Fail("PIPE_SHAPE_UNSUPPORTED", "round pipe opening on a wall", opening.Kind.ToString());
        if (opening.Kind == OpeningKind.Box && opening.ConcreteCutout is not RectangleProfile)
            return Fail("PIPE_SHAPE_UNSUPPORTED", "box pipe opening on a wall", opening.Kind.ToString());
        if (opening.Kind != OpeningKind.Round && opening.Kind != OpeningKind.Box)
            return Fail("PIPE_SHAPE_UNSUPPORTED", "round or box pipe opening on a wall", opening.Kind.ToString());

        // T14.a only attaches to the axis-aligned rectangular wall strips produced by T11.
        // Recheck the target snapshot: an opening may have been built for another chamber size.
        var vertices = owner.Footprint.Outer.Vertices;
        double minX = vertices.Min(p => p.X), maxX = vertices.Max(p => p.X);
        double minY = vertices.Min(p => p.Y), maxY = vertices.Max(p => p.Y);
        if (vertices.Length != 4 || !owner.Footprint.InnerLoops.IsEmpty ||
            vertices.Any(p => (p.X != minX && p.X != maxX) || (p.Y != minY && p.Y != maxY)) ||
            owner.Occupied is not ExtrusionSolid solid || solid.Frame.Origin != new Point3Mm(0, 0, 0) ||
            solid.Frame.U != new Vector3(1, 0, 0) || solid.Frame.V != new Vector3(0, 1, 0) ||
            solid.Frame.N != new Vector3(0, 0, 1) || solid.Depth != owner.ZRange ||
            !solid.Footprint.Equals(owner.Footprint))
            return Fail("OPENING_OWNER_UNSUPPORTED", "T11 rectangular wall extrusion", owner.Id);

        var wall = opening.WallFrame;
        if (owner.Id != $"Wall/{wall.WallId}")
            return Fail("OPENING_OWNER_UNSUPPORTED", "matching wall frame identity", owner.Id);
        if (wall.WallId is WallId.T1 or WallId.T3)
        {
            // T11 gives T1/T3 both corner strips; their clear face excludes one wall thickness at each end.
            double targetThickness = maxY - minY;
            minX += targetThickness;
            maxX -= targetThickness;
        }

        double minU, maxU, minCutZ, maxCutZ;
        if (opening.Kind == OpeningKind.Round)
        {
            var cut = (CircleProfile)opening.ConcreteCutout;
            minU = cut.Center.X - cut.RadiusMm; maxU = cut.Center.X + cut.RadiusMm;
            minCutZ = cut.Center.Y - cut.RadiusMm; maxCutZ = cut.Center.Y + cut.RadiusMm;
        }
        else
        {
            var rect = ((RectangleProfile)opening.ConcreteCutout).Rectangle;
            minU = rect.MinX; maxU = rect.MaxX;
            minCutZ = rect.MinY; maxCutZ = rect.MaxY;
        }

        foreach (double u in new[] { minU, maxU })
        foreach (double v in new[] { opening.ExtrusionRange.Start, opening.ExtrusionRange.End })
        foreach (double z in new[] { minCutZ, maxCutZ })
        {
            var point = wall.ToGlobal(u, v, z);
            if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY ||
                point.Z < owner.ZRange.Start || point.Z > owner.ZRange.End)
                return Fail("OPENING_CROSS_COMPONENT_UNSUPPORTED", "cut inside target wall extrusion", owner.Id);
        }

        var updatedOwner = new ComponentGeometry(owner.Id, owner.Kind, owner.MaterialId, owner.Footprint,
            owner.ZRange, owner.Occupied, owner.Voids.Add(opening), owner.FacePatches, owner.Sources);
        var updated = new GeometryModel(model.Components.Select(c => c.Id == owner.Id ? updatedOwner : c),
            model.Openings.Add(opening),model.PurchasedCovers);
        return new(true, updated,
            [new Diagnostic("OPENING_FACE_PATCHES_DEFERRED", DiagnosticSeverity.Review, DiagnosticScope.Global,
                null, null, source, "net face patches in downstream stage", owner.Id,
                "Void registered; existing face patches remain gross. No formwork or boolean cut was calculated.")]);

        OperationResult<GeometryModel> Fail(string code, string expected, string actual) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source,
                expected, actual, "Opening could not be attached; original model is unchanged.")]);
    }
}
