using System;

namespace DBTool.Core
{
    /// <summary>
    /// Pure layout calculations for fitting bar diagrams into one Tờ 5 row.
    /// The returned scale affects only the schematic polyline; dimension text
    /// continues to report the exact millimetre values from the schedule.
    /// </summary>
    public static class Sheet5DiagramLayout
    {
        public static (double WidthMm, double HeightMm) GetEnvelope(BarScheduleItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            double l = PositiveOrZero(item.L);
            double lt = PositiveOrZero(item.LT);
            double lp = PositiveOrZero(item.LP);
            double length = PositiveOrZero(item.LengthMm);

            return item.Shape switch
            {
                BarShapeType.L0M => (length, 0),
                BarShapeType.L1M => (length, Math.Max(lt, lp)),
                BarShapeType.L2M => (length, Math.Max(lt, lp)),
                BarShapeType.M_C => (Math.Max(60, lt), l),
                BarShapeType.M_U => (l, Math.Max(60, Math.Max(Math.Max(lt, lp), PositiveOrZero(item.L3)))),
                BarShapeType.M_Z => (2 * Math.Max(100, l), Math.Max(60, lt)),
                BarShapeType.VG => (Math.Max(200, length), 60),
                BarShapeType.GC_T =>
                    (l > 0 ? l : Math.Sqrt(length), lt > 0 ? lt : (l > 0 ? l : Math.Sqrt(length))),
                BarShapeType.GC_H =>
                    (l > 0 ? l : Math.Sqrt(length / Math.PI), l > 0 ? l : Math.Sqrt(length / Math.PI)),
                _ => (length, 0)
            };
        }

        public static double ScaleToFit(
            double widthMm,
            double heightMm,
            double availableWidth,
            double availableHeight,
            double preferredScale = 0.60)
        {
            if (!double.IsFinite(widthMm) || widthMm < 0)
                throw new ArgumentOutOfRangeException(nameof(widthMm));
            if (!double.IsFinite(heightMm) || heightMm < 0)
                throw new ArgumentOutOfRangeException(nameof(heightMm));
            if (widthMm == 0 && heightMm == 0)
                throw new ArgumentException("A bar diagram must have a non-zero envelope.");
            if (!double.IsFinite(availableWidth) || availableWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(availableWidth));
            if (!double.IsFinite(availableHeight) || availableHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(availableHeight));
            if (!double.IsFinite(preferredScale) || preferredScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(preferredScale));

            double scale = preferredScale;
            if (widthMm > 0)
                scale = Math.Min(scale, availableWidth / widthMm);
            if (heightMm > 0)
                scale = Math.Min(scale, availableHeight / heightMm);
            return scale;
        }

        private static double PositiveOrZero(double value)
        {
            return double.IsFinite(value) && value > 0 ? value : 0;
        }
    }
}
