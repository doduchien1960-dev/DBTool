using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;
public sealed record PresentationRequest
{
    public PresentationRequest(double scaleDenominator, ElevationMode elevationMode, ElevationM? baseElevationM, string themeId, bool includeDrawing)
        : this(scaleDenominator, elevationMode, baseElevationM, themeId, includeDrawing, null, "Fixed", null)
    {
    }

    public PresentationRequest(double scaleDenominator, ElevationMode elevationMode, ElevationM? baseElevationM, string themeId, bool includeDrawing, double? panelScale, string scaleMode = "Fixed", double? viewSpacingMm = null)
    {
        ScaleDenominator = ContractGuard.Positive(scaleDenominator, nameof(scaleDenominator));
        ElevationMode = ContractGuard.EnumValue(elevationMode);
        BaseElevationM = baseElevationM;
        ThemeId = ContractGuard.Text(themeId, nameof(themeId));
        IncludeDrawing = includeDrawing;
        PanelScale = panelScale is not null ? ContractGuard.Positive(panelScale.Value, nameof(panelScale)) : null;
        ScaleMode = ContractGuard.Text(scaleMode, nameof(scaleMode));
        ViewSpacingMm = viewSpacingMm is not null ? ContractGuard.NonNegative(viewSpacingMm.Value, nameof(viewSpacingMm)) : null;
        if (elevationMode == ElevationMode.Absolute && baseElevationM == null) throw new ArgumentException("Absolute presentation needs datum.");
    }

    public double ScaleDenominator { get; }
    public double BodyScale => ScaleDenominator;
    public double? PanelScale { get; }
    public string ScaleMode { get; }
    public double? ViewSpacingMm { get; }
    public ElevationMode ElevationMode { get; }
    public ElevationM? BaseElevationM { get; }
    public string ThemeId { get; }
    public bool IncludeDrawing { get; }
}
