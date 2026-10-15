namespace DBTool.Drawing.Tests.Fixtures;

/// <summary>Owner-supplied acceptance targets, separate from the original workbook.
/// Ctb is average neck height (owner correction), not a pipe-centre datum.
/// Remaining dimensions/features must come from matched, source-traced input.
/// </summary>
public static class G400_1_GoldenFixture
{
    public const string Name = "G400-1";
    public const double B = 1250;
    public const double H = 1400;
    public const double Ctb = 480;
    public const double VisitCatchCtb = 600;
    public const double L = 1000;
    public const double Dn = 400;
    public const string GoldenFamily = "2.1.Ga thăm BTCT nắp rời";
}
