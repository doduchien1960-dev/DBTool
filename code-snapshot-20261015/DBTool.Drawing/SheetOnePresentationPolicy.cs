using DBTool.Domain;

namespace DBTool.Drawing;

/// <summary>Published family 2.1 first-sheet convention, separate from physical openings.</summary>
public static class SheetOnePresentationPolicy
{
    public const string Family21Theme = "VHG-2.1-SheetOne";
    public static bool Enabled(PresentationRequest presentation) => presentation.ThemeId is Family21Theme or "VHG-Dark";

    public static GeometryModel ProjectionGeometry(GeometryModel physical, PresentationRequest presentation)
    {
        return physical;
    }
}
