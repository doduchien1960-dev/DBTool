namespace DBTool.Core
{
    /// <summary>
    /// Converts the workbook convention (positive offset is left while viewed
    /// from inside the manhole toward the wall exterior) into the local horizontal
    /// coordinate used by wall elevations and wall reinforcement calculations.
    /// </summary>
    public static class WallViewTransform
    {
        public static double ToLocalOffset(WallDirection direction, double offsetFromCenter)
        {
            if (!double.IsFinite(offsetFromCenter))
                throw new ArgumentOutOfRangeException(nameof(offsetFromCenter));

            return direction switch
            {
                WallDirection.T1 => -offsetFromCenter,
                WallDirection.T2 => offsetFromCenter,
                WallDirection.T3 => offsetFromCenter,
                WallDirection.T4 => -offsetFromCenter,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown wall direction.")
            };
        }
    }
}
