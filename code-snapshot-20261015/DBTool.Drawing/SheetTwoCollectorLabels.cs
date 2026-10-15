using System.Globalization;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Binds each actual collector group to its own bar label and leader with shared lane allocation.</summary>
public static class SheetTwoCollectorLabels
{
    public static IReadOnlyList<DrawingPrimitive> Apply(IEnumerable<DrawingPrimitive> primitives,
        IReadOnlyDictionary<string,(int Diameter,double? Pitch)> metadata)
    {
        var result=primitives.ToList();
        var snapshot=result.ToArray();
        var collectors=snapshot.OfType<PolylinePrimitive>().Where(p=>p.Role==SheetTwoGachCollector.CollectorRole).ToArray();

        // Process per ViewId so that lane allocation is shared across all collector groups in the view (L01)
        foreach(var viewGroup in collectors.GroupBy(p=>p.ViewId).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            string viewId = viewGroup.Key;
            var viewCollectors = viewGroup.OrderBy(p=>p.SemanticId,StringComparer.Ordinal).ToArray();

            // Find all existing nominal callouts in this view to remove them and extract prototypes
            var labels = snapshot.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoBarAnnotations.Role &&
                p.ViewId == viewId && !p.SemanticId.EndsWith("/Anchorage", StringComparison.Ordinal))
                .OrderBy(p => p.SemanticId, StringComparer.Ordinal).ToArray();
            var removed = labels.Select(p => p.SemanticId).ToHashSet(StringComparer.Ordinal);
            result.RemoveAll(p => removed.Contains(p.SemanticId) ||
                (p.Role == "Rebar/Leader" && removed.Contains(p.SemanticId.EndsWith("/Leader", StringComparison.Ordinal)
                    ? p.SemanticId[..^7] : "")));

            var context = snapshot.OfType<PolylinePrimitive>().Where(p => p.ViewId == viewId &&
                p.Role is "Rebar/Main" or "Rebar/Top" or "Rebar/Bottom" or "Geometry/BaseOutline"
                       or SheetTwoMaterialViews.BaseRole or SheetTwoMaterialViews.WallRole)
                .SelectMany(p => p.Points).ToArray();
            if (context.Length == 0)
                context = snapshot.OfType<PolylinePrimitive>().Where(p => p.ViewId == viewId)
                    .SelectMany(p => p.Points).ToArray();

            double left = context.Min(p => p.X), bottom = context.Min(p => p.Y), top = context.Max(p => p.Y);
            bool isSlab = viewId is "BaseTop" or "BaseBottom";
            if (!isSlab)
            {
                var topPoints = context.Where(p => Math.Abs(p.Y - top) < 1e-3).ToArray();
                if (topPoints.Length > 0) left = topPoints.Min(p => p.X);
            }

            // Shared lane allocator for this entire view across all families
            int bottomSlot = 0;
            int leftSlot = 0;

            // Pre-count collectors per direction/family to know if branch letters are needed
            var collectorDirections = viewCollectors.Select(c =>
            {
                double cdx = Math.Abs(c.Points[^1].X - c.Points[0].X);
                double cdy = Math.Abs(c.Points[^1].Y - c.Points[0].Y);
                bool horiz = cdx >= cdy;
                bool bottom = isSlab ? !horiz : horiz;
                string baseMark = isSlab ? (!horiz ? "D1" : "D2") : (horiz ? "T1" : "T2");
                return (Collector: c, IsBottom: bottom, BaseMark: baseMark);
            }).ToArray();

            var markCounts = collectorDirections.GroupBy(c => c.BaseMark)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var markIndices = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var item in collectorDirections)
            {
                var collector = item.Collector;
                var ticks = snapshot.OfType<BlockPrimitive>().Where(p => p.Role == SheetTwoGach.Role &&
                    p.SemanticId.StartsWith(collector.SemanticId + "/Gach/", StringComparison.Ordinal))
                    .OrderByDescending(p => p.OccurrenceId == collector.OccurrenceId)
                    .ThenBy(p => p.SemanticId, StringComparer.Ordinal).ToArray();
                if (ticks.Length == 0) continue;

                var tick = ticks[0];
                if (tick.OccurrenceId is not { } occurrence || !metadata.TryGetValue(occurrence, out var data))
                    continue;

                // Match prototype if any exists for this occurrence or mark
                var proto = labels.FirstOrDefault(l => l.OccurrenceId == occurrence || l.ScheduleMark == tick.ScheduleMark)
                    ?? labels.FirstOrDefault();

                // Compute display mark (with branches A/B only if multiple collectors in same base mark)
                string displayMark = item.BaseMark;
                if (markCounts[item.BaseMark] > 1)
                {
                    int idx = markIndices.GetValueOrDefault(item.BaseMark, 0);
                    markIndices[item.BaseMark] = idx + 1;
                    displayMark += (char)('A' + idx);
                }

                Point2Mm insertion;
                string blockRole;

                var pts = new List<Point2Mm> { tick.Insertion };
                void AddPoint(Point2Mm pt)
                {
                    if (Math.Abs(pt.X - pts[^1].X) > 1e-4 || Math.Abs(pt.Y - pts[^1].Y) > 1e-4)
                        pts.Add(pt);
                }

                if (item.IsBottom)
                {
                    double posX = left + (isSlab ? 340 : 315) + (bottomSlot % 3) * 420;
                    double posY = bottom - 150 - (bottomSlot / 3) * 180;
                    bottomSlot++;
                    insertion = new Point2Mm(posX, posY);
                    blockRole = isSlab ? "Block:BLT1" : "Block:BLT2";
                    AddPoint(new Point2Mm(tick.Insertion.X, posY));
                    AddPoint(insertion);
                }
                else
                {
                    double posX = left - 30;
                    double posY = top - (isSlab ? 280 : 345) - leftSlot * 180;
                    leftSlot++;
                    insertion = new Point2Mm(posX, posY);
                    blockRole = "Block:BLT2";
                    AddPoint(new Point2Mm(posX + 30, tick.Insertion.Y));
                    AddPoint(new Point2Mm(posX + 30, posY));
                    AddPoint(insertion);
                }

                int dia = data.Diameter > 0 ? data.Diameter : 10;
                string pitchStr = data.Pitch is > 0 ? "@" + data.Pitch.Value.ToString("0.###", CultureInfo.InvariantCulture) : " ";

                var attrs = new[]
                {
                    new BlockAttributeIntent("SH", displayMark),
                    new BlockAttributeIntent("DK", "%%c" + dia.ToString(CultureInfo.InvariantCulture)),
                    new BlockAttributeIntent("A", pitchStr)
                };

                string id = collector.SemanticId + "/Callout";
                result.Add(new BlockPrimitive(id, SheetTwoBarAnnotations.Role, tick.ViewId, tick.ComponentId,
                    proto?.BlockRole ?? blockRole, insertion, proto?.AnnotationScale ?? 0.3, attrs,
                    proto?.RotationRadians ?? 0, occurrence, tick.ScheduleMark, proto?.MirrorHorizontal ?? false));

                if (pts.Count >= 2)
                {
                    result.Add(new PolylinePrimitive(id + "/Leader", "Rebar/Leader", tick.ViewId, tick.ComponentId,
                        occurrence, tick.ScheduleMark, pts, false));
                }
            }
        }
        return result;
    }
}
