namespace DBTool.Domain.Geometry;

/// <summary>Places the actual purchased product independently of whether a concrete neck exists.</summary>
public static class PurchasedCoverGeometryBuilder
{
    public static OperationResult<PurchasedCoverPlacement> Build(ValidatedManholeSpec spec,Point2Mm centre,CalculationPolicy policy,NeckSpec? actualNeck=null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);
        var requested=spec.Input.PurchasedCoverSpec;
        OperationResult<PurchasedCoverPlacement> Fail(string code,string expected,string actual)=>new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Manhole,spec.Input.Id,null,
                requested?.Sources.FirstOrDefault() ?? spec.Sources.FirstOrDefault(),expected,actual,code)]);
        if(policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || spec.RuleVersion!=policy.Version)
            return Fail("COVER_POLICY_INVALID","matching S2 geometry/rebar policy",policy.Version);
        if(requested is null) return Fail("COVER_SPEC_REQUIRED","explicit purchased product placement","missing");
        if(!spec.Catalogs.Covers.TryGetValue(requested.CatalogId,out var catalog))
            return Fail("COVER_CATALOG_MISSING","cover catalog entry",requested.CatalogId);
        if(catalog.PhysicalSize!=requested.PhysicalSize)
            return Fail("COVER_CATALOG_SIZE_MISMATCH","physical size matches catalog",requested.CatalogId);
        if(catalog.LocalBounds is null || catalog.Sources.IsEmpty)
            return Fail("U_NECK_COVER_CATALOG","physical bounds and provenance",requested.CatalogId);
        CoverBlockPlacements? nativeViews=null;
        CoverSectionOutlines? outlines=null;
        if(catalog.BlockSet is { } blocks)
        {
            if(catalog.OpeningContract is null && blocks.NominalOpening is null)
                return Fail("COVER_NATIVE_ASSEMBLY_REQUIRED","approved aperture and support contract",requested.CatalogId);
            var resolved=CoverBlockPlacementResolver.Resolve(blocks,centre,requested.Anchor.Z,requested.RotationDegrees);
            if(!resolved.Success) return new(false,null,resolved.Diagnostics);
            nativeViews=resolved.Value!;
            if(Math.Abs(nativeViews.PlanInsertion.X-requested.Anchor.X)>policy.TolerancePolicy.GeometryMm ||
                Math.Abs(nativeViews.PlanInsertion.Y-requested.Anchor.Y)>policy.TolerancePolicy.GeometryMm)
                return Fail("COVER_NATIVE_ANCHOR_MISMATCH","requested insertion matches measured native aperture alignment",requested.CatalogId);
            if(blocks.SectionOutlineRule is not null)
            {
                var neck=actualNeck ?? spec.NeckSpec ?? spec.Input.NeckSpec;
                if(neck is null || neck.ThicknessMm<=0)
                    return Fail("COVER_OUTLINE_NECK_RULE_REQUIRED","explicit neck clear dimensions and positive thickness for current section-outline rule","missing");
                // Neck dimensions are already in the world plan frame. Product rotation
                // controls its native blocks, not the independent concrete support outline.
                var aperture=new RectangleProfile(new(-neck.InnerWidthMm/2,-neck.InnerLengthMm/2,
                    neck.InnerWidthMm/2,neck.InnerLengthMm/2));
                var built=CoverSectionOutlineBuilder.Build(aperture,centre,requested.Anchor.Z,0,
                    CoverSectionOutlineRule.FromNeckThickness(neck));
                if(!built.Success) return new(false,null,built.Diagnostics);
                outlines=built.Value!;
            }
        }
        return new(true,new PurchasedCoverPlacement(catalog.Code,requested.Anchor,catalog.PhysicalSize,1,requested.RotationDegrees,
            catalog.LocalBounds,requested.Sources.Concat(catalog.Sources),catalog.OpeningContract?.ClearProfile ?? catalog.BlockSet?.MeasuredAperture,nativeViews,outlines),[]);
    }
}
