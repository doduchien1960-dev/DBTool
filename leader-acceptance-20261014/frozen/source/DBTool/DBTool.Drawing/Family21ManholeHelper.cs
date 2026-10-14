using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public sealed record RecipeContext
{
    public RecipeContext(string recipeId, string manholeType, int visitProfile, IReadOnlyList<string> roleOrder, double preferredScaleDenominator, Bounds2Mm contentFrame)
    {
        RecipeId = recipeId;
        ManholeType = manholeType;
        VisitProfile = visitProfile;
        RoleOrder = roleOrder;
        PreferredScaleDenominator = preferredScaleDenominator;
        ContentFrame = contentFrame;
    }
    public string RecipeId { get; }
    public string ManholeType { get; }
    public int VisitProfile { get; }
    public IReadOnlyList<string> RoleOrder { get; }
    public double PreferredScaleDenominator { get; }
    public Bounds2Mm ContentFrame { get; }
}


/// <summary>Helper to resolve the active standard manhole code (G400-1, G500-2, G600-3, G800-1, G800-2)
/// and provide exact CAD model space coordinates across all sheets and sizes.</summary>
public static class Family21ManholeHelper
{
    public const string DefaultManhole = "G500-2";

    public static string? Resolve(DrawingInputs? inputs)
    {
        if (inputs is null) return null;
        if (!string.IsNullOrWhiteSpace(inputs.ManholeId))
        {
            var match = Normalize(inputs.ManholeId);
            if (match is not null) return match;
        }
        return null;
    }

    public static string? ResolveFromGeometry(GeometryModel? geometry)
    {
        if (geometry is null) return null;

        // Determine chamber interior clear length (L) and width (B) from wall components
        var t2 = geometry.Components.FirstOrDefault(c => c.Id == "Wall/T2");
        var t4 = geometry.Components.FirstOrDefault(c => c.Id == "Wall/T4");
        var t1 = geometry.Components.FirstOrDefault(c => c.Id == "Wall/T1");
        var t3 = geometry.Components.FirstOrDefault(c => c.Id == "Wall/T3");

        if (t2 is not null && t4 is not null && t1 is not null && t3 is not null)
        {
            double minX = t4.Footprint.Outer.Vertices.Max(p => p.X);
            double maxX = t2.Footprint.Outer.Vertices.Min(p => p.X);
            double minY = t3.Footprint.Outer.Vertices.Max(p => p.Y);
            double maxY = t1.Footprint.Outer.Vertices.Min(p => p.Y);

            double length = Math.Round(Math.Abs(maxX - minX));
            double width = Math.Round(Math.Abs(maxY - minY));

            if (Math.Abs(length - 1000) < 1e-3 && Math.Abs(width - 1100) < 1e-3) return "G400-1";
            if (Math.Abs(length - 1150) < 1e-3 && Math.Abs(width - 1350) < 1e-3) return "G500-2";
            if (Math.Abs(length - 1200) < 1e-3 && Math.Abs(width - 1450) < 1e-3) return "G600-3";
            if (Math.Abs(length - 1250) < 1e-3 && Math.Abs(width - 1550) < 1e-3) return "G800-1";
            if (Math.Abs(length - 1300) < 1e-3 && Math.Abs(width - 1650) < 1e-3) return "G800-2";
        }

        return null;
    }

    public static string? ResolveFromPackage(DrawingPackage package)
    {
        // Check text primitives for explicit code
        foreach (var p in package.Primitives.OfType<TextPrimitive>())
        {
            if (p.Content.Contains("G400-1", StringComparison.OrdinalIgnoreCase)) return "G400-1";
            if (p.Content.Contains("G500-2", StringComparison.OrdinalIgnoreCase)) return "G500-2";
            if (p.Content.Contains("G600-3", StringComparison.OrdinalIgnoreCase)) return "G600-3";
            if (p.Content.Contains("G800-1", StringComparison.OrdinalIgnoreCase)) return "G800-1";
            if (p.Content.Contains("G800-2", StringComparison.OrdinalIgnoreCase)) return "G800-2";
        }

        // Resolve each view independently: combining displaced views changes the measured width.
        foreach (var view in new[] { "Plan", "WallMeshPlan" })
        {
            foreach (var role in new[] { "Geometry/SectionHidden", "Base" })
            {
                var points = package.Primitives.OfType<PolylinePrimitive>()
                    .Where(p => p.ViewId == view && (role == "Base" ? p.ComponentId == "Base" : p.Role == role))
                    .SelectMany(p => p.Points).ToArray();
                if (points.Length == 0) continue;
                double width = points.Max(p => p.X) - points.Min(p => p.X);
                var widths = role == "Base" ? new[] { 1500d, 1650, 1700, 1750, 1800 }
                    : new[] { 1000d, 1150, 1200, 1250, 1300 };
                string[] codes = ["G400-1", "G500-2", "G600-3", "G800-1", "G800-2"];
                for (int i = 0; i < widths.Length; i++)
                    if (Math.Abs(width - widths[i]) < 1) return codes[i];
            }
        }

        return null;
    }

    public static string? Normalize(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        string upper = id.Trim().ToUpperInvariant().Replace('_', '-');
        if (upper == "G400-1" || upper == "G400") return "G400-1";
        if (upper == "G500-2" || upper == "G500") return "G500-2";
        if (upper == "G600-3" || upper == "G600") return "G600-3";
        if (upper == "G800-1") return "G800-1";
        if (upper == "G800-2") return "G800-2";
        return null;
    }

    public static (double FirstX, double Pitch, double BottomTitleY) GetSheetTwoThreeLayout(string code) => code switch
    {
        "G400-1" => (2295.0, 3390.0, 4550.0),
        "G500-2" => (2345.0, 3490.0, 4450.0),
        "G600-3" => (2395.0, 3590.0, 4500.0),
        "G800-1" => (2345.0, 3490.0, 4480.0),
        "G800-2" => (2395.0, 3590.0, 4380.0),
        _ => (2345.0, 3490.0, 4450.0)
    };

    public static Point2Mm? GetSheetTwoOrigin(string view, string code)
    {
        var (firstX, pitch, _) = GetSheetTwoThreeLayout(code);
        return (code, view) switch
        {
            // G400-1
            ("G400-1", "BaseTop") => new(2295.0, 6835.0),
            ("G400-1", "BaseBottom") => new(2295.0, 2775.0),
            ("G400-1", "Reinforcement/Wall/T1/Wall/T1/Outer/Top" or "Reinforcement/Wall/T1/Wall/T1/Default/Bottom") => new(5685.0, 6860.0),
            ("G400-1", "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom") => new(5685.0, 2800.0),
            ("G400-1", "Reinforcement/Wall/T2/Wall/T2/Outer/Top" or "Reinforcement/Wall/T2/Wall/T2/Default/Bottom") => new(9075.0, 6860.0),
            ("G400-1", "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom") => new(9075.0, 2800.0),

            // G500-2
            ("G500-2", "BaseTop") => new(2345.0, 6785.0),
            ("G500-2", "BaseBottom") => new(2345.0, 2625.0),
            ("G500-2", "Reinforcement/Wall/T1/Wall/T1/Outer/Top" or "Reinforcement/Wall/T1/Wall/T1/Default/Bottom") => new(5835.0, 6835.0),
            ("G500-2", "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom") => new(5835.0, 2675.0),
            ("G500-2", "Reinforcement/Wall/T2/Wall/T2/Outer/Top" or "Reinforcement/Wall/T2/Wall/T2/Default/Bottom") => new(9325.0, 6835.0),
            ("G500-2", "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom") => new(9325.0, 2675.0),

            // G600-3
            ("G600-3", "BaseTop") => new(2395.0, 6735.0),
            ("G600-3", "BaseBottom") => new(2395.0, 2625.0),
            ("G600-3", "Reinforcement/Wall/T1/Wall/T1/Outer/Top" or "Reinforcement/Wall/T1/Wall/T1/Default/Bottom") => new(5985.0, 6810.0),
            ("G600-3", "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom") => new(5985.0, 2700.0),
            ("G600-3", "Reinforcement/Wall/T2/Wall/T2/Outer/Top" or "Reinforcement/Wall/T2/Wall/T2/Default/Bottom") => new(9575.0, 6810.0),
            ("G600-3", "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom") => new(9575.0, 2700.0),

            // G800-1
            ("G800-1", "BaseTop") => new(2345.0, 6785.0),
            ("G800-1", "BaseBottom") => new(2345.0, 2655.0),
            ("G800-1", "Reinforcement/Wall/T1/Wall/T1/Outer/Top" or "Reinforcement/Wall/T1/Wall/T1/Default/Bottom") => new(5835.0, 6785.0),
            ("G800-1", "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom") => new(5835.0, 2655.0),
            ("G800-1", "Reinforcement/Wall/T2/Wall/T2/Outer/Top" or "Reinforcement/Wall/T2/Wall/T2/Default/Bottom") => new(9325.0, 6785.0),
            ("G800-1", "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom") => new(9325.0, 2655.0),

            // G800-2
            ("G800-2", "BaseTop") => new(2395.0, 6735.0),
            ("G800-2", "BaseBottom") => new(2395.0, 2505.0),
            ("G800-2", "Reinforcement/Wall/T1/Wall/T1/Outer/Top" or "Reinforcement/Wall/T1/Wall/T1/Default/Bottom") => new(5985.0, 6760.0),
            ("G800-2", "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom") => new(5985.0, 2530.0),
            ("G800-2", "Reinforcement/Wall/T2/Wall/T2/Outer/Top" or "Reinforcement/Wall/T2/Wall/T2/Default/Bottom") => new(9575.0, 6760.0),
            ("G800-2", "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom") => new(9575.0, 2530.0),

            _ => null
        };
    }

    public static Point2Mm? GetSheetThreeOrigin(string view, string code)
    {
        return (code, view) switch
        {
            // G400-1
            ("G400-1", "Reinforcement/Wall/T3/Wall/T3/Outer/Top" or "Reinforcement/Wall/T3/Wall/T3/Default/Bottom") => new(2295.0, 6860.0),
            ("G400-1", "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom") => new(2295.0, 2800.0),
            ("G400-1", "Reinforcement/Wall/T4/Wall/T4/Outer/Top" or "Reinforcement/Wall/T4/Wall/T4/Default/Bottom") => new(5685.0, 6860.0),
            ("G400-1", "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom") => new(5685.0, 2800.0),
            ("G400-1", "Reinforcement/Roof/Roof/Top/Outer/Top" or "Reinforcement/Roof/Top/Default/Top") => new(9075.0, 6935.0),
            ("G400-1", "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" or "Reinforcement/Roof/Bottom/Default/Bottom") => new(9075.0, 2875.0),

            // G500-2
            ("G500-2", "Reinforcement/Wall/T3/Wall/T3/Outer/Top" or "Reinforcement/Wall/T3/Wall/T3/Default/Bottom") => new(2345.0, 6835.0),
            ("G500-2", "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom") => new(2345.0, 2675.0),
            ("G500-2", "Reinforcement/Wall/T4/Wall/T4/Outer/Top" or "Reinforcement/Wall/T4/Wall/T4/Default/Bottom") => new(5835.0, 6835.0),
            ("G500-2", "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom") => new(5835.0, 2675.0),
            ("G500-2", "Reinforcement/Roof/Roof/Top/Outer/Top" or "Reinforcement/Roof/Top/Default/Top") => new(9325.0, 6885.0),
            ("G500-2", "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" or "Reinforcement/Roof/Bottom/Default/Bottom") => new(9325.0, 2725.0),

            // G600-3
            ("G600-3", "Reinforcement/Wall/T3/Wall/T3/Outer/Top" or "Reinforcement/Wall/T3/Wall/T3/Default/Bottom") => new(2395.0, 6810.0),
            ("G600-3", "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom") => new(2395.0, 2700.0),
            ("G600-3", "Reinforcement/Wall/T4/Wall/T4/Outer/Top" or "Reinforcement/Wall/T4/Wall/T4/Default/Bottom") => new(5985.0, 6810.0),
            ("G600-3", "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom") => new(5985.0, 2700.0),
            ("G600-3", "Reinforcement/Roof/Roof/Top/Outer/Top" or "Reinforcement/Roof/Top/Default/Top") => new(9575.0, 6835.0),
            ("G600-3", "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" or "Reinforcement/Roof/Bottom/Default/Bottom") => new(9575.0, 2725.0),

            // G800-1
            ("G800-1", "Reinforcement/Wall/T3/Wall/T3/Outer/Top" or "Reinforcement/Wall/T3/Wall/T3/Default/Bottom") => new(2345.0, 6785.0),
            ("G800-1", "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom") => new(2345.0, 2655.0),
            ("G800-1", "Reinforcement/Wall/T4/Wall/T4/Outer/Top" or "Reinforcement/Wall/T4/Wall/T4/Default/Bottom") => new(5835.0, 6785.0),
            ("G800-1", "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom") => new(5835.0, 2655.0),
            ("G800-1", "Reinforcement/Roof/Roof/Top/Outer/Top" or "Reinforcement/Roof/Top/Default/Top") => new(9325.0, 6785.0),
            ("G800-1", "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" or "Reinforcement/Roof/Bottom/Default/Bottom") => new(9325.0, 2655.0),

            // G800-2
            ("G800-2", "Reinforcement/Wall/T3/Wall/T3/Outer/Top" or "Reinforcement/Wall/T3/Wall/T3/Default/Bottom") => new(2395.0, 6760.0),
            ("G800-2", "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom") => new(2395.0, 2530.0),
            ("G800-2", "Reinforcement/Wall/T4/Wall/T4/Outer/Top" or "Reinforcement/Wall/T4/Wall/T4/Default/Bottom") => new(5985.0, 6760.0),
            ("G800-2", "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom") => new(5985.0, 2530.0),
            ("G800-2", "Reinforcement/Roof/Roof/Top/Outer/Top" or "Reinforcement/Roof/Top/Default/Top") => new(9575.0, 6735.0),
            ("G800-2", "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" or "Reinforcement/Roof/Bottom/Default/Bottom") => new(9575.0, 2505.0),

            _ => null
        };
    }

    public static Point2Mm? GetSheetTwoTitle(string view, string code)
    {
        var (firstX, pitch, bottomTitleY) = GetSheetTwoThreeLayout(code);
        return view switch
        {
            "BaseTop" => new(firstX, 8610.0),
            "BaseBottom" => new(firstX, bottomTitleY),
            "Reinforcement/Wall/T1/Wall/T1/Outer/Top" => new(firstX + pitch, 8610.0),
            "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom" => new(firstX + pitch, bottomTitleY),
            "Reinforcement/Wall/T2/Wall/T2/Outer/Top" => new(firstX + 2 * pitch, 8610.0),
            "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom" => new(firstX + 2 * pitch, bottomTitleY),
            _ => null
        };
    }

    public static Point2Mm? GetSheetThreeTitle(string view, string code)
    {
        var (firstX, pitch, bottomTitleY) = GetSheetTwoThreeLayout(code);
        return view switch
        {
            "Reinforcement/Wall/T3/Wall/T3/Outer/Top" => new(firstX, 8610.0),
            "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom" => new(firstX, bottomTitleY),
            "Reinforcement/Wall/T4/Wall/T4/Outer/Top" => new(firstX + pitch, 8610.0),
            "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom" => new(firstX + pitch, bottomTitleY),
            "Reinforcement/Roof/Roof/Top/Outer/Top" => new(firstX + 2 * pitch, 8610.0),
            "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" => new(firstX + 2 * pitch, bottomTitleY),
            _ => null
        };
    }
}
