using System;

namespace DBTool.Core
{
    /// <summary>
    /// Local XY extents of a template block, measured from its definition in the
    /// current drawing database.
    /// </summary>
    public readonly record struct BlockBounds2D(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double CenterX => (MinX + MaxX) / 2.0;
        public double CenterY => (MinY + MaxY) / 2.0;
    }

    /// <summary>
    /// A block insertion transform plus an offset from the design anchor to the
    /// block definition's insertion origin.
    /// </summary>
    public readonly record struct CoverBlockTransform(
        double ScaleX,
        double ScaleY,
        double ScaleZ,
        double OffsetX,
        double OffsetY);

    public static class CoverBlockScaleResolver
    {
        /// <summary>
        /// Resolves the scale and anchor offset for a configured lid block.
        /// Configured template blocks are authored at their stated millimetre
        /// dimensions. Section blocks are vertically scaled to the cover height
        /// specified in Excel while retaining their measured plan width.
        /// The older generic NapGaV block keeps the insertion scales measured in
        /// the VHGpro sample DWGs for backward compatibility.
        /// </summary>
        public static CoverBlockTransform Resolve(
            ManholeType type,
            string blockName,
            string viewSuffix,
            double coverHeight,
            double minX,
            double minY,
            double maxX,
            double maxY)
        {
            if (string.IsNullOrWhiteSpace(blockName))
                throw new ArgumentException("Tên block nắp không được để trống.", nameof(blockName));

            string suffix = viewSuffix?.Trim().ToUpperInvariant() ?? string.Empty;
            if (suffix is not (".MB" or ".AA" or ".BB"))
                throw new ArgumentOutOfRangeException(nameof(viewSuffix), viewSuffix,
                    "Block nắp chỉ hỗ trợ mặt bằng .MB và mặt cắt .AA/.BB.");

            if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
                !double.IsFinite(maxX) || !double.IsFinite(maxY) ||
                maxX <= minX || maxY <= minY)
                throw new ArgumentOutOfRangeException(nameof(maxX),
                    "Bounds block nắp phải hữu hạn và có chiều rộng, chiều cao dương.");

            BlockBounds2D bounds = new(minX, minY, maxX, maxY);
            double scaleX = 1.0;
            double scaleY = 1.0;
            double scaleZ = 1.0;

            if (IsLegacyNapGaV(blockName))
            {
                double legacyScale = suffix == ".MB"
                    ? 0.875
                    : type == ManholeType.GaThamThuNapRoi ? 17.0 / 18.0 : 8.0 / 9.0;
                scaleX = legacyScale;
                scaleY = legacyScale;
                scaleZ = legacyScale;
            }
            else if (suffix is ".AA" or ".BB")
            {
                if (!double.IsFinite(coverHeight) || coverHeight <= 0)
                    throw new ArgumentOutOfRangeException(nameof(coverHeight),
                        "Chiều cao nắp phải là số dương hữu hạn.");

                scaleY = coverHeight / bounds.Height;
            }

            // Plan blocks are centered on the clear opening. In sections, the
            // block's top edge is anchored at the designed cover top so its
            // scaled thickness reaches the supporting slab/mortar elevation.
            double offsetX = -bounds.CenterX * scaleX;
            double offsetY = suffix == ".MB"
                ? -bounds.CenterY * scaleY
                : -bounds.MaxY * scaleY;

            return new CoverBlockTransform(scaleX, scaleY, scaleZ, offsetX, offsetY);
        }

        private static bool IsLegacyNapGaV(string blockName) =>
            blockName.Contains("NapGaV", StringComparison.OrdinalIgnoreCase);
    }
}
