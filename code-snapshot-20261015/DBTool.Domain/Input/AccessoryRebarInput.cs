using System.Collections.Immutable;
namespace DBTool.Domain;
public enum VhgTieShape { C, Z, U }
public enum VhgOpeningComposition { Bo, Square, Diagonal, BoSquare, BoDiagonal }
public sealed record VhgTieInput
{
    public VhgTieInput(MeshSpec mesh,bool staggered,IEnumerable<KeyValuePair<ComponentKind,VhgTieShape>> shapes,double hookC,double hookZU,double middleU,IEnumerable<SourceRef> sources)
    {Mesh=ContractGuard.Required(mesh);Staggered=staggered;Shapes=ContractGuard.Map(shapes);HookC=ContractGuard.NonNegative(hookC,nameof(hookC));HookZU=ContractGuard.NonNegative(hookZU,nameof(hookZU));MiddleU=ContractGuard.NonNegative(middleU,nameof(middleU));Sources=ContractGuard.Copy(sources);}
    public MeshSpec Mesh {get;} public bool Staggered {get;} public ImmutableDictionary<ComponentKind,VhgTieShape> Shapes {get;}
    public double HookC {get;} public double HookZU {get;} public double MiddleU {get;} public ImmutableArray<SourceRef> Sources {get;}
}
public sealed record VhgOpeningInput
{
    public VhgOpeningInput(int diameter,int count,double extension,double lapMultiple,double cover,VhgOpeningComposition composition,IEnumerable<SourceRef> sources)
    {Diameter=ContractGuard.Positive(diameter,nameof(diameter));Count=count>=0?count:throw new ArgumentOutOfRangeException(nameof(count));Extension=ContractGuard.NonNegative(extension,nameof(extension));LapMultiple=ContractGuard.NonNegative(lapMultiple,nameof(lapMultiple));Cover=ContractGuard.NonNegative(cover,nameof(cover));Composition=ContractGuard.EnumValue(composition);Sources=ContractGuard.Copy(sources);}
    public VhgOpeningComposition Composition {get;}
    public int Diameter {get;} public int Count {get;} public double Extension {get;} public double LapMultiple {get;} public double Cover {get;} public ImmutableArray<SourceRef> Sources {get;}
}
public sealed record VhgLiftingInput
{
    public VhgLiftingInput(int count,int diameter,double width,double height,double foot,IEnumerable<SourceRef> sources)
    {Count=count is 2 or 4?count:throw new ArgumentOutOfRangeException(nameof(count));Diameter=ContractGuard.Positive(diameter,nameof(diameter));Width=ContractGuard.Positive(width,nameof(width));Height=ContractGuard.Positive(height,nameof(height));Foot=ContractGuard.Positive(foot,nameof(foot));Sources=ContractGuard.Copy(sources);}
    public int Count {get;} public int Diameter {get;} public double Width {get;} public double Height {get;} public double Foot {get;} public ImmutableArray<SourceRef> Sources {get;}
}
public sealed record AccessoryRebarInput
{
    public const string RuleVersion="VHG-ACCESSORY-INPUT-1C-v2";
    public AccessoryRebarInput(AnchorSpec? anchor,DowelSpec? dowel,VhgLiftingInput? lifting,VhgTieInput? ties,VhgOpeningInput? pipe,VhgOpeningInput? roof,IEnumerable<SourceRef> sources,double? dowelHoleDiameterMm=null,
        double? lapMultiple=null, MeshSpec? catchMesh=null, IEnumerable<KeyValuePair<string, string>>? prefixes=null, double? pipeOpeningClearanceMm=null)
    {
        Anchor=anchor;Dowel=dowel;Lifting=lifting;Ties=ties;Pipe=pipe;Roof=roof;Sources=ContractGuard.Copy(sources);DowelHoleDiameterMm=dowelHoleDiameterMm;
        LapMultiple=lapMultiple;
        CatchMesh=catchMesh;
        Prefixes=prefixes is null ? ImmutableDictionary<string, string>.Empty : prefixes.ToImmutableDictionary();
        PipeOpeningClearanceMm=pipeOpeningClearanceMm;
        if(dowel is not null && (!dowelHoleDiameterMm.HasValue || !double.IsFinite(dowelHoleDiameterMm.Value) || dowelHoleDiameterMm.Value<=dowel.DiameterMm))
            throw new ArgumentException("Physical dowel hole diameter must exceed the bar diameter.",nameof(dowelHoleDiameterMm));
    }

    public AccessoryRebarInput(AnchorSpec? anchor,DowelSpec? dowel,VhgLiftingInput? lifting,VhgTieInput? ties,VhgOpeningInput? pipe,VhgOpeningInput? roof,IEnumerable<SourceRef> sources,double? dowelHoleDiameterMm)
        : this(anchor, dowel, lifting, ties, pipe, roof, sources, dowelHoleDiameterMm, null, null, null, null)
    {
    }

    public double? DowelHoleDiameterMm {get;}
    public double? LapMultiple { get; }
    public MeshSpec? CatchMesh { get; }
    public ImmutableDictionary<string, string> Prefixes { get; }
    public double? PipeOpeningClearanceMm { get; }
    public AnchorSpec? Anchor {get;} public DowelSpec? Dowel {get;} public VhgLiftingInput? Lifting {get;}
    public VhgTieInput? Ties {get;} public VhgOpeningInput? Pipe {get;} public VhgOpeningInput? Roof {get;} public ImmutableArray<SourceRef> Sources {get;}
}
