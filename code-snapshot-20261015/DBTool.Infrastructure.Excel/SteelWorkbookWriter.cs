using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;

namespace DBTool.Infrastructure.Excel;

public sealed class SteelWorkbookWriter : ICancellableSteelWorkbookWriter
{
    private readonly Action<XLWorkbook,string> saveWorkbook;

    public SteelWorkbookWriter(Action<XLWorkbook,string>? saveWorkbook=null) =>
        this.saveWorkbook=saveWorkbook ?? ((book,path)=>book.SaveAs(path));

    private static readonly string[] SteelHeaders =
    ["Mark","ComponentKind","ComponentLabel","Layer","Axis","DiameterMm","Quantity",
     "SingleLengthMm","TotalLengthMm","UnitMassKgPerM","MassKg","RuleId"];

    public OperationResult<ExportArtifact> Write(CalculationPackage package,WorkbookWriteRequest request)
        => Write(package,request,CancellationToken.None);

    public OperationResult<ExportArtifact> Write(CalculationPackage package,WorkbookWriteRequest request,CancellationToken token)
    {
        if(token.IsCancellationRequested)return Block("OPERATION_CANCELLED","Export cancelled before write.");
        var valid=WorkbookPackageValidator.Validate(package);
        if (!valid.Success) return new(false,null,valid.Diagnostics);
        if (request is null) return Block("EXPORT_WRITE_FAILED","Workbook request is required.");

        var reserved=OutputFileNamer.ReserveNewFile(new OutputNameRequest(request.OutputDirectory,
            request.DisplayName,".xlsx",request.RunId));
        if (!reserved.Success) return new(false,null,reserved.Diagnostics);
        var reservation=reserved.Value!;
        string staging=Path.Combine(Path.GetDirectoryName(reservation.Path)!,
            $".{Path.GetFileNameWithoutExtension(reservation.Path)}.{Guid.NewGuid():N}.staging.xlsx");
        try
        {
            token.ThrowIfCancellationRequested();
            using (var book=BuildWorkbook(package,valid.Value!)) saveWorkbook(book,staging);
            token.ThrowIfCancellationRequested();
            var finalized=OutputFileNamer.FinalizeReservation(reservation,staging);
            if (!finalized.Success) return new(false,null,finalized.Diagnostics);
            return new(true,new ExportArtifact(reservation.Path,package.InputHash,package.RuleVersion,
                package.Scope,RecalculationStatus.NotRun),[]);
        }
        catch (OperationCanceledException ex) { return Block("OPERATION_CANCELLED",ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { return Block("EXPORT_WRITE_FAILED",ex.Message); }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
            if (File.Exists(reservation.ReservationPath)) OutputFileNamer.ReleaseReservation(reservation);
        }
    }

    private static XLWorkbook BuildWorkbook(CalculationPackage package,SteelSchedule schedule)
    {
        var book=new XLWorkbook();
        var steel=book.AddWorksheet("Steel");
        var angles=package.AngleSchedule is null ? null : book.AddWorksheet("AngleSteel");
        var summary=book.AddWorksheet("Summary");
        var trace=book.AddWorksheet("Trace");
        var metadata=book.AddWorksheet("Metadata");
        string[] angleHeaders=["Mark","CatalogCode","ComponentId","Quantity","SingleLengthMm",
            "TotalLengthMm","UnitMassKgPerM","MassKg"];
        for(int column=0;column<SteelHeaders.Length;column++) steel.Cell(1,column+1).Value=SteelHeaders[column];
        if (angles is not null)
            for(int column=0;column<angleHeaders.Length;column++) angles.Cell(1,column+1).Value=angleHeaders[column];
        trace.Cell("A1").Value="OccurrenceId";
        trace.Cell("B1").Value="Mark";
        trace.Cell("C1").Value="ComponentId";
        var occurrences=package.Occurrences.ToDictionary(bar=>bar.Id,StringComparer.Ordinal);
        int steelRow=2,traceRow=2;
        foreach(var row in schedule.Rows)
        {
            var labels=row.OccurrenceIds.Select(id=>occurrences[id].ComponentId)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            steel.Cell(steelRow,1).Value=row.Mark;
            steel.Cell(steelRow,2).Value=row.ComponentKind.ToString();
            steel.Cell(steelRow,3).Value=string.Join("; ",labels);
            steel.Cell(steelRow,4).Value=row.Layer.ToString();
            steel.Cell(steelRow,5).Value=row.Axis.ToString();
            steel.Cell(steelRow,6).Value=row.DiameterMm;
            steel.Cell(steelRow,7).Value=row.Quantity;
            steel.Cell(steelRow,8).Value=row.SingleLengthMm;
            steel.Cell(steelRow,9).Value=row.TotalLengthMm;
            steel.Cell(steelRow,10).Value=row.UnitMassKgPerM;
            steel.Cell(steelRow,11).Value=row.MassKg.Value;
            steel.Cell(steelRow,12).Value=row.RuleId;
            foreach(string id in row.OccurrenceIds)
            {
                trace.Cell(traceRow,1).Value=id;
                trace.Cell(traceRow,2).Value=row.Mark;
                trace.Cell(traceRow,3).Value=occurrences[id].ComponentId;
                traceRow++;
            }
            steelRow++;
        }
        int angleRow=2;
        if(package.AngleSchedule is not null)
        {
            foreach(var row in package.AngleSchedule.Rows)
            {
                angles!.Cell(angleRow,1).Value=row.Mark;
                angles.Cell(angleRow,2).Value=row.CatalogCode;
                angles.Cell(angleRow,3).Value=row.ComponentId;
                angles.Cell(angleRow,4).Value=row.Quantity;
                angles.Cell(angleRow,5).Value=row.SingleLengthMm;
                angles.Cell(angleRow,6).Value=row.TotalLengthMm;
                angles.Cell(angleRow,7).Value=row.UnitMassKgPerM;
                angles.Cell(angleRow,8).Value=row.MassKg.Value;
                angleRow++;
            }
        }
        string[] summaryHeaders=["ComponentKind","Scope","RawTotalKg","DisplayTotalKg","CoreTotalKg","SteelRowsSumKg",
            "AngleTotalMassKg","RawSteelTotalMassKg"];
        for(int column=0;column<summaryHeaders.Length;column++) summary.Cell(1,column+1).Value=summaryHeaders[column];
        int summaryRow=2;
        foreach(var component in schedule.SummaryByComponent.OrderBy(item=>item.Key))
        {
            summary.Cell(summaryRow,1).Value=component.Key.ToString();
            summary.Cell(summaryRow,2).Value=package.Scope.ToString();
            summary.Cell(summaryRow,3).Value=component.Value.Value;
            summary.Cell(summaryRow,4).Value=Display(component.Value.Value);
            summary.Cell(summaryRow,5).Value=component.Value.Value;
            summaryRow++;
        }
        summary.Cell(summaryRow,1).Value="GrandTotal";
        summary.Cell(summaryRow,2).Value=package.Scope.ToString();
        summary.Cell(summaryRow,3).FormulaA1=$"SUM(C2:C{summaryRow-1})";
        summary.Cell(summaryRow,4).Value=Display(schedule.TotalMassKg.Value);
        summary.Cell(summaryRow,5).Value=schedule.TotalMassKg.Value;
        summary.Cell(summaryRow,6).FormulaA1=$"SUM(Steel!K2:K{steelRow-1})";
        double angleTotal=package.AngleSchedule?.TotalMassKg.Value ?? 0d;
        summary.Cell(summaryRow,7).Value=angleTotal;
        summary.Cell(summaryRow,8).Value=schedule.TotalMassKg.Value+angleTotal;

        var metadataRows=new (string Key,string Value)[]
        {
            ("InputHash",package.InputHash),("SourceWorkbookHash",package.SourceWorkbookHash),
            ("EngineVersion",package.EngineVersion),("ContractVersion",package.ContractVersion),
            ("RuleVersion",package.RuleVersion),("PolicyId",package.PolicyId),
            ("MassPolicy",schedule.MassPolicyId),("MassPolicyVersion",schedule.MassPolicyVersion),
            ("AngleMassKg",angleTotal.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("RawSteelTotalMassKg",(schedule.TotalMassKg.Value+angleTotal).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Scope",package.Scope.ToString()),
            ("BaseRebarOnly",(package.Scope==CalculationScope.BaseRebarOnly).ToString()),
            ("EvidenceStatus",package.PolicyId=="S1-Analytic-1" ? "ExperimentalSoftwareFixture" : "Unverified"),
            ("Experimental",(package.PolicyId=="S1-Analytic-1").ToString()),
            ("ConcreteStatus",package.ConcreteStatus.ToString()),
            ("FormworkStatus",package.FormworkStatus.ToString()),
            ("ConcreteNotCalculated",(package.ConcreteStatus==CalculationStatus.NotCalculated).ToString()),
            ("FormworkNotCalculated",(package.FormworkStatus==CalculationStatus.NotCalculated).ToString()),
            ("RecalculationStatus",RecalculationStatus.NotRun.ToString()),
            ("ExcelHostStatus","ExcelHostRecalcPending")
        };
        for(int row=0;row<metadataRows.Length;row++)
        {
            metadata.Cell(row+1,1).Value=metadataRows[row].Key;
            metadata.Cell(row+1,2).Value=metadataRows[row].Value;
        }
        // Excel numeric storage is limited to 15 significant digits. Preserve the source doubles as text too.
        int rawRow=metadataRows.Length+1;
        foreach(var row in schedule.Rows)
        {
            metadata.Cell(rawRow,1).Value="RawSteel/"+row.Mark;
            metadata.Cell(rawRow++,2).Value=System.Text.Json.JsonSerializer.Serialize(new {
                row.Quantity,row.SingleLengthMm,row.TotalLengthMm,row.UnitMassKgPerM,MassKg=row.MassKg.Value });
        }
        foreach(var row in package.AngleSchedule?.Rows ?? [])
        {
            metadata.Cell(rawRow,1).Value="RawAngle/"+row.Mark;
            metadata.Cell(rawRow++,2).Value=System.Text.Json.JsonSerializer.Serialize(new {
                row.Quantity,row.SingleLengthMm,row.TotalLengthMm,row.UnitMassKgPerM,MassKg=row.MassKg.Value });
        }
        steel.Row(1).Style.Font.Bold=true;
        if (angles is not null) angles.Row(1).Style.Font.Bold=true;
        summary.Row(1).Style.Font.Bold=true;
        trace.Row(1).Style.Font.Bold=true;
        steel.SheetView.FreezeRows(1);
        if (angles is not null) angles.SheetView.FreezeRows(1);
        trace.SheetView.FreezeRows(1);
        return book;
    }

    private static double Display(double value) => Math.Round(value,3,MidpointRounding.AwayFromZero);
    private static OperationResult<ExportArtifact> Block(string code,string message) =>
        new(false,null,[new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,null,null,null,message)]);
}
