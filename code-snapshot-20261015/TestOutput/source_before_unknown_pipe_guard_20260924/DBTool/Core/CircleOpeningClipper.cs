using System;
using System.Collections.Generic;

namespace DBTool.Core
{
    /// <summary>
    /// A retained one-dimensional interval after removing the intersection
    /// with a circular opening.
    /// </summary>
    public readonly record struct Interval1D(double Start, double End)
    {
        public double Length => Math.Max(0, End - Start);
    }

    /// <summary>
    /// Pure geometry helpers for clipping a straight bar centerline around
    /// a circular opening. The caller supplies the effective exclusion radius,
    /// including any specified cover/clearance.
    /// </summary>
    public static class CircleOpeningClipper
    {
        public static IReadOnlyList<Interval1D> SubtractCircleFromHorizontal(
            double startX,
            double endX,
            double y,
            double centerX,
            double centerY,
            double exclusionRadius,
            double tolerance = 1e-9)
        {
            Validate(startX, endX, y, centerX, centerY, exclusionRadius, tolerance);

            double offset = y - centerY;
            if (Math.Abs(offset) >= exclusionRadius - tolerance)
                return new[] { new Interval1D(startX, endX) };

            double halfChord = Math.Sqrt(Math.Max(0, exclusionRadius * exclusionRadius - offset * offset));
            return SubtractInterval(startX, endX, centerX - halfChord, centerX + halfChord, tolerance);
        }

        public static IReadOnlyList<Interval1D> SubtractCircleFromVertical(
            double startY,
            double endY,
            double x,
            double centerX,
            double centerY,
            double exclusionRadius,
            double tolerance = 1e-9)
        {
            Validate(startY, endY, x, centerX, centerY, exclusionRadius, tolerance);

            double offset = x - centerX;
            if (Math.Abs(offset) >= exclusionRadius - tolerance)
                return new[] { new Interval1D(startY, endY) };

            double halfChord = Math.Sqrt(Math.Max(0, exclusionRadius * exclusionRadius - offset * offset));
            return SubtractInterval(startY, endY, centerY - halfChord, centerY + halfChord, tolerance);
        }

        private static IReadOnlyList<Interval1D> SubtractInterval(
            double start,
            double end,
            double cutStart,
            double cutEnd,
            double tolerance)
        {
            double clippedStart = Math.Max(start, cutStart);
            double clippedEnd = Math.Min(end, cutEnd);

            if (clippedEnd - clippedStart <= tolerance)
                return new[] { new Interval1D(start, end) };

            var retained = new List<Interval1D>(2);
            if (clippedStart - start > tolerance)
                retained.Add(new Interval1D(start, clippedStart));
            if (end - clippedEnd > tolerance)
                retained.Add(new Interval1D(clippedEnd, end));
            return retained;
        }

        private static void Validate(
            double start,
            double end,
            double fixedCoordinate,
            double centerX,
            double centerY,
            double radius,
            double tolerance)
        {
            if (!double.IsFinite(start) || !double.IsFinite(end) || !double.IsFinite(fixedCoordinate) ||
                !double.IsFinite(centerX) || !double.IsFinite(centerY) || !double.IsFinite(radius) ||
                !double.IsFinite(tolerance))
                throw new ArgumentOutOfRangeException(nameof(radius), "Circle clipping inputs must be finite.");
            if (end < start)
                throw new ArgumentException("The interval end must be greater than or equal to its start.");
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), "The exclusion radius cannot be negative.");
            if (tolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance), "The tolerance cannot be negative.");
        }
    }
}
