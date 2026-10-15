using DBTool.Domain.StageContractsV2;

namespace DBTool.Drawing.PlanarRegions;

/// <summary>All geometry is analytic, view-local millimetres; no host objects cross this port.</summary>
public interface IPlanarOccupiedRegionKernel
{
    PlanarOccupiedRegionResult Execute(PlanarOccupiedRegionRequest request);
}

public sealed record PlanarRegionTolerance(double LinearMm, double AngularRad, double AreaMm2, string[] SourceIds, double BasisDimensionless);
public sealed record PlanarRegionOperation(int Sequence, string Id, string Kind, LoopV2[] Loops, string[] SourceIds);
public sealed record PlanarComponentRegion(string ComponentId, PlanarRegionOperation[] Operations, string[] SourceIds);
public sealed record PlanarOccupiedRegionRequest(string RequestId, string AssemblyId, string MaterialId, string ViewId,
    PlanarComponentRegion[] Components, PlanarRegionTolerance Tolerance, string[] SourceIds);
public sealed record PlanarRegionLoop(LoopV2 Boundary, string? ParentLoopId, string[] ComponentSources);
public sealed record PlanarOccupiedRegionResult(string RequestId, string AssemblyId, string MaterialId, string ViewId,
    bool IsEmpty, PlanarRegionLoop[] Boundaries, double TotalOccupiedAreaMm2, int InputEdgeCount,
    int OutputEdgeCount, string[] UnconsumedEdgeIds, string CanonicalGeometryHash, string[] SourceIds)
{
    public LoopV2[] Loops => Boundaries.Select(b => b.Boundary).ToArray();
}
