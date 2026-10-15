using System;
using System.Collections.Generic;

namespace DBTool.Core
{
    public sealed class SectionWidthDimension
    {
        public SectionWidthDimension(double halfWidth, double offsetFromPadBottom)
        {
            HalfWidth = halfWidth;
            OffsetFromPadBottom = offsetFromPadBottom;
        }

        public double HalfWidth { get; }
        public double OffsetFromPadBottom { get; }
    }

    /// <summary>
    /// Nested width dimensions for a manhole section: clear span, wall outside,
    /// concrete base outside, and bedding outside.
    /// </summary>
    public static class SectionDimensionLayout
    {
        public static IReadOnlyList<SectionWidthDimension> BuildFoundationWidthDimensions(
            SectionGeometryModel section,
            double dimensionOffset)
        {
            if (section is null)
                throw new ArgumentNullException(nameof(section));
            if (!double.IsFinite(dimensionOffset) || dimensionOffset <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimensionOffset),
                    "Khoảng cách các đường kích thước phải là số dương hữu hạn.");

            return new[]
            {
                new SectionWidthDimension(section.HalfSpan, dimensionOffset),
                new SectionWidthDimension(section.HalfOuter, dimensionOffset * 2),
                new SectionWidthDimension(section.HalfBase, dimensionOffset * 3),
                new SectionWidthDimension(section.HalfPad, dimensionOffset * 4)
            };
        }
    }
}
