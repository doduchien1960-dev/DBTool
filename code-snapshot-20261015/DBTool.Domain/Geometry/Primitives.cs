namespace DBTool.Domain.Geometry;

public readonly record struct Point2Mm
{
    public Point2Mm(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite.");
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

public readonly record struct Point3Mm
{
    public Point3Mm(double x, double y, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite.");
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}

public readonly record struct Interval1D
{
    public Interval1D(double start, double end)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end))
            throw new ArgumentOutOfRangeException(nameof(start), "Interval endpoints must be finite.");
        if (end < start)
            throw new ArgumentException("The interval end must be greater than or equal to its start.");
        Start = start;
        End = end;
    }

    public double Start { get; }
    public double End { get; }
    public double Length => End - Start;
}
