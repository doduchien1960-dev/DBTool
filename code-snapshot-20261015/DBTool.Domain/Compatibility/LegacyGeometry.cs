namespace DBTool.Core
{
    /// <summary>Legacy retained interval; kept separate from the validated Domain primitive.</summary>
    public readonly record struct Interval1D(double Start, double End)
    {
        public double Length => Math.Max(0, End - Start);
    }

    public record Point2D(double X, double Y)
    {
        public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    }

    public record Rect2D(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public Point2D Center => new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);
        public Point2D BottomLeft => new(MinX, MinY);
        public Point2D TopRight => new(MaxX, MaxY);
    }

    public enum WallDirection
    {
        T1 = 1,
        T2 = 2,
        T3 = 3,
        T4 = 4
    }
}
