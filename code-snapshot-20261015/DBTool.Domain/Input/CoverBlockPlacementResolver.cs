using DBTool.Domain.Geometry;

namespace DBTool.Domain;

// The domain returns a semantic native view (AA/BB). CAD block-name resolution belongs to the adapter.
public sealed record CoverSectionPlacement
{
    public CoverSectionPlacement(string nativeView,Point2Mm insertion,int horizontalDirection)
    {
        if(nativeView is not ("AA" or "BB")) throw new ArgumentException("Unknown semantic native section.",nameof(nativeView));
        if(horizontalDirection is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(horizontalDirection));
        NativeView=nativeView;Insertion=insertion;HorizontalDirection=horizontalDirection;
    }
    public string NativeView {get;}
    public Point2Mm Insertion {get;}
    public int HorizontalDirection {get;}
}
public sealed record CoverBlockPlacements
{
    public CoverBlockPlacements(Point2Mm planInsertion,double rotationDegrees,CoverSectionPlacement sectionAA,CoverSectionPlacement sectionBB)
    {
        PlanInsertion=planInsertion;RotationDegrees=ContractGuard.Finite(rotationDegrees,nameof(rotationDegrees));
        SectionAA=ContractGuard.Required(sectionAA);SectionBB=ContractGuard.Required(sectionBB);
    }
    public Point2Mm PlanInsertion {get;}
    public double RotationDegrees {get;}
    public CoverSectionPlacement SectionAA {get;}
    public CoverSectionPlacement SectionBB {get;}
}

/// <summary>Aligns measured native block references to the neck. Does not approve structural support.</summary>
public static class CoverBlockPlacementResolver
{
    public static OperationResult<CoverBlockPlacements> Resolve(CoverBlockSet blocks,Point2Mm neckCenter,
        double coverTopMm,double rotationDegrees)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        OperationResult<CoverBlockPlacements> Block(string code,string message)=>new(false,null,
            [new(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,blocks.Sources[0],
                "Measured MB/AA/BB alignment at a quarter-turn rotation",null,message)]);
        if(!double.IsFinite(rotationDegrees) || !double.IsFinite(coverTopMm))
            return Block("COVER_VIEW_PLACEMENT_INVALID","Placement requires finite angle and elevation.");
        double normalized=(rotationDegrees%360+360)%360;
        if(Math.Abs(normalized/90-Math.Round(normalized/90))>1e-9)
            return Block("COVER_VIEW_ROTATION_UNSUPPORTED","Oblique sections require a sourced projection; never rotate an orthographic section as a plan block.");
        if(blocks.Views.Any(v=>v.AlignmentPoint is null || v.AlignmentSources.IsEmpty))
            return Block("COVER_VIEW_ALIGNMENT_REQUIRED","Each MB/AA/BB block requires its own measured alignment reference and provenance.");
        int turn=(int)Math.Round(normalized/90)%4;
        var mb=blocks.Views.Single(v=>v.View=="MB").AlignmentPoint!.Value;
        // Exact quarter-turn coefficients preserve sign, avoiding integer overflow for large input angles.
        (int cos,int sin)=turn switch {0=>(1,0),1=>(0,1),2=>(-1,0),_=>(0,-1)};
        double px=neckCenter.X-(cos*mb.X-sin*mb.Y),py=neckCenter.Y-(sin*mb.X+cos*mb.Y);
        var (aaName,aaDirection,bbName,bbDirection)=turn switch
        {
            0=>("AA",1,"BB",1),
            1=>("BB",1,"AA",-1),
            2=>("AA",-1,"BB",-1),
            _=>("BB",-1,"AA",1)
        };
        var aa=Section(aaName,aaDirection,neckCenter.Y);
        var bb=Section(bbName,bbDirection,neckCenter.X);
        if(!double.IsFinite(px) || !double.IsFinite(py) || aa is null || bb is null)
            return Block("COVER_VIEW_PLACEMENT_INVALID","Derived cover insertion coordinates overflow.");
        return new(true,new(new(px,py),turn*90,aa,bb),[]);

        CoverSectionPlacement? Section(string view,int direction,double target)
        {
            var block=blocks.Views.Single(v=>v.View==view);
            var reference=block.AlignmentPoint!.Value;
            double x=target-direction*reference.X,y=coverTopMm-reference.Y;
            return double.IsFinite(x) && double.IsFinite(y)?new(block.View,new(x,y),direction):null;
        }
    }
}
