using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed partial record WallFrame
{
    /// <summary>
    /// Creates the FRAME-001 basis at the centre of the inner wall face.
    /// U points left when looking from inside out, V points outward, and Z points up.
    /// Dimensions and coordinates are in millimetres.
    /// </summary>
    public static WallFrame Create(WallId wallId, double innerLmm, double innerBmm)
    {
        ContractGuard.EnumValue(wallId);
        ContractGuard.Positive(innerLmm, nameof(innerLmm));
        ContractGuard.Positive(innerBmm, nameof(innerBmm));
        var up = new Vector3(0, 0, 1);
        return wallId switch
        {
            WallId.T1 => new(wallId, new Point3Mm(0, innerBmm / 2, 0),
                new Vector3(-1, 0, 0), new Vector3(0, 1, 0), up),
            WallId.T2 => new(wallId, new Point3Mm(innerLmm / 2, 0, 0),
                new Vector3(0, 1, 0), new Vector3(1, 0, 0), up),
            WallId.T3 => new(wallId, new Point3Mm(0, -innerBmm / 2, 0),
                new Vector3(1, 0, 0), new Vector3(0, -1, 0), up),
            WallId.T4 => new(wallId, new Point3Mm(-innerLmm / 2, 0, 0),
                new Vector3(0, -1, 0), new Vector3(-1, 0, 0), up),
            _ => throw new ArgumentOutOfRangeException(nameof(wallId))
        };
    }

    /// <summary>Maps local millimetres to world coordinates; V=0 is inside, V=t is outside.</summary>
    public Point3Mm ToGlobal(double uMm, double vMm, double zMm)
    {
        ContractGuard.Finite(uMm, nameof(uMm));
        ContractGuard.Finite(vMm, nameof(vMm));
        ContractGuard.Finite(zMm, nameof(zMm));
        return new Point3Mm(
            Origin.X + uMm * Left.X + vMm * Outward.X + zMm * Up.X,
            Origin.Y + uMm * Left.Y + vMm * Outward.Y + zMm * Up.Y,
            Origin.Z + uMm * Left.Z + vMm * Outward.Z + zMm * Up.Z);
    }

    /// <summary>Projects the displacement from the origin onto the orthonormal basis.</summary>
    public LocalWallPointMm ToLocal(Point3Mm point)
    {
        var displacement = new Vector3(point.X - Origin.X, point.Y - Origin.Y, point.Z - Origin.Z);
        return new LocalWallPointMm(
            FrameInvariant.Dot(displacement, Left),
            FrameInvariant.Dot(displacement, Outward),
            FrameInvariant.Dot(displacement, Up));
    }

    /// <summary>Face elevation viewed from inside out: screen right is -U for every wall.</summary>
    public Point2Mm ToFaceScreen(LocalWallPointMm point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new Point2Mm(-point.U, point.Z);
    }
}
