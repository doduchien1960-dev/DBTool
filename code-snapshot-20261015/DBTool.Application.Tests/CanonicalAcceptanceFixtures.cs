using System.Text.Json;
using DBTool.Drawing;

namespace DBTool.Application.Tests;

/// <summary>Synthetic only. All numeric/source choices below belong to this evaluator fixture.</summary>
internal static class CanonicalAcceptanceFixtures
{
    public static (string Contract,string Snapshot) Circle()
    {
        var hash=new string('B',64);
        var frame=new{id="f",origin_xyz_mm=new[]{0d,0,0},basis_u_xyz=new[]{1d,0,0},basis_v_xyz=new[]{0d,1,0},normal_xyz=new[]{0d,0,1},source_ids=new[]{"test"}};
        var kinds=new Dictionary<string,string>{{"a","Circle"}};
        var contract=JsonSerializer.Serialize(new {
            schema_version=CanonicalPhysicalValidator.ContractVersion,required_kinds_by_id=kinds,allow_extra_entities=false,
            sources=new[]{new{id="test",sha256=hash}},frames=new[]{frame},
            unit_bindings=new[]{new{source_dwg_sha256=hash,coordinate_unit="mm",scale_to_mm=1d,source_ids=new[]{"test"}}},
            tolerances=new{coordinate_mm=1e-6,angle_rad=1e-8,dimensionless=1e-8,source_ids=new[]{"test"}},
            entity_bindings=new Dictionary<string,object>{{"a",new{space="World",frame_id="f",source_ids=new[]{"test"}}}},encoding_rules=System.Array.Empty<object>()
        });
        var snapshot=JsonSerializer.Serialize(new {
            schema_version=CanonicalPhysicalValidator.SchemaVersion,run_id="synthetic-r04",units="mm",source_dwg_sha256=hash,expected_contract_sha256=CanonicalPhysicalValidator.Hash(contract),frames=new[]{frame},
            coverage=new{required_ids=new[]{"a"},required_kinds_by_id=kinds},
            entities=new[]{new{semantic_id="a",native_kind="Circle",kind="Circle",space="World",frame_id="f",source_ids=new[]{"test"},
                physical_payload=new{center=new{xyz_mm=new[]{0d,0,0}},normal=new{xyz=new[]{0d,0,1}},radius_mm=5d},placement=new{transform=new[]{1d,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1}},raw_readback=new{},field_mapping=new{}}}
        });
        return(contract,snapshot);
    }
}
