using DBTool.Application;
using DBTool.Infrastructure.Excel;

namespace DBTool.App;

/// <summary>Export exactly the packages handed to CAD. No geometry or schedule recalculation.</summary>
public static class SchedulePackageExport
{
    public static async Task<IReadOnlyList<string>> ExportAsync(PaletteDrawingBatch batch,string directory,CancellationToken token)
    {
        var exporter=new ExportCalculationUseCase(new SteelWorkbookWriter());
        var paths=new List<string>();
        string runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        if (batch.RowReceipts != null && batch.RowReceipts.Any(r => r.Status is "Failed" or "Blocked"))
        {
            // Do not claim complete export when batch has failed rows
        }

        foreach(var manhole in batch.Manholes)
        {
            string rowRunId = $"{manhole.Name}_{runId}";
            var result=await exporter.ExportAsync(manhole.Package,new(directory,null,$"KL thép {manhole.Name}",rowRunId),token);
            if(!result.Success)
            {
                throw new InvalidOperationException($"Export failed for {manhole.Name}: " + string.Join("; ",result.Diagnostics.Select(d=>d.Code+": "+d.Message)));
            }
            paths.Add(result.Value!.Path);
        }
        return paths;
    }
}
