# PER-FINDING — CODE LIVE SAU ĐƯỜNG1

Closed ở đây là kết luận kỹ thuật của leader theo predicate đã thực thi; không là chữ ký GO. Các native evidence R03 được ghi rõ là kế thừa, không gọi là host đã chạy lại trong R04.

| ID | Gói | R03 | R04live | Bằng chứng ngắn |
| --- | --- | --- | --- | --- |
| R01 | W01 | StillOpen | StillOpen | Thiếu schema/payload hoặc đổi góc arc vẫn có thể NoDifference. |
| NEW-A03 | W01 | StillOpen | StillOpen | Chữ Paper dịch 10 mm vẫn NumericalMismatch; World→Paper còn LayoutOnly. |
| G08 | W02 | StillOpen | StillOpen | Font/glyph/layer metrics còn giả hoặc thiếu; source-byte hash đã sửa, không gọi phần này còn sai. |
| G09 | W02 | StillOpen | StillOpen | Role/tag local đã kiểm; native round-trip đủ active feature/save-reopen chưa chạy. |
| G10 | W02 | StillOpen | StillOpen | Input pad=0 nhận được nhưng legacy consumer vẫn ném Extrusion positive-depth. |
| R02 | W02 | StillOpen | StillOpen | Chuỗi amendment mới có hash thực; lịch sử W06/W07 chưa chứng minh. |
| NEW-A01 | W02 | StillOpen | Closed | Full hash, đủ 11 field conflict guard, nguồn bất biến, JSON giữ hợp đồng. |
| NEW-A02 | W02 | Closed | Closed | Build code live với SDK AutoCAD 2026: 0 error. |
| D08 | W03 | StillOpen | StillOpen | Trừ void lồng ra 8000 thay 8400; donut+island ra 8400 thay 8800. |
| D11 | W03 | StillOpen | Closed | Rejected/blank/source/host lỗi đều Block; alias/role order/coverage được giữ. |
| AC-N03 | W03 | Closed | Closed | Equal/engulfed Difference:0 loop, 0 mm². |
| AC-N04 | W03 | Closed | Closed | PublicEmit donut:8400 mm², 2 loops, 1 Inner. |
| AC-N05 | W03 | Closed | Closed | Half-disk:50π mm², giữ analytic arc. |
| NEW-R03-W01-COMPAT | W01 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W02-N01 | W02 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W02-N02 | W02 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W02-N03 | W02 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W03-N01 | W03 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W03-N02 | W03 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W03-N03 | W03 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W03-N04 | W03 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W03-N05 | W03 | NewIssue | StillOpen | Xem chi tiết và raw evidence bên dưới. |
| R03-W03-N06 | W03 | NewIssue | Closed | Xem chi tiết và raw evidence bên dưới. |
## R01 — StillOpen

- Expected: Reject absent/null schema and unversioned arrays; require the entire per-kind geometry; reject unknown unconsumed geometry fields. Direct native consumer must enforce the same validity/coverage gate.
- Actual: Absent/null schema and array roots with full line geometry return NoDifference=true. A single Start.X, LengthMm-only line, negative-radius circle, arc without angles, invented VertexBogus, Space-only block and Layer-only hatch also return NoDifference=true. Accepted direct Arc EndAngle changes π/2→π are discarded and report NoDifference=true. Native consumer directly calls Compare; empty snapshot pair still returns NoDifference=true.
- Unit/tolerance: schema presence, entity coverage; mm for endpoints/radius; rad for arc angles; Required-field/schema/unknown-field checks exact; arc EndAngle delta 1.5707963267948966 rad must survive parsing. Coordinate control tolerance 1e-6 mm; raw-mass tolerance 1e-9 kg.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W01\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W01", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W01\\results.json", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-batch-A-round2-review-03\\frozen\\source\\DBTool\\TestOutput\\Session-09\\host-20260930T075502510\\actual-semantic.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W01/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W01/results.json>).

Code: [SemanticDiff.cs:96](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:96>); [SemanticDiff.cs:309](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:309>); [SemanticDiff.cs:315](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:315>); [SemanticDiff.cs:332](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:332>); [SemanticDiff.cs:241](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:241>); [SemanticDiff.cs:525](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:525>); [DrawingRegressionCommand.cs:62](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/DrawingRegressionCommand.cs:62>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| R03-schema-absent-complete-line | "Diagnostic rejection before NoDifference" | {"NoDifference": true, "IsLayoutOnly": false, "missing": 0, "unexpected": 0, "changed": []} | schema/geometry; exact required field presence |
| R03-schema-null-complete-line | "Diagnostic rejection before NoDifference" | {"NoDifference": true, "IsLayoutOnly": false, "missing": 0, "unexpected": 0, "changed": []} | schema/geometry; exact required field presence |
| R03-arc-direct-angle-delta | "Changed single entity; Nature=NumericalMismatch" | {"NoDifference": true, "IsLayoutOnly": false, "missing": 0, "unexpected": 0, "changed": []} | rad; delta=1.5707963267948966 rad; exact geometry field presence |
| R03-direct-native-consumer-empty | "Reject empty snapshots before NoDifference" | {"NoDifference": true, "IsLayoutOnly": false, "missing": 0, "unexpected": 0, "changed": []} | entity coverage; at least one physical entity |

## NEW-A03 — StillOpen

- Expected: Pure translation of a Paper text label within its unchanged sheet frame is LayoutOnly. A World→Paper coordinate-space identity change is not harmless layout.
- Actual: Numeric/string SheetPlacement controls now correctly classify LayoutOnly. But a Text entity with Space=Paper, Frame=sheet-1, unchanged text and height and Point.X 10→20 is NumericalMismatch because Point.* wins before checking Space. Conversely changing only Space World→Paper is LayoutOnly because either side being Paper is sufficient.
- Unit/tolerance: paper mm for label position; coordinate-space identity; Label translation=10 paper mm; coordinate tolerance=1e-6 mm. World/Paper identity comparison exact.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W01\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W01", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W01\\results.json", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-batch-A-round2-review-03\\frozen\\source\\DBTool\\TestOutput\\Session-09\\host-20260930T075502510\\actual-semantic.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W01/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W01/results.json>).

Code: [SemanticDiff.cs:396](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:396>); [SemanticDiff.cs:407](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:407>); [SemanticDiff.cs:409](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:409>); [SemanticDiff.cs:414](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:414>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| R03-declared-paper-point-translation | "Changed single entity; Nature=LayoutOnly" | {"NoDifference": false, "IsLayoutOnly": false, "missing": 0, "unexpected": 0, "changed": [{"SemanticId": "sheet-label", "nature": "NumericalMismatch", "fields": [{"Field": "Point.X", "Expected": "10", "Actual": "20", "Tolerance": 1e-06}]}]} | paper mm; tịnh tiến thuần=10 mm; coordinate=0.000001 mm |
| R03-declared-world-to-paper-space | "Changed single entity; Nature=AttributeDifference" | {"NoDifference": false, "IsLayoutOnly": true, "missing": 0, "unexpected": 0, "changed": [{"SemanticId": "line-1", "nature": "LayoutOnly", "fields": [{"Field": "Space", "Expected": "World", "Actual": "Paper", "Tolerance": null}]}]} | coordinate space; exact frame/space identity |

## G08 — StillOpen

- Expected: Exact native metrics; missing required data blocks; true font-file SHA256/native glyph ink and advance; native INSERT dependencies; stroke from native lineweight/approved plot policy; sourced aperture/contact/approval; descriptor SHA256(source bytes).
- Actual: Freshlive: missing/badRotation defaults0; missingmodes default; NaNheight accepted; nonexistentfonts still generatefake210glyphs; Awidth56 for bothXScale0.8/0.4; LineWeight050 gives0.25paper mm. L04corrected CadHandle.sha256 toexactDWGbytes; no longer report theoldsignaturehashdefect ascurrent. Productinventory lacks measuredaperture/contact/AA/BB approvalbinding; source-required flag is not itselfapproval.
- Unit/tolerance: native mm; paper mm; rad; dimensionless; source identity/SHA256; Exact transfer/hash/role/approval invariants; no invented engineering acceptance tolerance. Native measurement tolerance and provider certificate absent.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W02", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/results.json>).

Code: [TemplateContractValidator.cs:384](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:384>); [TemplateContractValidator.cs:394](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:394>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| G08-missing-Rotation | "Block inspected missing metric" | {"tag": "TONG", "default_text": "123", "style_id": "VHG-Unicode.Regular", "height_mm": 200, "width_factor": 0.8, "insertion": {"x_mm": 30, "y_mm": 40, "z_mm": 0}, "alignment": {"x_mm": 50, "y_mm": 60, "z_mm": 0}, "rotation_rad": 0, "horizontal_mode": "TextCenter", "vertical_mode": "TextVerticalMid"} | mm; rad; categorical; exact transfer; no guessed engineering tolerance |
| G08-malformed-rotation | "Block malformed inspected rotation" | 0 | rad; exact transfer; no guessed engineering tolerance |
| G08-nonfinite-height | "Block nonfinite native height" | true | mm; exact transfer; no guessed engineering tolerance |
| G08-layer-LineWeight050 | "Paper stroke reflects actual layer lineweight or explicit sourced plot rule" | {"layer_id": "VHG-Thep", "semantic_role": "VHG-Thep", "stroke_paper_mm": 0.25, "source_ids": ["SRC-LYR-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA-VHG-Thep"]} | paper mm; exact transfer; no guessed engineering tolerance |
| W02-typography-no-font-I/O | "Block nonexistent font; native measured glyphs must depend on resolved font bytes and XScale" | {"glyphCount": 210, "a08": {"style_id": "VHG-Unicode.Regular", "codepoint": 65, "advance_native_mm": 56, "ink": {"min_x_mm": 0, "min_y_mm": 0, "max_x_mm": 56, "max_y_mm": 100}, "font_file_hash": "23AD78C242282ACCED6A0E0DFAD0CEAA5D0411E658BB5C52139E4F8331F8E5EA", "reference_height_mm": 100}, "a04": {"style_id": "VHG-Unicode.Regular", "codepoint": 65, "advance_native_mm": 56, "ink": {"min_x_mm": 0, "min_y_mm": 0, "max_x_mm": 56, "max_y_mm": 100}, "font_file_hash": "CCEE0DF392799AEB04007D83A3FC6CB8F851FFB79E0A6AB4B677E26B5E3FBBE4", "reference_height_mm": 100}, "sameMetric": true} | native mm; SHA256; exact transfer; no guessed engineering tolerance |
| W02-font-hash | "font_hash must identify actual font-file bytes" | {"styleSignatureHash": "E76468BDDE503D834B5431DA722931D35C59856E5A79F2621EA93EFA8F0B4EA6", "font_hash": "E76468BDDE503D834B5431DA722931D35C59856E5A79F2621EA93EFA8F0B4EA6"} | SHA256; exact file identity |
| G08-source-hash | "CadHandle descriptor bound to the declared exact DWG bytes; signature hash retained separately" | {"sourceDwgHash": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "signatureHash": "440EB5951F1507EFE3BA825BD65F01E64BD0AB11C58458E0F782E6C052858D7E", "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "relative_path": "VHGpro 3.2/Template/VHG-Template.dwg", "bundleSource": {"id": "SRC-DWG-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "kind": "Dwg", "relative_path": "template.dwg", "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "sheet": null, "cell": null, "page": null, "handle": null, "raw_text": "Template DWG bundle", "parent_ids": [], "formula_id": null}} | SHA256/path; exact identity |
| G08-product-existence-is-approval | "Missing measured aperture/contact/AA/BB and approval source must Block; outside bbox is not aperture" | {"id": "CoverC", "code": "CoverC", "plan_asset": "CoverC.MB", "section_aa_asset": "", "section_bb_asset": "", "aperture_x_mm": 1000, "aperture_y_mm": 800, "rotation_rad": 0, "aperture": [], "contact_equations": [], "approved": true, "source_ids": ["SRC-DWG-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"], "placement_surface_id": "CoverSupport", "registration_offset": {"x_mm": 0, "y_mm": 0, "z_mm": 0}} | mm; approval; exact role/approval/source contract |

## G09 — StillOpen

- Expected: Original template passes reader→full active-role validator→converter/importer with exact tags and dependencies; delete each active resource/tag and obtain the correct scoped diagnostic.
- Actual: ExactCUATHU1/CUATHU2/feature-scope localchecks rerun. Fullactivefeature native importedtargetsave/reopen notexecuted afterpatch; G09 remainsStillOpen.
- Unit/tolerance: role/tag/diagnostic identity; native mm/rad; Exact native schema and active feature scope; numeric tolerance must come from source.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W02", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/results.json>).

Code: [VhgTemplateRoles.cs:31](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/VhgTemplateRoles.cs:31>); [TemplateManifestReader.cs:205](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:205>); [W02NativeRemediationCommands.cs:41](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/W02NativeRemediationCommands.cs:41>); [W02NativeRemediationCommands.cs:124](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/W02NativeRemediationCommands.cs:124>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| G09-exact-tag-local | "CUATHU1/CUATHU2 pass; TONG rejects; scoped missing VHG-HoThu diagnosed" | {"exactSuccess": true, "oldTongSuccess": false, "oldTongCodes": ["TEMPLATE_TAG_MISSING"], "activeCatchCodes": ["TEMPLATE_ASSET_MISSING"], "baselineRequiresCatchOrDowel": false} | schema; exact role/tag diagnostic |

## G10 — StillOpen

- Expected: Changing only PadThickness150→0 accepts input, removes Pad/PadBottom/hatch and preserves base datum; negatives reject by field.
- Actual: 0mm and150mm accepted; -10mm throwsArgumentOutOfRangeException(padThicknessMm). Public GeometryStageAdapter.Build pad=0 still throwsArgumentException:Extrusion must have positive depth from unconditional Pad slab atChamberGeometryBuilder44. No fresh native hatch evidence in this review.
- Unit/tolerance: mm; component/datum/hatch counts; Exact nonnegative invariant; exact zero pad feature counts and unchanged base datum.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W02", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/results.json>).

Code: [ManholeInput.cs:18](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/Input/ManholeInput.cs:18>); [ChamberGeometryBuilder.cs:44](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/Geometry/ChamberGeometryBuilder.cs:44>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| G10-domain-invariant | "0/150 accepted; negative rejected" | {"zero": 0, "positive": 150, "negative": "ArgumentOutOfRangeException: Specified argument was out of the range of valid values. (Parameter 'padThicknessMm')"} | mm; nonnegative invariant exact |
| G10-legacy-build | "PadOFF creates no Pad component and no exception" | "ArgumentException: Extrusion must have positive depth." | mm / component count; pad=0 exact; no native hatch claim |

## R02 — StillOpen

- Expected: Preserved immutable receipts; exact before/after byte snapshots and evidence-backed actor/time for all historic transitions; no fabricated reconstruction of missing chronology.
- Actual: Trailing whitespace only; current receipt amendments cannot establish lost historic actor/time
- Unit/tolerance: SHA256/source bytes/history; Exact bytes/hash; trailing whitespace accepted only as documented non-code delta. No timeline inferred from file timestamp or current dependency hash.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["C:\\Users\\DucBao\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\publish-route1-receipts.py"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/ledger-current-reconciliation.json>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/ledger-current-reconciliation.json>).

Code: [Drawing.ContractsV2.cs:386](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/StageContractsV2/Drawing.ContractsV2.cs:386>); [r02-amendment-evidence.json:6](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/packages/W02/rework/r02-amendment-evidence.json:6>).

## NEW-A01 — Closed

- Expected: Full SHA256 IDs; two bundles with the same8-characterprefix coexist; every SourceV2 field conflict rejected; external parentarray mutation cannot alter provenance; source.sha256 identifies exactDWGbytes.
- Actual: All stated invariants observed on livecode: SOURCE_DESCRIPTOR_CONFLICT for mismatched descriptors; immutableparentchains; original11-fieldconstructor/JSONnames/order preserved.
- Unit/tolerance: SHA256/fieldidentity/JSONbytes; Exact
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\registry\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\green\\registry", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\registry\\green-results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-registry/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-registry/results.json>).

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\guards\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\green\\guards", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\guards\\green-results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-guards/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-guards/results.json>).

Code: [TemplateContractValidator.cs:130](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:130>); [Domain.ContractsV2.cs:37](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs:37>).

## NEW-A02 — Closed

- Expected: Build đúng SDK AutoCAD2026 không còn CS1061 BlockTableRecord.Layer
- Actual: Freshlive build against AutoCAD2026 SDK completed with0errors; NativeGatefull pipeline remainsNotRun.
- Unit/tolerance: compiler diagnostic,host execution; 0errorCS1061; exact
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "build", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\DBTool\\DBTool.AutoCAD.Tests\\DBTool.AutoCAD.Tests.csproj", "-p:AutoCADManagedPath=C:/Program Files/Autodesk/AutoCAD 2026", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-apply\\native", "--nologo", "-v", "quiet"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/live-native-build/output.log>).

Code: [NativeAssetSignatures.cs:27](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs:27>); [NativeAssetSignatures.cs:48](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs:48>).

## D08 — StillOpen

- Expected: Miền vật liệu phải bất biến khi trừ cutter nằm hoàn toàn trong void:8400mm²; donut∪island=8800mm²; arc phản chiếu giữ midpoint y=-10mm; section mỏng=.04mm².
- Actual: Lần lượt8000mm²;8400mm²; midpoint+10mm;0mm². Full2π arc ném CSG_STITCH_FAILED; input thiếu cạnh có thể mất một phần im lặng.
- Unit/tolerance: mm²,mm,rad,topology; Các oracle probe:area1e-6mm²,midpoint1e-9mm. Đây là tolerance kiểm phản ví dụ, không tự nâng thành tolerance sản phẩm.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:100](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:100>); [AssemblyBoundaryEmitter.cs:396](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:396>); [AssemblyBoundaryEmitter.cs:548](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:548>); [AssemblyBoundaryEmitter.cs:611](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:611>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| NESTED-VOID-DIFFERENCE | {"area": 8400, "loops": 2} | {"area": 8000, "loops": 3, "inner": 2, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |
| EMIT-ISLAND-FILLS-VOID | {"area": 8800, "loops": 3} | {"area": 8400, "loops": 2, "inner": 1, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |
| REFLECTED-ARC-DOMAIN | {"midY": -10} | {"midY": 10, "sweep_rad": 3.141592653589793, "normal": {"x": 0, "y": 0, "z": 1}} | mm,rad; midY<=1e-9mm |
| VERTICAL-THIN-SECTION | {"area": 0.04, "loops": 1} | {"area": 0, "loops": 0, "inner": 0, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |
| SINGLE-ARC-CIRCLES-DISJOINT | {"area": 628.3185307179587, "loops": 2} | {"exception": "CSG_STITCH_FAILED: subject=Union, fields=[loops], expected=closed manifold stitched loops, actual=failed to stitch 2 segments: ArcSegment2D[10.000,0.000 -> 10.000,0.000]; ArcSegment2D[40.000,0.000 -> 40.000,0.000]"} | mm2, count; area<=1e-6 mm2; count exact |
| PARTIAL-STITCH-MUST-FAULT | {"fault": "CSG_STITCH_FAILED"} | [{"id": "Stitched/0", "role": "Outer", "curves": [{"id": "Valid/C0", "kind": "Line", "start": {"x_mm": 0, "y_mm": 0, "z_mm": 0}, "end": {"x_mm": 10, "y_mm": 0, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}, {"id": "Valid/C1", "kind": "Line", "start": {"x_mm": 10, "y_mm": 0, "z_mm": 0}, "end": {"x_mm": 10, "y_mm": 10, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}, {"id": "Valid/C2", "kind": "Line", "start": {"x_mm": 10, "y_mm": 10, "z_mm": 0}, "end": {"x_mm": 0, "y_mm": 10, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}, {"id": "Valid/C3", "kind": "Line", "start": {"x_mm": 0, "y_mm": 10, "z_mm": 0}, "end": {"x_mm": 0, "y_mm": 0, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}], "closed": true, "signed_area_mm2": 100}] | none; exact diagnostic |

## D11 — Closed

- Expected: Rejected/missingapproval/source/blankscope must fault in Plan andS5; hostmissing/duplicate mustfault forroundbar andAngle; exactaliases/orderedroles/C3vsC3A/math remaincorrect.
- Actual: Typed SHAPE_RECIPE_BINDING_MISSING/GROUP_HOST_BINDING_MISSING faults observed; valid andexplicitwildcard controls remainconsumable; exactaliases/membercoverage/orderedroles preserved.
- Unit/tolerance: scope/approval/ID/membercount/mm; Exactidentity; numericaltolerances preserved in eachpredicate record
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\recipe\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\green\\recipe"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-recipe/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-recipe/results.json>).

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\guards\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\green\\guards", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\guards\\green-results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-guards/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-guards/results.json>).

Code: [PresentationGroupPlanner.cs:446](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:446>); [PresentationGroupPlanner.cs:461](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:461>).

## AC-N03 — Closed

- Expected: A\A hoặc A bị cutter bao kín:0 loop, 0 mm²
- Actual: 0 loop, 0 mm²
- Unit/tolerance: count,mm²; Topology exact; diện tích0exact
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:755](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:755>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| CSG-EQUAL | {"area": 0, "loops": 0} | {"area": 0, "loops": 0, "inner": 0, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |
| CSG-ENGULFED | {"area": 0, "loops": 0} | {"area": 0, "loops": 0, "inner": 0, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |

## AC-N04 — Closed

- Expected: Public Emit:outer10000−hole1600=8400mm²,2loops; ca workerouter10000−hole400=9600mm²,2loops
- Actual: Freshlive PublicEmit produces8400mm2 with2loops and1Inner. R03worker9600case is archived evidence, not rerun inR04.
- Unit/tolerance: mm²,count; Oracle independent1e-6mm²; topology exact
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:598](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:598>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| EMIT-HOLE | {"area": 8400, "loops": 2} | {"area": 8400, "loops": 2, "inner": 1, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |

## AC-N05 — Closed

- Expected: Nửa đĩa r10:50π=157.07963267948966mm²; giữ arc
- Actual: 157.07963267948966mm² trong tolerance; các ca lens/tangency/domain controls đúng
- Unit/tolerance: mm²; 1e-6mm² cho oracle độc lập
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:1126](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:1126>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| ARC-HALFCUT | {"area": 157.07963267948966, "loops": 1} | {"area": 157.07963267948966, "loops": 1, "inner": 0, "arcs": 2} | mm2, count; area<=1e-6 mm2; count exact |

## NEW-R03-W01-COMPAT — StillOpen

- Expected: Valid individual native Circle, DBText and RotatedDimension entities remain comparable after adding an explicit supported schema wrapper. Whole-capture normalization must preserve all native fields and its explicit PendingGuiHost Hatch readback; it must not certify complete native evidence.
- Actual: Existing direct Deserialize<SemanticSnapshot> + Compare accepts the 9 entities. ComparePhysicalManifests with explicit VHG-PHYSICAL-MANIFEST-V1 rejects Circle Numbers.Radius as missing geometry; separate native DBText and RotatedDimension entities are rejected as unknown kinds. Native producer emits CLR type names and Radius, while validator expects a different hard-coded kind/field catalog. No caller uses ComparePhysicalManifests outside probes; existing native command still bypasses it. The nine-entity capture also explicitly marks Hatch readback PendingGuiHost; it is a format control, not evidence of complete native readiness.
- Unit/tolerance: entity count / typed native field contract; native circle radius=100 mm; Exact kind/field mapping; unchanged captured native snapshot, so zero geometric delta.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W01\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W01", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W01\\results.json", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-batch-A-round2-review-03\\frozen\\source\\DBTool\\TestOutput\\Session-09\\host-20260930T075502510\\actual-semantic.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W01/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W01/results.json>).

Code: [SemanticDiff.cs:60](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:60>); [SemanticDiff.cs:176](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:176>); [SemanticDiff.cs:315](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/SemanticDiff.cs:315>); [SemanticSnapshotReader.cs:44](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/SemanticSnapshotReader.cs:44>); [SemanticSnapshotReader.cs:45](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/SemanticSnapshotReader.cs:45>); [SemanticSnapshotReader.cs:61](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/SemanticSnapshotReader.cs:61>); [SemanticSnapshotReader.cs:63](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/SemanticSnapshotReader.cs:63>); [SemanticSnapshotReader.cs:91](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/SemanticSnapshotReader.cs:91>); [DrawingRegressionCommand.cs:62](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/DrawingRegressionCommand.cs:62>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| R03-native-kind-Circle | "Valid equivalent manifests: NoDifference=true" | "InvalidOperationException: PHYSICAL_MANIFEST_MISSING_GEOMETRY: Entity 'arc-1' of kind 'Circle' is missing required geometry payload." | contract; exact valid native field contract |
| R03-native-kind-DBText | "Valid equivalent manifests: NoDifference=true" | "InvalidOperationException: PHYSICAL_MANIFEST_UNKNOWN_KIND: Entity 'text-1' has unknown or unsupported kind 'DBText'." | contract; exact valid native field contract |
| R03-native-kind-RotatedDimension | "Valid equivalent manifests: NoDifference=true" | "InvalidOperationException: PHYSICAL_MANIFEST_UNKNOWN_KIND: Entity 'dimension-1' has unknown or unsupported kind 'RotatedDimension'." | contract; exact valid native field contract |

## R03-W02-N01 — StillOpen

- Expected: Native source/target content signature differs; TEMPLATE_TARGET_CONFLICT before DuplicateRecordCloning.Ignore. Child INSERT dependency names/hash complete.
- Actual: R03retainednative/runner evidence; affectedNativeAssetSignatures/runner code unchanged byL04/L05. No freshnativehostrun inR04. RequiredL02repair remainsunimplemented; see inheritedR03record andcurrentcomponenthashes.
- Unit/tolerance: geometry identity/SHA256; mm; Exact canonical content/dependency identity; geometry mutation20mm→40mm inside unchanged100mm bounds must be detected.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W02", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/results.json>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/independent-native.json>).

Native evidence kế thừa R03: [runmetadataR03](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/execution.json>). Inherited native evidence; native signature/runner components unchanged; no fresh hostrun claimed.

Code: [NativeAssetSignatures.cs:27](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs:27>); [TemplateManifestReader.cs:172](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:172>); [TemplateManifestReader.cs:179](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:179>); [TemplateAssetSignature.cs:37](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateAssetSignature.cs:37>); [IndependentNativeProbe.cs:22](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/root-native/IndependentNativeProbe.cs:22>); [IndependentNativeProbe.cs:32](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/root-native/IndependentNativeProbe.cs:32>).

## R03-W02-N02 — StillOpen

- Expected: Native mm contract; incompatible units require explicit source-backed conversion receipt or blocked final output; product1:1 preserved.
- Actual: Importer independently acceptsMM/Undefined/Feet/Inches sourceandtarget, without equality/scale/receipt check. Converter ignores signatureUnits. ProbeUnitsFeet withbounds100 emitsunitsmm,bounds100mm instead of30480mm orBlock. Physical differencefactor304.8 forrealfeet; no assertion that this actual legacytemplatecoordinategeometryisfeet.
- Unit/tolerance: ft/in input; mm output; physical scale dimensionless; Exact dimensional conversion/receipt identity; no guessed acceptance tolerance.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W02", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/results.json>).

Code: [TemplateAssetImporter.cs:31](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/TemplateAssetImporter.cs:31>); [NativeAssetSignatures.cs:32](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs:32>); [TemplateContractValidator.cs:189](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:189>); [TemplateContractValidator.cs:420](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs:420>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| W02-units-feet | "Reject incompatible native units or normalize100ft to30480mm with sourced conversion" | {"units": "mm", "bounds": {"min_x_mm": 0, "min_y_mm": 0, "max_x_mm": 100, "max_y_mm": 200}} | ft input; mm output; exact dimensional conversion |

## R03-W02-N03 — StillOpen

- Expected: Closure values generated from exact run artifacts; independently expected/actual field metrics; nonzero host exit/anyfailedcheck/staleartifact fail gate.
- Actual: R03retainednative/runner evidence; affectedNativeAssetSignatures/runner code unchanged byL04/L05. No freshnativehostrun inR04. RequiredL02repair remainsunimplemented; see inheritedR03record andcurrentcomponenthashes.
- Unit/tolerance: native mm; gate boolean; SHA256/run identity; Exact executable artifact identity; glyph measurement tolerance absent; no tolerance for false gate/fabricated sample.
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W02", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W02\\results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W02/results.json>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/independent-native.json>).

Native evidence kế thừa R03: [runmetadataR03](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/execution.json>). Inherited native evidence; native signature/runner components unchanged; no fresh hostrun claimed.

Code: [new-a01-closure-evidence.json:24](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/packages/W02/rework/new-a01-closure-evidence.json:24>); [run_w02_native.ps1:33](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/packages/W02/rework/run_w02_native.ps1:33>); [W02NativeRemediationCommands.cs:103](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/W02NativeRemediationCommands.cs:103>); [W02NativeRemediationCommands.cs:265](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD.Tests/W02NativeRemediationCommands.cs:265>).

## R03-W03-N01 — StillOpen

- Expected: Outer10000 minus hole1600 is8400; subtracting nested400 void leaves8400. Union of this donut with400 island is8800, including via public Emit.
- Actual: Nested difference8000 with2 inner loops; signed union/public Emit discard island and output8400.
- Unit/tolerance: mm2; 1e-6mm2; count exact
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:611](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:611>); [AssemblyBoundaryEmitter.cs:1117](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:1117>); [AssemblyBoundaryEmitter.cs:100](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:100>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| NESTED-VOID-DIFFERENCE | {"area": 8400, "loops": 2} | {"area": 8000, "loops": 3, "inner": 2, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |
| EMIT-ISLAND-FILLS-VOID | {"area": 8800, "loops": 3} | {"area": 8400, "loops": 2, "inner": 1, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |

## R03-W03-N02 — StillOpen

- Expected: Rigid right-handed frame u=(-1,0,0),v=(0,1,0),n=(0,0,-1) must map lower semicircle midpoint(0,-10) to(0,-10).
- Actual: Emitted arc midpoint becomes(0,+10); sweep stays+pi and normal stays+Z.
- Unit/tolerance: mm, rad; 1e-9mm midpoint
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:548](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:548>); [AssemblyBoundaryEmitter.cs:556](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:556>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| REFLECTED-ARC-DOMAIN | {"midY": -10} | {"midY": 10, "sweep_rad": 3.141592653589793, "normal": {"x": 0, "y": 0, "z": 1}} | mm,rad; midY<=1e-9mm |

## R03-W03-N03 — StillOpen

- Expected: Two valid2pi radius10 closed analytic boundaries should represent2 disjoint disks of total200pi=628.3185307179587.
- Actual: CSG_STITCH_FAILED, because each closed loop has one segment and stitch requires>=2.
- Unit/tolerance: mm2, rad; 1e-6mm2; topology exact
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [Domain.ContractsV2.cs:386](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs:386>); [AssemblyBoundaryEmitter.cs:1247](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:1247>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| SINGLE-ARC-CIRCLES-DISJOINT | {"area": 628.3185307179587, "loops": 2} | {"exception": "CSG_STITCH_FAILED: subject=Union, fields=[loops], expected=closed manifold stitched loops, actual=failed to stitch 2 segments: ArcSegment2D[10.000,0.000 -> 10.000,0.000]; ArcSegment2D[40.000,0.000 -> 40.000,0.000]"} | mm2, count; area<=1e-6 mm2; count exact |

## R03-W03-N04 — StillOpen

- Expected: Closed10x10 rectangle plus a declared-closed second rectangle missing its fourth edge must fault, since not all kept segments form closed manifold loops.
- Actual: Returns only valid100mm2 rectangle and silently drops3 unmatched edges of second loop.
- Unit/tolerance: count, mm2; all kept segments consumed; exact
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:761](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:761>); [AssemblyBoundaryEmitter.cs:1247](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:1247>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| PARTIAL-STITCH-MUST-FAULT | {"fault": "CSG_STITCH_FAILED"} | [{"id": "Stitched/0", "role": "Outer", "curves": [{"id": "Valid/C0", "kind": "Line", "start": {"x_mm": 0, "y_mm": 0, "z_mm": 0}, "end": {"x_mm": 10, "y_mm": 0, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}, {"id": "Valid/C1", "kind": "Line", "start": {"x_mm": 10, "y_mm": 0, "z_mm": 0}, "end": {"x_mm": 10, "y_mm": 10, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}, {"id": "Valid/C2", "kind": "Line", "start": {"x_mm": 10, "y_mm": 10, "z_mm": 0}, "end": {"x_mm": 0, "y_mm": 10, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}, {"id": "Valid/C3", "kind": "Line", "start": {"x_mm": 0, "y_mm": 10, "z_mm": 0}, "end": {"x_mm": 0, "y_mm": 0, "z_mm": 0}, "center": null, "normal": null, "radius_mm": null, "sweep_rad": null, "role": "Boundary"}], "closed": true, "signed_area_mm2": 100}] | none; exact diagnostic |

## R03-W03-N05 — StillOpen

- Expected: Footprint y interval[-.0002,.0002]mm and100mm depth yields.04mm2 section; width.0004mm exceeds configured default1e-4mm geometry tolerance.
- Actual: Round(...,3) merges two distinct boundary hits and emits no region, area0.
- Unit/tolerance: mm, mm2; 1e-6mm2 independent area; reproduction default geom1e-4mm/area1e-4mm2
- Kết luận: StillOpen; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\remaining-W03\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\post-state-W03"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/post-state-W03/results.json>).

Code: [AssemblyBoundaryEmitter.cs:396](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:396>).

Các predicate live:

| ID | Expected | Actual | Unit / tolerance |
| --- | --- | --- | --- |
| VERTICAL-THIN-SECTION | {"area": 0.04, "loops": 1} | {"area": 0, "loops": 0, "inner": 0, "arcs": 0} | mm2, count; area<=1e-6 mm2; count exact |

## R03-W03-N06 — Closed

- Expected: Rejected/missingapproval/source/blankscope must fault in Plan andS5; hostmissing/duplicate mustfault forroundbar andAngle; exactaliases/orderedroles/C3vsC3A/math remaincorrect.
- Actual: Typed SHAPE_RECIPE_BINDING_MISSING/GROUP_HOST_BINDING_MISSING faults observed; valid andexplicitwildcard controls remainconsumable; exactaliases/membercoverage/orderedroles preserved.
- Unit/tolerance: scope/approval/ID/membercount/mm; Exactidentity; numericaltolerances preserved in eachpredicate record
- Kết luận: Closed; not a signed batchGO.

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\recipe\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\green\\recipe"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-recipe/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-recipe/results.json>).

Command đã chạy:

    ["dotnet", "run", "--project", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\guards\\LiveProbe.csproj", "--artifacts-path", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\build\\green\\guards", "--", "C:\\Users\\DucBao\\Desktop\\Dbtool\\Ver 1\\outputs\\leader-acceptance\\leader-field-contracts-20261013-r2\\20261015-leader-live-remediation-04\\probes\\guards\\green-results.json"]

Exitcode: 0. Log: [rawoutput](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-guards/output.log>).

Field expected/actual: [JSON](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/green-guards/results.json>).

Code: [PresentationGroupPlanner.cs:461](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:461>).

R03-W03-N06 là blocker chi tiết củaD11; không cộng thành một nghĩa vụ sửa độc lập.
