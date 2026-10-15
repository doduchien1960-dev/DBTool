using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DBTool.Core;

namespace DBTool.Cad
{
    /// <summary>
    /// Các hàm vẽ nguyên thủy CAD an toàn, nhất quán theo đơn vị milimet (1 unit = 1 mm)
    /// </summary>
    public static class CadPrimitives
    {
        public static ObjectId DrawPolyline(
            BlockTableRecord btr,
            Transaction tr,
            IReadOnlyList<Point2D> points,
            bool closed = true,
            string layer = "VHG-BT",
            short colorIndex = 256)
        {
            if (points == null || points.Count < 2) return ObjectId.Null;

            using var pline = new Polyline();
            for (int i = 0; i < points.Count; i++)
            {
                pline.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            pline.Closed = closed;
            pline.Layer = layer;
            pline.ColorIndex = colorIndex;

            btr.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
            return pline.ObjectId;
        }

        public static ObjectId DrawLine(
            BlockTableRecord btr,
            Transaction tr,
            double x1, double y1,
            double x2, double y2,
            string layer = "VHG-BT",
            short colorIndex = 256)
        {
            using var line = new Line(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0))
            {
                Layer = layer,
                ColorIndex = colorIndex
            };
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            return line.ObjectId;
        }

        public static ObjectId DrawRectangle(
            BlockTableRecord btr,
            Transaction tr,
            double minX, double minY,
            double maxX, double maxY,
            string layer = "VHG-BT",
            short colorIndex = 256)
        {
            var pts = new[]
            {
                new Point2D(minX, minY),
                new Point2D(maxX, minY),
                new Point2D(maxX, maxY),
                new Point2D(minX, maxY)
            };
            return DrawPolyline(btr, tr, pts, true, layer, colorIndex);
        }

        public static ObjectId DrawText(
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            string content,
            double x, double y,
            double height = 60,
            string layer = "VHG-Text",
            short colorIndex = 256,
            TextHorizontalMode horiz = TextHorizontalMode.TextCenter,
            ObjectId textStyleId = default)
        {
            if (string.IsNullOrEmpty(content)) return ObjectId.Null;

            var pt = new Point3d(x, y, 0);
            using var text = new DBText
            {
                TextString = content,
                Position = pt,
                Height = height,
                Layer = layer,
                ColorIndex = colorIndex,
                HorizontalMode = horiz,
                TextStyleId = textStyleId.IsNull ? TemplateManager.GetTextStyleId(db) : textStyleId
            };

            if (horiz != TextHorizontalMode.TextLeft)
            {
                text.AlignmentPoint = pt;
                text.AdjustAlignment(db);
            }

            btr.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
            return text.ObjectId;
        }

        public static ObjectId DrawDim(
            BlockTableRecord btr,
            Transaction tr,
            ObjectId dimStyleId,
            double x1, double y1,
            double x2, double y2,
            double xText, double yText,
            double rotation = 0,
            string overrideText = "",
            string layer = "VHG-Dim")
        {
            if (Math.Abs(x1 - x2) < 0.01 && Math.Abs(y1 - y2) < 0.01) return ObjectId.Null;

            using var dim = new RotatedDimension(
                rotation,
                new Point3d(x1, y1, 0),
                new Point3d(x2, y2, 0),
                new Point3d(xText, yText, 0),
                overrideText,
                dimStyleId)
            {
                Layer = layer,
                ColorIndex = 256
            };

            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            return dim.ObjectId;
        }

        public static ObjectId DrawDimNgang(
            BlockTableRecord btr,
            Transaction tr,
            ObjectId dimStyleId,
            double x1, double x2, double y, double yText,
            string overrideText = "",
            string layer = "VHG-Dim")
        {
            return DrawDim(btr, tr, dimStyleId, x1, y, x2, y, (x1 + x2) / 2.0, yText, 0, overrideText, layer);
        }

        public static ObjectId DrawDimDoc(
            BlockTableRecord btr,
            Transaction tr,
            ObjectId dimStyleId,
            double x, double y1, double y2, double xText,
            string overrideText = "",
            string layer = "VHG-Dim")
        {
            return DrawDim(btr, tr, dimStyleId, x, y1, x, y2, xText, (y1 + y2) / 2.0, Math.PI / 2.0, overrideText, layer);
        }

        public static ObjectId InsertBlock(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            string blockName,
            double x, double y,
            double scale = 1.0,
            string layer = "VHG-BT",
            Dictionary<string, string>? attributes = null)
        {
            if (!blockTable.Has(blockName)) return ObjectId.Null;

            ObjectId blockId = blockTable[blockName];
            var blockDef = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);

            using var blkRef = new BlockReference(new Point3d(x, y, 0), blockId)
            {
                ScaleFactors = new Scale3d(scale, scale, scale),
                Layer = layer
            };

            btr.AppendEntity(blkRef);
            tr.AddNewlyCreatedDBObject(blkRef, true);

            // Điền thuộc tính ATTDEF
            if (blockDef.HasAttributeDefinitions && attributes != null)
            {
                foreach (ObjectId attId in blockDef)
                {
                    var ent = tr.GetObject(attId, OpenMode.ForRead);
                    if (ent is AttributeDefinition attDef)
                    {
                        using var attRef = new AttributeReference();
                        attRef.SetAttributeFromBlock(attDef, blkRef.BlockTransform);

                        string tag = attDef.Tag.Trim().ToUpperInvariant();
                        if (attributes.TryGetValue(tag, out string? val))
                        {
                            attRef.TextString = val;
                        }

                        blkRef.AttributeCollection.AppendAttribute(attRef);
                        tr.AddNewlyCreatedDBObject(attRef, true);
                    }
                }
            }

            return blkRef.ObjectId;
        }

        public static ObjectId CreateHatch(
            BlockTableRecord btr,
            Transaction tr,
            string patternName,
            double scale,
            string layer,
            ObjectId outerBoundaryId,
            IEnumerable<ObjectId>? innerHoleIds = null)
        {
            try
            {
                using var hatch = new Hatch();
                hatch.SetHatchPattern(HatchPatternType.PreDefined, patternName);
                hatch.PatternScale = Math.Max(0.1, scale);
                hatch.Layer = layer;
                hatch.ColorIndex = 256;

                btr.AppendEntity(hatch);
                tr.AddNewlyCreatedDBObject(hatch, true);

                hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { outerBoundaryId });
                if (innerHoleIds != null)
                {
                    foreach (var hId in innerHoleIds)
                    {
                        if (!hId.IsNull)
                        {
                            hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection { hId });
                        }
                    }
                }
                hatch.EvaluateHatch(true);
                return hatch.ObjectId;
            }
            catch (Exception)
            {
                return ObjectId.Null;
            }
        }

        public static void DrawElevationSymbol(
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            double x, double y,
            double elevationMeter,
            string labelPrefix = "",
            bool isLeft = true)
        {
            double sign = isLeft ? -1.0 : 1.0;
            double triSize = 60; // Kích thước tam giác cao độ (mm)
            double lineLen = 300;

            // Tam giác cao độ chỉ xuống
            var pts = new[]
            {
                new Point2D(x, y),
                new Point2D(x + sign * triSize, y + triSize),
                new Point2D(x - sign * triSize, y + triSize)
            };
            DrawPolyline(btr, tr, pts, true, "VHG-BT");
            DrawLine(btr, tr, x - sign * triSize - 20, y + triSize, x + sign * lineLen, y + triSize, "VHG-BT");

            string elevText = (elevationMeter >= 0 ? "+" : "") + elevationMeter.ToString("F3");
            string fullText = string.IsNullOrEmpty(labelPrefix) ? elevText : $"{labelPrefix}={elevText}";

            double textX = x + sign * (lineLen / 2.0);
            double textY = y + triSize + 25;
            DrawText(btr, tr, db, fullText, textX, textY, height: 60, layer: "VHG-Text", colorIndex: 7);
        }

        public static void DrawTitleWithScale(
            BlockTable blockTable,
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            string title,
            string scale,
            double x, double y)
        {
            if (blockTable.Has("TenMC"))
            {
                var atts = new Dictionary<string, string>
                {
                    ["TIEUDE"] = title,
                    ["TYLE"] = scale
                };
                InsertBlock(blockTable, btr, tr, "TenMC", x, y, scale: 0.3, layer: "VHG-Text", attributes: atts);
            }
            else
            {
                // Fallback DBText
                DrawText(btr, tr, db, title, x, y, height: 120, layer: "VHG-Text", colorIndex: 3);
                DrawText(btr, tr, db, scale, x, y - 160, height: 80, layer: "VHG-Text", colorIndex: 7);
            }
        }
    }
}
