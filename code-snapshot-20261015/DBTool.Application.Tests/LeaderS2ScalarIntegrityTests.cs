using DBTool.Application.StageContractsV2;
using DBTool.Domain.StageContractsV2;
using Xunit;
namespace DBTool.Application.Tests;

// Synthetic input counterexamples only. These do not certify a complete DBTool output.
public sealed class LeaderS2ScalarIntegrityTests
{
    public static S1StateV2 Valid()
    {
        RawCellV2 Number(string key,double value)=>new(key,"GaThamLienKhoi","C4","Number",null,value.ToString("R",System.Globalization.CultureInfo.InvariantCulture),null,null,null,null,false,true,"SRC-"+key,[]);
        RawCellV2 Text(string key,string value)=>new(key,"CF.Ga","E11","Text",value,null,null,null,null,null,false,true,"SRC-"+key,[]);
        RawCellV2[] cells=[Number("Length",1379.625),Number("Width",1637.25),Number("Height",2117.75),Number("BaseThickness",173.125),Number("WallThickness",147.5),Number("RoofThickness",162.75),Number("BaseExpansion",0),Number("PadThickness",0),Number("BaseHaunch",0),Number("TopHaunch",0),Number("NeckHeight",113.25),Number("NeckThickness",87.5),Number("BaseElevationM",-1.75),Text("NeckStructure","BTCT"),Text("CoverType","None"),Text("NeckSize","613.75x587.125"),Text("NeckPosition","Giữa ga"),Text("Annotation.NoteStyle","Kiểu 1"),Text("Annotation.PitchSymbol","@"),Text("Elevation.C","Tương đối")];
        var context=new ContextV2("synthetic-run","synthetic-job","synthetic-manhole",4,"build","template","rules",false,[],[],[],Path.GetTempPath(),[],[],null);
        var job=new JobV2(context.job_id,"synthetic","scratch.xlsx",[],"workbook","GaThamLienKhoi","GaThamLienKhoi","en-US",[],null,null);
        var request=new RequestV2([job],"scratch.dwg","Create",new(0,0,0),37.5,null,"Fixed",null,"Full",100,"plot","manifest","manifest-hash","rules","rules-hash",[]);
        return new(context,request,new("template",[],[],"no-font-needed-for-scalar-probe","mm",[],[],[],[]),new("rules",[],null,null,null,null,null,null,null,null),new("workbook","GaThamLienKhoi","GaThamLienKhoi",4,[new(4,"Synthetic scalar-only input",cells)],[],[],"VHGpro3.2"));
    }
    public static S1StateV2 Cell(S1StateV2 input,string key,Func<RawCellV2,RawCellV2> edit)=>input with{raw=input.raw with{rows=[input.raw.rows[0] with{cells=input.raw.rows[0].cells.Select(c=>c.field_key==key?edit(c):c).ToArray()}]}};
    static S1StateV2 Without(S1StateV2 input,string key)=>input with{raw=input.raw with{rows=[input.raw.rows[0] with{cells=input.raw.rows[0].cells.Where(c=>c.field_key!=key).ToArray()}]}};
    public static IEnumerable<object[]> Invalid()
    {
        var v=Valid();
        yield return ["duplicate-row",v with{raw=v.raw with{rows=[v.raw.rows[0],v.raw.rows[0]]}},"ROW_AMBIGUOUS"];
        yield return ["selection-mismatch",v with{raw=v.raw with{selected_row_index=5}},"ROW_SELECTION_MISMATCH"];
        yield return ["missing-numeric-culture",v with{request=v.request with{jobs=[]}},"NUMERIC_CULTURE_UNRESOLVED"];
        yield return ["unknown-profile",v with{raw=v.raw with{profile="InventedProfile"}},"PROFILE_UNRESOLVED"];
        yield return ["null-profile",v with{raw=v.raw with{profile=null!}},"PROFILE_UNRESOLVED"];
        yield return ["duplicate-field",v with{raw=v.raw with{config=[v.raw.rows[0].cells[0]]}},"RAW_FIELD_AMBIGUOUS"];
        yield return ["bool-as-number",Cell(v,"Length",c=>c with{kind="Bool",text="1379.625",raw_number_text=null,boolean=true}),"CELL_KIND_INVALID"];
        yield return ["comma-cannot-be-thousands",Cell(v,"Length",c=>c with{raw_number_text="1,2"}),"NUMBER_FORMAT"];
        yield return ["nan",Cell(v,"Length",c=>c with{raw_number_text="NaN"}),"NUMBER_NOT_FINITE"];
        yield return ["verified-flag-is-not-proof",Cell(v,"Length",c=>c with{kind="Formula",formula="=1+1",cache_kind="Number",cache_text="2",cache_verified=true}),"FORMULA_CACHE_UNVERIFIED"];
        yield return ["missing-neck-size",Without(v,"NeckSize"),"ACTIVE_FIELD_BLANK"];
        yield return ["malformed-neck-size",Cell(v,"NeckSize",c=>c with{text="613.75xBROKEN"}),"DIMENSIONS_FORMAT"];
        yield return ["unknown-position",Cell(v,"NeckPosition",c=>c with{text="InventedPosition"}),"NECK_POSITION_UNRESOLVED"];
        yield return ["blank-cover-kind",Cell(v,"CoverType",c=>c with{text=""}),"ACTIVE_FIELD_BLANK"];
        yield return ["missing-purchased-code",Cell(v,"CoverType",c=>c with{text="Gang"}),"ACTIVE_FIELD_BLANK"];
        yield return ["oversized-neck-tuple",Cell(v,"NeckSize",c=>c with{text="99999x587.125"}),"NECK_OPENING_OUTSIDE"];
        yield return ["invalid-panel-scale",v with{request=v.request with{panel_scale=double.NaN}},"SCALE_INVALID"];
        yield return ["invalid-scale",v with{request=v.request with{body_scale=0}},"SCALE_INVALID"];
    }
    [Theory][MemberData(nameof(Invalid))]
    public void InvalidInputMustFaultBeforeInventingValues(string _,S1StateV2 input,string expected)
    {var fault=Assert.Throws<StageFault>(()=>new GeometryStageAdapter().Execute(input));Assert.Equal(expected,fault.Code);Assert.NotEmpty(fault.FieldPaths);}
    [Fact]public void TupleUsesSelectedJobCultureWithoutGroupingSeparators()
    {var v=Valid();v=v with{request=v.request with{jobs=[v.request.jobs[0] with{numeric_culture="vi-VN"}]}};v=Cell(v,"NeckSize",c=>c with{text="613,75x587,125"});Assert.Equal(613.75,new GeometryStageAdapter().Execute(v).effective.neck_clear_x_mm);}
    [Fact]public void RawNumericValueWinsOverFormattedDisplayText()
    {var v=Cell(Valid(),"Length",c=>c with{text="9999"});Assert.Equal(1379.625,new GeometryStageAdapter().Execute(v).effective.clear_x_mm);}
    [Fact]public void ExactFractionalInputAndNegativeAbsoluteElevationSurvive()
    {var v=Valid();v=v with{request=v.request with{elevation_mode_override="Absolute"}};var actual=new GeometryStageAdapter().Execute(v).effective;Assert.Equal(1379.625,actual.clear_x_mm);Assert.Equal(613.75,actual.neck_clear_x_mm);Assert.Equal(-1.75,actual.base_elevation_m);}
}
