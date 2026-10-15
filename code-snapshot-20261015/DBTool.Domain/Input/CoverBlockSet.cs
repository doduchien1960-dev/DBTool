using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Measured asset data. View bounds are inspection extents, not opening or support contours.</summary>
public sealed record CoverBlockView
{
    public CoverBlockView(string name, string view, Point3Mm basePoint, Rectangle2Mm bounds,
        Point2Mm? alignmentPoint=null, IEnumerable<SourceRef>? alignmentSources=null)
    {
        Name = ContractGuard.Text(name,nameof(name));
        if(view is not ("MB" or "AA" or "BB")) throw new ArgumentException("Unknown cover view.",nameof(view));
        View=view;BasePoint=basePoint;Bounds=ContractGuard.Required(bounds);
        AlignmentPoint=alignmentPoint;
        AlignmentSources=ContractGuard.Copy(alignmentSources ?? []);
        if(alignmentPoint.HasValue && AlignmentSources.IsEmpty)
            throw new ArgumentException("Measured alignment point requires provenance.",nameof(alignmentSources));
    }
    public string Name {get;}
    public string View {get;}
    public Point3Mm BasePoint {get;}
    public Rectangle2Mm Bounds {get;}
    /// <summary>MB: aperture centre. AA/BB: aperture horizontal centre at the nominal cover top datum.
    /// Coordinates belong to this view's block, not to its bounding box.</summary>
    public Point2Mm? AlignmentPoint {get;}
    public ImmutableArray<SourceRef> AlignmentSources {get;}
}

public sealed record CoverBlockSet
{
    public CoverBlockSet(IEnumerable<CoverBlockView> views, Profile2Mm? measuredAperture,
        IEnumerable<string> unresolvedFields, IEnumerable<SourceRef> sources,
        IEnumerable<CoverContactSection>? contactSections=null,CoverSectionOutlineRule? sectionOutlineRule=null,
        CoverNominalOpening? nominalOpening=null)
    {
        Views=ContractGuard.Copy(views);
        if(Views.Length!=3 || Views.Select(v=>v.View).Distinct().Count()!=3)
            throw new ArgumentException("Cover asset requires exactly MB, AA and BB.",nameof(views));
        MeasuredAperture=measuredAperture;
        UnresolvedFields=ContractGuard.Copy(unresolvedFields);
        Sources=ContractGuard.Copy(sources);
        if(Sources.IsEmpty) throw new ArgumentException("Cover block set requires provenance.",nameof(sources));
        ContactSections=ContractGuard.Copy(contactSections ?? []);
        SectionOutlineRule=sectionOutlineRule;
        NominalOpening=nominalOpening;
        if(ContactSections.Select(s=>s.View).Distinct().Count()!=ContactSections.Length)
            throw new ArgumentException("Contact sections must have unique native views.",nameof(contactSections));
    }
    public ImmutableArray<CoverBlockView> Views {get;}
    public Profile2Mm? MeasuredAperture {get;}
    public ImmutableArray<string> UnresolvedFields {get;}
    public ImmutableArray<SourceRef> Sources {get;}
    public ImmutableArray<CoverContactSection> ContactSections {get;}
    public CoverSectionOutlineRule? SectionOutlineRule {get;}
    public CoverNominalOpening? NominalOpening {get;}
}
