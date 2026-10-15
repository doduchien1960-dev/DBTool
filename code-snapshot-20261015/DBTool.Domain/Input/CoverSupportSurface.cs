using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>An actual horizontal rectangular concrete ring, in plan coordinates.
/// The rectangles describe its physical outer edge and void, not asset extents.</summary>
public sealed record CoverSupportSurface
{
    public CoverSupportSurface(Rectangle2Mm outer, Rectangle2Mm opening, double elevationMm, IEnumerable<SourceRef> sources)
    {
        Outer=ContractGuard.Required(outer);
        Opening=ContractGuard.Required(opening);
        ElevationMm=ContractGuard.Finite(elevationMm,nameof(elevationMm));
        Sources=ContractGuard.Copy(sources);
        if(Sources.IsEmpty || opening.MinX<outer.MinX || opening.MaxX>outer.MaxX ||
            opening.MinY<outer.MinY || opening.MaxY>outer.MaxY)
            throw new ArgumentException("Physical support requires provenance and an opening inside its outer contour.");
    }
    public Rectangle2Mm Outer {get;}
    public Rectangle2Mm Opening {get;}
    public double ElevationMm {get;}
    public ImmutableArray<SourceRef> Sources {get;}
}
