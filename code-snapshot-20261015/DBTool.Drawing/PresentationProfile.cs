using System.Collections.Immutable;
using System.Globalization;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public sealed record PresentationProfile
{
    public PresentationProfile(double paperWidthMm, double paperHeightMm,
        double marginLeftMm, double marginBottomMm, double marginRightMm, double marginTopMm,
        double titleblockReservedMm, double interViewGapMm, IEnumerable<double> allowedScales,
        double primaryScale, IEnumerable<double> textPaperHeights, string themeId, bool allowRotation = false)
        : this(paperWidthMm, paperHeightMm, marginLeftMm, marginBottomMm, marginRightMm, marginTopMm,
              titleblockReservedMm, interViewGapMm, allowedScales, primaryScale, textPaperHeights, themeId, allowRotation,
              1.0, null, null)
    {
    }

    public PresentationProfile(double paperWidthMm, double paperHeightMm,
        double marginLeftMm, double marginBottomMm, double marginRightMm, double marginTopMm,
        double titleblockReservedMm, double interViewGapMm, IEnumerable<double> allowedScales,
        double primaryScale, IEnumerable<double> textPaperHeights, string themeId, bool allowRotation,
        double minInkGapMm, string? approvalId = null, IEnumerable<SourceRef>? sources = null)
    {
        PaperWidthMm = ContractGuard.Positive(paperWidthMm, nameof(paperWidthMm));
        PaperHeightMm = ContractGuard.Positive(paperHeightMm, nameof(paperHeightMm));
        MarginLeftMm = ContractGuard.NonNegative(marginLeftMm, nameof(marginLeftMm));
        MarginBottomMm = ContractGuard.NonNegative(marginBottomMm, nameof(marginBottomMm));
        MarginRightMm = ContractGuard.NonNegative(marginRightMm, nameof(marginRightMm));
        MarginTopMm = ContractGuard.NonNegative(marginTopMm, nameof(marginTopMm));
        TitleblockReservedMm = ContractGuard.NonNegative(titleblockReservedMm, nameof(titleblockReservedMm));
        InterViewGapMm = ContractGuard.NonNegative(interViewGapMm, nameof(interViewGapMm));
        AllowedScales = ContractGuard.Copy(allowedScales.Select(scale => ContractGuard.Positive(scale, nameof(allowedScales))));
        if (AllowedScales.IsEmpty || AllowedScales.Distinct().Count() != AllowedScales.Length)
            throw new ArgumentException("At least one distinct scale is required.", nameof(allowedScales));
        PrimaryScale = ContractGuard.Positive(primaryScale, nameof(primaryScale));
        if (!AllowedScales.Contains(PrimaryScale)) throw new ArgumentException("Primary scale must be allowed.", nameof(primaryScale));
        TextPaperHeights = ContractGuard.Copy(textPaperHeights.Select(height => ContractGuard.Positive(height, nameof(textPaperHeights))));
        if (TextPaperHeights.IsEmpty) throw new ArgumentException("At least one text height is required.", nameof(textPaperHeights));
        ThemeId = ContractGuard.Text(themeId, nameof(themeId));
        AllowRotation = allowRotation;
        MinInkGapMm = ContractGuard.NonNegative(minInkGapMm, nameof(minInkGapMm));
        ApprovalId = approvalId;
        Sources = sources is null ? ImmutableArray<SourceRef>.Empty : ContractGuard.Copy(sources);
        if (PrintableWidthMm <= 0 || PrintableHeightMm <= 0) throw new ArgumentException("Printable area must be positive.");
    }

    public double PaperWidthMm { get; }
    public double PaperHeightMm { get; }
    public double MarginLeftMm { get; }
    public double MarginBottomMm { get; }
    public double MarginRightMm { get; }
    public double MarginTopMm { get; }
    public double TitleblockReservedMm { get; }
    public double InterViewGapMm { get; }
    public ImmutableArray<double> AllowedScales { get; }
    public double PrimaryScale { get; }
    public ImmutableArray<double> TextPaperHeights { get; }
    public string ThemeId { get; }
    public bool AllowRotation { get; }
    public double MinInkGapMm { get; }
    public string? ApprovalId { get; }
    public ImmutableArray<SourceRef> Sources { get; }
    public double ModelWidthMm => PaperWidthMm * PrimaryScale;
    public double ModelHeightMm => PaperHeightMm * PrimaryScale;
    public double PrintableWidthMm => PaperWidthMm - MarginLeftMm - MarginRightMm - TitleblockReservedMm;
    public double PrintableHeightMm => PaperHeightMm - MarginBottomMm - MarginTopMm;
    public Bounds2Mm PrintableBounds => new(MarginLeftMm, MarginBottomMm,
        PaperWidthMm - MarginRightMm - TitleblockReservedMm, PaperHeightMm - MarginTopMm);
    public MarginsMm Margins => new(MarginLeftMm, MarginBottomMm, MarginRightMm, MarginTopMm);

    public static double ModelTextHeight(double paperHeightMm, double scaleDenominator) =>
        ContractGuard.Positive(paperHeightMm, nameof(paperHeightMm)) * ContractGuard.Positive(scaleDenominator, nameof(scaleDenominator));

    public static string LabelFor(double scaleDenominator) =>
        $"1:{scaleDenominator.ToString("0.###############", CultureInfo.InvariantCulture)}";
}

public sealed record MarginsMm
{
    public MarginsMm(double left, double bottom, double right, double top)
    {
        Left = ContractGuard.NonNegative(left, nameof(left));
        Bottom = ContractGuard.NonNegative(bottom, nameof(bottom));
        Right = ContractGuard.NonNegative(right, nameof(right));
        Top = ContractGuard.NonNegative(top, nameof(top));
    }
    public double Left { get; }
    public double Bottom { get; }
    public double Right { get; }
    public double Top { get; }
}
