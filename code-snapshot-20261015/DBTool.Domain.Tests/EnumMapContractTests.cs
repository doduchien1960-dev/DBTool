using DBTool.Domain;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class EnumMapContractTests
{
    private enum Shape { C, U }

    [Fact]
    public void EnumMapCopiesAndPreservesTypedKeys()
    {
        var source = new Dictionary<ComponentKind, Shape> { [ComponentKind.Wall] = Shape.C };
        var snapshot = ContractGuard.Map(source);
        source[ComponentKind.Wall] = Shape.U;
        Assert.Equal(Shape.C, snapshot[ComponentKind.Wall]);
        Assert.Empty(ContractGuard.Map(Array.Empty<KeyValuePair<ComponentKind, Shape>>()));
    }

    [Fact]
    public void EnumMapRejectsNullUndefinedEnumsAndDuplicateKeys()
    {
        Assert.Throws<ArgumentNullException>(() => ContractGuard.Map<ComponentKind, Shape>(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractGuard.Map(new[] { new KeyValuePair<ComponentKind, Shape>((ComponentKind)999, Shape.C) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractGuard.Map(new[] { new KeyValuePair<ComponentKind, Shape>(ComponentKind.Wall, (Shape)999) }));
        Assert.Throws<ArgumentException>(() => ContractGuard.Map(new[] {
            new KeyValuePair<ComponentKind, Shape>(ComponentKind.Wall, Shape.C),
            new KeyValuePair<ComponentKind, Shape>(ComponentKind.Wall, Shape.U) }));
        Assert.Throws<ArgumentException>(() => ContractGuard.Map(new[] {
            new KeyValuePair<ComponentKind, Shape>(ComponentKind.Wall, Shape.C),
            new KeyValuePair<ComponentKind, Shape>(ComponentKind.Wall, Shape.C) }));
    }

    [Fact]
    public void StringMapRetainsOrdinalKeysAndNullValidation()
    {
        var snapshot = ContractGuard.Map(new Dictionary<string, string> { ["Wall"] = "C", ["wall"] = "U" });
        Assert.Equal(2, snapshot.Count);
        Assert.Throws<ArgumentException>(() => ContractGuard.Map(new Dictionary<string, string> { ["Wall"] = null! }));
    }
}
