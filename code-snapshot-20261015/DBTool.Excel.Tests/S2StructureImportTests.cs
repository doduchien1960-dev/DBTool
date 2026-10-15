using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class S2StructureImportTests
{
    [Theory]
    [InlineData("G400-1")]
    [InlineData("G500-2")]
    [InlineData("G600-3")]
    [InlineData("G800-1")]
    public void PrimaryCompositionFromSourceCopyHasEveryMemberAndScheduleBijection(string name)
    {
        string path=TestWorkbookFactory.CopySource(2,b=>DeactivateAccessories(b,name=="G400-1"));
        try
        {
            var (_,validation)=ValidateSource(path,name);
            Assert.True(validation.Success,string.Join(";",validation.Diagnostics.Select(d=>d.Code)));
            var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
            var pipeline=new CalculationPipeline(new GeometryStageAdapter(),new FullRebarStage(),new SteelScheduleStageAdapter(),new DrawingStageAdapter());
            var spec=Assert.Single(validation.Value!.SelectedManholes);
            var result=pipeline.Calculate(validation.Value,policy,new("Phase1A",[spec.Input.Id],CalculationScope.FullRebar,policy.PolicyId,true),
                new(30,spec.Input.ElevationMode,spec.Input.BaseElevationM,"Default",false),default);
            Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code+":"+d.Message+":"+d.Actual)));
            var package=result.Value!;
            if(name=="G400-1") {
                var neckOuter=package.Occurrences.Where(b=>b.FrameId=="Neck/T2" && b.Surface==RebarSurface.Outer && b.Direction==RebarDirection.Vertical).ToArray();
                Assert.NotEmpty(neckOuter);
                // Both faces terminate below the recessed support, including their cover.
                Assert.All(neckOuter,b=>{Assert.Equal(500,b.Path.Segments[1].Start.X,6);Assert.Equal(1235,b.Path.Segments[1].Start.Z,6);Assert.Equal(spec.Input.NeckSeatSpec!.SeatElevationMm-spec.Input.MainRebar!.NeckOuter!.CoverMm-b.DiameterMm/2d,b.Path.Segments[1].End.Z,6);Assert.Equal(610,b.ScheduleLengthMm,6);});
                var horizontal=package.Occurrences.Where(b=>b.FrameId=="Neck/T2" && b.Surface==RebarSurface.Outer && b.Direction==RebarDirection.Horizontal).ToArray();
                Assert.All(horizontal,b=>{Assert.Equal(490,b.Path.Segments[1].Start.X,6);Assert.Equal(-250,b.Path.Segments[1].Start.Y,6);Assert.Equal(250,b.Path.Segments[1].End.Y,6);Assert.Equal(630,b.ScheduleLengthMm,6);});
                var ladder=package.Occurrences.Where(b=>b.FamilyId=="Ladder").ToArray();
                Assert.NotEmpty(ladder);
                Assert.All(ladder,b=>{Assert.Equal(1100,b.ScheduleLengthMm);Assert.Equal(1100-4*(63-Math.PI*31.5/2),b.FabricationLengthMm!.Value,6);});
            }
            Assert.Contains(package.Occurrences,b=>b.ComponentId=="Base");
            Assert.Contains(package.Occurrences,b=>b.ComponentId=="Roof");
            Assert.Contains(package.Occurrences,b=>b.ComponentId=="Neck" && b.Direction==RebarDirection.Vertical && b.Surface==RebarSurface.Inner);
            Assert.Contains(package.Occurrences,b=>b.ComponentId=="Neck" && b.Direction==RebarDirection.Horizontal && b.Surface==RebarSurface.Outer);
            Assert.All(package.Occurrences.Where(b=>b.ComponentId=="Neck" && b.Direction==RebarDirection.Horizontal && b.Surface==RebarSurface.Outer && b.FrameId is "Neck/T2" or "Neck/T4"),
                b=>Assert.Equal(3,b.Path.Segments.Length));
            foreach(var wall in new[]{"Wall/T1","Wall/T2","Wall/T3","Wall/T4"})
                Assert.Contains(package.Occurrences,b=>b.ComponentId==wall && b.Direction==RebarDirection.Vertical && b.Surface==RebarSurface.Inner);
            Assert.Equal(package.Occurrences.Length,package.Occurrences.Select(b=>b.Id).Distinct().Count());
            var assigned=package.SteelSchedule!.Rows.SelectMany(r=>r.OccurrenceIds).ToArray();
            Assert.Equal(package.Occurrences.Select(b=>b.Id).Order(),assigned.Order());
            Assert.Equal(package.Occurrences.Length,package.SteelSchedule.Rows.Sum(r=>r.Quantity));
            Assert.All(package.Occurrences,b=>Assert.True(double.IsFinite(b.ScheduleLengthMm) && b.ScheduleLengthMm>0));
            Assert.All(package.SteelSchedule.Rows,row=>{
                Assert.Equal(row.SingleLengthMm*row.Quantity,row.TotalLengthMm,6);
                Assert.Equal(row.TotalLengthMm/1000*row.UnitMassKgPerM,row.MassKg.Value,6);
            });
            if(name=="G400-1") {
                using var output=new TemporaryOutput();
                var export=new SteelWorkbookWriter().Write(package,new(output.Directory,null,name,"phase1b"));
                Assert.True(export.Success,string.Join(";",export.Diagnostics.Select(d=>d.Code)));
                using var book=new XLWorkbook(export.Value!.Path);
                var sheet=book.Worksheet("Steel");
                var ladderMarks=package.SteelSchedule.Rows.Where(r=>r.FamilyId=="Ladder").Select(r=>r.Mark).ToHashSet();
                Assert.All(sheet.RowsUsed().Skip(1).Where(r=>ladderMarks.Contains(r.Cell(1).GetString())),r=>Assert.Equal(1100,r.Cell(8).GetDouble()));
            }
            Assert.Contains(result.Diagnostics,d=>d.Code=="HOOK_FABRICATION_FALLBACK");
            Assert.DoesNotContain(result.Diagnostics,d=>d.Severity==DiagnosticSeverity.Block);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("J", "WallOuter")]
    [InlineData("K", "WallInner")]
    [InlineData("M", "RoofTop")]
    [InlineData("N", "RoofBottom")]
    [InlineData("Y", "NeckOuter")]
    [InlineData("Z", "NeckInner")]
    public void EachPrimaryMeshChangesItsTypedLayerAndSemanticHash(string column,string member)
    {
        string path=TestWorkbookFactory.CopySource(2,b=>DeactivateAccessories(b,false));
        try {
            var (raw,before)=ValidateSource(path);
            using(var b=new XLWorkbook(path)) {
                var sheet=b.Worksheet("GaThamNapRoi");
                var row=raw.Rows.Single(r=>r.Cells["Name"].Text=="G400-1").RowIndex;
                sheet.Cell(column+row).Value="D12A125"; b.Save();
            }
            var (_,after)=ValidateSource(path);
            Assert.True(after.Success,string.Join(";",after.Diagnostics.Select(d=>d.Code)));
            Assert.NotEqual(before.Value!.InputHash,after.Value!.InputHash);
            var a=Assert.Single(before.Value.SelectedManholes).Input.MainRebar!;
            var z=Assert.Single(after.Value.SelectedManholes).Input.MainRebar!;
            Assert.Equal(member=="WallOuter"?12:10,z.WallOuter!.VerticalDiameterMm);
            Assert.Equal(member=="WallInner"?12:10,z.WallInner!.VerticalDiameterMm);
            Assert.Equal(member=="NeckOuter"?12:10,z.NeckOuter!.VerticalDiameterMm);
            Assert.Equal(member=="NeckInner"?12:10,z.NeckInner!.VerticalDiameterMm);
            Assert.Equal(member=="RoofTop"?12:10,z.RoofTop!.DiameterMm);
            Assert.Equal(member=="RoofBottom"?12:10,z.RoofBottom!.DiameterMm);
        } finally {File.Delete(path);}
    }

    [Fact]
    public void RoofCrossingRoundPipeAndRectangularNeckOpeningComposeTogether()
    {
        string path=TestWorkbookFactory.CopySource(2,b=>DeactivateAccessories(b,false));
        try {
            var (raw,_)=ValidateSource(path);
            int row=raw.Rows.Single(r=>r.Cells["Name"].Text=="G400-1").RowIndex;
            using(var b=new XLWorkbook(path)) {b.Worksheet("GaThamNapRoi").Cell("R"+row).Value=1000;b.Save();}
            var (_,validation)=ValidateSource(path);
            Assert.True(validation.Success);
            var spec=Assert.Single(validation.Value!.SelectedManholes);
            var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
            var geometry=new GeometryStageAdapter().Build(spec,policy);
            Assert.True(geometry.Success,string.Join(";",geometry.Diagnostics.Select(d=>d.Code+":"+d.Actual)));
            Assert.Equal(2,geometry.Value!.Components.Single(c=>c.Id=="Roof").Voids.Length);
            var result=new FullRebarStage().Plan(spec,geometry.Value,policy);
            Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
            Assert.Contains(result.Value!.Occurrences,b=>b.ComponentId=="Roof");
            Assert.Equal(result.Value.Occurrences.Length,result.Value.Occurrences.Select(b=>b.Id).Distinct().Count());
        } finally {File.Delete(path);}
    }

    [Theory]
    [InlineData("G400-1")]
    [InlineData("G500-2")]
    [InlineData("G600-3")]
    [InlineData("G800-1")]
    public void UntouchedSourcesRetainOnlyAccessoryCapabilityGates(string name)
    {
        string path=TestWorkbookFactory.CopySource(2);
        try {
            var (_,validation)=ValidateSource(path,name);
            Assert.True(validation.Success,string.Join(";",validation.Diagnostics.Select(d=>d.Code)));
            var spec=Assert.Single(validation.Value!.SelectedManholes);
            var support=new FeatureSupportValidator().ValidateS2Rebar(spec,CalculationScope.FullRebar,
                CalculationPolicy.Select("S2Rebar-1",true).Value!);
            Assert.False(support.Success);
            Assert.Contains(support.Diagnostics,d=>d.Severity==DiagnosticSeverity.Block);
        } finally {File.Delete(path);}
    }

    [Theory]
    [InlineData("G500-2")]
    [InlineData("G600-3")]
    [InlineData("G800-1")]
    public void LadderPlacementDoesNotSilentlyCrossSourcePipeOpenings(string name)
    {
        string path=TestWorkbookFactory.CopySource(2,b=>DeactivateAccessories(b,true));
        try {
            var (_,validation)=ValidateSource(path,name);Assert.True(validation.Success);
            var spec=Assert.Single(validation.Value!.SelectedManholes);
            var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
            var geometry=new GeometryStageAdapter().Build(spec,policy);Assert.True(geometry.Success);
            var result=new FullRebarStage().Plan(spec,geometry.Value!,policy);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,d=>d.Code=="LADDER_OPENING_COLLISION");
        } finally {File.Delete(path);}
    }

    private sealed class TemporaryOutput : IDisposable
    {
        public string Directory {get;}=Path.Combine(Path.GetTempPath(),"DBTool-Phase1B-"+Guid.NewGuid().ToString("N"));
        public TemporaryOutput()=>System.IO.Directory.CreateDirectory(Directory);
        public void Dispose() {
            foreach(var file in System.IO.Directory.GetFiles(Directory)) File.Delete(file);
            System.IO.Directory.Delete(Directory);
        }
    }

    private static void DeactivateAccessories(XLWorkbook b,bool ladder)
    {
        TestWorkbookFactory.DeclareSyntheticCover(b);
        var config=b.Worksheet("CF.Ga");
        foreach(var cell in new[]{"C9","H11"}) config.Cell(cell).Value="Không";
        config.Cell("H9").Value="0D12";config.Cell("J11").Value="0";config.Cell("H23").Value="0";
        config.Cell("D28").Value=0;config.Cell("F28").Value=0;config.Cell("C30").Value=0;config.Cell("E30").Value=0;
        if(!ladder) config.Cell("C25").Value="Không";
    }

    [Theory]
    [InlineData("G400-1")]
    [InlineData("G500-2")]
    [InlineData("G600-3")]
    [InlineData("G800-1")]
    public void ActualGangSourceUsesAuthorizedVhgMappingWithoutPassivePanels(string name)
    {
        string path=TestWorkbookFactory.CopySource(2);
        try
        {
            var (raw,result)=ValidateSource(path,name);
            Assert.Equal("D0917949603848209F5B14D3CABD3204ECAA72B9358D16FFFB0A437D9A7843F1",raw.WorkbookHash);
            Assert.Contains(raw.Rows,r=>r.Cells["Name"].Text==name);
            Assert.Equal(2,raw.ConfigCells["PanelCount"].Number);
            Assert.Equal("I13",raw.ConfigCells["PanelCount"].Source.CellAddress);
            Assert.False(raw.ConfigCells.ContainsKey("PanelSupportMode"));
            Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
            var spec=Assert.Single(result.Value!.SelectedManholes);
            Assert.NotNull(spec.Input.PurchasedCoverSpec);
            Assert.Null(spec.Input.CoverPanelSpec);
            double rise=raw.ConfigCells["CoverHeight"].Number!.Value;
            Assert.Equal(spec.Input.H+spec.Input.RoofThicknessMm+spec.Input.NeckHeight+rise,
                spec.Input.PurchasedCoverSpec!.Anchor.Z,6);
            Assert.Contains(spec.Input.PurchasedCoverSpec.Sources,s=>s.CellAddress=="D13");
            var geometry=new GeometryStageAdapter().Build(spec,CalculationPolicy.Select("S2Rebar-1",true).Value!);
            Assert.True(geometry.Success,string.Join(";",geometry.Diagnostics.Select(d=>d.Code)));
            Assert.Equal(spec.Input.PurchasedCoverSpec.Anchor.Z,Assert.Single(geometry.Value!.PurchasedCovers).Anchor.Z,6);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ActualSourceWithBtctCoverStillBlocksMissingSemanticSupport()
    {
        string path=TestWorkbookFactory.CopySource(2,b=>b.Worksheet("CF.Ga").Cell("C13").Value="BTCT");
        try
        {
            var (_,result)=ValidateSource(path);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,d=>d.Code=="PANEL_FIELDS_REQUIRED" && d.Severity==DiagnosticSeverity.Block
                && d.Source?.CellAddress=="I13");
            Assert.DoesNotContain(result.Diagnostics,d=>d.Code=="U_NECK_COVER_CATALOG");
            Assert.DoesNotContain(result.Diagnostics,d=>d.Code=="U_NECK_SOURCE_MAPPING" && d.Source?.CellAddress=="F13");
        }
        finally { File.Delete(path); }
    }

    private static (RawProjectInput Raw,OperationResult<ValidatedProjectInput> Result) ValidateSource(string path,string name="G400-1")
    {
        var raw=new WorkbookImporter().Read(new("Phase1A",path,Profile.GaThamNapRoi,"GaThamNapRoi"));
        Assert.True(raw.Success);
        var numeric=new NumericPolicy("en-US",false,false);
        var catalogs=new CatalogReader().Read(raw.Value!,numeric);
        Assert.True(catalogs.Success,string.Join(";",catalogs.Diagnostics.Select(d=>d.Code)));
        var ids=new[]{ManholeIdentity.Create("Phase1A",raw.Value!.Profile,name)};
        var result=new ManholeValidator(new NumericCellReader(),numeric).Validate(raw.Value,catalogs.Value!,
            new("Phase1A",ids,CalculationScope.FullRebar,"S2Rebar-1",true),CalculationPolicy.Select("S2Rebar-1",true).Value!);
        return (raw.Value,result);
    }

    [Fact]
    public void VersionedPhysicalCatalogFlowsThroughRealImporterAndAffectsSemanticHash()
    {
        string path=TestWorkbookFactory.CopySource(2,b=>
        {
            // Isolate catalog normalization. This is an explicit software fixture, not approval
            // of the unresolved production seat, units or collar from the untouched source.
            b.Worksheet("CF.Ga").Cell("F11").Value="Không";
            b.Worksheet("CF.Ga").Cell("F13").Value="Không";
            // A square software aperture isolates rotation/hash behavior from rectangular compatibility.
            b.Worksheet("CF.Ga").Cell("E11").Value="700x700";
            b.Worksheet("CF.Ga").Cell("E13").Value="TEST.CATALOG";
            b.AddWorksheet("DBTool.Schema").Cell("B1").Value="DBTool-Input-1A-v1";
            var c=b.AddWorksheet("DBTool.Covers");
            string[] values={"TEST.CATALOG","1050","745","0","0","0","90","NeckTop","mm",
                "-525","-372.5","525","372.5","-350","-350","350","350","20"};
            for(int n=0;n<values.Length;n++) c.Cell(4,n+1).Value=values[n];
        });
        try
        {
            var (raw,a)=ValidateSource(path);
            Assert.True(a.Success,string.Join(";",a.Diagnostics.Select(d=>d.Code)));
            foreach(var name in new[]{"G400-1","G500-2","G600-3","G800-1"})
            {
                var (_,perGa)=ValidateSource(path,name);
                Assert.True(perGa.Success,string.Join(";",perGa.Diagnostics.Select(d=>d.Code)));
                var s=Assert.Single(perGa.Value!.SelectedManholes);
                Assert.Null(s.Input.CoverPanelSpec);
                Assert.NotNull(s.Input.PurchasedCoverSpec);
                Assert.Equal(2,s.Input.UnsupportedRawFields["Config.PanelCount"].Number);
                Assert.Equal("I13",s.Input.UnsupportedRawFields["Config.PanelCount"].Source.CellAddress);
                Assert.Contains(s.Input.PurchasedCoverSpec!.Sources,x=>x.CellAddress=="E13");
                Assert.Contains(s.Input.PurchasedCoverSpec.Sources,x=>x.CellAddress=="E11");
                Assert.Contains(s.Input.PurchasedCoverSpec.Sources,x=>x.CellAddress=="W"+raw.Rows.Single(r=>r.Cells["Name"].Text==name).RowIndex);
                Assert.Equal(90,s.Input.PurchasedCoverSpec.RotationDegrees);
                Assert.Contains(s.Catalogs.Covers[s.Input.PurchasedCoverSpec.CatalogId].Sources,x=>x.SheetName=="DBTool.Covers");
            }
            using(var book=new XLWorkbook(path)) { book.Worksheet("DBTool.Covers").Cell("G4").Value=0;book.Save(); }
            var (_,b)=ValidateSource(path);
            Assert.True(b.Success);
            Assert.NotEqual(a.Value!.InputHash,b.Value!.InputHash);
            using(var book=new XLWorkbook(path)) { book.Worksheet("CF.Ga").Cell("I13").Value=99;book.Save(); }
            var (_,passive)=ValidateSource(path);
            Assert.True(passive.Success);
            Assert.Equal(b.Value.InputHash,passive.Value!.InputHash);
            Assert.NotEqual(b.Value.SourceWorkbookHash,passive.Value.SourceWorkbookHash);
            var featureGate=new FeatureSupportValidator().ValidateS2Rebar(passive.Value.SelectedManholes[0],CalculationScope.FullRebar,
                CalculationPolicy.Select("S2Rebar-1",true).Value!);
            Assert.False(featureGate.Success);
            Assert.Contains(featureGate.Diagnostics,d=>d.Code=="FEATURE_UNSUPPORTED" && d.Severity==DiagnosticSeverity.Block);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PhysicalCatalogWithoutSchemaDeclarationIsBlockedRatherThanSilentlyIgnored()
    {
        string path=TestWorkbookFactory.CopySource(2,b=>b.AddWorksheet("DBTool.Covers").Cell("A4").Value="VIN.TNM.LD.820x570");
        try
        {
            var raw=new WorkbookImporter().Read(new("Phase1A",path,Profile.GaThamNapRoi,"GaThamNapRoi"));
            Assert.False(raw.Success);
            Assert.Contains(raw.Diagnostics,d=>d.Code=="WORKBOOK_SCHEMA_REQUIRED" && d.Severity==DiagnosticSeverity.Block);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void VersionedPanelSupportIsSemanticAndUnversionedZ13IsNotAHiddenDefault()
    {
        string path=TestWorkbookFactory.CopySource(2,b=>
        {
            b.Worksheet("CF.Ga").Cell("C13").Value="BTCT";
            b.Worksheet("CF.Ga").Cell("F11").Value="Không";
            b.Worksheet("CF.Ga").Cell("Z13").Value="OverOuter";
        });
        try
        {
            var (_,legacy)=ValidateSource(path);
            Assert.False(legacy.Success);
            Assert.Contains(legacy.Diagnostics,d=>d.Code=="PANEL_FIELDS_REQUIRED");
            using(var book=new XLWorkbook(path)) { book.AddWorksheet("DBTool.Schema").Cell("B1").Value="DBTool-Input-1A-v1";book.Save(); }
            var (_,versioned)=ValidateSource(path);
            Assert.True(versioned.Success,string.Join(";",versioned.Diagnostics.Select(d=>d.Code)));
            var panel=Assert.Single(versioned.Value!.SelectedManholes).Input.CoverPanelSpec!;
            Assert.Equal(PanelSupportMode.OverOuter,panel.SupportMode);
            Assert.Contains(panel.Sources,s=>s.CellAddress=="Z13");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void KnownHaunchCellIsTypedWithRealCellSourceAndConsumedByGeometry()
    {
        var path=TestWorkbookFactory.CreateS1FromJson();
        try
        {
            using(var workbook=new XLWorkbook(path))
            {
                workbook.Worksheet("CF.Ga").Cell("D5").Value=100;
                workbook.Save();
            }
            var rawResult=new WorkbookImporter().Read(new WorkbookReadRequest("S1",path,Profile.GaThamLienKhoi,"GaThamLienKhoi"));
            Assert.True(rawResult.Success);
            var raw=rawResult.Value!;
            Assert.Equal("D5",raw.ConfigCells["BaseHaunch"].Source.CellAddress);
            var numeric=new NumericPolicy("en-US",false,false);
            var catalogs=new CatalogReader().Read(raw,numeric);
            Assert.True(catalogs.Success);
            var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
            var id=ManholeIdentity.Create(raw.ProjectId,raw.Profile,raw.Rows[0].Cells["Name"].Text!);
            var validated=new ManholeValidator(new NumericCellReader(),numeric).Validate(raw,catalogs.Value!,
                new CalculationRequest("S1",[id],CalculationScope.GeometryOnly,"S2Geometry-1",true),policy);
            Assert.True(validated.Success,string.Join(";",validated.Diagnostics.Select(d=>d.Code)));
            var spec=Assert.Single(validated.Value!.SelectedManholes);
            Assert.Equal(100,spec.BaseHaunchSpec!.WidthMm);
            Assert.Equal("D5",Assert.Single(spec.BaseHaunchSpec.Sources).CellAddress);
            var model=ChamberGeometryBuilder.Build(spec,policy);
            Assert.True(model.Success,string.Join(";",model.Diagnostics.Select(d=>d.Code)));
            Assert.Contains(model.Value!.Components,c=>c.Id=="Base/Haunch");
        }
        finally { File.Delete(path); }
    }
}
