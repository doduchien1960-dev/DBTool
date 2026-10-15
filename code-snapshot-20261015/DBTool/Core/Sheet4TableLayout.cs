using System;

namespace DBTool.Core
{
    public sealed record TwoColumnTableLayout(
        double FirstColumnX,
        double SecondColumnX,
        double ScaledBlockWidth,
        double InterColumnGap,
        double RightEdge,
        double RightMargin);

    /// <summary>
    /// Computes the two-column statistics-table footprint inside the A3 frame.
    /// All dimensions use ModelSpace millimetres.
    /// </summary>
    public static class Sheet4TableLayout
    {
        public static TwoColumnTableLayout CalculateTwoColumn(
            double frameWidth,
            double firstColumnX,
            double blockBaseWidth,
            double scale,
            double minRightMargin,
            double minColumnGap)
        {
            if (!double.IsFinite(frameWidth) || frameWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameWidth));
            if (!double.IsFinite(firstColumnX) || firstColumnX < 0)
                throw new ArgumentOutOfRangeException(nameof(firstColumnX));
            if (!double.IsFinite(blockBaseWidth) || blockBaseWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(blockBaseWidth));
            if (!double.IsFinite(scale) || scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale));
            if (!double.IsFinite(minRightMargin) || minRightMargin < 0)
                throw new ArgumentOutOfRangeException(nameof(minRightMargin));
            if (!double.IsFinite(minColumnGap) || minColumnGap < 0)
                throw new ArgumentOutOfRangeException(nameof(minColumnGap));

            double scaledWidth = blockBaseWidth * scale;
            double secondColumnX = frameWidth - minRightMargin - scaledWidth;
            double gap = secondColumnX - firstColumnX - scaledWidth;
            if (gap < minColumnGap)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameWidth),
                    "Hai cột bảng thống kê không đáp ứng được lề phải và khoảng cách tối thiểu trong khung.");
            }

            double rightEdge = secondColumnX + scaledWidth;
            return new TwoColumnTableLayout(
                firstColumnX,
                secondColumnX,
                scaledWidth,
                gap,
                rightEdge,
                frameWidth - rightEdge);
        }
    }
}
