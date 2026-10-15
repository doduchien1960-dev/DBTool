namespace DBTool.Domain.Geometry;

public static class CollarGeometryBuilder
{
    private static readonly Vector3 X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    public static OperationResult<ComponentGeometry> Build(ValidatedManholeSpec spec,NeckSeatSpec seat,CollarSpec collar,CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(seat); ArgumentNullException.ThrowIfNull(collar); ArgumentNullException.ThrowIfNull(policy);
        OperationResult<ComponentGeometry> Fail(string code) => new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Component,
            spec.Input.Id,"Collar",collar.Sources.FirstOrDefault(),"rectangular contained collar","unsupported geometry",code)]);
        if(policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1") || spec.RuleVersion!=policy.Version) return Fail("COLLAR_POLICY_INVALID");
        if(collar.OuterProfile is not RectangleProfile outer || collar.OpeningProfile is not RectangleProfile inner) return Fail("COLLAR_PROFILE_UNSUPPORTED");
        if((seat.SeatElevationMm < collar.ZRange.Start-policy.TolerancePolicy.GeometryMm ||
           seat.SeatElevationMm > collar.ZRange.End+policy.TolerancePolicy.GeometryMm) &&
           Math.Abs(collar.ZRange.Start-(spec.Input.H+spec.Input.RoofThicknessMm+(spec.Input.NeckSpec?.HeightMm ?? spec.Input.NeckHeight)))>policy.TolerancePolicy.GeometryMm)
            return Fail("COLLAR_SUPPORT_INVALID");
        double roofHalfX=spec.Input.L/2+spec.Input.WallThicknessMm,roofHalfY=spec.Input.B/2+spec.Input.WallThicknessMm;
        if(outer.Rectangle.MinX < -roofHalfX || outer.Rectangle.MaxX>roofHalfX ||
           outer.Rectangle.MinY < -roofHalfY || outer.Rectangle.MaxY>roofHalfY)
            return Fail("COLLAR_ROOF_CONTAINMENT");
        if(inner.Rectangle.MinX<=outer.Rectangle.MinX || inner.Rectangle.MaxX>=outer.Rectangle.MaxX ||
           inner.Rectangle.MinY<=outer.Rectangle.MinY || inner.Rectangle.MaxY>=outer.Rectangle.MaxY) return Fail("COLLAR_OPENING_INVALID");
        var footprint=NeckGeometryBuilder.Ring(outer.Rectangle,inner.Rectangle);
        var occupied=new ExtrusionSolid(footprint,new Frame3Mm(new Point3Mm(0,0,0),X,Y,Z),collar.ZRange);
        var area=(outer.Rectangle.MaxX-outer.Rectangle.MinX)*(outer.Rectangle.MaxY-outer.Rectangle.MinY)-
            (inner.Rectangle.MaxX-inner.Rectangle.MinX)*(inner.Rectangle.MaxY-inner.Rectangle.MinY);
        if(!double.IsFinite(area) || area<=0 || !double.IsFinite(area*collar.ZRange.Length)) return Fail("COLLAR_VOLUME_INVALID");
        var patches=NeckGeometryBuilder.HorizontalRingPatches("Collar",outer.Rectangle,inner.Rectangle,collar.ZRange.End,
            FaceRole.Top,null,collar.Sources);
        return new(true,new ComponentGeometry("Collar",ComponentKind.Collar,collar.MaterialId,footprint,collar.ZRange,occupied,[],patches,collar.Sources),[]);
    }
}
