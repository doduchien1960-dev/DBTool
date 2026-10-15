namespace DBTool.Domain.Geometry;

/// <summary>Section outlines at their true aperture centre/top, never at inspection bounding-box centres.</summary>
public sealed record CoverSectionOutlines(Rectangle2Mm AA,Rectangle2Mm BB,CoverSectionOutlineRule Rule);

public static class CoverSectionOutlineBuilder
{
    /// <summary>Directional nominal opening from the owner's approved catalog, not a physical contour.</summary>
    public static OperationResult<CoverSectionOutlines> Build(CoverNominalOpening nominal,Point2Mm centre,
        double topMm,double rotationDegrees,CoverSectionOutlineRule rule)
        => Build(new RectangleProfile(new(-nominal.Size.Width/2,-nominal.Size.Height/2,
            nominal.Size.Width/2,nominal.Size.Height/2)),centre,topMm,rotationDegrees,rule);

    public static OperationResult<CoverSectionOutlines> Build(Profile2Mm aperture,Point2Mm centre,
        double topMm,double rotationDegrees,CoverSectionOutlineRule rule)
    {
        ArgumentNullException.ThrowIfNull(aperture);
        ArgumentNullException.ThrowIfNull(rule);
        OperationResult<CoverSectionOutlines> Block(string code,string message)=>new(false,null,
            [new(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,rule.Sources[0],
                "Finite section outline from actual aperture and sourced rule",null,message)]);
        if(!double.IsFinite(topMm) || !double.IsFinite(rotationDegrees))
            return Block("COVER_OUTLINE_PLACEMENT_INVALID","Nonfinite top elevation or cover rotation.");
        var dimensions=aperture switch
        {
            CircleProfile c=>(Width:2*c.RadiusMm,Height:2*c.RadiusMm,Angle:0d),
            RectangleProfile r=>(Width:r.Rectangle.MaxX-r.Rectangle.MinX,Height:r.Rectangle.MaxY-r.Rectangle.MinY,Angle:0d),
            RoundedRectangleProfile r=>(Width:r.Size.Width,Height:r.Size.Height,Angle:r.RotationDegrees),
            _=>(Width:double.NaN,Height:double.NaN,Angle:0d)
        };
        if(!double.IsFinite(dimensions.Width) || !double.IsFinite(dimensions.Height))
            return Block("COVER_OUTLINE_PROFILE_UNSUPPORTED","Requires a sourced circle, rectangle or rounded rectangle; no bounding-box substitute.");
        double angle=((rotationDegrees%360)+(dimensions.Angle%360)+360)%360;
        if(Math.Abs(angle/90-Math.Round(angle/90))>1e-9)
            return Block("COVER_OUTLINE_ROTATION_UNSUPPORTED","Oblique sections require an explicit projection.");
        bool swap=(int)Math.Round(angle/90)%2==1;
        double aaHalf=(swap?dimensions.Width:dimensions.Height)/2+rule.SideExtensionMm;
        double bbHalf=(swap?dimensions.Height:dimensions.Width)/2+rule.SideExtensionMm;
        double aaLeft=centre.Y-aaHalf,aaRight=centre.Y+aaHalf,
            bbLeft=centre.X-bbHalf,bbRight=centre.X+bbHalf,bottom=topMm-rule.DepthMm;
        if(new[]{aaHalf,bbHalf,aaLeft,aaRight,bbLeft,bbRight,bottom}.Any(v=>!double.IsFinite(v)) ||
            aaLeft>=aaRight || bbLeft>=bbRight || bottom>=topMm)
            return Block("COVER_OUTLINE_OVERFLOW","Derived coordinates overflow or lose the required positive extent.");
        return new(true,new(new(aaLeft,bottom,aaRight,topMm),new(bbLeft,bottom,bbRight,topMm),rule),[]);
    }
}
