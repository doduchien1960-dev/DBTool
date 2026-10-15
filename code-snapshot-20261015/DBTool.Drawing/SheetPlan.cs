using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

public enum VhgSheetFamily { Visit, VisitAndCatch }
public enum SheetContent { Structure, StructureSteel, BaseAndWalls12, Walls34AndRoof, NeckAndCatch, Tables, Bending, Auxiliary, BaseSlab, Walls12, Walls34, Roof }
public sealed record AuxiliarySheetManifestEntry
{
    public AuxiliarySheetManifestEntry(string id,string manifestId,IEnumerable<string> viewIds)
    { Id=ContractGuard.Text(id,nameof(id));ManifestId=ContractGuard.Text(manifestId,nameof(manifestId));ViewIds=viewIds.ToImmutableArray(); }
    public string Id { get; }
    public string ManifestId { get; }
    public ImmutableArray<string> ViewIds { get; }
}
public sealed record SheetPlanPage
{
    public SheetPlanPage(string id,SheetContent content,string? manifestId,ImmutableArray<string> viewIds)
        : this(id, content, manifestId, viewIds, null, content.ToString(), 1, 30.0, null, null)
    {
    }

    public SheetPlanPage(
        string id,
        SheetContent content,
        string? manifestId,
        ImmutableArray<string> viewIds,
        string? manholeId,
        string? pageRole = null,
        int pageIndex = 1,
        double scaleDenominator = 30.0,
        Bounds2Mm? inkBounds = null,
        IEnumerable<string>? ownedViewBundles = null)
    {
        Id = id;
        Content = content;
        ManifestId = manifestId;
        ViewIds = viewIds;
        ManholeId = manholeId;
        PageRole = pageRole ?? content.ToString();
        PageIndex = pageIndex;
        ScaleDenominator = scaleDenominator;
        InkBounds = inkBounds;
        OwnedViewBundles = ownedViewBundles != null ? ownedViewBundles.ToImmutableArray() : viewIds;
    }

    public string Id { get; }
    public string SheetPlanPageContent => Content.ToString();
    public SheetContent Content { get; }
    public string? ManifestId { get; }
    public ImmutableArray<string> ViewIds { get; }
    public string? ManholeId { get; }
    public string PageRole { get; }
    public int PageIndex { get; }
    public double ScaleDenominator { get; }
    public Bounds2Mm? InkBounds { get; }
    public ImmutableArray<string> OwnedViewBundles { get; }
}

/// <summary>Registry of frames and view placements across sheets without model-name lookups.</summary>
public sealed class FrameRegistry
{
    private readonly Dictionary<string, SheetPlanPage> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SheetViewFrame> _viewFrames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _placedViewIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Bounds2Mm> _viewInks = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> PlacedViewIds => _placedViewIds;
    public IReadOnlyCollection<SheetPlanPage> Pages => _pages.Values;
    public IReadOnlyCollection<SheetViewFrame> Frames => _viewFrames.Values;

    public void Register(SheetPlanPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _pages[page.Id] = page;
        foreach (var viewId in page.ViewIds)
        {
            _placedViewIds.Add(viewId);
        }
    }

    public void Register(SheetViewFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _viewFrames[frame.ViewId] = frame;
        _placedViewIds.Add(frame.ViewId);
    }

    public void RegisterPlacement(string viewId, string frameId, Bounds2Mm ink)
    {
        _placedViewIds.Add(viewId);
        _viewInks[viewId] = ink;
    }

    public bool IsViewPlaced(string viewId) => _placedViewIds.Contains(viewId);

    public SheetViewFrame? GetFrame(string viewId) =>
        _viewFrames.TryGetValue(viewId, out var frame) ? frame : null;

    public SheetPlanPage? GetPage(string pageId) =>
        _pages.TryGetValue(pageId, out var page) ? page : null;

    public Bounds2Mm? GetInk(string viewId) =>
        _viewInks.TryGetValue(viewId, out var ink) ? ink : null;

    public OperationResult<FrameRegistry> VerifyCoverage(IEnumerable<string> requiredViewIds)
    {
        var required = requiredViewIds.ToHashSet(StringComparer.Ordinal);
        var missing = required.Except(_placedViewIds).ToList();
        if (missing.Count > 0)
        {
            return new(false, null, [
                new Diagnostic("LAYOUT_REQUIRED_VIEW_COVERAGE", DiagnosticSeverity.Block, DiagnosticScope.Global,
                    null, null, null, string.Join(",", required), string.Join(",", _placedViewIds),
                    $"Required views not placed: {string.Join(", ", missing)}")
            ]);
        }
        return new(true, this, []);
    }
}
/// <summary>Ordered primary pages plus explicitly selected manifest auxiliary pages; never inferred from output.</summary>
public sealed record SheetPlan
{
    private SheetPlan(IEnumerable<SheetPlanPage> pages) { Pages=pages.ToImmutableArray(); }
    public ImmutableArray<SheetPlanPage> Pages { get; }
    public static SheetPlan Create(VhgSheetFamily family,IEnumerable<AuxiliarySheetManifestEntry> auxiliary,
        IReadOnlyDictionary<SheetContent,IReadOnlyList<string>>? assignments=null, bool isLarge=false)
    {
        ContractGuard.EnumValue(family);
        var contents=new List<SheetContent>{SheetContent.Structure};
        if(isLarge)
        {
            contents.AddRange([SheetContent.StructureSteel, SheetContent.BaseSlab, SheetContent.Walls12, SheetContent.Walls34, SheetContent.Roof]);
        }
        else
        {
            contents.AddRange([SheetContent.BaseAndWalls12, SheetContent.Walls34AndRoof]);
        }
        if(family==VhgSheetFamily.VisitAndCatch) contents.Add(SheetContent.NeckAndCatch);
        contents.AddRange([SheetContent.Tables,SheetContent.Bending]);
        var pages=contents.Select((content,index)=>new SheetPlanPage($"Primary/{index+1}",content,null,
            assignments is not null && assignments.TryGetValue(content,out var views) ? views.ToImmutableArray() : [])).ToList();
        foreach(var entry in auxiliary)
            pages.Add(new(ContractGuard.Text(entry.Id,nameof(entry.Id)),SheetContent.Auxiliary,
                ContractGuard.Text(entry.ManifestId,nameof(entry.ManifestId)),entry.ViewIds.ToImmutableArray()));
        if(pages.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=pages.Count)
            throw new ArgumentException("Duplicate sheet identity.");
        var allViews=pages.SelectMany(p=>p.ViewIds).ToArray();
        if(allViews.Distinct(StringComparer.Ordinal).Count()!=allViews.Length)
            throw new ArgumentException("A view may be assigned to exactly one sheet.");
        return new(pages);
    }
}
