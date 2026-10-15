using System;
using System.Collections.Generic;

namespace DBTool.Core
{
    /// <summary>
    /// Clips straight reinforcement centerlines around a rectangular opening.
    /// Clearance expands the opening in both axes before the line is cut.
    /// </summary>
    public static class RectangleOpeningClipper
    {
        public static IReadOnlyList<Interval1D> SubtractFromHorizontal(
            double startX,
            double endX,
            double y,
            Rect2D opening,
            double clearance = 0,
            double tolerance = 1e-9)
        {
            Validate(startX, endX, y, opening, clearance, tolerance);
            if (y < opening.MinY - clearance - tolerance || y > opening.MaxY + clearance + tolerance)
                return new[] { new Interval1D(startX, endX) };
            return SubtractInterval(startX, endX,
                opening.MinX - clearance, opening.MaxX + clearance, tolerance);
        }

        public static IReadOnlyList<Interval1D> SubtractFromVertical(
            double startY,
            double endY,
            double x,
            Rect2D opening,
            double clearance = 0,
            double tolerance = 1e-9)
        {
            Validate(startY, endY, x, opening, clearance, tolerance);
            if (x < opening.MinX - clearance - tolerance || x > opening.MaxX + clearance + tolerance)
                return new[] { new Interval1D(startY, endY) };
            return SubtractInterval(startY, endY,
                opening.MinY - clearance, opening.MaxY + clearance, tolerance);
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
            Rect2D opening,
            double clearance,
            double tolerance)
        {
            if (!double.IsFinite(start) || !double.IsFinite(end) || !double.IsFinite(fixedCoordinate) ||
                !double.IsFinite(opening.MinX) || !double.IsFinite(opening.MinY) ||
                !double.IsFinite(opening.MaxX) || !double.IsFinite(opening.MaxY) ||
                !double.IsFinite(clearance) || !double.IsFinite(tolerance))
                throw new ArgumentOutOfRangeException(nameof(opening), "Rectangle clipping inputs must be finite.");
            if (end < start)
                throw new ArgumentException("The interval end must be greater than or equal to its start.");
            if (opening.MaxX < opening.MinX || opening.MaxY < opening.MinY)
                throw new ArgumentException("The opening rectangle must have non-negative width and height.", nameof(opening));
            if (clearance < 0)
                throw new ArgumentOutOfRangeException(nameof(clearance), "The opening clearance cannot be negative.");
            if (tolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance), "The tolerance cannot be negative.");
        }
    }
}
