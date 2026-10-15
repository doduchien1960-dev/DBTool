using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DBTool.Domain.StageContractsV2;
using Xunit;
namespace DBTool.Application.Tests;

public sealed class LeaderFormulaProofTests
{
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    [Theory][InlineData(false)][InlineData(true)]
    public void CertificateAndLogCannotAuthorizeDifferentWorkbookFormulaCache(bool forgedTuple)
    {
        string directory=Path.Combine(Path.GetTempPath(),"DBTool-SyntheticCacheProof-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            using var stream=new MemoryStream();
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
            {
                void Part(string name,string content){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(content);}
                Part("xl/workbook.xml","<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='CF.Ga' sheetId='1' r:id='r1'/></sheets></workbook>");
                Part("xl/_rels/workbook.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Target='worksheets/sheet1.xml'/></Relationships>");
                Part("xl/worksheets/sheet1.xml","<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='11'><c r='E11'><f>1+1</f><v>2</v></c></row></sheetData></worksheet>");
            }
            var bytes=stream.ToArray();string workbook=Path.Combine(directory,"synthetic.xlsx");File.WriteAllBytes(workbook,bytes);string hash=Hash(bytes);
            var cell=new RawCellV2("TestFormula","CF.Ga","E11","Formula",null,null,null,"1+1","Number",forgedTuple?"999":"2",true,true,"synthetic-source",[]);
            string log=Path.Combine(directory,"execution.json");
            File.WriteAllText(log,JsonSerializer.Serialize(new{schema_version=FormulaRecalculationProofV2.ExecutionRevision,run_id="synthetic-proof-contract-only",engine="MicrosoftExcel",engine_version="synthetic-not-a-native-run",operation="CalculateFullRebuild",exit_code=0,save_completed=true,output_workbook_sha256=hash,output_workbook_path=workbook,source_before_sha256=hash,source_after_sha256=hash}));
            string certificate=Path.Combine(directory,"certificate.json");
            File.WriteAllText(certificate,JsonSerializer.Serialize(new{schema_version=FormulaRecalculationProofV2.CertificateRevision,run_id="synthetic-proof-contract-only",engine="MicrosoftExcel",engine_version="synthetic-not-a-native-run",workbook_sha256=hash,execution_log_path=log,execution_log_sha256=Hash(File.ReadAllBytes(log)),cells=new[]{new{sheet=cell.sheet,address=cell.address,formula=cell.formula,cache_kind=cell.cache_kind,cache_text=cell.cache_text}}}));
            var input=LeaderS2ScalarIntegrityTests.Valid();
            var job=new DBTool.Application.StageContractsV2.JobV2(input.context.job_id,"synthetic",workbook,bytes,hash,input.raw.profile,input.raw.main_sheet,"en-US",[],certificate,Hash(File.ReadAllBytes(certificate)));
            input=input with{request=input.request with{jobs=[job]},raw=input.raw with{workbook_hash=hash}};
            bool actual=FormulaRecalculationProofV2.Matches(input,cell,out var reason);
            Assert.Equal(!forgedTuple,actual);Assert.NotEmpty(reason);
            File.AppendAllText(log," ");
            Assert.False(FormulaRecalculationProofV2.Matches(input,cell,out var changedLog));Assert.Contains("log byte/hash mismatch",changedLog);
        }
        finally
        {
            // Only this test's verified absolute temporary directory can be removed.
            string root=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(directory).StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("TEST_PATH_ESCAPE");
            Directory.Delete(directory,true);
        }
    }
}
