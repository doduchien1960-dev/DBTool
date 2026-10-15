using System;
using System.Collections.Generic;

namespace DBTool.Core
{
    /// <summary>
    /// Pure geometry helpers used by the bar bending schedule renderer.
    /// </summary>
    public static class BarBendingGeometry
    {
        public static IReadOnlyList<Point2D> BuildL1MPolyline(
            double straightLengthMm,
            double hookAtStartMm,
            double hookAtEndMm,
            double scale)
        {
            if (!double.IsFinite(straightLengthMm) || straightLengthMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(straightLengthMm));
            if (!double.IsFinite(hookAtStartMm) || hookAtStartMm < 0)
                throw new ArgumentOutOfRangeException(nameof(hookAtStartMm));
            if (!double.IsFinite(hookAtEndMm) || hookAtEndMm < 0)
                throw new ArgumentOutOfRangeException(nameof(hookAtEndMm));
            if (!double.IsFinite(scale) || scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale));
            if (hookAtStartMm > 0 && hookAtEndMm > 0)
                throw new ArgumentException("Thanh 1M chỉ được có một đầu móc.");

            double scaledLength = straightLengthMm * scale;
            if (hookAtStartMm > 0)
            {
                double scaledHook = hookAtStartMm * scale;
                return new[]
                {
                    new Point2D(0, scaledHook),
                    new Point2D(0, 0),
                    new Point2D(scaledLength, 0)
                };
            }

            if (hookAtEndMm > 0)
            {
                double scaledHook = hookAtEndMm * scale;
                return new[]
                {
                    new Point2D(0, 0),
                    new Point2D(scaledLength, 0),
                    new Point2D(scaledLength, scaledHook)
                };
            }

            return new[] { new Point2D(0, 0), new Point2D(scaledLength, 0) };
        }
    }
}
