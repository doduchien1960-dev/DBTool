using System.Collections.Immutable;
using DBTool.Domain.Geometry;
namespace DBTool.Domain;
internal static class FrameInvariant
{
    internal static double Dot(Vector3 a, Vector3 b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static void Check(Vector3 a, Vector3 b, Vector3 c)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b); ArgumentNullException.ThrowIfNull(c);
        if (new[] { Math.Abs(Dot(a,a)-1),Math.Abs(Dot(b,b)-1),Math.Abs(Dot(c,c)-1),Math.Abs(Dot(a,b)),Math.Abs(Dot(a,c)),Math.Abs(Dot(b,c)) }.Any(v => v > 1e-9))
            throw new ArgumentException("Frame must be orthonormal.");
    }
}
public sealed record Frame3Mm
{
    public Frame3Mm(Point3Mm origin, Vector3 u, Vector3 v, Vector3 n)
    {
        Origin = origin;
        U = ContractGuard.Required(u);
        V = ContractGuard.Required(v);
        N = ContractGuard.Required(n);
        FrameInvariant.Check(u,v,n);
    }
    public Point3Mm Origin { get; }
    public Vector3 U { get; }
    public Vector3 V { get; }
    public Vector3 N { get; }
}
public sealed partial record WallFrame
{
    public WallFrame(WallId wallId, Point3Mm origin, Vector3 left, Vector3 outward, Vector3 up)
    {
        WallId = ContractGuard.EnumValue(wallId);
        Origin = origin;
        Left = ContractGuard.Required(left);
        Outward = ContractGuard.Required(outward);
        Up = ContractGuard.Required(up);
        FrameInvariant.Check(left,outward,up);
    }
    public WallId WallId { get; }
    public Point3Mm Origin { get; }
    public Vector3 Left { get; }
    public Vector3 Outward { get; }
    public Vector3 Up { get; }
}
