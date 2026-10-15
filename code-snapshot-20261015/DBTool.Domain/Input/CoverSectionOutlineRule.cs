using System.Collections.Immutable;

namespace DBTool.Domain;

/// <summary>Sourced section drawing convention, independent of product material bearing and concrete volume.</summary>
public sealed record CoverSectionOutlineRule
{
    public CoverSectionOutlineRule(double sideExtensionMm,double depthMm,IEnumerable<SourceRef> sources,
        string ruleVersion="VHG-CATALOG-SECTION-LEGACY")
    {
        SideExtensionMm=ContractGuard.NonNegative(sideExtensionMm,nameof(sideExtensionMm));
        DepthMm=ContractGuard.Positive(depthMm,nameof(depthMm));
        Sources=ContractGuard.Copy(sources);
        if(Sources.IsEmpty) throw new ArgumentException("Section outline requires provenance.",nameof(sources));
        RuleVersion=ContractGuard.Text(ruleVersion,nameof(ruleVersion));
    }
    public double SideExtensionMm {get;}
    public double DepthMm {get;}
    public ImmutableArray<SourceRef> Sources {get;}
    public string RuleVersion {get;}

    public static CoverSectionOutlineRule FromNeckThickness(NeckSpec neck)
    {
        var source=new SourceRef(SourceKind.RuleDocument,
            "docs/research-vhgpro-regression-20261012/PROMPT-THUC-THI-HOI-QUY.md",
            "D6F786B26A2C416B3C21D72D4D1DCCA5931E8CE5EF7CF6000A35C5DEC96082BA",null,null,null,
            "OWNER-COVER-20261012-v2: W_bao = clear + 2*t_cổ; depth 100 mm; native product scale 1.");
        return new(neck.ThicknessMm,100,neck.Sources.Append(source),"OWNER-COVER-20261012-v2");
    }
}
