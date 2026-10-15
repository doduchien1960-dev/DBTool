namespace DBTool.Core
{
    // Local model-space origins measured from the Khung insertion in the
    // supplied VHGpro G400-1 structural drawing (1 drawing unit = 1 mm).
    public readonly record struct Sheet1ViewOrigins(
        Point2D Plan,
        Point2D SectionB,
        Point2D SectionARebar,
        Point2D SectionA,
        Point2D WallRebarPlan,
        Point2D SectionBRebar,
        double TopTitleY,
        double BottomTitleY);

    public static class Sheet1ViewLayout
    {
        public static Sheet1ViewOrigins Calculate(Point2D frameOrigin) => new(
            new Point2D(frameOrigin.X + 2295, frameOrigin.Y - 2255),
            new Point2D(frameOrigin.X + 6185, frameOrigin.Y - 3110),
            new Point2D(frameOrigin.X + 10075, frameOrigin.Y - 3110),
            new Point2D(frameOrigin.X + 2295, frameOrigin.Y - 7180),
            new Point2D(frameOrigin.X + 6185, frameOrigin.Y - 6135),
            new Point2D(frameOrigin.X + 10075, frameOrigin.Y - 7180),
            frameOrigin.Y - 300,
            frameOrigin.Y - 4360);

        public static Sheet1ViewOrigins Calculate(
            Point2D frameOrigin, double coverTopZ, ManholeType type)
        {
            // The two supplied G400-1 layouts keep the cover level at a
            // stable sheet Y. The catch-basin profile has 100 mm more room
            // between the two rows of section views.
            bool hasCatchBasin = type == ManholeType.GaThamThuNapRoi;
            double upperSectionY = frameOrigin.Y - 1080 - coverTopZ;
            double lowerSectionY = frameOrigin.Y - (hasCatchBasin ? 5250 : 5150) - coverTopZ;
            return new Sheet1ViewOrigins(
                new Point2D(frameOrigin.X + 2295, frameOrigin.Y - (hasCatchBasin ? 2255 : 2355)),
                new Point2D(frameOrigin.X + 6185, upperSectionY),
                new Point2D(frameOrigin.X + 10075, upperSectionY),
                new Point2D(frameOrigin.X + 2295, lowerSectionY),
                new Point2D(frameOrigin.X + 6185, frameOrigin.Y - 6135),
                new Point2D(frameOrigin.X + 10075, lowerSectionY),
                frameOrigin.Y - 300,
                frameOrigin.Y - 4360);
        }
    }
}
