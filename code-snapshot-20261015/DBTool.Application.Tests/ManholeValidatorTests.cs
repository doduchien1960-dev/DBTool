using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;
namespace DBTool.Application.Tests;

public class ManholeValidatorTests
{
    internal const string Id="S1/GaThamLienKhoi/S1-MONO-001";
    internal static RawCell Cell(object? value,string address,string sheet="GaThamLienKhoi")
    {
        // Explicit synthetic workbook-cell metadata; never evidence of a real source workbook.
        var source=new SourceRef(SourceKind.WorkbookCell,"synthetic-test.xlsx",new string('a',64),sheet,address,null,value?.ToString()??"");
        return value switch { null=>new(RawCellKind.Blank,null,null,null,null,null,source), double n=>new(RawCellKind.Number,null,n,null,null,null,source), int n=>new(RawCellKind.Number,null,n,null,null,null,source), _=>new(RawCellKind.Text,value.ToString(),null,null,null,null,source) };
    }
    internal static RawProjectInput Raw(string? field=null,object? value=null,bool config=false,IEnumerable<Diagnostic>? diagnostics=null)
    {
        var cells=new Dictionary<string,RawCell>();
        foreach(var (key,address,v) in new (string,string,object?)[]{("Name","B4","S1-MONO-001"),("Length","C4",1000),("Width","D4",1250),("Height","E4",1400),("BaseThickness","F4",150),("BaseBottomMesh","G4","D10A150"),("BaseTopMesh","H4","D10A150"),("WallThickness","I4",150),("RoofThickness","L4",150),("NeckHeight","W4",0),("BaseElevationM","AA4",10),("PipeCodeT1","O4","D400"),("InvertT1","P4",300),("PipeCodeT2","Q4",null),("InvertT2","R4",null),("PipeCodeT3","S4",null),("InvertT3","T4",null),("PipeCodeT4","U4",null),("InvertT4","V4",null)}) cells[key]=Cell(v,address);
        var cfg=new Dictionary<string,RawCell>();
        foreach(var (key,address,v) in new (string,string,object?)[]{("PadExpansion","C3",100),("PadThickness","D3",100),("BaseExpansion","C5",100),("BaseHaunch","D5",0),("TopHaunch","C9",0),("NeckSize","E11","700x700"),("NeckPosition","D11","Giữa ga"),("CoverBottom","C17",30),("Distribution","K19","Chia đều"),("HookPattern.C","C20","0M"),("HookPattern.D","D20","0M"),("PipeMortar","C28",50),("PipeOffsetT1","I28",0),("PipeOffsetT2","J28",0),("PipeOffsetT3","K28",0),("PipeOffsetT4","L28",0),("Elevation.C","C32","Tuyệt đối")}) cfg[key]=Cell(v,address,"CF.Ga");
        if(field!=null) { var map=config?cfg:cells; map[field]=Cell(value,map[field].Source.CellAddress!,map[field].Source.SheetName!); }
        return new("S1",new string('a',64),Profile.GaThamLienKhoi,"GaThamLienKhoi",[new(4,cells)],cfg,new Dictionary<string,IEnumerable<RawManholeRow>>(),diagnostics??[]);
    }
    internal static CatalogSet Catalogs()=>new(new Dictionary<string,PipeCatalogEntry>{{"D400",new("D400",PipeKind.Round,400,null,null,55,null,0)},{"D200",new("D200",PipeKind.Round,200,null,null,30,null,0)}},new Dictionary<string,SteelAngleEntry>(),new Dictionary<string,HatchEntry>(),new Dictionary<string,string>());
    internal static OperationResult<ValidatedProjectInput> Validate(RawProjectInput raw)=>new ManholeValidator(new NumericCellReader(),new NumericPolicy("en-US",false,false)).Validate(raw,Catalogs(),new("S1",[Id],CalculationScope.BaseRebarOnly,"S1-Analytic-1",true),CalculationPolicy.Select("S1-Analytic-1",true).Value!);
    [Fact] public void S1IsOneValidatedSnapshot()
    {
        var result=Validate(Raw()); Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var spec=Assert.Single(result.Value!.SelectedManholes); Assert.Equal(Id,spec.Input.Id); Assert.Equal(1000,spec.Input.L); Assert.Equal(300,Assert.Single(spec.Input.PipeConnections).InvertMm);
        Assert.Equal(new string('A',64),result.Value.SourceWorkbookHash);
    }
    [Theory]
    [InlineData("BaseBottomMesh",null,"REBAR_REQUIRED","G4")]
    [InlineData("InvertT1",null,"PIPE_INVERT_REQUIRED","P4")]
    [InlineData("PipeCodeT1",null,"PIPE_CODE_REQUIRED","O4")]
    [InlineData("BaseElevationM",null,"ELEVATION_REQUIRED","AA4")]
    [InlineData("Length",0,"DIMENSION_NONPOSITIVE","C4")]
    [InlineData("Length","NaN","NUMBER_NOT_FINITE","C4")]
    [InlineData("BaseThickness",90,"BASE_LAYER_COLLISION","F4")]
    public void InvalidInputNeverProducesSnapshot(string field,object? value,string code,string address)
    {
        var result=Validate(Raw(field,value)); Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code==code && d.Source?.CellAddress==address);
    }
    [Fact] public void ZeroInvertIsNotMissing()
    {
        var result=Validate(Raw("InvertT1",0)); Assert.True(result.Success); Assert.DoesNotContain(result.Diagnostics,d=>d.Code=="PIPE_INVERT_REQUIRED");
    }
    [Fact] public void D200WithBlankP4ReportsP4()
    {
        var raw=Raw("InvertT1",null);
        var row=new RawManholeRow(4,raw.Rows[0].Cells.SetItem("PipeCodeT1",Cell("D200","O4")));
        var result=Validate(new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[row],raw.ConfigCells,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]));
        Assert.Null(result.Value); Assert.Contains(result.Diagnostics,d=>d.Code=="PIPE_INVERT_REQUIRED" && d.Source?.CellAddress=="P4");
    }
    [Fact] public void NegativeAbsoluteElevationIsValid()=>Assert.Equal(-1.25,Assert.Single(Validate(Raw("BaseElevationM",-1.25)).Value!.SelectedManholes).Input.BaseElevationM!.Value.Value);
    [Fact] public void SymbolicBlankRemainsNull()
    {
        var raw=Raw("BaseElevationM",null); var cfg=raw.ConfigCells.SetItem("Elevation.C",Cell("Ký hiệu","C32","CF.Ga"));
        var result=Validate(new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,raw.Rows,cfg,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]));
        Assert.True(result.Success); Assert.Null(result.Value!.SelectedManholes[0].Input.BaseElevationM);
    }
    [Fact] public void RoofOpeningMustFit()=>Assert.Contains(Validate(Raw("NeckSize","1100x700",true)).Diagnostics,d=>d.Code=="ROOF_OPENING_OUTSIDE");
    [Fact] public void HashTracksCalculationNotWorkbookBytesOrRawNumberFormatting()
    {
        var first=Validate(Raw()).Value!; var raw=Raw("Length","1000.0");
        var second=Validate(new(raw.ProjectId,new string('b',64),raw.Profile,raw.MainSheet,raw.Rows,raw.ConfigCells,new Dictionary<string,IEnumerable<RawManholeRow>>(),[])).Value!;
        Assert.Equal(first.InputHash,second.InputHash); Assert.NotEqual(first.SourceWorkbookHash,second.SourceWorkbookHash);
        Assert.NotEqual(first.InputHash,Validate(Raw("Length",1100)).Value!.InputHash);
    }
    [Fact] public void DuplicateCanonicalNamesBlockEvenWhenCaseAndWhitespaceDiffer()
    {
        var raw=Raw(); var duplicate=new RawManholeRow(5,raw.Rows[0].Cells.SetItem("Name",Cell(" s1-mono-001 ","B5")));
        var result=Validate(new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[raw.Rows[0],duplicate],raw.ConfigCells,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]));
        Assert.False(result.Success); Assert.Contains(result.Diagnostics,d=>d.Code=="MANHOLE_ID_DUPLICATE");
    }
    [Fact] public void OtherManholeBlockDoesNotPoisonSubsetButGlobalAlwaysDoes()
    {
        var other=new Diagnostic("OTHER",DiagnosticSeverity.Block,DiagnosticScope.Manhole,"S1/GaThamLienKhoi/B",null,null,null,null,"other");
        Assert.True(Validate(Raw(diagnostics:[other])).Success);
        var global=new Diagnostic("CONFIG",DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,Cell("bad","C3","CF.Ga").Source,null,null,"global");
        Assert.False(Validate(Raw(diagnostics:[global])).Success);
        Assert.True(Validate(Raw(diagnostics:[new("REVIEW",DiagnosticSeverity.Review,DiagnosticScope.Global,null,null,null,null,null,"review")])).Success);
    }
    [Theory]
    [InlineData("PadExpansion",-1)] [InlineData("CoverBottom",-1)] [InlineData("PipeOffsetT1","Infinity")]
    public void InvalidConfigCannotBuildSnapshot(string field,object value)=>Assert.False(Validate(Raw(field,value,true)).Success);
    [Fact] public void OptionalBlankSteelOutsideScopeIsNotRequired()
    {
        var raw=Raw(); var row=new RawManholeRow(4,raw.Rows[0].Cells.SetItem("WallOuterMesh",Cell(null,"J4")));
        Assert.True(Validate(new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[row],raw.ConfigCells,new Dictionary<string,IEnumerable<RawManholeRow>>(),[])).Success);
    }
    [Fact] public void PresentationOnlyConfigDoesNotChangeInputHash()
    {
        var raw=Raw();
        var cfg=raw.ConfigCells.SetItem("DrawingScale",Cell(30,"Z1","CF.Ga")).SetItem("Elevation.D",Cell("Z_BASE","D32","CF.Ga"));
        var second=new RawProjectInput(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,raw.Rows,cfg,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        Assert.Equal(Validate(raw).Value!.InputHash,Validate(second).Value!.InputHash);
    }
    [Fact] public void DisabledBooleanFeatureRemainsDisabled()
    {
        var raw=Raw(); var source=Cell(false,"C9","CF.Ga").Source;
        var cfg=raw.ConfigCells.SetItem("HasDowel",new RawCell(RawCellKind.Boolean,null,null,false,null,null,source));
        var result=Validate(new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,raw.Rows,cfg,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]));
        Assert.True(result.Success);
        Assert.False(Assert.Single(result.Value!.SelectedManholes[0].Input.FeatureRequests).Requested);
    }
    [Theory]
    [InlineData("WallOuterMesh","D10A150"," d10a150 ",false)]
    [InlineData("PanelCount",0,"0",true)]
    public void UnconsumedOptionalSpellingDoesNotChangeCalculationIdentity(string field,object first,object second,bool config)
    {
        RawProjectInput Change(object value)
        {
            var raw=Raw();
            var cfg=config?raw.ConfigCells.SetItem(field,Cell(value,"I13","CF.Ga")):raw.ConfigCells;
            var rows=config?raw.Rows:new[]{new RawManholeRow(4,raw.Rows[0].Cells.SetItem(field,Cell(value,"J4")))}.ToImmutableArray();
            return new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,rows,cfg,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        }
        Assert.Equal(Validate(Change(first)).Value!.InputHash,Validate(Change(second)).Value!.InputHash);
    }
}
