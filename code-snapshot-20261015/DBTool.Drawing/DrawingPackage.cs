using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;
public sealed record DrawingPackage
{
    public DrawingPackage(string inputHash, string ruleVersion, IEnumerable<ViewDefinition> views, IEnumerable<DrawingPrimitive> primitives, Bounds2Mm? bounds, CalculationScope scope,
        IEnumerable<string>? semanticIds = null, IEnumerable<string>? targets = null, IEnumerable<string>? groups = null, Bounds2Mm? inkBounds = null, IEnumerable<SheetViewFrame>? frames = null, string? intentManifest = null)
    {
        InputHash = ContractGuard.Hash(inputHash);
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        Views = ContractGuard.Copy(views);
        Primitives = ContractGuard.Copy(primitives);
        Bounds = bounds;
        Scope = ContractGuard.EnumValue(scope);
        if(Views.Select(v=>v.Id).Distinct().Count()!=Views.Length || Primitives.Select(p=>p.SemanticId).Distinct().Count()!=Primitives.Length) throw new ArgumentException("Duplicate drawing identity.");
        if(Primitives.FirstOrDefault(p=>!Views.Any(v=>v.Id==p.ViewId)) is { } missingPrimitive)
            throw new ArgumentException($"Unresolved drawing view: {missingPrimitive.ViewId} from primitive {missingPrimitive.SemanticId}");
        if(!Primitives.IsEmpty && bounds==null) throw new ArgumentException("Nonempty drawing needs bounds.");

        SemanticIds = semanticIds is not null ? ContractGuard.Copy(semanticIds) : Primitives.Select(p => p.SemanticId).ToImmutableArray();
        Targets = targets is not null ? ContractGuard.Copy(targets) : ImmutableArray<string>.Empty;
        Groups = groups is not null ? ContractGuard.Copy(groups) : Primitives.Select(p => p.OccurrenceId).Where(id => id != null).Distinct()!.ToImmutableArray()!;
        InkBounds = inkBounds ?? bounds;
        Frames = frames is not null ? ContractGuard.Copy(frames) : ImmutableArray<SheetViewFrame>.Empty;
        IntentManifest = intentManifest;
    }
    public string InputHash { get; }
    public string RuleVersion { get; }
    public ImmutableArray<ViewDefinition> Views { get; }
    public ImmutableArray<DrawingPrimitive> Primitives { get; }
    public Bounds2Mm? Bounds { get; }
    public CalculationScope Scope { get; }
    public string SchemaVersion => DrawingPackageJson.SupportedSchemaVersion;

    public ImmutableArray<string> SemanticIds { get; }
    public ImmutableArray<string> Targets { get; }
    public ImmutableArray<string> Groups { get; }
    public Bounds2Mm? InkBounds { get; }
    public ImmutableArray<SheetViewFrame> Frames { get; }
    public string? IntentManifest { get; }
}
