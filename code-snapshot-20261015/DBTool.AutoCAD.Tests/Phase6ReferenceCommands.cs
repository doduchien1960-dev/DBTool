using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DBTool.AutoCAD.Tests;

/// <summary>Read-only evidence from a scratch reference DWG; never emits candidate geometry.</summary>
public sealed class Phase6ReferenceCommands
{
    private static readonly HashSet<string> BlockGeometryGaps=new(StringComparer.Ordinal);
    [CommandMethod("DBTOOL_P6_INSPECT_REFERENCE", CommandFlags.Session)]
    public void Inspect()
    {
        BlockGeometryGaps.Clear();
        var run = Path.GetFullPath(Environment.GetEnvironmentVariable("DBTOOL_P6_RUN") ?? throw new InvalidOperationException("Scratch run required"));
        var doc = Acad.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No document");
        if (!Path.GetFullPath(doc.Name).StartsWith(run + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(doc.Name) != "scratch-reference.dwg") throw new InvalidOperationException("Scratch reference only");
        // Read-only inspector accepts an explicit window for a candidate scratch copy.
        // Default keeps the published reference selection unchanged.
        var window = new[] { -3196.491, -72736.523, 9403.51, -63826.521 };
        var requestedWindow = Environment.GetEnvironmentVariable("DBTOOL_P6_INSPECT_WINDOW");
        if (!string.IsNullOrWhiteSpace(requestedWindow))
        {
            var values = requestedWindow.Split(',').Select(v => double.Parse(v,
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (values.Length != 4 || values.Any(v => !double.IsFinite(v)) ||
                values[0] >= values[2] || values[1] >= values[3])
                throw new InvalidOperationException("Inspection window requires minX,minY,maxX,maxY.");
            window = values;
        }
        using var tr = doc.Database.TransactionManager.StartTransaction();
        var model = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database), OpenMode.ForRead);
        var blocks = new List<object>();
        var dimensions = new List<object>();
        var sheetOneEntities = new List<object>();
        using var progress = new StreamWriter(Path.Combine(run, "inspection-progress.log")) { AutoFlush = true };
        foreach (ObjectId id in model)
        {
            var entity = (Entity)tr.GetObject(id, OpenMode.ForRead);
            progress.WriteLine($"Entity {entity.Handle} {entity.GetType().Name}");
            try
            {
                var bounds=entity.GeometricExtents;
                // First published sheet only. Evidence describes presentation, never copies geometry to a candidate.
                if(bounds.MinPoint.X>=window[0] && bounds.MaxPoint.X<=window[2] &&
                   bounds.MinPoint.Y>=window[1] && bounds.MaxPoint.Y<=window[3])
                    sheetOneEntities.Add(new { Type=entity.GetType().Name,entity.Layer,entity.ColorIndex,entity.LinetypeScale,
                        Lineweight=entity.LineWeight.ToString(),entity.Linetype,
                        EffectiveColorIndex=EffectiveColor(entity,tr,entity.Layer,7),
                        EffectiveColorRGB=EffectiveRgb(entity,tr,entity.Layer,[255,255,255]),
                        EffectiveLineweight=EffectiveWeight(entity,tr,entity.Layer,"ByLineWeightDefault"),
                        EffectiveLinetype=EffectiveType(entity,tr,entity.Layer,"Continuous"),
                        LineStart=entity is Line lineStart?Point(lineStart.StartPoint):null,
                        LineEnd=entity is Line lineEnd?Point(lineEnd.EndPoint):null,
                        PolylineWidths=entity is Polyline widths?Enumerable.Range(0,widths.NumberOfVertices)
                            .Select(i=>new[]{widths.GetStartWidthAt(i),widths.GetEndWidthAt(i)}).ToArray():null,
                        Measurement=entity is Dimension measure?measure.Measurement:(double?)null,
                        DimensionText=entity is Dimension dimensionText?dimensionText.DimensionText:null,
                        DimensionPoints=DimensionPoints(entity),
                        DimensionStyleData=entity is Dimension nativeDim?ReadDimensionStyle(nativeDim,tr):null,
                        DimensionTextPosition=entity is Dimension textDim?Point(textDim.TextPosition):null,
                        BlockName=entity is BlockReference blockName?((BlockTableRecord)tr.GetObject(blockName.BlockTableRecord,OpenMode.ForRead)).Name:null,
                        BlockPosition=entity is BlockReference blockPosition?Point(blockPosition.Position):null,
                        BlockScale=entity is BlockReference blockScale?new[]{blockScale.ScaleFactors.X,blockScale.ScaleFactors.Y,blockScale.ScaleFactors.Z}:null,
                        BlockRotation=entity is BlockReference blockRotation?blockRotation.Rotation:(double?)null,
                        Attributes=entity is BlockReference attributes?ReadAttributes(attributes,tr):null,
                        BlockGeometryHash=entity is BlockReference shape?ReadBlockHash(shape,tr):null,
                        BlockGeometry=entity is BlockReference children?ReadBlockShape(children,tr):null,
                        BlockTexts=entity is BlockReference blockTexts?ReadBlockTexts(blockTexts.BlockTableRecord,tr,new HashSet<ObjectId>()):null,
                        DimensionBlockTexts=entity is Dimension dimensionTexts && !dimensionTexts.DimBlockId.IsNull
                            ?ReadBlockTexts(dimensionTexts.DimBlockId,tr,new HashSet<ObjectId>()):null,
                        Text=entity is DBText text?text.TextString:entity is MText mtext?mtext.Contents:null,
                        Height=entity is DBText height?height.Height:entity is MText mth?mth.TextHeight:(double?)null,
                        WidthFactor=entity is DBText width?width.WidthFactor:(double?)null,
                        TextHorizontalMode=entity is DBText hm?hm.HorizontalMode.ToString():null,
                        TextVerticalMode=entity is DBText vm?vm.VerticalMode.ToString():null,
                        TextAlignmentPoint=entity is DBText ap?new[]{ap.AlignmentPoint.X,ap.AlignmentPoint.Y}:null,
                        TextStyle=entity is DBText ts?((TextStyleTableRecord)tr.GetObject(ts.TextStyleId,OpenMode.ForRead)).Name:entity is MText mts?((TextStyleTableRecord)tr.GetObject(mts.TextStyleId,OpenMode.ForRead)).Name:null,
                        LeaderVertices=entity is Leader leader?Enumerable.Range(0,leader.NumVertices).Select(i=>new[]{leader.VertexAt(i).X,leader.VertexAt(i).Y}).ToArray():null,
                        PolylineVertices=entity is Polyline poly?Enumerable.Range(0,poly.NumberOfVertices).Select(i=>new[]{poly.GetPoint2dAt(i).X,poly.GetPoint2dAt(i).Y,poly.GetBulgeAt(i)}).ToArray():null,
                        PolylineClosed=entity is Polyline closed?closed.Closed:(bool?)null,
                        X=entity is DBText x?x.Position.X:entity is MText mx?mx.Location.X:(double?)null,
                        Y=entity is DBText y?y.Position.Y:entity is MText my?my.Location.Y:(double?)null,
                        TextRotation=entity is DBText trt?trt.Rotation:entity is MText mrt?mrt.Rotation:(double?)null,
                        TextAttachment=entity is MText ma?ma.Attachment.ToString():null,
                        HatchPattern=entity is Hatch pattern?pattern.PatternName:null,
                        HatchScale=entity is Hatch scale?scale.PatternScale:(double?)null,
                        HatchAngle=entity is Hatch angle?angle.PatternAngle:(double?)null,
                        HatchOrigin=entity is Hatch origin?new[]{origin.Origin.X,origin.Origin.Y}:null,
                        HatchLoops=entity is Hatch boundary?ReadHatchLoops(boundary):null,
                        Bounds=new[]{bounds.MinPoint.X,bounds.MinPoint.Y,bounds.MaxPoint.X,bounds.MaxPoint.Y} });
            }
            catch(Autodesk.AutoCAD.Runtime.Exception) { /* Non-graphical/proxy extents are not usable evidence. */ }
            progress.WriteLine("Extents and presentation read");
            if (entity is BlockReference block)
            {
                var definition = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                var attributes = block.AttributeCollection.Cast<ObjectId>().Select(a => (AttributeReference)tr.GetObject(a, OpenMode.ForRead))
                    .Select(a => new { a.Tag, a.TextString, a.Height, a.Position.X, a.Position.Y, Style = ((TextStyleTableRecord)tr.GetObject(a.TextStyleId, OpenMode.ForRead)).Name }).ToArray();
                blocks.Add(new { Name = definition.Name, block.Position.X, block.Position.Y,
                    Scale = new[] { block.ScaleFactors.X, block.ScaleFactors.Y, block.ScaleFactors.Z }, block.Rotation,
                    block.Layer, block.ColorIndex, Attributes = attributes });
            }
            if (entity is Dimension dim)
                dimensions.Add(new { dim.Measurement, dim.DimensionText, dim.Layer, dim.TextPosition.X, dim.TextPosition.Y,
                    Endpoints = DimensionPoints(dim),
                    Style = ((DimStyleTableRecord)tr.GetObject(dim.DimensionStyle, OpenMode.ForRead)).Name });
        }
        progress.WriteLine("Model iteration complete");
        var layout = (Layout)tr.GetObject(LayoutManager.Current.GetLayoutId("Model"), OpenMode.ForRead);
        progress.WriteLine("Layout read");
        var styles = ((TextStyleTable)tr.GetObject(doc.Database.TextStyleTableId, OpenMode.ForRead)).Cast<ObjectId>()
            .Select(id => (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead))
            .Select(s => new { s.Name, s.FileName, s.BigFontFileName, s.XScale, FontDescriptor=s.Font }).ToArray();
        progress.WriteLine("Text styles read");
        var dims = ((DimStyleTable)tr.GetObject(doc.Database.DimStyleTableId, OpenMode.ForRead)).Cast<ObjectId>()
            .Select(id => (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead))
            .Select(s => new { s.Name, s.Dimtxt, s.Dimscale, TextStyle = s.Dimtxsty.IsNull ? null :
                ((TextStyleTableRecord)tr.GetObject(s.Dimtxsty, OpenMode.ForRead)).Name }).ToArray();
        progress.WriteLine("Dimension styles read");
        var layers=((LayerTable)tr.GetObject(doc.Database.LayerTableId,OpenMode.ForRead)).Cast<ObjectId>()
            .Select(id=>(LayerTableRecord)tr.GetObject(id,OpenMode.ForRead))
            .Select(l=>new { l.Name, ColorIndex=l.Color.ColorIndex, Lineweight=l.LineWeight.ToString() }).ToArray();
        progress.WriteLine("Layers read");
        var nativeCoverStyles=new List<object>();
        var blockTable=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
        foreach(string name in new[]{"NapGaV-MB","NapGaV-MC"})
        {
            if(!blockTable.Has(name)) continue;
            var definition=(BlockTableRecord)tr.GetObject(blockTable[name],OpenMode.ForRead);
            foreach(ObjectId id in definition)
            {
                var e=(Entity)tr.GetObject(id,OpenMode.ForRead);
                nativeCoverStyles.Add(new { Block=name, Type=e.GetType().Name,e.Layer,e.ColorIndex,
                    Lineweight=e.LineWeight.ToString(),
                    HatchPattern=e is Hatch h?h.PatternName:null,
                    HatchScale=e is Hatch hs?hs.PatternScale:(double?)null });
            }
        }
        progress.WriteLine("Cover styles read");
        File.WriteAllText(Path.Combine(run, "reference-graphics.json"), JsonSerializer.Serialize(new {
            SchemaVersion=3, BlockGeometryGaps=BlockGeometryGaps.Order().ToArray(), Scope = "Read-only reference evidence; no candidate geometry copied", Blocks = blocks, Dimensions = dimensions, Styles = styles, DimStyles = dims,
            SheetOneEntities=sheetOneEntities, Layers=layers, NativeCoverStyles=nativeCoverStyles, GlobalLinetypeScale=doc.Database.Ltscale,
            Plot = new { layout.PlotConfigurationName, layout.CanonicalMediaName, layout.CurrentStyleSheet,
                Rotation = layout.PlotRotation.ToString(), Type = layout.PlotType.ToString(),
                Scale = new[] { layout.CustomPrintScale.Numerator, layout.CustomPrintScale.Denominator },
                layout.PlotCentered, layout.UseStandardScale, layout.PrintLineweights, layout.ScaleLineweights,
                layout.PlotWindowArea, layout.PlotPaperMargins, layout.PlotPaperSize, layout.PlotOrigin }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static double[] Point(Autodesk.AutoCAD.Geometry.Point3d p)=>[p.X,p.Y,p.Z];
    private static object ReadDimensionStyle(Dimension dimension,Transaction tr)
    {
        using var settings=dimension.GetDimstyleData();
        var text=(TextStyleTableRecord)tr.GetObject(settings.Dimtxsty,OpenMode.ForRead);
        return new { Style=((DimStyleTableRecord)tr.GetObject(dimension.DimensionStyle,OpenMode.ForRead)).Name,
            TextStyle=text.Name,Font=text.FileName,text.XScale,settings.Dimtxt,settings.Dimscale,
            settings.Dimasz,settings.Dimgap,settings.Dimtix,settings.Dimatfit,settings.Dimtmove,
            settings.Dimtad,settings.Dimjust,dimension.UsingDefaultTextPosition,TextColorIndex=settings.Dimclrt.ColorIndex,
            DimensionColorIndex=settings.Dimclrd.ColorIndex,ExtensionColorIndex=settings.Dimclre.ColorIndex };
    }
    private static double[][]? DimensionPoints(Entity entity)=>entity switch {
        RotatedDimension d=>[[d.XLine1Point.X,d.XLine1Point.Y],[d.XLine2Point.X,d.XLine2Point.Y],[d.DimLinePoint.X,d.DimLinePoint.Y]],
        AlignedDimension d=>[[d.XLine1Point.X,d.XLine1Point.Y],[d.XLine2Point.X,d.XLine2Point.Y],[d.DimLinePoint.X,d.DimLinePoint.Y]],
        _=>null };
    private static object[] ReadAttributes(BlockReference block,Transaction tr)=>block.AttributeCollection.Cast<ObjectId>()
        .Select(id=>(AttributeReference)tr.GetObject(id,OpenMode.ForRead))
        .Select(a=>(object)new {a.Tag,a.TextString,a.Height,a.Position.X,a.Position.Y,
            a.Layer,a.ColorIndex,a.WidthFactor,HorizontalMode=a.HorizontalMode.ToString(),VerticalMode=a.VerticalMode.ToString(),
            AlignmentPoint=Point(a.AlignmentPoint),a.Rotation,
            EffectiveColorIndex=EffectiveColor(a,tr,block.Layer,EffectiveColor(block,tr,block.Layer,7)),
            EffectiveColorRGB=EffectiveRgb(a,tr,block.Layer,EffectiveRgb(block,tr,block.Layer,[255,255,255])),
            Style=((TextStyleTableRecord)tr.GetObject(a.TextStyleId,OpenMode.ForRead)).Name,
            Font=((TextStyleTableRecord)tr.GetObject(a.TextStyleId,OpenMode.ForRead)).FileName,
            Bounds=new[]{a.GeometricExtents.MinPoint.X,a.GeometricExtents.MinPoint.Y,
                a.GeometricExtents.MaxPoint.X,a.GeometricExtents.MaxPoint.Y}}).ToArray();
    private static LayerTableRecord Layer(Entity e,Transaction tr,string inheritedLayer)
    {
        var layers=(LayerTable)tr.GetObject(e.Database.LayerTableId,OpenMode.ForRead);
        return (LayerTableRecord)tr.GetObject(layers[e.Layer=="0"?inheritedLayer:e.Layer],OpenMode.ForRead);
    }
    private static int EffectiveColor(Entity e,Transaction tr,string inheritedLayer,int inheritedColor)=>
        e.ColorIndex==0?inheritedColor:e.ColorIndex==256?Layer(e,tr,inheritedLayer).Color.ColorIndex:e.ColorIndex;
    private static int[] EffectiveRgb(Entity e,Transaction tr,string inheritedLayer,int[] inheritedRgb)
    {
        var color=e.Color;
        if(color.ColorMethod.ToString()=="ByBlock")return inheritedRgb;
        if(color.ColorMethod.ToString()=="ByLayer")color=Layer(e,tr,inheritedLayer).Color;
        // Red/Green/Blue expose the raw true-color payload, which is zero for ACI.
        // ColorValue resolves the indexed palette as well as explicit RGB.
        var value=color.ColorValue;
        return [(int)value.R,(int)value.G,(int)value.B];
    }
    private static string EffectiveWeight(Entity e,Transaction tr,string inheritedLayer,string inheritedWeight)=>
        e.LineWeight==LineWeight.ByBlock?inheritedWeight:e.LineWeight==LineWeight.ByLayer?Layer(e,tr,inheritedLayer).LineWeight.ToString():e.LineWeight.ToString();
    private static string EffectiveType(Entity e,Transaction tr,string inheritedLayer,string inheritedType)=>
        e.Linetype.Equals("ByBlock",StringComparison.OrdinalIgnoreCase)?inheritedType:e.Linetype.Equals("ByLayer",StringComparison.OrdinalIgnoreCase)?
            ((LinetypeTableRecord)tr.GetObject(Layer(e,tr,inheritedLayer).LinetypeObjectId,OpenMode.ForRead)).Name:e.Linetype;
    private static string[]? ReadBlockTexts(ObjectId definition,Transaction tr,HashSet<ObjectId> stack)
    {
        if(!stack.Add(definition))return null;
        var result=new List<string>();
        foreach(ObjectId id in (BlockTableRecord)tr.GetObject(definition,OpenMode.ForRead))
        {
            var e=(Entity)tr.GetObject(id,OpenMode.ForRead);
            if(e is AttributeDefinition)continue;
            if(e is DBText text)result.Add(text.TextString);
            else if(e is MText mtext)result.Add(mtext.Text);
            else if(e is BlockReference child)
            {
                var values=ReadBlockTexts(child.BlockTableRecord,tr,stack);
                if(values is null)return null;
                result.AddRange(values);
                result.AddRange(child.AttributeCollection.Cast<ObjectId>().Select(a=>((AttributeReference)tr.GetObject(a,OpenMode.ForRead)).TextString));
            }
        }
        stack.Remove(definition);return result.ToArray();
    }
    private static string? ReadBlockHash(BlockReference block,Transaction tr)
    {
        var shape=ReadBlockShape(block,tr);
        return shape is null?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(shape))));
    }
    // Expose the exact hash input for diagnosis; preserve the existing hash and fail-closed gaps.
    private static object[]? ReadBlockShape(BlockReference block,Transaction tr)=>
        ReadBlockGeometry(block.BlockTableRecord,tr,new HashSet<ObjectId>(),block.Layer,
            EffectiveColor(block,tr,block.Layer,7),EffectiveWeight(block,tr,block.Layer,"ByLineWeightDefault"),EffectiveType(block,tr,block.Layer,"Continuous"),EffectiveRgb(block,tr,block.Layer,[255,255,255]));
    private static object[]? ReadBlockGeometry(ObjectId definition,Transaction tr,HashSet<ObjectId> stack,string layer,int color,string weight,string type,int[] rgb)
    {
        if(!stack.Add(definition))return null;
        var result=new List<object>();
        foreach(ObjectId id in (BlockTableRecord)tr.GetObject(definition,OpenMode.ForRead))
        {
            var e=(Entity)tr.GetObject(id,OpenMode.ForRead);
            if(e is DBText or MText)continue; // Technical content is compared separately; font/layout is advisory.
            object? geometry=e switch {
                Line line=>new {Start=Point(line.StartPoint),End=Point(line.EndPoint)},
                Polyline poly=>new {poly.Closed,Vertices=Enumerable.Range(0,poly.NumberOfVertices)
                    .Select(i=>new[]{poly.GetPoint2dAt(i).X,poly.GetPoint2dAt(i).Y,poly.GetBulgeAt(i),poly.GetStartWidthAt(i),poly.GetEndWidthAt(i)}).ToArray()},
                Circle circle=>new {Center=Point(circle.Center),circle.Radius,Normal=new[]{circle.Normal.X,circle.Normal.Y,circle.Normal.Z}},
                Arc arc=>new {Center=Point(arc.Center),arc.Radius,arc.StartAngle,arc.EndAngle,Normal=new[]{arc.Normal.X,arc.Normal.Y,arc.Normal.Z}},
                Solid solid=>Enumerable.Range(0,4).Select(i=>Point(solid.GetPointAt((short)i))).ToArray(),
                Hatch hatch=>ReadHatchGeometry(hatch),
                BlockReference child=>ReadBlockGeometry(child.BlockTableRecord,tr,stack,e.Layer=="0"?layer:e.Layer,
                    EffectiveColor(e,tr,layer,color),EffectiveWeight(e,tr,layer,weight),EffectiveType(e,tr,layer,type),EffectiveRgb(e,tr,layer,rgb)) is { } nested?
                    new {Position=Point(child.Position),Scale=new[]{child.ScaleFactors.X,child.ScaleFactors.Y,child.ScaleFactors.Z},child.Rotation,Geometry=nested}:null,
                _=>null };
            if(geometry is null)
            {
                BlockGeometryGaps.Add($"{((BlockTableRecord)tr.GetObject(definition,OpenMode.ForRead)).Name}: {e.GetType().Name}");
                return null; // Unsupported/proxy content cannot produce a certified partial hash.
            }
            result.Add(new {Type=e.GetType().Name,e.Layer,e.ColorIndex,e.LinetypeScale,Lineweight=e.LineWeight.ToString(),e.Linetype,
                EffectiveColorIndex=EffectiveColor(e,tr,layer,color),EffectiveColorRGB=EffectiveRgb(e,tr,layer,rgb),EffectiveLineweight=EffectiveWeight(e,tr,layer,weight),EffectiveLinetype=EffectiveType(e,tr,layer,type),Geometry=geometry});
        }
        stack.Remove(definition);return result.ToArray();
    }
    private static object? ReadHatchGeometry(Hatch hatch)
    {
        for(int i=0;i<hatch.NumberOfLoops;i++)
        {
            var loop=hatch.GetLoopAt(i);
            if(!loop.IsPolyline)
            {
                var unsupported=loop.Curves.Cast<object>().Where(c=>c is not (Autodesk.AutoCAD.Geometry.LineSegment2d or Autodesk.AutoCAD.Geometry.CircularArc2d or Autodesk.AutoCAD.Geometry.EllipticalArc2d)).ToArray();
                if(unsupported.Length>0)
                {
                    foreach(var curve in unsupported)BlockGeometryGaps.Add($"HatchCurve: {curve.GetType().Name}");
                    return null;
                }
            }
        }
        return new {hatch.PatternName,hatch.PatternScale,hatch.PatternAngle,Loops=ReadHatchLoops(hatch)};
    }
    private static object[] ReadHatchLoops(Hatch hatch) => Enumerable.Range(0,hatch.NumberOfLoops)
        .Select(index =>
        {
            var loop=hatch.GetLoopAt(index);
            return (object)new {
                Kind=loop.IsPolyline?"Polyline":"Curves",Type=loop.LoopType.ToString(),
                Vertices=loop.IsPolyline?loop.Polyline.Cast<BulgeVertex>()
                    .Select(v=>new[]{v.Vertex.X,v.Vertex.Y,v.Bulge}).ToArray():null,
                Edges=loop.IsPolyline?null:loop.Curves.Cast<object>().Select(c=>
                    c is Autodesk.AutoCAD.Geometry.LineSegment2d line
                        ? (object)new { Type=c.GetType().Name,Start=new[]{line.StartPoint.X,line.StartPoint.Y},
                            End=new[]{line.EndPoint.X,line.EndPoint.Y} }
                        : c is Autodesk.AutoCAD.Geometry.CircularArc2d arc
                            ? new { Type=c.GetType().Name,Center=new[]{arc.Center.X,arc.Center.Y},arc.Radius,
                                arc.StartAngle,arc.EndAngle,arc.IsClockWise,ReferenceVector=new[]{arc.ReferenceVector.X,arc.ReferenceVector.Y},
                                Start=new[]{arc.StartPoint.X,arc.StartPoint.Y},End=new[]{arc.EndPoint.X,arc.EndPoint.Y} }
                            : c is Autodesk.AutoCAD.Geometry.EllipticalArc2d ellipse
                                ? new { Type=c.GetType().Name,Center=new[]{ellipse.Center.X,ellipse.Center.Y},ellipse.MajorRadius,ellipse.MinorRadius,
                                    MajorAxis=new[]{ellipse.MajorAxis.X,ellipse.MajorAxis.Y},MinorAxis=new[]{ellipse.MinorAxis.X,ellipse.MinorAxis.Y},
                                    ellipse.StartAngle,ellipse.EndAngle,ellipse.IsClockWise,
                                    Start=new[]{ellipse.StartPoint.X,ellipse.StartPoint.Y},End=new[]{ellipse.EndPoint.X,ellipse.EndPoint.Y} }
                                : (object)new { Type=c.GetType().Name }).ToArray()
            };
        }).ToArray();
}
