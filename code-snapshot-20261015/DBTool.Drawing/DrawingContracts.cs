using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;
public enum ViewPurpose
{
    Plan, Section, BaseMesh, SteelTable,
    NeckDetail, CatchBasinDetail, PanelDetail, AccessoryDetail, QuantityTable, Schematic
}
public enum TableCellKind { Text, Number }
public sealed record PackageIdentity
{
    public PackageIdentity(string inputHash, string ruleVersion, CalculationScope scope)
    {
        InputHash = ContractGuard.Hash(inputHash);
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        Scope = ContractGuard.EnumValue(scope);

    }
    public string InputHash { get; }
    public string RuleVersion { get; }
    public CalculationScope Scope { get; }
}
public sealed record ViewDefinition
{
    // Origin is the placed lower-left anchor of Bounds, in model millimetres.
    // ScaleDenominator is a plot/presentation metric, not a second physical geometry divisor.
    public ViewDefinition(string id, ViewPurpose purpose, Point2Mm origin, Bounds2Mm? bounds, double scaleDenominator)
    {
        Id = ContractGuard.Text(id, nameof(id));
        Purpose = ContractGuard.EnumValue(purpose);
        Origin = origin;
        Bounds = bounds;
        ScaleDenominator = ContractGuard.Positive(scaleDenominator, nameof(scaleDenominator));

    }
    public string Id { get; }
    public ViewPurpose Purpose { get; }
    public Point2Mm Origin { get; }
    public Bounds2Mm? Bounds { get; }
    public double ScaleDenominator { get; }
}
public sealed record TableCell
{
    public TableCell(string columnKey, TableCellKind kind, string? text, double? number)
    {
        ColumnKey = ContractGuard.Text(columnKey, nameof(columnKey));
        Kind = ContractGuard.EnumValue(kind);
        Text = text;
        Number = number;
        if(kind==TableCellKind.Text ? text==null || number!=null : text!=null || number==null) throw new ArgumentException("Cell payload mismatch.");
        if(number.HasValue) ContractGuard.Finite(number.Value,nameof(number));
    }
    public string ColumnKey { get; }
    public TableCellKind Kind { get; }
    public string? Text { get; }
    public double? Number { get; }
}
