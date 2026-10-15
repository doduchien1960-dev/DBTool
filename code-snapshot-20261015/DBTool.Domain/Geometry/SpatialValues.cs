using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
public sealed record Dimensions2Mm
{
    public Dimensions2Mm(double width, double height)
    {
        Width = ContractGuard.Positive(width, nameof(width));
        Height = ContractGuard.Positive(height, nameof(height));

    }
    public double Width { get; }
    public double Height { get; }
}
public sealed record Rectangle2Mm
{
    public Rectangle2Mm(double minX, double minY, double maxX, double maxY)
    {
        MinX = ContractGuard.Finite(minX, nameof(minX));
        MinY = ContractGuard.Finite(minY, nameof(minY));
        MaxX = ContractGuard.Finite(maxX, nameof(maxX));
        MaxY = ContractGuard.Finite(maxY, nameof(maxY));
        if (minX >= maxX || minY >= maxY) throw new ArgumentException("Rectangle must have positive dimensions.");
    }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
}
public sealed record Bounds2Mm
{
    public Bounds2Mm(double minX, double minY, double maxX, double maxY)
    {
        MinX = ContractGuard.Finite(minX, nameof(minX));
        MinY = ContractGuard.Finite(minY, nameof(minY));
        MaxX = ContractGuard.Finite(maxX, nameof(maxX));
        MaxY = ContractGuard.Finite(maxY, nameof(maxY));
        if (minX > maxX || minY > maxY) throw new ArgumentException("Bounds are reversed.");
    }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
}
public sealed record LocalWallPointMm
{
    public LocalWallPointMm(double u, double v, double z)
    {
        U = ContractGuard.Finite(u, nameof(u));
        V = ContractGuard.Finite(v, nameof(v));
        Z = ContractGuard.Finite(z, nameof(z));

    }
    public double U { get; }
    public double V { get; }
    public double Z { get; }
}
public sealed record Vector3
{
    public Vector3(double x, double y, double z)
    {
        X = ContractGuard.Finite(x, nameof(x));
        Y = ContractGuard.Finite(y, nameof(y));
        Z = ContractGuard.Finite(z, nameof(z));

    }
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}

