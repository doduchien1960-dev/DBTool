using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Application;

public enum GenerationMode
{
    Create,
    Replace
}

public sealed record GenerationRequest
{
    public GenerationRequest(
        GenerationMode mode,
        string? expectedDocumentToken,
        string? expectedGenerationId,
        string expectedInputHash,
        Point2Mm insertionPoint,
        string? requestedGenerationId = null)
    {
        Mode = ContractGuard.EnumValue(mode);
        ExpectedDocumentToken = string.IsNullOrWhiteSpace(expectedDocumentToken) ? null : expectedDocumentToken;
        ExpectedGenerationId = string.IsNullOrWhiteSpace(expectedGenerationId) ? null : expectedGenerationId;
        ExpectedInputHash = ContractGuard.Hash(expectedInputHash);
        InsertionPoint = insertionPoint;
        RequestedGenerationId = string.IsNullOrWhiteSpace(requestedGenerationId) ? null : requestedGenerationId;
    }

    public GenerationMode Mode { get; }
    public string? ExpectedDocumentToken { get; }
    public string? ExpectedGenerationId { get; }
    public string ExpectedInputHash { get; }
    public Point2Mm InsertionPoint { get; }
    public string? RequestedGenerationId { get; }
}

public sealed record RenderedEntity
{
    public RenderedEntity(string semanticId, string handle, IEnumerable<string>? childHandles = null)
    {
        SemanticId = ContractGuard.Text(semanticId, nameof(semanticId));
        Handle = ContractGuard.Text(handle, nameof(handle));
        ChildHandles = childHandles is null ? ImmutableArray<string>.Empty : ContractGuard.Copy(childHandles);
    }

    public string SemanticId { get; }
    public string Handle { get; }
    public ImmutableArray<string> ChildHandles { get; }
}

public sealed record RenderedEntityMap
{
    public RenderedEntityMap(IEnumerable<RenderedEntity> entities)
    {
        Entities = ContractGuard.Copy(entities);
        if (Entities.Select(x => x.SemanticId).Distinct(StringComparer.Ordinal).Count() != Entities.Length)
            throw new ArgumentException("Duplicate rendered semantic identity.", nameof(entities));
    }

    public ImmutableArray<RenderedEntity> Entities { get; }
}

public sealed record GenerationArtifact
{
    public GenerationArtifact(
        string documentToken,
        string generationId,
        string inputHash,
        string templateHash,
        string snapshotVersion,
        IEnumerable<RenderedEntity> entities,
        bool committed)
    {
        DocumentToken = ContractGuard.Text(documentToken, nameof(documentToken));
        GenerationId = ContractGuard.Text(generationId, nameof(generationId));
        InputHash = ContractGuard.Hash(inputHash);
        TemplateHash = ContractGuard.Hash(templateHash);
        SnapshotVersion = ContractGuard.Text(snapshotVersion, nameof(snapshotVersion));
        EntityMap = new RenderedEntityMap(entities);
        Committed = committed;
    }

    public string DocumentToken { get; }
    public string GenerationId { get; }
    public string InputHash { get; }
    public string TemplateHash { get; }
    public string SnapshotVersion { get; }
    public RenderedEntityMap EntityMap { get; }
    public bool Committed { get; }
}
