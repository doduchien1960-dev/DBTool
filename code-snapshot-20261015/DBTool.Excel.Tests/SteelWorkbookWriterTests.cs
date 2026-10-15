using System.Security.Cryptography;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class SteelWorkbookWriterTests
{
    [Fact]
    public void S1PackageWritesFourRowsFortyEightTraceAndRawTotals()
    {
        using var output=new TemporaryOutput();
        var package=PackageFixture.S1();
        var result=new SteelWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory,null,"S1-MONO-001","run1"));
        Assert.True(result.Success,Details(result.Diagnostics));
        Assert.Equal(RecalculationStatus.NotRun,result.Value!.RecalculationStatus);
        Assert.Equal(package.InputHash,result.Value.InputHash);
        Assert.True(File.Exists(result.Value.Path));
        Assert.Empty(Directory.GetFiles(output.Directory,"*.reserve"));
        using var book=new XLWorkbook(result.Value.Path);
        Assert.Equal(new[]{"Steel","Summary","Trace","Metadata"},book.Worksheets.Select(s=>s.Name));
        var steel=book.Worksheet("Steel");
        Assert.Equal("Mark",steel.Cell("A1").GetString());
        Assert.Equal("ComponentKind",steel.Cell("B1").GetString());
        Assert.Equal("RuleId",steel.Cell("L1").GetString());
        Assert.Equal(5,steel.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal(new[]{13,11,13,11},Enumerable.Range(2,4).Select(r=>(int)steel.Cell(r,7).GetDouble()));
        Assert.All(Enumerable.Range(2,4),r=>Assert.Equal(XLDataType.Number,steel.Cell(r,11).DataType));
        Assert.Equal(48,Enumerable.Range(2,4).Sum(r=>(int)steel.Cell(r,7).GetDouble()));
        Assert.Equal(74620,Enumerable.Range(2,4).Sum(r=>steel.Cell(r,9).GetDouble()),6);
        Assert.InRange(Math.Abs(Enumerable.Range(2,4).Sum(r=>steel.Cell(r,11).GetDouble())-46.06172839506173),0,1e-9);
        var trace=book.Worksheet("Trace");
        Assert.Equal(49,trace.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal(48,Enumerable.Range(2,48).Select(r=>trace.Cell(r,1).GetString()).Distinct().Count());
        var summary=book.Worksheet("Summary");
        Assert.Equal("Base",summary.Cell("A2").GetString());
        Assert.Equal("BaseRebarOnly",summary.Cell("B2").GetString());
        Assert.InRange(Math.Abs(summary.Cell("C2").GetDouble()-46.06172839506173),0,1e-9);
        Assert.Equal(46.062,summary.Cell("D3").GetDouble(),3);
        Assert.InRange(Math.Abs(summary.Cell("E3").GetDouble()-46.06172839506173),0,1e-9);
        Assert.Contains("SUM(C2:C2)",summary.Cell("C3").FormulaA1,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("K2:K5",summary.Cell("F3").FormulaA1,StringComparison.OrdinalIgnoreCase);
        var metadata=book.Worksheet("Metadata");
        Assert.Equal(package.InputHash,Metadata(metadata,"InputHash"));
        Assert.Equal(package.RuleVersion,Metadata(metadata,"RuleVersion"));
        Assert.Equal(package.SteelSchedule!.MassPolicyId,Metadata(metadata,"MassPolicy"));
        Assert.Equal("BaseRebarOnly",Metadata(metadata,"Scope"));
        Assert.Equal("ExperimentalSoftwareFixture",Metadata(metadata,"EvidenceStatus"));
        Assert.Equal("True",Metadata(metadata,"BaseRebarOnly"));
        Assert.Equal("True",Metadata(metadata,"Experimental"));
        Assert.Equal("True",Metadata(metadata,"ConcreteNotCalculated"));
        Assert.Equal("True",Metadata(metadata,"FormworkNotCalculated"));
        Assert.Equal("NotCalculated",Metadata(metadata,"ConcreteStatus"));
        Assert.Equal("NotCalculated",Metadata(metadata,"FormworkStatus"));
        Assert.Equal("ExcelHostRecalcPending",Metadata(metadata,"ExcelHostStatus"));
        string? artifactDirectory=Environment.GetEnvironmentVariable("DBTOOL_T33A_ARTIFACT_DIR");
        if(!string.IsNullOrWhiteSpace(artifactDirectory))
        {
            Directory.CreateDirectory(artifactDirectory);
            File.Copy(result.Value.Path,Path.Combine(artifactDirectory,"S1-MONO-001-steel.xlsx"),overwrite:true);
        }
    }

    [Fact]
    public void TwoHundredScheduleRowsAndTracesAreNotTruncated()
    {
        using var output=new TemporaryOutput();
        var package=PackageFixture.ManyRows(200);
        var result=new SteelWorkbookWriter().Write(package,
            new WorkbookWriteRequest(output.Directory,null,"Many rows","run1"));
        Assert.True(result.Success,Details(result.Diagnostics));
        using var book=new XLWorkbook(result.Value!.Path);
        var steel=book.Worksheet("Steel");
        Assert.Equal(201,steel.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal(201,book.Worksheet("Trace").LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        Assert.Equal(200,Enumerable.Range(2,200).Sum(r=>(int)steel.Cell(r,7).GetDouble()));
        Assert.Equal(200000,Enumerable.Range(2,200).Sum(r=>steel.Cell(r,9).GetDouble()));
        Assert.InRange(Math.Abs(book.Worksheet("Summary").Cell("E3").GetDouble()-123.45679012345679),0,1e-9);
        Assert.Contains("K2:K201",book.Worksheet("Summary").Cell("F3").FormulaA1,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComponentLabelDoesNotChangeEnumSummaryAndSecondExportKeepsFirstBytes()
    {
        using var output=new TemporaryOutput();
        var package=PackageFixture.S1(componentLabel:"Bản trần");
        var request=new WorkbookWriteRequest(output.Directory,null,"Same","run1");
        var first=new SteelWorkbookWriter().Write(package,request);
        Assert.True(first.Success,Details(first.Diagnostics));
        byte[] firstBytes=File.ReadAllBytes(first.Value!.Path);
        var second=new SteelWorkbookWriter().Write(package,request);
        Assert.True(second.Success,Details(second.Diagnostics));
        Assert.NotEqual(first.Value.Path,second.Value!.Path);
        Assert.Contains("-r002",second.Value.Path);
        Assert.Equal(firstBytes,File.ReadAllBytes(first.Value.Path));
        using var book=new XLWorkbook(second.Value.Path);
        Assert.Equal("Bản trần",book.Worksheet("Steel").Cell("C2").GetString());
        Assert.Equal("Base",book.Worksheet("Summary").Cell("A2").GetString());
    }

    [Fact]
    public void UnknownAndDuplicateTraceIdsAreRejectedByPackageValidator()
    {
        var package=PackageFixture.S1();
        var rows=package.SteelSchedule!.Rows;
        var duplicate=WorkbookPackageValidator.ValidateTraceIds(package.Occurrences,
            rows.SelectMany(r=>r.OccurrenceIds).Append(rows[0].OccurrenceIds[0]));
        Assert.False(duplicate.Success); Assert.Null(duplicate.Value);
        Assert.Contains(duplicate.Diagnostics,d=>d.Code=="EXPORT_DUPLICATE_OCCURRENCE" && d.Severity==DiagnosticSeverity.Block);
        var unknown=WorkbookPackageValidator.ValidateTraceIds(package.Occurrences,
            rows.SelectMany(r=>r.OccurrenceIds).Skip(1).Append("unknown-id"));
        Assert.False(unknown.Success); Assert.Null(unknown.Value);
        Assert.Contains(unknown.Diagnostics,d=>d.Code=="EXPORT_OCCURRENCE_UNKNOWN" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void SaveFailureBeforeFinalizeLeavesNoWorkbookOrOwnReservation()
    {
        using var output=new TemporaryOutput();
        var writer=new SteelWorkbookWriter((_,path)=>
        {
            File.WriteAllText(path,"incomplete workbook");
            throw new IOException("simulated disk failure");
        });
        var result=writer.Write(PackageFixture.S1(),
            new WorkbookWriteRequest(output.Directory,null,"Fault","run1"));
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="EXPORT_WRITE_FAILED" && d.Severity==DiagnosticSeverity.Block);
        Assert.Empty(Directory.GetFiles(output.Directory));
    }

    private static string Metadata(IXLWorksheet sheet,string key)
    {
        for(int row=1;row<=(sheet.LastRowUsed()?.RowNumber() ?? 0);row++)
            if(sheet.Cell(row,1).GetString()==key) return sheet.Cell(row,2).GetString();
        throw new Xunit.Sdk.XunitException($"Metadata key missing: {key}");
    }
    private static string Details(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("; ",diagnostics.Select(d=>$"{d.Code}: {d.Message}"));

    private sealed class TemporaryOutput : IDisposable
    {
        public TemporaryOutput() { Directory=Path.Combine(Path.GetTempPath(),"dbtool-t33a-"+Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(Directory); }
        public string Directory { get; }
        public void Dispose() => System.IO.Directory.Delete(Directory,true);
    }

    private static class PackageFixture
    {
        private const string FixtureId="S1/GaThamLienKhoi/S1-MONO-001";
        private const string Rule="MESH-UNIFORM-TEST-001";
        public static CalculationPackage S1(string componentLabel="Base") => Build(
            new[] { (Layer.Bottom,Axis.X,13,1440d,-115d),(Layer.Bottom,Axis.Y,11,1690d,-105d),
                (Layer.Top,Axis.X,13,1440d,-35d),(Layer.Top,Axis.Y,11,1690d,-45d) },componentLabel);

        public static CalculationPackage ManyRows(int count)
        {
            string componentLabel="Base";
            var geometry=Geometry(componentLabel);
            var bars=new List<BarOccurrence>();
            var rows=new List<SteelScheduleRow>();
            for(int i=1;i<=count;i++)
            {
                string id=$"{FixtureId}/Base/Test/{i:000}";
                bars.Add(Bar(id,componentLabel,Layer.Bottom,Axis.X,1000,0));
                rows.Add(Row($"D{i:000}",Layer.Bottom,Axis.X,1000,[id]));
            }
            return Package(geometry,bars,rows);
        }

        private static CalculationPackage Build((Layer Layer,Axis Axis,int Count,double Length,double Z)[] groups,string componentLabel)
        {
            var bars=new List<BarOccurrence>();
            var rows=new List<SteelScheduleRow>();
            for(int g=0;g<groups.Length;g++)
            {
                var group=groups[g];
                var ids=new List<string>();
                for(int i=1;i<=group.Count;i++)
                {
                    string id=$"{FixtureId}/Base/{group.Layer}/{group.Axis}/{i:000}";
                    ids.Add(id);
                    bars.Add(Bar(id,componentLabel,group.Layer,group.Axis,group.Length,group.Z));
                }
                rows.Add(Row($"D{g+1:000}",group.Layer,group.Axis,group.Length,ids));
            }
            return Package(Geometry(componentLabel),bars,rows);
        }

        private static CalculationPackage Package(GeometryModel geometry,List<BarOccurrence> bars,List<SteelScheduleRow> rows)
        {
            double total=rows.Sum(r=>r.MassKg.Value);
            var schedule=new SteelSchedule(rows,
                new[]{new KeyValuePair<ComponentKind,MassKg>(ComponentKind.Base,new MassKg(total))},
                new MassKg(total),"D2Over162","MASS-162-001/v1");
            string json=Path.Combine(TestWorkbookFactory.WorkspaceRoot(),"DBTool","Tests","Fixtures","s1-mono-001.json");
            string inputHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(json)));
            string workbook=TestWorkbookFactory.CreateS1FromJson();
            string sourceHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workbook)));
            return new CalculationPackage(inputHash,sourceHash,"S1-test-engine","contract-v1","S1-Analytic-1/v1",
                "S1-Analytic-1",CalculationScope.BaseRebarOnly,geometry,bars,schedule,
                CalculationStatus.NotCalculated,null,CalculationStatus.NotCalculated,null,null,[]);
        }

        private static SteelScheduleRow Row(string mark,Layer layer,Axis axis,double length,IEnumerable<string> ids)
        {
            string[] occurrences=ids.ToArray();
            int quantity=occurrences.Length;
            double unit=100d/162;
            return new SteelScheduleRow(mark,$"shape-v1|Line|{length}",ComponentKind.Base,layer,axis,10,
                quantity,length,quantity*length,unit,new MassKg(quantity*length/1000*unit),occurrences,Rule);
        }

        private static BarOccurrence Bar(string id,string component,Layer layer,Axis axis,double length,double z)
        {
            var start=new Point3Mm(0,0,z);
            var end=axis==Axis.X ? new Point3Mm(length,0,z) : new Point3Mm(0,length,z);
            var path=new BarPath([new LineSegment3Mm(start,end)],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
            return new BarOccurrence(id,null,component,ComponentKind.Base,layer,axis,10,path,length,length,
                LengthStatus.Known,Rule,[]);
        }

        private static GeometryModel Geometry(string componentLabel)
        {
            var polygon=new Polygon2Mm([new Point2Mm(-750,-875),new Point2Mm(750,-875),
                new Point2Mm(750,875),new Point2Mm(-750,875)]);
            var footprint=new Footprint(polygon,[]);
            var range=new Interval1D(-150,0);
            var frame=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(0,0,1));
            var component=new ComponentGeometry(componentLabel,ComponentKind.Base,"Concrete",footprint,range,
                new ExtrusionSolid(footprint,frame,range),[],[],[]);
            return new GeometryModel([component],[]);
        }
    }
}
