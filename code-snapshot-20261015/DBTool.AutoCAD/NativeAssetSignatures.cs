using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;
using DBTool.Application.Templates;

namespace DBTool.AutoCAD.Template;

/// <summary>Handle-independent native symbol properties. All reads stay inside the caller transaction.</summary>
public static class NativeAssetSignatures
{
    public const string CanonicalBundleVersion = "leader-field-contracts-20261013-r2";
    public const string CustomizationVersion = "VHGpro-20261013";

    public static TemplateAssetSignature DimensionProperties(Transaction tr, Dimension dimension)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in typeof(Dimension).GetProperties().Where(p =>
            p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.Name != "DimBlockId" && p.Name != "DimensionBlockId" &&
            p.CanRead && p.GetIndexParameters().Length == 0))
            values.Add(property.Name, Format(tr, property.GetValue(dimension)));
        values["CanonicalVersion"] = CanonicalBundleVersion;
        return new(values);
    }

    public static TemplateAssetSignature ReadBlock(Transaction tr, BlockTableRecord btr) => ReadBlock(tr,btr,new List<ObjectId>());
    private static TemplateAssetSignature ReadBlock(Transaction tr,BlockTableRecord btr,List<ObjectId> stack)
    {
        if(stack.Contains(btr.ObjectId))throw new NativeAssetContractException("ASSET_DEPENDENCY_CYCLE",btr.Name,string.Join(" -> ",stack.Select(id=>((BlockTableRecord)tr.GetObject(id,OpenMode.ForRead)).Name).Append(btr.Name)));
        stack.Add(btr.ObjectId);
        try{return ReadBlockContent(tr,btr,stack);}finally{stack.RemoveAt(stack.Count-1);}
    }
    private static TemplateAssetSignature ReadBlockContent(Transaction tr,BlockTableRecord btr,List<ObjectId> stack)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        values["Name"] = btr.Name;
        values["Origin"] = $"{btr.Origin.X.ToString("R", CultureInfo.InvariantCulture)},{btr.Origin.Y.ToString("R", CultureInfo.InvariantCulture)},{btr.Origin.Z.ToString("R", CultureInfo.InvariantCulture)}";
        values["Units"] = btr.Units.ToString();
        values["CanonicalVersion"] = CanonicalBundleVersion;

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        bool hasEntities = false;
        var distinctLayers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var tagList = new List<string>();
        var payloads = new List<string>();
        var childNames=new SortedSet<string>(StringComparer.Ordinal);
        foreach (ObjectId id in btr)
        {
            var obj = tr.GetObject(id, OpenMode.ForRead);
            if (obj is Entity ent)
            {
                payloads.Add(EntityPayload(tr,ent,stack).Hash);
                if(ent is BlockReference child){var childRecord=(BlockTableRecord)tr.GetObject(child.BlockTableRecord,OpenMode.ForRead);childNames.Add(childRecord.Name);values["Child/"+childRecord.Name+"/ContentHash"]=ReadBlock(tr,childRecord,stack).Hash;}
                if (!string.IsNullOrWhiteSpace(ent.Layer))
                    distinctLayers.Add(ent.Layer);

                try
                {
                    var ext = ent.GeometricExtents;
                    minX = Math.Min(minX, ext.MinPoint.X);
                    minY = Math.Min(minY, ext.MinPoint.Y);
                    maxX = Math.Max(maxX, ext.MaxPoint.X);
                    maxY = Math.Max(maxY, ext.MaxPoint.Y);
                    hasEntities = true;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { }
            }
            if (obj is AttributeDefinition att)
            {
                tagList.Add(att.Tag);
                if (!hasEntities)
                {
                    minX = Math.Min(minX, att.Position.X);
                    minY = Math.Min(minY, att.Position.Y);
                    maxX = Math.Max(maxX, att.Position.X);
                    maxY = Math.Max(maxY, att.Position.Y);
                }
                string style = tr.GetObject(att.TextStyleId, OpenMode.ForRead) is TextStyleTableRecord st ? st.Name : "";
                values[$"AttDef/{att.Tag}/Tag"] = att.Tag;
                values[$"AttDef/{att.Tag}/DefaultText"] = att.TextString ?? "";
                values[$"AttDef/{att.Tag}/Style"] = style;
                values[$"AttDef/{att.Tag}/Height"] = att.Height.ToString("R", CultureInfo.InvariantCulture);
                values[$"AttDef/{att.Tag}/WidthFactor"] = att.WidthFactor.ToString("R", CultureInfo.InvariantCulture);
                values[$"AttDef/{att.Tag}/Position"] = $"{att.Position.X.ToString("R", CultureInfo.InvariantCulture)},{att.Position.Y.ToString("R", CultureInfo.InvariantCulture)},{att.Position.Z.ToString("R", CultureInfo.InvariantCulture)}";
                values[$"AttDef/{att.Tag}/Alignment"] = $"{att.AlignmentPoint.X.ToString("R", CultureInfo.InvariantCulture)},{att.AlignmentPoint.Y.ToString("R", CultureInfo.InvariantCulture)},{att.AlignmentPoint.Z.ToString("R", CultureInfo.InvariantCulture)}";
                values[$"AttDef/{att.Tag}/Rotation"] = att.Rotation.ToString("R", CultureInfo.InvariantCulture);
                values[$"AttDef/{att.Tag}/HorizontalMode"] = att.HorizontalMode.ToString();
                values[$"AttDef/{att.Tag}/VerticalMode"] = att.VerticalMode.ToString();
            }
        }
        values["ChildAssetIds"]=System.Text.Json.JsonSerializer.Serialize(childNames);
        payloads.Sort(StringComparer.Ordinal);
        values["EntityPayloadHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",payloads))));
        tagList.Sort(StringComparer.Ordinal);
        values["AttributeTags"] = string.Join(",", tagList);

        if (distinctLayers.Count == 1)
        {
            values["Layer"] = distinctLayers.Min!;
            values["Layers"] = distinctLayers.Min!;
        }
        else if (distinctLayers.Count > 1)
        {
            values["Layer"] = $"MultiLayer:{string.Join(",", distinctLayers)}";
            values["Layers"] = string.Join(",", distinctLayers);
            values["LayerCount"] = distinctLayers.Count.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            values["Layer"] = "0";
            values["Layers"] = "0";
            values["LayerCount"] = "0";
        }

        if (double.IsInfinity(minX))
        {
            minX = btr.Origin.X;
            minY = btr.Origin.Y;
            maxX = btr.Origin.X;
            maxY = btr.Origin.Y;
        }

        values["Bounds/MinX"] = minX.ToString("R", CultureInfo.InvariantCulture);
        values["Bounds/MinY"] = minY.ToString("R", CultureInfo.InvariantCulture);
        values["Bounds/MaxX"] = maxX.ToString("R", CultureInfo.InvariantCulture);
        values["Bounds/MaxY"] = maxY.ToString("R", CultureInfo.InvariantCulture);
        double width = maxX - minX;
        double height = maxY - minY;
        values["RawNativeWidthCoordinates"] = width.ToString(CultureInfo.InvariantCulture);
        values["RawNativeHeightCoordinates"] = height.ToString(CultureInfo.InvariantCulture);
        if (!hasEntities)
        {
            values["Bounds/Status"] = "<empty>";
        }

        return new(values);
    }

    // Exact analytic payload, independent of handle allocation and entity enumeration order.
    public static TemplateAssetSignature EntityPayload(Transaction tr,Entity entity) => EntityPayload(tr,entity,new List<ObjectId>());
    private static TemplateAssetSignature EntityPayload(Transaction tr,Entity entity,List<ObjectId> stack)
    {
        var p=new Dictionary<string,string>(StringComparer.Ordinal);
        void Add(string key,object? value)=>p.Add(key,Format(tr,value));
        void Properties(params string[] names)
        {
            foreach(var name in names)
            {
                var property=entity.GetType().GetProperty(name)??throw new NativeAssetContractException("ASSET_ENTITY_UNSUPPORTED",entity.GetType().Name+"/"+name,"Required native field unavailable.");
                Add(name,property.GetValue(entity));
            }
        }
        string Dependency(ObjectId id)
        {
            if(id.IsNull)return "null";
            if(id.IsErased||!id.IsValid)throw new NativeAssetContractException("ASSET_DEPENDENCY_MISSING",entity.GetType().Name,"Referenced native definition missing.");
            var record=tr.GetObject(id,OpenMode.ForRead);
            return record is BlockTableRecord block?ReadBlock(tr,block,stack).Hash:Read(tr,record,stack).Hash;
        }
        Add("Kind",entity.GetType().FullName); Add("Layer",entity.Layer); Add("Color",entity.Color);
        Add("Visible",entity.Visible); Add("LineWeight",entity.LineWeight); Add("LinetypeScale",entity.LinetypeScale);
        Add("Transparency",entity.Transparency.IsByAlpha?entity.Transparency.Alpha.ToString(CultureInfo.InvariantCulture):entity.Transparency.IsByLayer?"ByLayer":"ByBlock");
        Add("LayerContentHash",Dependency(entity.LayerId)); Add("LinetypeContentHash",Dependency(entity.LinetypeId));
        switch(entity)
        {
            case Line: Properties("StartPoint","EndPoint","Normal","Thickness"); break;
            case Circle: Properties("Center","Normal","Radius","Thickness"); break;
            case Arc: Properties("Center","Normal","Radius","StartAngle","EndAngle","Thickness"); break;
            case Polyline line:
                Properties("Closed","Elevation","Normal","Thickness"); Add("VertexCount",line.NumberOfVertices);
                for(var i=0;i<line.NumberOfVertices;i++)
                { Add($"Vertex/{i}/Point",line.GetPoint2dAt(i)); Add($"Vertex/{i}/Bulge",line.GetBulgeAt(i)); Add($"Vertex/{i}/StartWidth",line.GetStartWidthAt(i)); Add($"Vertex/{i}/EndWidth",line.GetEndWidthAt(i)); }
                break;
            case Polyline2d line:
                Properties("Closed","Normal","Thickness","PolyType");
                var vertexIndex=0;
                foreach(ObjectId vertexId in line)
                {
                    var vertex=(Vertex2d)tr.GetObject(vertexId,OpenMode.ForRead);var prefix=$"Vertex/{vertexIndex++}/";
                    Add(prefix+"Position",vertex.Position);Add(prefix+"Bulge",vertex.Bulge);Add(prefix+"StartWidth",vertex.StartWidth);Add(prefix+"EndWidth",vertex.EndWidth);
                    Add(prefix+"Type",vertex.VertexType);Add(prefix+"Tangent",vertex.Tangent);
                }
                Add("VertexCount",vertexIndex);break;
            case Ellipse:
                Properties("Center","Normal","MajorAxis","MinorAxis","RadiusRatio","StartParam","EndParam");break;
            case Spline spline:
                var nurbs=spline.NurbsData;
                Add("Nurbs/Degree",nurbs.Degree);Add("Nurbs/Closed",nurbs.Closed);Add("Nurbs/Periodic",nurbs.Periodic);Add("Nurbs/Rational",nurbs.Rational);
                Add("Nurbs/ControlPointTolerance",nurbs.ControlPointTolerance);Add("Nurbs/KnotTolerance",nurbs.KnotTolerance);
                var points=nurbs.GetControlPoints();for(var i=0;i<points.Count;i++)Add($"Nurbs/ControlPoint/{i}",points[i]);
                var knots=nurbs.GetKnots();for(var i=0;i<knots.Count;i++)Add($"Nurbs/Knot/{i}",knots[i]);
                var weights=nurbs.GetWeights();for(var i=0;i<weights.Count;i++)Add($"Nurbs/Weight/{i}",weights[i]);
                break;
            case Hatch hatch:
                Properties("Associative","Elevation","Normal","HatchStyle","IsSolidFill","IsGradient","Origin","PatternName","PatternType","PatternScale","PatternAngle","PatternDouble","PatternSpace","BackgroundColor");
                if(hatch.IsGradient)
                {
                    Properties("GradientAngle","GradientName","GradientOneColorMode","GradientShift","GradientType","ShadeTintValue");
                    var colors=hatch.GetGradientColors();for(var i=0;i<colors.Length;i++){Add($"Gradient/{i}/Color",colors[i].get_Color());Add($"Gradient/{i}/Value",colors[i].get_Value());}
                }
                Add("LoopCount",hatch.NumberOfLoops);
                for(var i=0;i<hatch.NumberOfLoops;i++)
                {
                    var loop=hatch.GetLoopAt(i);Add($"Loop/{i}/Type",loop.LoopType);Add($"Loop/{i}/Polyline",loop.IsPolyline);
                    if(loop.IsPolyline)
                    {
                        var vertices=loop.Polyline;for(var j=0;j<vertices.Count;j++){Add($"Loop/{i}/Vertex/{j}/Point",vertices[j].Vertex);Add($"Loop/{i}/Vertex/{j}/Bulge",vertices[j].Bulge);}
                    }
                    else for(var j=0;j<loop.Curves.Count;j++)Add($"Loop/{i}/Curve/{j}",HatchCurvePayload(tr,loop.Curves[j]).Hash);
                }
                Add("PatternDefinitionCount",hatch.NumberOfPatternDefinitions);
                for(var i=0;i<hatch.NumberOfPatternDefinitions;i++)
                {
                    var def=hatch.GetPatternDefinitionAt(i);Add($"Pattern/{i}/Angle",def.Angle);Add($"Pattern/{i}/BaseX",def.BaseX);Add($"Pattern/{i}/BaseY",def.BaseY);Add($"Pattern/{i}/OffsetX",def.OffsetX);Add($"Pattern/{i}/OffsetY",def.OffsetY);
                    var dashes=def.GetDashes();for(var j=0;j<dashes.Count;j++)Add($"Pattern/{i}/Dash/{j}",dashes[j]);
                }
                break;
            case Region region:
                Properties("Normal","Area","Perimeter");var edges=new DBObjectCollection();region.Explode(edges);var edgeHashes=new List<string>();
                try{foreach(DBObject edge in edges){if(edge is not Entity e)throw new NativeAssetContractException("ASSET_ENTITY_UNSUPPORTED","Region/edge","Non-entity boundary.");edgeHashes.Add(EntityPayload(tr,e,stack).Hash);}}
                finally{foreach(DBObject edge in edges)edge.Dispose();}
                Add("Boundary",string.Join(";",edgeHashes.OrderBy(x=>x,StringComparer.Ordinal)));break;
            case AttributeDefinition att:
                Properties("Tag","Prompt","TextString","Position","AlignmentPoint","Height","WidthFactor","Rotation","HorizontalMode","VerticalMode","Normal","Oblique","IsMirroredInX","IsMirroredInY","Invisible","Constant","Preset","Verifiable","LockPositionInBlock");
                Add("TextStyleContentHash",Dependency(att.TextStyleId));
                if(att.IsMTextAttributeDefinition)throw new NativeAssetContractException("ASSET_ENTITY_UNSUPPORTED",att.Tag,"Multiline ATTDEF requires full MText payload.");
                break;
            case DBText text:
                Properties("TextString","Position","AlignmentPoint","Height","WidthFactor","Rotation","HorizontalMode","VerticalMode","Normal","Oblique","IsMirroredInX","IsMirroredInY","Thickness");
                Add("TextStyleContentHash",Dependency(text.TextStyleId)); break;
            case MText text:
                Properties("Contents","Location","TextHeight","Width","Rotation","Normal","Attachment","FlowDirection","LineSpacingFactor","LineSpacingStyle","BackgroundFill");
                if(text.BackgroundFill)Properties("UseBackgroundColor","BackgroundScaleFactor","BackgroundFillColor");
                Add("TextStyleContentHash",Dependency(text.TextStyleId)); break;
            case BlockReference block:
                Properties("Position","ScaleFactors","Rotation","Normal"); Add("ChildContentHash",Dependency(block.BlockTableRecord));
                if(block.IsDynamicBlock)throw new NativeAssetContractException("ASSET_DYNAMIC_STATE_UNSUPPORTED",block.Name,"Dynamic evaluator state cannot be reduced to reference properties.");
                var attributes=new List<string>();
                foreach(ObjectId id in block.AttributeCollection)
                {
                    var att=(AttributeReference)tr.GetObject(id,OpenMode.ForRead);
                    var a=new Dictionary<string,string>{{"Tag",att.Tag},{"Text",att.TextString},{"Position",Format(tr,att.Position)},{"Alignment",Format(tr,att.AlignmentPoint)},{"Height",Format(tr,att.Height)},{"WidthFactor",Format(tr,att.WidthFactor)},{"Rotation",Format(tr,att.Rotation)},{"HorizontalMode",att.HorizontalMode.ToString()},{"VerticalMode",att.VerticalMode.ToString()},{"Invisible",att.Invisible.ToString()},{"Style",Dependency(att.TextStyleId)},{"Normal",Format(tr,att.Normal)},{"Oblique",Format(tr,att.Oblique)},{"MirrorX",att.IsMirroredInX.ToString()},{"MirrorY",att.IsMirroredInY.ToString()}};
                    attributes.Add(new TemplateAssetSignature(a).Hash);
                }
                Add("Attributes",string.Join(";",attributes.OrderBy(x=>x,StringComparer.Ordinal))); break;
            case RotatedDimension dimension:
                Properties("XLine1Point","XLine2Point","DimLinePoint","Rotation"); Dimension(dimension); break;
            case AlignedDimension dimension:
                Properties("XLine1Point","XLine2Point","DimLinePoint"); Dimension(dimension); break;
            case Solid solid:
                Properties("Normal","Thickness"); for(var i=0;i<4;i++)Add($"Point/{i}",solid.GetPointAt((short)i)); break;
            case DBPoint: Properties("Position","Normal","Thickness"); break;
            default: throw new NativeAssetContractException("ASSET_ENTITY_UNSUPPORTED",entity.GetType().Name,"Full analytic native entity payload not implemented; no partial signature emitted.");
        }
        void Dimension(Dimension dimension)
        {
            Properties("TextPosition","Normal","DimensionText","TextRotation","HorizontalRotation","Elevation");
            Add("DimensionProperties",DimensionProperties(tr,dimension).Hash);Add("DimensionStyleContentHash",Dependency(dimension.DimensionStyle));
        }
        return new(p);
    }
    public static TemplateAssetSignature HatchCurvePayload(Transaction tr,Curve2d curve)
    {
        var p=new Dictionary<string,string>{{"Kind",curve.GetType().Name}};
        void Fields(params string[] names){foreach(var name in names){var property=curve.GetType().GetProperty(name)??throw new NativeAssetContractException("ASSET_ENTITY_UNSUPPORTED","HatchCurve/"+name,"Required analytic property missing.");p.Add(name,Format(tr,property.GetValue(curve)));}}
        switch(curve)
        {
            case LineSegment2d:Fields("StartPoint","EndPoint");break;
            case CircularArc2d:Fields("Center","Radius","StartAngle","EndAngle","ReferenceVector","IsClockWise");break;
            case EllipticalArc2d:Fields("Center","MajorAxis","MinorAxis","MajorRadius","MinorRadius","StartAngle","EndAngle","IsClockWise");break;
            case NurbCurve2d spline:
                var data=spline.DefinitionData;p.Add("Degree",Format(tr,data.Degree));p.Add("Periodic",Format(tr,data.Periodic));p.Add("Rational",Format(tr,data.Rational));
                for(var i=0;i<data.ControlPoints.Count;i++)p.Add($"ControlPoint/{i}",Format(tr,data.ControlPoints[i]));
                for(var i=0;i<data.Knots.Count;i++)p.Add($"Knot/{i}",Format(tr,data.Knots[i]));
                for(var i=0;i<data.Weights.Count;i++)p.Add($"Weight/{i}",Format(tr,data.Weights[i]));
                break;
            default:throw new NativeAssetContractException("ASSET_ENTITY_UNSUPPORTED","HatchCurve/"+curve.GetType().Name,"Exact hatch boundary not implemented; no chord approximation.");
        }
        return new TemplateAssetSignature(p);
    }
    public static TemplateAssetSignature Read(Transaction tr,DBObject record)=>Read(tr,record,new List<ObjectId>());
    private static TemplateAssetSignature Read(Transaction tr,DBObject record,List<ObjectId> stack)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string key, object? value)
        {
            values.Add(key,Format(tr,value));
            if(value is ObjectId id&&!id.IsNull)
            {
                if(id.IsErased||!id.IsValid)throw new NativeAssetContractException("ASSET_DEPENDENCY_MISSING",key,"Referenced native symbol missing.");
                var dependency=tr.GetObject(id,OpenMode.ForRead);
                if(dependency is SymbolTableRecord)values.Add(key+"/ContentHash",Read(tr,dependency,stack).Hash);
            }
        }
        void ReadApplicable(string key, Func<object?> read)
        {
            try { Add(key, read()); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.NotApplicable)
            { values.Add(key, "<not-applicable>"); }
        }
        values["CanonicalVersion"] = CanonicalBundleVersion;
        switch (record)
        {
            case BlockTableRecord btr:
                return ReadBlock(tr, btr,stack);
            case LayerTableRecord layer:
                Add("Color", layer.Color); Add("ColorIndex", layer.Color.ColorIndex); Add("Linetype", layer.LinetypeObjectId);
                Add("LineWeight", layer.LineWeight); Add("IsPlottable", layer.IsPlottable);
                Add("IsOff", layer.IsOff); Add("IsFrozen", layer.IsFrozen); Add("IsLocked", layer.IsLocked);
                Add("Transparency/ByLayer", layer.Transparency.IsByLayer); Add("Transparency/ByBlock", layer.Transparency.IsByBlock);
                Add("Transparency/Alpha", layer.Transparency.IsByAlpha ? layer.Transparency.Alpha.ToString(CultureInfo.InvariantCulture) : "<not-alpha>");
                break;
            case TextStyleTableRecord text:
                Add("FileName", text.FileName.ToLowerInvariant()); Add("BigFontFileName", text.BigFontFileName.ToLowerInvariant());
                Add("XScale", text.XScale); Add("TextSize", text.TextSize); Add("ObliquingAngle", text.ObliquingAngle);
                Add("IsVertical", text.IsVertical); Add("FlagBits", text.FlagBits);
                var font = text.Font;
                Add("Font/Typeface", font.TypeFace); Add("Font/Bold", font.Bold); Add("Font/Italic", font.Italic);
                Add("Font/CharacterSet", font.CharacterSet); Add("Font/PitchAndFamily", font.PitchAndFamily);
                break;
            case DimStyleTableRecord dim:
                Add("Dimclrd/ColorIndex", dim.Dimclrd.ColorIndex); Add("Dimclre/ColorIndex", dim.Dimclre.ColorIndex);
                Add("Dimclrt/ColorIndex", dim.Dimclrt.ColorIndex);
                foreach (var property in typeof(DimStyleTableRecord).GetProperties()
                    .Where(p => p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.CanRead && p.GetIndexParameters().Length == 0))
                    Add(property.Name, property.GetValue(dim));
                break;
            case LinetypeTableRecord line:
                Add("PatternLength", line.PatternLength); Add("NumDashes", line.NumDashes);
                for (var i = 0; i < line.NumDashes; i++)
                {
                    Add($"Dash/{i}/Length", line.DashLengthAt(i)); ReadApplicable($"Dash/{i}/Text", () => line.TextAt(i));
                    ReadApplicable($"Dash/{i}/Style", () => line.ShapeStyleAt(i)); ReadApplicable($"Dash/{i}/Number", () => line.ShapeNumberAt(i));
                    ReadApplicable($"Dash/{i}/Scale", () => line.ShapeScaleAt(i)); ReadApplicable($"Dash/{i}/Rotation", () => line.ShapeRotationAt(i));
                    ReadApplicable($"Dash/{i}/Offset", () => line.ShapeOffsetAt(i)); ReadApplicable($"Dash/{i}/Ucs", () => line.ShapeIsUcsOrientedAt(i));
                }
                break;
            default:
                throw new InvalidOperationException($"Native signature unsupported: {record.GetType().Name}");
        }
        return new(values);
    }

    private static string Format(Transaction tr, object? value) => value switch
    {
        null => "",
        ObjectId id when id.IsNull => "null",
        ObjectId id => tr.GetObject(id, OpenMode.ForRead) is SymbolTableRecord symbol ? symbol.Name :
            throw new InvalidOperationException("Signature references a non-symbol native object."),
        Color color => $"{color.ColorMethod}:{color.ColorIndex}:{color.Red},{color.Green},{color.Blue}",
        double number when !double.IsFinite(number) => throw new NativeAssetContractException("ASSET_NATIVE_FIELD_INVALID","number","Nonfinite native field."),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        Point3d point => string.Join(",",new[]{point.X,point.Y,point.Z}.Select(n=>Format(tr,n))),
        Point2d point => string.Join(",",new[]{point.X,point.Y}.Select(n=>Format(tr,n))),
        Vector2d vector => string.Join(",",new[]{vector.X,vector.Y}.Select(n=>Format(tr,n))),
        Vector3d vector => string.Join(",",new[]{vector.X,vector.Y,vector.Z}.Select(n=>Format(tr,n))),
        Scale3d scale => string.Join(",",new[]{scale.X,scale.Y,scale.Z}.Select(n=>Format(tr,n))),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    public static TemplateAssetSignature? Find(Database db, Transaction tr, AssetKind kind, string name)
    {
        ObjectId tableId = kind switch
        {
            AssetKind.Layer => db.LayerTableId, AssetKind.TextStyle => db.TextStyleTableId,
            AssetKind.DimStyle => db.DimStyleTableId, AssetKind.Linetype => db.LinetypeTableId,
            AssetKind.Block => db.BlockTableId,
            _ => ObjectId.Null
        };
        if (tableId.IsNull) return null;
        var table = (SymbolTable)tr.GetObject(tableId, OpenMode.ForRead);
        return table.Has(name) ? Read(tr, tr.GetObject(table[name], OpenMode.ForRead)) : null;
    }

    public static string ComputeAssetHash(string name, AssetKind kind, TemplateAssetSignature signature, IEnumerable<string>? dependencyHashes = null)
    {
        var sb = new StringBuilder();
        sb.Append(name).Append(':').Append(kind).Append(':').Append(signature.Hash);
        if (dependencyHashes != null)
        {
            foreach (var h in dependencyHashes.OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(':').Append(h);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}









