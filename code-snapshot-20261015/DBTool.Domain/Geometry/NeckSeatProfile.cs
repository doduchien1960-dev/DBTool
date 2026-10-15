using System.Collections.Immutable;

namespace DBTool.Domain.Geometry;

public sealed record NeckSeatProfile
{
    public NeckSeatProfile(Rectangle2Mm innerBoundary,Rectangle2Mm outerBoundary,double supportPlaneMm,double topPlaneMm,double seatHeightMm,IEnumerable<SourceRef> sources)
    {
        InnerBoundary=ContractGuard.Required(innerBoundary); OuterBoundary=ContractGuard.Required(outerBoundary);
        SupportPlaneMm=ContractGuard.Finite(supportPlaneMm,nameof(supportPlaneMm));
        TopPlaneMm=ContractGuard.Finite(topPlaneMm,nameof(topPlaneMm));
        SeatHeightMm=ContractGuard.NonNegative(seatHeightMm,nameof(seatHeightMm)); Sources=ContractGuard.Copy(sources);
        if(topPlaneMm<supportPlaneMm || innerBoundary.MinX<outerBoundary.MinX || innerBoundary.MaxX>outerBoundary.MaxX ||
            innerBoundary.MinY<outerBoundary.MinY || innerBoundary.MaxY>outerBoundary.MaxY)
            throw new ArgumentException("Invalid seat boundary or support plane.");
    }
    public Rectangle2Mm InnerBoundary { get; }
    public Rectangle2Mm OuterBoundary { get; }
    public double SupportPlaneMm { get; }
    public double TopPlaneMm { get; }
    public double SeatHeightMm { get; }
    public ImmutableArray<SourceRef> Sources { get; }
}
