using System.Collections.Immutable;

namespace DBTool.Domain;

/// <summary>Owner-selected directional opening dimensions; not a measured product contour or bearing approval.</summary>
public sealed record CoverNominalOpening
{
    public CoverNominalOpening(Dimensions2Mm size,IEnumerable<SourceRef> sources)
    {
        Size=ContractGuard.Required(size);
        Sources=ContractGuard.Copy(sources);
        if(Sources.IsEmpty) throw new ArgumentException("Nominal opening requires provenance.",nameof(sources));
    }
    public Dimensions2Mm Size {get;}
    public ImmutableArray<SourceRef> Sources {get;}
}
