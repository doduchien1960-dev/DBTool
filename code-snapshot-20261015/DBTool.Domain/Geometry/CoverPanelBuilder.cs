using System.Collections.Immutable;

namespace DBTool.Domain.Geometry;

public sealed record CoverPanel(string Id,Rectangle2Mm Bounds,Frame3Mm Frame,Interval1D ZRange,string SupportRef,ImmutableArray<SourceRef> Sources)
{
    public double VolumeMm3 => (Bounds.MaxX-Bounds.MinX)*(Bounds.MaxY-Bounds.MinY)*ZRange.Length;
}
public sealed record PanelLayout(ImmutableArray<CoverPanel> Panels,double TotalVolumeMm3);

public static class CoverPanelBuilder
{
    private static readonly Vector3 X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    public static OperationResult<PanelLayout> Build(NeckSeatProfile seat,CoverPanelSpec spec,CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(seat); ArgumentNullException.ThrowIfNull(spec); ArgumentNullException.ThrowIfNull(policy);
        OperationResult<PanelLayout> Fail(string code,string expected,string actual) => new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,spec.Sources.FirstOrDefault(),expected,actual,code)]);
        if(policy.PolicyId is not ("S2Geometry-1" or "S2Rebar-1")) return Fail("PANEL_POLICY_INVALID","S2Geometry-1 or S2Rebar-1",policy.PolicyId);
        if(spec.Count>100000) return Fail("PANEL_COUNT_TOO_LARGE","manageable panel count",spec.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var support=spec.SupportMode==PanelSupportMode.Recess?seat.InnerBoundary:seat.OuterBoundary;
        double span=spec.SplitAxis==SplitAxis.X?support.MaxX-support.MinX:support.MaxY-support.MinY;
        double cross=spec.SplitAxis==SplitAxis.X?support.MaxY-support.MinY:support.MaxX-support.MinX;
        int perimeterGaps=spec.SupportMode==PanelSupportMode.Recess?spec.Count+1:spec.Count-1;
        double dimension=(span-perimeterGaps*spec.GapMm)/spec.Count;
        double crossDimension=spec.SupportMode==PanelSupportMode.Recess?cross-2*spec.GapMm:cross;
        double thickness=spec.CapHeightMm+seat.SeatHeightMm;
        if(!double.IsFinite(dimension) || !double.IsFinite(crossDimension) || !double.IsFinite(thickness) ||
            dimension<=0 || crossDimension<=0 || thickness<=0)
            return Fail("PANEL_CLEARANCE_INVALID","positive panel dimensions and thickness",$"{dimension}; {crossDimension}; {thickness}");
        double start=(spec.SplitAxis==SplitAxis.X?support.MinX:support.MinY)+
            (spec.SupportMode==PanelSupportMode.Recess?spec.GapMm:0);
        double crossStart=(spec.SplitAxis==SplitAxis.X?support.MinY:support.MinX)+
            (spec.SupportMode==PanelSupportMode.Recess?spec.GapMm:0);
        var range=new Interval1D(seat.SupportPlaneMm,seat.SupportPlaneMm+thickness);
        var panels=ImmutableArray.CreateBuilder<CoverPanel>(spec.Count);
        double total=0;
        for(int index=0;index<spec.Count;index++)
        {
            double low=start+index*(dimension+spec.GapMm),high=low+dimension;
            var bounds=spec.SplitAxis==SplitAxis.X
                ? new Rectangle2Mm(low,crossStart,high,crossStart+crossDimension)
                : new Rectangle2Mm(crossStart,low,crossStart+crossDimension,high);
            double cx=(bounds.MinX+bounds.MaxX)/2,cy=(bounds.MinY+bounds.MaxY)/2;
            var panel=new CoverPanel($"Panel/{index+1:D3}",bounds,new Frame3Mm(new Point3Mm(cx,cy,range.Start),X,Y,Z),
                range,"NeckSeat/SupportPlane",spec.Sources.AddRange(seat.Sources));
            panels.Add(panel); total+=panel.VolumeMm3;
        }
        if(!double.IsFinite(total)) return Fail("PANEL_VOLUME_INVALID","finite panel volume",total.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new(true,new PanelLayout(panels.MoveToImmutable(),total),[]);
    }
}
