using DBTool.Domain.Geometry;
namespace DBTool.Domain;

/// <summary>Validates the explicit owner nominal drawing assembly. Does not certify product bearing capacity.</summary>
public static class OwnerCoverAssemblyValidator
{
    public static OperationResult<CoverSectionOutlines> Validate(CoverCatalogEntry entry,Rectangle2Mm opening,
        double coverHeightMm,double toleranceMm,SourceRef coverSource,SourceRef openingSource)
    {
        OperationResult<CoverSectionOutlines> Block(string code,string message)=>new(false,null,
            [new(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,coverSource,
                $"Compatible opening at {openingSource.SheetName}!{openingSource.CellAddress}",null,message)]);
        if(entry.BlockSet is not {NominalOpening:not null,SectionOutlineRule:not null} blocks ||
            entry.InsertionOffset is null || entry.RotationDegrees is null)
            return Block("COVER_OWNER_CONTRACT_REQUIRED","Explicit sourced nominal dimensions, section rule and insertion required.");
        if(!double.IsFinite(toleranceMm) || toleranceMm<0)
            return Block("COVER_ASSEMBLY_TOLERANCE_INVALID","Finite nonnegative tolerance required.");
        // VHG CF.Ga Cao nap is the installation rise from neck top to design top.
        // The section outline depth is a separate drawing convention, not this rise
        // and not a dimension by which the native product may be stretched.
        if(!double.IsFinite(coverHeightMm) || coverHeightMm<0)
            return Block("COVER_INSTALLATION_HEIGHT_INVALID","Cover installation rise must be finite and nonnegative.");
        var centre=new Point2Mm(opening.MinX/2+opening.MaxX/2,opening.MinY/2+opening.MaxY/2);
        var outline=CoverSectionOutlineBuilder.Build(blocks.NominalOpening,centre,0,entry.RotationDegrees.Value,blocks.SectionOutlineRule);
        if(!outline.Success) return outline;
        var actual=outline.Value!;
        double width=actual.BB.MaxX-actual.BB.MinX-2*blocks.SectionOutlineRule.SideExtensionMm;
        double height=actual.AA.MaxX-actual.AA.MinX-2*blocks.SectionOutlineRule.SideExtensionMm;
        if(Math.Abs(width-(opening.MaxX-opening.MinX))>toleranceMm || Math.Abs(height-(opening.MaxY-opening.MinY))>toleranceMm)
            return Block("COVER_OPENING_INCOMPATIBLE",$"Nominal cover axes do not match {openingSource.SheetName}!{openingSource.CellAddress}; expected {width}x{height}.");
        var placement=CoverBlockPlacementResolver.Resolve(blocks,new(0,0),0,entry.RotationDegrees.Value);
        if(!placement.Success) return new(false,null,placement.Diagnostics);
        if(Math.Abs(placement.Value!.PlanInsertion.X-entry.InsertionOffset.Value.X)>toleranceMm ||
            Math.Abs(placement.Value.PlanInsertion.Y-entry.InsertionOffset.Value.Y)>toleranceMm || Math.Abs(entry.InsertionOffset.Value.Z)>toleranceMm)
            return Block("COVER_VIEW_ANCHOR_INCOMPATIBLE","Declared insertion disagrees with the per-view reference.");
        return outline;
    }
}
