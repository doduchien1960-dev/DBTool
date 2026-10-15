using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using System.Xml.Linq;
using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;
namespace DBTool.Application;

/// <summary>Consumes exact retained recalculation evidence, never a cell's assertion alone.
/// This validator cannot establish that an arbitrary producer actually ran Excel;
/// final independent audit must reconcile the retained execution to the native producer.
/// </summary>
public static class FormulaRecalculationProofV2
{
    public const string CertificateRevision="DBTOOL-EXCEL-RECALC-1";
    public const string ExecutionRevision="DBTOOL-EXCEL-RECALC-EXECUTION-1";
    public static bool Matches(S1StateV2 input,RawCellV2 cell,out string reason)
    {
        reason="Unverified";
        try
        {
            var jobs=input.request.jobs.Where(j=>j.job_id==input.context.job_id).ToArray();
            if(jobs.Length!=1){reason="Selected job is absent or ambiguous";return false;}
            var job=jobs[0];
            if(job.workbook_bytes is null||job.workbook_bytes.Length==0||!Same(Hash(job.workbook_bytes),job.workbook_sha256)||!Same(job.workbook_sha256,input.raw.workbook_hash))
            {reason="Frozen workbook byte/hash mismatch";return false;}
            if(string.IsNullOrWhiteSpace(job.recalculation_receipt_path)||string.IsNullOrWhiteSpace(job.recalculation_receipt_sha256)||!File.Exists(job.recalculation_receipt_path))
            {reason="Recalculation certificate path/hash missing";return false;}
            var bytes=File.ReadAllBytes(job.recalculation_receipt_path);
            if(!Same(Hash(bytes),job.recalculation_receipt_sha256)){reason="Recalculation certificate byte/hash mismatch";return false;}
            using var certificate=JsonDocument.Parse(bytes);var root=certificate.RootElement;
            string Text(JsonElement obj,string key)=>obj.GetProperty(key).GetString()??throw new JsonException("Null "+key);
            if(Text(root,"schema_version")!=CertificateRevision||Text(root,"engine")!="MicrosoftExcel"||string.IsNullOrWhiteSpace(Text(root,"engine_version"))||string.IsNullOrWhiteSpace(Text(root,"run_id"))||!Same(Text(root,"workbook_sha256"),job.workbook_sha256))
            {reason="Certificate revision/engine/run/workbook binding mismatch";return false;}
            string logPath=Text(root,"execution_log_path");
            if(!Path.IsPathFullyQualified(logPath))logPath=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(job.recalculation_receipt_path))!,logPath));
            if(!File.Exists(logPath)){reason="Retained native execution log missing";return false;}
            var logBytes=File.ReadAllBytes(logPath);
            if(!Same(Hash(logBytes),Text(root,"execution_log_sha256"))){reason="Native execution log byte/hash mismatch";return false;}
            using var log=JsonDocument.Parse(logBytes);var execution=log.RootElement;
            if(Text(execution,"schema_version")!=ExecutionRevision||Text(execution,"run_id")!=Text(root,"run_id")||Text(execution,"engine")!="MicrosoftExcel"||Text(execution,"engine_version")!=Text(root,"engine_version")||Text(execution,"operation")!="CalculateFullRebuild"||execution.GetProperty("exit_code").GetInt32()!=0||!execution.GetProperty("save_completed").GetBoolean()||!Same(Text(execution,"output_workbook_sha256"),job.workbook_sha256)||!Same(Text(execution,"source_before_sha256"),Text(execution,"source_after_sha256")))
            {reason="Native execution status/engine/operation/source-preservation mismatch";return false;}
            string outputPath=Text(execution,"output_workbook_path");
            if(!Path.IsPathFullyQualified(outputPath)||!File.Exists(outputPath)||!Same(Hash(File.ReadAllBytes(outputPath)),job.workbook_sha256))
            {reason="Recalculated output bytes missing/changed";return false;}
            var cells=root.GetProperty("cells").EnumerateArray().Where(c=>Text(c,"sheet")==cell.sheet&&Text(c,"address")==cell.address).ToArray();
            if(cells.Length!=1){reason="Formula cell absent/ambiguous in certificate";return false;}
            var proof=cells[0];
            if(Text(proof,"formula")!=cell.formula||Text(proof,"cache_kind")!=cell.cache_kind||Text(proof,"cache_text")!=cell.cache_text)
            {reason="Formula/cache tuple differs from retained certificate";return false;}
            if(!MatchesWorkbookCell(job.workbook_bytes,cell,out reason))return false;
            reason="Exact workbook/certificate/native-log/formula/cache tuple matched; producer authenticity still requires independent audit";
            return true;
        }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or OverflowException or NullReferenceException or System.Xml.XmlException)
        {reason="Malformed/unavailable recalculation evidence: "+e.GetType().Name+": "+e.Message;return false;}
    }
    static bool MatchesWorkbookCell(byte[] bytes,RawCellV2 raw,out string reason)
    {
        reason="Formula/cache tuple differs from exact OOXML bytes";
        using var stream=new MemoryStream(bytes,false);using var zip=new ZipArchive(stream,ZipArchiveMode.Read);
        XDocument Xml(string name)
        {
            var entries=zip.Entries.Where(e=>e.FullName==name).ToArray();
            if(entries.Length!=1)throw new InvalidDataException("Missing/ambiguous OOXML part "+name);
            using var source=entries[0].Open();return XDocument.Load(source);
        }
        XNamespace sheetNs="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheets=Xml("xl/workbook.xml").Descendants(sheetNs+"sheet").Where(s=>(string?)s.Attribute("name")==raw.sheet).ToArray();
        if(sheets.Length!=1)return false;
        string? relationship=(string?)sheets[0].Attribute(relNs+"id");
        var links=Xml("xl/_rels/workbook.xml.rels").Root!.Elements().Where(r=>(string?)r.Attribute("Id")==relationship).ToArray();
        if(links.Length!=1||(string?)links[0].Attribute("TargetMode")=="External")return false;
        string? target=(string?)links[0].Attribute("Target");
        if(string.IsNullOrWhiteSpace(target)||target.Contains('\\')||target.Contains(':'))return false;
        var segments=new List<string>();
        foreach(var part in (target.StartsWith('/')?target.TrimStart('/') : "xl/"+target).Split('/'))
        {if(part=="..") {if(segments.Count==0)return false;segments.RemoveAt(segments.Count-1);}else if(part is not("" or "."))segments.Add(part);}
        var cells=Xml(string.Join('/',segments)).Descendants(sheetNs+"c").Where(c=>(string?)c.Attribute("r")==raw.address).ToArray();
        if(cells.Length!=1)return false;var cell=cells[0];
        var formulas=cell.Elements(sheetNs+"f").ToArray();var values=cell.Elements(sheetNs+"v").ToArray();
        if(formulas.Length!=1||values.Length!=1)return false;
        // Shared/array formulas need the workbook importer's proven expansion, not inference here.
        if(formulas[0].Attributes().Any())return false;
        string kind=(string?)cell.Attribute("t") switch{null or "n"=>"Number","str"=>"Text","b"=>"Bool","e"=>"Error",_=>"Unresolved"};
        return formulas[0].Value==raw.formula&&kind==raw.cache_kind&&values[0].Value==raw.cache_text;
    }
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    static bool Same(string? first,string? second)=>first is not null&&second is not null&&string.Equals(first,second,StringComparison.OrdinalIgnoreCase);
}
