namespace DBTool.Drawing;

/// <summary>Physical section materials in logical model millimetres; never scales by plot denominator.</summary>
public sealed record SectionHatchStyle
{
    public SectionHatchStyle(string role, DrawingMaterialKind kind, string pattern, double scale)
    { Role = role; Kind = kind; Pattern = pattern; Scale = scale; }
    public string Role { get; }
    public DrawingMaterialKind Kind { get; }
    public string Pattern { get; }
    public double Scale { get; }
}

public static class SectionMaterialPolicy
{
    public const string SheetOnePipeRole="Material/SheetOnePipe";
    public static IReadOnlyList<SectionHatchStyle> ForSheetOneMaterial(string materialId,bool steelSection=false)
    {
        if(materialId=="Concrete") return steelSection?[]:
            [Resolve("Concrete")!,new("Material/ConcreteAggregate",DrawingMaterialKind.Concrete,"AR-CONC",12)];
        if(materialId=="BeddingMortar") return [new("Material/Mortar",DrawingMaterialKind.Concrete,"ANSI37",120)];
        var style=Resolve(materialId,steelSection);
        return style is null?[]:[style];
    }
    public static SectionHatchStyle? Resolve(string materialId, bool steelSection = false)
    {
        ArgumentNullException.ThrowIfNull(materialId);
        if (steelSection && materialId == "Concrete") return null;
        return materialId switch
        {
            "Concrete" => new("Material/Concrete", DrawingMaterialKind.Concrete, "ANSI32", 120),
            "Bedding" => new("Material/Bedding", DrawingMaterialKind.Concrete, "AR-CONC", 12),
            "Pipe" => new("Material/Pipe", DrawingMaterialKind.Concrete, "AR-CONC", 12),
            _ => null
        };
    }

    public static SectionHatchStyle? ForRole(string role) => role switch
    {
        "Material/Concrete" => Resolve("Concrete"),
        "Material/Bedding" => Resolve("Bedding"),
        "Material/Pipe" => Resolve("Pipe"),
        SheetOnePipeRole => new(SheetOnePipeRole,DrawingMaterialKind.Concrete,"ANSI31",120),
        "Material/ConcreteAggregate" => new("Material/ConcreteAggregate",DrawingMaterialKind.Concrete,"AR-CONC",12),
        "Material/Mortar" => new("Material/Mortar",DrawingMaterialKind.Concrete,"ANSI37",120),
        SheetTwoMortar.Role => new(SheetTwoMortar.Role,DrawingMaterialKind.Concrete,"ANSI37",120),
        _ => null
    };
}
