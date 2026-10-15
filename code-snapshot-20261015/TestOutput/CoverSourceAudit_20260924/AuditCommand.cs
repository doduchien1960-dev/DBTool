using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CoverSourceAudit;
    public sealed class AuditCommand
    {
    [CommandMethod("DBDUMPBLOCKS")]
    public void DumpConfiguredCoverBlocks()
    {
        var doc = Application.DocumentManager.MdiActiveDocument!;
        var db = doc.Database;
        var sb = new StringBuilder();
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId blockId in bt)
        {
            var b = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
            if (!b.Name.StartsWith("VIN.TNM.LD.820x570.", StringComparison.OrdinalIgnoreCase)) continue;
            sb.AppendLine($"BLOCK|{b.Name}|origin={b.Origin}");
            foreach (ObjectId id in b)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
                try
                {
                    var ex = e.GeometricExtents;
                    if (e is Polyline pl)
                    {
                        var pts = new List<string>();
                        for (int i = 0; i < pl.NumberOfVertices; i++) pts.Add(pl.GetPoint2dAt(i).ToString());
                        sb.AppendLine($"PLINE|layer={e.Layer}|closed={pl.Closed}|ext={ex.MinPoint}..{ex.MaxPoint}|pts={string.Join(";", pts)}");
                    }
                    else if (e is Line line)
                        sb.AppendLine($"LINE|layer={e.Layer}|{line.StartPoint}->{line.EndPoint}|ext={ex.MinPoint}..{ex.MaxPoint}");
                    else if (e is DBText txt)
                        sb.AppendLine($"TEXT|layer={e.Layer}|{txt.TextString}|at={txt.Position}|ext={ex.MinPoint}..{ex.MaxPoint}");
                    else if (e is BlockReference br)
                        sb.AppendLine($"INSERT|layer={e.Layer}|block={br.Name}|pos={br.Position}|scale={br.ScaleFactors}|rot={br.Rotation}|ext={ex.MinPoint}..{ex.MaxPoint}");
                    else
                        sb.AppendLine($"ENTITY|type={e.GetType().Name}|layer={e.Layer}|ext={ex.MinPoint}..{ex.MaxPoint}");
                }
                catch { }
            }
        }
        tr.Commit();
        File.WriteAllText(Path.ChangeExtension(db.Filename, ".blockgeom.txt"), sb.ToString(), Encoding.UTF8);
        doc.Editor.WriteMessage("\nDBDUMPBLOCKS completed.\n");
    }

    [CommandMethod("DBDUMPLABELS")]
    public void DumpLabels()
    {
        var doc = Application.DocumentManager.MdiActiveDocument!;
        var db = doc.Database;
        var sb = new StringBuilder();
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            string content = e switch
            {
                DBText t => t.TextString,
                MText t => t.Contents,
                _ => string.Empty
            };
            if (content.Contains("G400-1", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("G400", StringComparison.OrdinalIgnoreCase))
            {
                string location = e switch
                {
                    DBText t => t.Position.ToString(),
                    MText t => t.Location.ToString(),
                    _ => ""
                };
                sb.AppendLine($"LABEL|{e.GetType().Name}|{content}|pos={location}|layer={e.Layer}");
            }
        }
        tr.Commit();
        File.WriteAllText(Path.ChangeExtension(db.Filename, ".labels.txt"), sb.ToString(), Encoding.UTF8);
        doc.Editor.WriteMessage("\nDBDUMPLABELS completed.\n");
    }

    [CommandMethod("DBDUMPFOOT")]
    public void DumpFoot()
    {
        var doc = Application.DocumentManager.MdiActiveDocument!;
        var db = doc.Database;
        var sb = new StringBuilder();
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            try
            {
                var ex = e.GeometricExtents;
                if (ex.MaxPoint.X < -1800 || ex.MinPoint.X > 1800 ||
                    ex.MaxPoint.Y < -71000 || ex.MinPoint.Y > -67500) continue;
                if (e is Polyline pl)
                {
                    var vertices = new List<string>();
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                        vertices.Add($"{pl.GetPoint2dAt(i)}{{bulge={pl.GetBulgeAt(i)}}}");
                    sb.AppendLine($"PLINE|layer={e.Layer}|closed={pl.Closed}|ext={ex.MinPoint}..{ex.MaxPoint}|v={string.Join(";", vertices)}");
                }
                else if (e is Line line)
                    sb.AppendLine($"LINE|layer={e.Layer}|{line.StartPoint}->{line.EndPoint}");
                else if (e is RotatedDimension dim)
                    sb.AppendLine($"DIM|layer={e.Layer}|measurement={dim.Measurement}|text={dim.DimensionText}|p1={dim.XLine1Point}|p2={dim.XLine2Point}|dimline={dim.DimLinePoint}");
                else if (e is MText mtext)
                    sb.AppendLine($"MTEXT|layer={e.Layer}|{mtext.Contents}|at={mtext.Location}");
                else if (e is DBText text)
                    sb.AppendLine($"TEXT|layer={e.Layer}|{text.TextString}|at={text.Position}");
                else if (e is BlockReference blockRef)
                {
                    var attributes = new List<string>();
                    foreach (ObjectId attributeId in blockRef.AttributeCollection)
                    {
                        if (tr.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                            attributes.Add($"{attribute.Tag}={attribute.TextString}");
                    }
                    sb.AppendLine($"INSERT|layer={e.Layer}|block={blockRef.Name}|pos={blockRef.Position}|scale={blockRef.ScaleFactors}|rot={blockRef.Rotation}|attrs={string.Join(";", attributes)}");
                }
                else
                    sb.AppendLine($"ENTITY|type={e.GetType().Name}|layer={e.Layer}|ext={ex.MinPoint}..{ex.MaxPoint}");
            }
            catch { }
        }
        tr.Commit();
        File.WriteAllText(Path.ChangeExtension(db.Filename, ".footgeom.txt"), sb.ToString(), Encoding.UTF8);
        doc.Editor.WriteMessage("\nDBDUMPFOOT completed.\n");
    }

    [CommandMethod("DBAUDITCOVER")]
    public void Run()
    {
        var doc = Application.DocumentManager.MdiActiveDocument!;
        var db = doc.Database;
        var sb = new StringBuilder();
        sb.AppendLine($"DWG|{db.Filename}");
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        sb.AppendLine("[LAYERS]");
        foreach (ObjectId layerId in layerTable)
        {
            var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
            if (layer.Name.Equals("VHG-VL", StringComparison.OrdinalIgnoreCase) ||
                layer.Name.Equals("VHG-BT", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"LAYER|{layer.Name}|color={layer.Color.ColorIndex}|weight={layer.LineWeight}|plot={layer.IsPlottable}");
        }
        sb.AppendLine("[COVER DEFINITIONS]");
        foreach (ObjectId id in bt)
        {
            var b = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
            if (!(b.Name.Contains("NAP", StringComparison.OrdinalIgnoreCase) || b.Name.Contains("TNT", StringComparison.OrdinalIgnoreCase) || b.Name.Contains("VIN", StringComparison.OrdinalIgnoreCase))) continue;
            var box = GetBounds(b, tr);
            sb.AppendLine($"DEF|{b.Name}|origin={b.Origin}|bounds={box}");
        }
        sb.AppendLine("[COVER REFERENCES]");
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
            var b = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
            if (!(b.Name.Contains("NAP", StringComparison.OrdinalIgnoreCase) || b.Name.Contains("TNT", StringComparison.OrdinalIgnoreCase) || b.Name.Contains("VIN", StringComparison.OrdinalIgnoreCase))) continue;
            sb.AppendLine($"REF|{b.Name}|pos={br.Position}|scale={br.ScaleFactors}|rot={br.Rotation}|layer={br.Layer}");
        }
        sb.AppendLine("[HATCHES]");
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not Hatch hatch) continue;
            sb.AppendLine($"HATCH|layer={hatch.Layer}|pattern={hatch.PatternName}|scale={hatch.PatternScale}|space={hatch.PatternSpace}|angle={hatch.PatternAngle}|weight={hatch.LineWeight}|color={hatch.Color.ColorIndex}|solid={hatch.IsSolidFill}|loops={hatch.NumberOfLoops}");
        }
        tr.Commit();
        File.WriteAllText(Path.ChangeExtension(db.Filename, ".coveraudit.txt"), sb.ToString(), Encoding.UTF8);
        doc.Editor.WriteMessage("\nDBAUDITCOVER completed.\n");
    }
    private static string GetBounds(BlockTableRecord b, Transaction tr)
    {
        bool any=false; double minX=0,minY=0,minZ=0,maxX=0,maxY=0,maxZ=0;
        foreach(ObjectId id in b)
        {
            if(tr.GetObject(id,OpenMode.ForRead) is not Entity e) continue;
            try { var ex=e.GeometricExtents; if(!any){minX=ex.MinPoint.X;minY=ex.MinPoint.Y;minZ=ex.MinPoint.Z;maxX=ex.MaxPoint.X;maxY=ex.MaxPoint.Y;maxZ=ex.MaxPoint.Z;any=true;} else {minX=Math.Min(minX,ex.MinPoint.X); minY=Math.Min(minY,ex.MinPoint.Y); minZ=Math.Min(minZ,ex.MinPoint.Z); maxX=Math.Max(maxX,ex.MaxPoint.X); maxY=Math.Max(maxY,ex.MaxPoint.Y); maxZ=Math.Max(maxZ,ex.MaxPoint.Z);} } catch {}
        }
        return any ? $"({minX},{minY},{minZ})..({maxX},{maxY},{maxZ})" : "empty";
    }
}

