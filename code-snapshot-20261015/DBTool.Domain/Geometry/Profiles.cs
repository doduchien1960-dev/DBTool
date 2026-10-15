using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public abstract record Profile2Mm { private protected Profile2Mm() {} }
public sealed record CircleProfile : Profile2Mm
{
    public CircleProfile(Point2Mm center, double radiusMm)
    {
        Center = center;
        RadiusMm = ContractGuard.Positive(radiusMm, nameof(radiusMm));

    }
    public Point2Mm Center { get; }
    public double RadiusMm { get; }
}
public sealed record RectangleProfile : Profile2Mm
{
    public RectangleProfile(Rectangle2Mm rectangle)
    {
        Rectangle = ContractGuard.Required(rectangle);

    }
    public Rectangle2Mm Rectangle { get; }
}
/// <summary>Analytical contour: four tangent straight sides and four circular corner arcs.</summary>
public sealed record RoundedRectangleProfile : Profile2Mm
{
    public RoundedRectangleProfile(Point2Mm center,Dimensions2Mm size,double cornerRadiusMm,double rotationDegrees=0)
    {
        Center=center;Size=ContractGuard.Required(size);
        CornerRadiusMm=ContractGuard.Positive(cornerRadiusMm,nameof(cornerRadiusMm));
        if(cornerRadiusMm>Math.Min(size.Width,size.Height)/2)
            throw new ArgumentException("Corner arcs do not fit the clear interface.",nameof(cornerRadiusMm));
        RotationDegrees=ContractGuard.Finite(rotationDegrees,nameof(rotationDegrees));
        double angle=(rotationDegrees%360)*Math.PI/180;
        double x=Math.Abs(Math.Cos(angle))*(size.Width/2-cornerRadiusMm)
            +Math.Abs(Math.Sin(angle))*(size.Height/2-cornerRadiusMm)+cornerRadiusMm;
        double y=Math.Abs(Math.Sin(angle))*(size.Width/2-cornerRadiusMm)
            +Math.Abs(Math.Cos(angle))*(size.Height/2-cornerRadiusMm)+cornerRadiusMm;
        AxisBounds=new(center.X-x,center.Y-y,center.X+x,center.Y+y);
    }
    public Point2Mm Center {get;}
    public Dimensions2Mm Size {get;}
    public double CornerRadiusMm {get;}
    public double RotationDegrees {get;}
    /// <summary>Directional extent for placement checks; never replaces the rounded physical contour.</summary>
    public Rectangle2Mm AxisBounds {get;}
}
public sealed record PolygonProfile : Profile2Mm
{
    public PolygonProfile(Polygon2Mm polygon)
    {
        Polygon = ContractGuard.Required(polygon);

    }
    public Polygon2Mm Polygon { get; }
}
public sealed record MultiCellProfile : Profile2Mm
{
    public MultiCellProfile(IEnumerable<PolygonProfile> cells)
    {
        Cells = ContractGuard.Copy(cells);
        if (Cells.IsEmpty) throw new ArgumentException("At least one inner cell is required.", nameof(cells));
    }
    public ImmutableArray<PolygonProfile> Cells { get; }
}
public sealed record OpenTopProfile : Profile2Mm
{
    public OpenTopProfile(Polygon2Mm polygon)
    {
        Polygon = ContractGuard.Required(polygon);

    }
    public Polygon2Mm Polygon { get; }
}
public abstract record SolidDescriptor { private protected SolidDescriptor() {} }
public sealed record ExtrusionSolid : SolidDescriptor
{
    public ExtrusionSolid(Footprint footprint, Frame3Mm frame, Interval1D depth)
    {
        Footprint = ContractGuard.Required(footprint);
        Frame = ContractGuard.Required(frame);
        Depth = depth;
        if(depth.Length<=0) throw new ArgumentException("Extrusion must have positive depth.");
    }
    public Footprint Footprint { get; }
    public Frame3Mm Frame { get; }
    public Interval1D Depth { get; }
}
public sealed record UnionSolid : SolidDescriptor
{
    public UnionSolid(IEnumerable<SolidDescriptor> solids)
    {
        Solids = ContractGuard.Copy(solids);
        if(Solids.IsEmpty) throw new ArgumentException("Empty union.");
    }
    public ImmutableArray<SolidDescriptor> Solids { get; }
}
public sealed record DifferenceSolid : SolidDescriptor
{
    public DifferenceSolid(SolidDescriptor occupied, IEnumerable<SolidDescriptor> cuts)
    {
        Occupied = ContractGuard.Required(occupied);
        Cuts = ContractGuard.Copy(cuts);

    }
    public SolidDescriptor Occupied { get; }
    public ImmutableArray<SolidDescriptor> Cuts { get; }
}
