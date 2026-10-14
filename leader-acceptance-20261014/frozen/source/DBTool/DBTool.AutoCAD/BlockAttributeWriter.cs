using Autodesk.AutoCAD.DatabaseServices;

namespace DBTool.AutoCAD.Render;

internal static class BlockAttributeWriter
{
    public static IReadOnlyList<string> Write(
        Transaction transaction,
        BlockTableRecord definition,
        BlockReference reference,
        IReadOnlyDictionary<string, string> values,
        Action<DBObject>? tagObject = null,
        string? attributeTextStyle=null,
        bool fitMarkToCircle=false)
    {
        var normalized = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        if (!definition.HasAttributeDefinitions) return result;
        ObjectId styleId=ObjectId.Null;
        if(attributeTextStyle is not null)
        {
            var styles=(TextStyleTable)transaction.GetObject(reference.Database.TextStyleTableId,OpenMode.ForRead);
            if(!styles.Has(attributeTextStyle)) throw new InvalidOperationException("TEMPLATE_ASSET_MISSING: TextStyle "+attributeTextStyle);
            styleId=styles[attributeTextStyle];
        }

        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not AttributeDefinition definitionAttribute) continue;
            var attributeTag = definitionAttribute.Tag.Trim();
            var attribute = new AttributeReference();
            attribute.SetAttributeFromBlock(definitionAttribute, reference.BlockTransform);
            if (normalized.TryGetValue(attributeTag, out var value))
                attribute.TextString = value;
            else
                attribute.TextString = string.Empty; // Do not retain template sample values

            if(!styleId.IsNull) attribute.TextStyleId=styleId;
            tagObject?.Invoke(attribute);
            reference.AttributeCollection.AppendAttribute(attribute);
            transaction.AddNewlyCreatedDBObject(attribute, true);
            // Text changes alter the width of centred/right-aligned native attributes.
            attribute.AdjustAlignment(reference.Database);
            if(fitMarkToCircle && attributeTag.Equals("SH",StringComparison.OrdinalIgnoreCase))
            {
                // Preserve the canonical schedule mark and native circle. Only compress text width
                // when a longer mark would exceed the source symbol's inner readable area.
                var circle=definition.Cast<ObjectId>().Select(id=>transaction.GetObject(id,OpenMode.ForRead))
                    .OfType<Circle>().OrderBy(c=>c.Center.TransformBy(reference.BlockTransform).DistanceTo(attribute.AlignmentPoint)).FirstOrDefault();
                if(circle is null) throw new InvalidOperationException("TEMPLATE_SHAPE_MISSING: BLT mark circle");
                double available=2*circle.Radius*reference.ScaleFactors.X*.8;
                var bounds=attribute.GeometricExtents;
                double width=bounds.MaxPoint.X-bounds.MinPoint.X;
                if(width>available)
                {
                    attribute.WidthFactor*=available/width;
                    attribute.AdjustAlignment(reference.Database);
                }
            }

            if (normalized.TryGetValue(attributeTag, out var expected) && attribute.TextString != expected)
            {
                throw new InvalidOperationException($"NATIVE_READBACK_MISMATCH: Attribute {attributeTag} readback failed.");
            }

            result.Add(attribute.ObjectId.Handle.ToString());
        }

        return result;
    }
}
