using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public sealed record CalculationPackage
{
    public CalculationPackage(string inputHash, string sourceWorkbookHash, string engineVersion, string contractVersion, string ruleVersion, string policyId, CalculationScope scope, GeometryModel geometry, IEnumerable<BarOccurrence> occurrences, SteelSchedule? steelSchedule, CalculationStatus concreteStatus, IEnumerable<QuantityLine>? concreteLines, CalculationStatus formworkStatus, IEnumerable<QuantityLine>? formworkLines, DrawingPackage? drawingPackage, IEnumerable<Diagnostic> diagnostics, AngleSchedule? angleSchedule = null)
    {
        InputHash = ContractGuard.Hash(inputHash);
        SourceWorkbookHash = ContractGuard.Hash(sourceWorkbookHash);
        EngineVersion = ContractGuard.Text(engineVersion, nameof(engineVersion));
        ContractVersion = ContractGuard.Text(contractVersion, nameof(contractVersion));
        RuleVersion = ContractGuard.Text(ruleVersion, nameof(ruleVersion));
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
        Scope = ContractGuard.EnumValue(scope);
        Geometry = ContractGuard.Required(geometry);
        Occurrences = ContractGuard.Copy(occurrences);
        SteelSchedule = steelSchedule;
        AngleSchedule = angleSchedule;
        ConcreteStatus = ContractGuard.EnumValue(concreteStatus);
        ConcreteLines = concreteLines is null ? null : ContractGuard.Copy(concreteLines);
        FormworkStatus = ContractGuard.EnumValue(formworkStatus);
        FormworkLines = formworkLines is null ? null : ContractGuard.Copy(formworkLines);
        DrawingPackage = drawingPackage;
        Diagnostics = ContractGuard.Copy(diagnostics);
        if((concreteStatus==CalculationStatus.NotCalculated)!=(concreteLines==null) || (formworkStatus==CalculationStatus.NotCalculated)!=(formworkLines==null)) throw new ArgumentException("Calculation status and payload mismatch.");
        if(scope is CalculationScope.BaseRebarOnly or CalculationScope.FullRebar or CalculationScope.Full && steelSchedule==null) throw new ArgumentException("Steel scope requires schedule.");
        if(scope==CalculationScope.BaseRebarOnly && (concreteStatus!=CalculationStatus.NotCalculated || formworkStatus!=CalculationStatus.NotCalculated)) throw new ArgumentException("BaseRebarOnly must not claim other quantities.");
        if(scope is CalculationScope.Full or CalculationScope.ConcreteOnly && concreteStatus!=CalculationStatus.Calculated) throw new ArgumentException("Scope requires calculated concrete quantities.");
        if(scope is CalculationScope.Full or CalculationScope.FormworkOnly && formworkStatus!=CalculationStatus.Calculated) throw new ArgumentException("Scope requires calculated formwork quantities.");
        if(Diagnostics.Any(d=>d.Severity==DiagnosticSeverity.Block)) throw new ArgumentException("Successful package cannot contain Block.");
        if(Occurrences.Select(o=>o.Id).Distinct().Count()!=Occurrences.Length) throw new ArgumentException("Duplicate occurrence.");
        if(Occurrences.Any(o=>!Geometry.Components.Any(c=>c.Id==o.ComponentId && c.Kind==o.ComponentKind))) throw new ArgumentException("Unresolved occurrence component.");
        if(steelSchedule!=null)
        {
            var scheduled=steelSchedule.Rows.SelectMany(r=>r.OccurrenceIds).OrderBy(x=>x,StringComparer.Ordinal);
            if(!scheduled.SequenceEqual(Occurrences.Select(o=>o.Id).OrderBy(x=>x,StringComparer.Ordinal))) throw new ArgumentException("Schedule must account for all occurrences.");
        }
        if(drawingPackage!=null)
        {
            if(drawingPackage.InputHash!=InputHash || drawingPackage.RuleVersion!=ruleVersion || drawingPackage.Scope!=scope) throw new ArgumentException("Drawing identity mismatch.");
            foreach(var p in drawingPackage.Primitives)
            {
                if(p.ComponentId!=null && !Geometry.Components.Any(c=>c.Id==p.ComponentId))
                    throw new ArgumentException($"Unresolved drawing trace: ComponentId '{p.ComponentId}' on '{p.SemanticId}' not found in Geometry.");
                if(p.OccurrenceId!=null && !Occurrences.Any(o=>o.Id==p.OccurrenceId) && !Geometry.Openings.Any(o=>o.Id==p.OccurrenceId))
                    throw new ArgumentException($"Unresolved drawing trace: OccurrenceId '{p.OccurrenceId}' on '{p.SemanticId}' not found in Occurrences or Openings.");
                if(p.ScheduleMark!=null && !(steelSchedule?.Rows.Any(r=>r.Mark==p.ScheduleMark)??false))
                    throw new ArgumentException($"Unresolved drawing trace: ScheduleMark '{p.ScheduleMark}' on '{p.SemanticId}' not found in Schedule.");
            }
        }
    }
    public string InputHash { get; }
    public string SourceWorkbookHash { get; }
    public string EngineVersion { get; }
    public string ContractVersion { get; }
    public string RuleVersion { get; }
    public string PolicyId { get; }
    public CalculationScope Scope { get; }
    public GeometryModel Geometry { get; }
    public ImmutableArray<BarOccurrence> Occurrences { get; }
    public SteelSchedule? SteelSchedule { get; }
    public AngleSchedule? AngleSchedule { get; }
    public CalculationStatus ConcreteStatus { get; }
    public ImmutableArray<QuantityLine>? ConcreteLines { get; }
    public CalculationStatus FormworkStatus { get; }
    public ImmutableArray<QuantityLine>? FormworkLines { get; }
    public DrawingPackage? DrawingPackage { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
}
