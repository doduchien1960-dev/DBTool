# Phụ lục finding – file, dòng, spec và cách đóng

39 mục được giữ ID G/D/N/R để truy về review độc lập và counterexample. Tất cả đang Open; không sửa sản phẩm trong lượt review.

| ID | Mức | Gói sửa | Thiếu sót |
|---|---|---|---|
| G01 | P1 | W06 | Thiếu dữ liệu vẫn tự điền kích thước hoặc chọn dòng khác |
| G02 | P1 | W06, W07 | S2 làm mất feature và nguồn đã yêu cầu |
| G03 | P1 | W06 | S3 thiếu assembly, opening ownership và joint |
| G04 | P1 | W07 | S4 dựng thép giả và lặp đường thanh tại gốc |
| G05 | P1 | W07, W06 | Chốt và lỗ chờ cố định, trùng tâm, thiếu joint vẫn Ready |
| G06 | P1 | W07 | BindingMissing vẫn được truyền thành S4State Ready |
| G07 | P1 | W06, W07 | Hash không bao gồm hình học vật lý mà nó tuyên bố khóa |
| G08 | P1 | W02 | Converter asset tự tạo ATTDEF metrics và extents |
| G09 | P1 | W02 | Role template bắt buộc không thể được reader tạo ra |
| G10 | P1 | W06, W02 | Pad=0 qua validator rồi crash constructor |
| G11 | P1 | W07 | Đường legacy bỏ Catch.M và dựng catch steel sai kích thước/frame |
| G12 | P1 | W07, W06 | Tắt thép tăng cường cống làm Protect.H mất tác dụng |
| D01 | P1 | W10, W13 | S6 chỉ tạo ba view cửa thu |
| D02 | P1 | W11 | S9 không đặt bảng thống kê và hình uốn từ S8 |
| D03 | P1 | W04, W09 | Binder khác tên enum trong spec và âm thầm để trống |
| D04 | P1 | W04, W09, W10 | Thiếu asset thì tự tạo block/schema/hash |
| D05 | P1 | W04, W09, W10 | Ink được ước tính bằng số ký tự |
| D06 | P1 | W04, W10 | Hết vị trí hợp lệ vẫn trả nhãn chồng lấn |
| D07 | P1 | W04, W10 | Collector/tick/BLTC không gắn đúng giao điểm thanh thật |
| D08 | P1 | W03, W10 | Boolean không thực hiện CSG và arc biến thành chord |
| D09 | P1 | W04, W09 | Tổng con bằng 0 do nhận owner qua tiền tố sai |
| D10 | P1 | W08, W09, W10, W13 | Đường chạy thật mang nhưng không dùng S5 registry |
| D11 | P1 | W03, W08 | Thiếu recipe/prefix hoặc sai scope vẫn có nhóm hợp lệ giả |
| D12 | P1 | W10, W12 | Catch view phụ thuộc nhầm vào thép cổ |
| D13 | P2 | W11 | Sheet hash bỏ placement/scale/ink |
| N01 | P1 | W13, W05 | Entrypoint sản phẩm không nối production pipeline mới |
| N02 | P1 | W13, W05 | Native port chỉ ghi manifest, không render entity |
| N03 | P1 | W13 | Thiếu port vẫn sinh Committed/Passed/Reopened giả |
| N04 | P1 | W05, W13 | Readback chỉ kiểm handle tồn tại, không so field đo thật |
| N05 | P1 | W05, W13 | Expected được gán lại thành actual; dung sai bị tự điền |
| N06 | P1 | W05, W13 | Visual/reference Passed không có chứng minh đúng PDF và vùng xem |
| N07 | P1 | W13 | Thiếu bảo vệ output và kiểm stale target trước Replace |
| N08 | P1 | W13, W05 | Một row lỗi làm mất các row tốt và row receipts |
| N09 | P2 | W13 | Scale/elevation được khai báo nhưng UI/command chưa dùng đúng |
| N10 | P1 | W13, W09 | Bảng TK vẫn fallback text và số thiếu thành 0 |
| N11 | P2 | W05 | Manifest bị ghi đè mất provenance và lỗi persist bị nuốt |
| N12 | P2 | W13 | TagOwned ghi schema mới nhưng ReadMetadata đọc schema cũ |
| R01 | P1 | W01 | Physical manifest thiếu schema vẫn NoDifference; tọa độ vật lý bị coi LayoutOnly |
| R02 | P2 | W02, W07 | Ledger thay đổi file chưa khép kín |

## G01 — Thiếu dữ liệu vẫn tự điền kích thước hoặc chọn dòng khác

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:70](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S2; H14; F47 |
| Hành vi và điều kiện kích hoạt | GetNum dùng mặc định 1000/1500/200/150; mất row_index thì chọn rows[0]; bỏ kiểm kiểu và formula cache. |
| Gói chịu trách nhiệm | W06 |
| Cách sửa | Lookup đúng dòng; parse từng field, unit, activity và cache; thiếu bắt buộc trả StageFault/receipt Blocked. |
| Đóng bằng phép kiểm nào | Input trống, dòng không tồn tại, số lỗi và cache chưa xác minh phải Block; probe P-MISSING-INPUT phải hết tái hiện. |
| Loại bằng chứng | ExecutedCounterexampleAndSource |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
68:         var rules = input.rules;
69:         var context = input.context;
70: 
71:         var row = raw.rows.FirstOrDefault(r => r.row_index == context.row_index)
72:             ?? (raw.rows.Length > 0 ? raw.rows[0] : null)
73:             ?? throw new StageFault("ROW_MISSING", context.manhole_id, ["raw.rows"], "existing row", "none");
74: 
75:         string GetCell(string key, string fallback = "") =>
76:             row.cells.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.text
77:             ?? row.cells.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.raw_number_text
78:             ?? raw.config.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.text
79:             ?? raw.config.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.raw_number_text
80:             ?? fallback;
81: 
82:         double GetNum(string key, double fallback = 0.0)
83:         {
```

## G02 — S2 làm mất feature và nguồn đã yêu cầu

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:166](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S2; H14; NR23 |
| Hành vi và điều kiện kích hoạt | members/pipes/source_ids/parsed_fields luôn rỗng; catch/panel null; cổ bị giới hạn 700 và luôn ở giữa. |
| Gói chịu trách nhiệm | W06, W07 |
| Cách sửa | Map đủ 206 bindings và mọi feature đang ON; giải aperture/contact/position từ nguồn; không tắt feature để qua gate. |
| Đóng bằng phép kiểm nào | Bật riêng từng family, 4 tường cống, catch, panel và vị trí cổ; OUT có đúng giá trị/source hoặc diagnostic cụ thể. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
164:             neckClearX, neckClearY, null, null, neckWall, neckHeight,
165:             neckPos, neckCenter, neckReinforced, coverKind, coverCode,
166:             0.0, frameOuterX, frameOuterY, frameHeight, baseElev,
167:             elevMode, bodyScale, panelScale, noteStyle, pitchSymbol,
168:             datums.ToArray(), [], [], null, null, [], [], contactEquations.ToArray()
169:         );
170: 
171:         return new S2StateV2(context, request, input.assets, rules, raw, effective);
172:     }
173: 
174:     public S3StateV2 Execute(S2StateV2 input)
175:     {
176:         ArgumentNullException.ThrowIfNull(input);
177:         var e = input.effective;
178:         var M = e.id;
179:         double halfX = e.clear_x_mm / 2.0;
```

## G03 — S3 thiếu assembly, opening ownership và joint

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:223](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S3; H03/H04/H05; F27/F28 |
| Hành vi và điều kiện kích hoạt | Không compose pipe/catch/panel/haunch/seat/collar/frame thật; RoofAccess không gắn Roof.void_ids; joint.dowel_required=false. |
| Gói chịu trách nhiệm | W06 |
| Cách sửa | Dựng đủ solid/void/host/contact theo từng nhánh; feature ON chưa dựng phải Block. |
| Đóng bằng phép kiểm nào | Đối chiếu contour/depth/owner từng component; roof hole trừ đúng một lần; type3 không catch phải lỗi; pad0 không có Pad. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
221:             double z0 = depth0;
222:             double z1 = depth1;
223:             double thickness = depth1 - depth0;
224:             return new ComponentV2(compId, kind, assembly, material, frame, loops, z0, z1, thickness, [], [], sources, [part], key, kind);
225:         }
226: 
227:         if (e.pad_mm > 0)
228:         {
229:             var padLoops = new[] { RectLoop("Pad/Outer", -padX, -padY, padX, padY, 0, "Outer") };
230:             components.Add(MakePrism("Pad", "Pad", $"{M}:Pad", "Bedding", planFrame, padLoops, baseBottom - e.pad_mm, baseBottom, ["PadThickness", "PadExpansion"]));
231:         }
232: 
233:         var baseLoops = new[] { RectLoop("Base/Outer", -baseX, -baseY, baseX, baseY, 0, "Outer") };
234:         components.Add(MakePrism("Base", "Base", bodyAssembly, "Concrete", planFrame, baseLoops, baseBottom, 0, ["BaseThickness", "BaseExpansion"]));
235: 
236:         (string stripKey, double x0, double y0, double x1, double y1)[] strips = [
```

## G04 — S4 dựng thép giả và lặp đường thanh tại gốc

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:572](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S4; H07/H08; F32/F36 |
| Hành vi và điều kiện kích hoạt | count mặc định 5, bước150, D10, run lấy pitch; mọi đường thanh trùng origin→run; Angle dùng giá trị mass/path không tương thích. |
| Gói chịu trách nhiệm | W07 |
| Cách sửa | Nối physical planners thật; validate recipe/stack/support; phân station, clip/hook và giữ provenance, length basis. |
| Đóng bằng phép kiểm nào | Thanh nằm đúng host và cover; station khác nhau; conservation khi clip; thiếu recipe phải Block, không Known/Ready. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
570:             }
571: 
572:             if (s.family == "Angle")
573:             {
574:                 var curve = new CurveV2("Curve0", "Line", new Point3V2(0, 0, 0), new Point3V2(100, 0, 0), null, null, null, null, "Straight");
575:                 var angleOcc = new AngleOccurrenceV2(
576:                     $"{s.id}/Angle/0", host.id, null, s.shape_recipe_id, 4.0, [curve],
577:                     (s.requested_pitch_mm ?? 1000) * 4, 16.0, s.shape_recipe_id, s.source_ids, s.id, []);
578:                 anglesList.Add(angleOcc);
579:                 featuresList.Add(new FeatureStateV2(s.id, "Ready", "physical angle frames", [angleOcc.id], []));
580:                 continue;
581:             }
582: 
583:             int count = s.count ?? 5;
584:             var madeIds = new List<string>();
585:             for (int i = 0; i < count; i++)
```

## G05 — Chốt và lỗ chờ cố định, trùng tâm, thiếu joint vẫn Ready

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:611](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S4; H09; NR06; F34 |
| Hành vi và điều kiện kích hoạt | Dowel dài500, hole25/depth100, mọi center tại origin; thiếu joint dùng chuỗi Joint; station chỉ nằm trong metadata. |
| Gói chịu trách nhiệm | W07, W06 |
| Cách sửa | Dùng joint thật và NR06 bindings cho station/embed/depth/diameter; một bar/hole cùng transform. |
| Đóng bằng phép kiểm nào | Tâm bar/hole khớp từng pair, khác nhau theo station; chiều dài/đường kính theo input; thiếu joint/binding phải Block. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
609:             }
610: 
611:             var joint = g.joints.FirstOrDefault(j => j.dowel_required);
612:             int count = s.count ?? 4;
613:             var madeIds = new List<string>();
614:             for (int i = 0; i < count; i++)
615:             {
616:                 string parentId = $"{e.id}:{s.host_id}:None:Bottom:Vertical:Dowel:{i}";
617:                 string barId = $"{parentId}/0";
618:                 double runMm = 500;
619:                 var curves = new[] { new CurveV2("Curve0", "Line", new Point3V2(0, 0, 0), new Point3V2(0, 0, -runMm), null, null, null, null, "Straight") };
620:                 var occ = new OccurrenceV2(
621:                     barId, parentId, s.host_id, $"{e.id}:Roof", null, "None", "Bottom", "Vertical", "Dowel",
622:                     i, i * 200.0, s.diameter_mm ?? 18, 500, 200.0, [], "Uncut", curves,
623:                     [new RoleValueV2("Length", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
624:                     s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
```

## G06 — BindingMissing vẫn được truyền thành S4State Ready

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:565](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S4 required-feature gate; H00 |
| Hành vi và điều kiện kích hoạt | Host không tồn tại chỉ thêm FeatureState rồi continue; cuối hàm vẫn canonical và trả S4StateV2. |
| Gói chịu trách nhiệm | W07 |
| Cách sửa | Giữ partial preview riêng; tổng hợp diagnostic rồi RAISE REBAR_REQUIRED_FEATURE_BLOCKED trước accounting. |
| Đóng bằng phép kiểm nào | Một member thiếu host cùng một member hợp lệ: không được có Ready S4; receipt và partial output phải đủ nguồn lỗi. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
563:             }
564: 
565:             var host = g.components.FirstOrDefault(c => c.id == s.host_id);
566:             if (host is null)
567:             {
568:                 featuresList.Add(new FeatureStateV2(s.id, "BindingMissing", $"Host {s.host_id} missing", [], []));
569:                 continue;
570:             }
571: 
572:             if (s.family == "Angle")
573:             {
574:                 var curve = new CurveV2("Curve0", "Line", new Point3V2(0, 0, 0), new Point3V2(100, 0, 0), null, null, null, null, "Straight");
575:                 var angleOcc = new AngleOccurrenceV2(
576:                     $"{s.id}/Angle/0", host.id, null, s.shape_recipe_id, 4.0, [curve],
577:                     (s.requested_pitch_mm ?? 1000) * 4, 16.0, s.shape_recipe_id, s.source_ids, s.id, []);
578:                 anglesList.Add(angleOcc);
```

## G07 — Hash không bao gồm hình học vật lý mà nó tuyên bố khóa

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:307](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S3/S4; F46 |
| Hành vi và điều kiện kích hoạt | Geometry bỏ XY/loops/frames/void ownership; rebar bỏ path/station; hole hash bỏ center/axis. Đổi L1000→1137 vẫn cùng hash. |
| Gói chịu trách nhiệm | W06, W07 |
| Cách sửa | Canonical JSON bất biến culture, đúng thứ tự topology; hash đủ physical leaves, loại riêng display/layout/handles. |
| Đóng bằng phép kiểm nào | Probe P-GEOMETRY-HASH hết tái hiện; đổi từng endpoint, hole center, aperture, product rotation phải đổi hash. |
| Loại bằng chứng | ExecutedCounterexampleAndSource |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
305:         }
306: 
307:         var hashSb = new StringBuilder();
308:         foreach (var c in components.OrderBy(c => c.id, StringComparer.Ordinal))
309:             hashSb.Append($"{c.id}|{c.kind}|{c.material_id}|{c.z_min_mm}|{c.z_max_mm}|{c.thickness_mm};");
310:         foreach (var o in openings.OrderBy(o => o.id, StringComparer.Ordinal))
311:             hashSb.Append($"{o.id}|{o.kind}|{o.depth_min_mm}|{o.depth_max_mm};");
312:         foreach (var j in joints.OrderBy(j => j.id, StringComparer.Ordinal))
313:             hashSb.Append($"{j.id}|{j.z_mm};");
314:         foreach (var p in products.OrderBy(p => p.id, StringComparer.Ordinal))
315:             hashSb.Append($"{p.id}|{p.contract_id}|{p.anchor.z_mm};");
316:         string physicalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSb.ToString())));
317: 
318:         var geometry = new GeometryV2(components.ToArray(), openings.ToArray(), joints.ToArray(), physicalHash, products.ToArray());
319:         return new S3StateV2(input.context, input.request, input.assets, input.rules, input.raw, e, geometry);
320:     }
```

## G08 — Converter asset tự tạo ATTDEF metrics và extents

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:315](../../../../DBTool/DBTool.AutoCAD/TemplateManifestReader.cs) |
| Spec bị vi phạm | S0; AssetsV2/AttributeDefV2; NR16 |
| Hành vi và điều kiện kích hoạt | ToAssetsV2 dùng bounds0, height200,width1, basepoint chung; glyphs/products/layers/font_styles rỗng; source_ids chưa đúng descriptor. |
| Gói chịu trách nhiệm | W02 |
| Cách sửa | Đọc metrics/ATTDEF/dependency/font/product thật trong transaction; thiếu dữ liệu bắt buộc phải Block. |
| Đóng bằng phép kiểm nào | So từng field của ít nhất hai block có alignment/style khác nhau với native; mọi source_id giải được trong registry. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
313:         new(false, null, [Block(code, message)]);
314: 
315:     public static AssetsV2 ToAssetsV2(TemplateManifest manifest, string fontPolicyId = "NR-16", string units = "mm")
316:     {
317:         var definitions = manifest.Assets.Select(a =>
318:         {
319:             var p = a.Signature?.Properties;
320:             double x = 0, y = 0, z = 0;
321:             if (p != null && p.TryGetValue("Origin", out var oStr))
322:             {
323:                 var parts = oStr.Split(',');
324:                 if (parts.Length == 3)
325:                 {
326:                     double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
327:                     double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
328:                     double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
```

## G09 — Role template bắt buộc không thể được reader tạo ra

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:211](../../../../DBTool/DBTool.AutoCAD/TemplateManifestReader.cs) |
| Spec bị vi phạm | S0; exact native role/tag schema |
| Hành vi và điều kiện kích hoạt | RequiredRoles yêu cầu CatchSummary/Geometry-DowelHole nhưng MapBlockRole không map hai role đó; CatchSummary đòi TONG trái CUATHU1/CUATHU2 nguồn. |
| Gói chịu trách nhiệm | W02 |
| Cách sửa | Map đúng block/tag thật và scope feature; không buộc asset ngoài phạm vi mọi case; round-trip reader→validator→import. |
| Đóng bằng phép kiểm nào | Template gốc phải qua round-trip với role/tag đúng; xóa asset active phải lỗi chính xác, không thiếu role giả toàn cục. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
209:         "TK.THKL" => "SteelSummary",
210:         "TK.TG" => "AngleScheduleRow",
211:         "TK.THKLNAP" => "PanelSummary",
212:         "TK.GC.T" when shape == TemplateShapeKind.RoundRing => "RoundOpeningReinforcement",
213:         "TK.GC.H" when shape == TemplateShapeKind.RectangularFourBar => "RectangularOpeningReinforcement",
214:         _ => $"Block:{name}"
215:     };
216: 
217:     private static bool IsShapeSensitiveBlock(string name) => name is "TK.GC.T" or "TK.GC.H";
218:     private static bool IsCoverBlock(string name) => name.Contains(".MB", StringComparison.OrdinalIgnoreCase) ||
219:         name.Contains(".AA", StringComparison.OrdinalIgnoreCase) || name.Contains(".BB", StringComparison.OrdinalIgnoreCase);
220:     private static bool IsRequired(IReadOnlyList<TemplateRequirement> roles, string role) => roles.Any(x => string.Equals(x.Role, role, StringComparison.Ordinal));
221: 
222:     private static SourceRef CreateSource(string? relativePath, string hash) =>
223:         new(SourceKind.Dwg, NormalizeRelative(relativePath), hash, null, null, null, "read-only DWG manifest inspection");
224: 
```

## G10 — Pad=0 qua validator rồi crash constructor

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Domain/Input/ManholeInput.cs:21](../../../../DBTool/DBTool.Domain/Input/ManholeInput.cs) |
| Spec bị vi phạm | S3 padOFF; input PadThickness |
| Hành vi và điều kiện kích hoạt | Validator cho NonNegative nhưng ManholeInput vẫn Positive; ca padOFF hợp lệ ném ArgumentOutOfRangeException. |
| Gói chịu trách nhiệm | W06, W02 |
| Cách sửa | Đồng bộ invariant >=0 và không tạo pad degenerate trong builder; giữ base datum. |
| Đóng bằng phép kiểm nào | Chỉ đổi pad dương→0: validate được, không Pad/hatch/PadBottom; pad âm trả diagnostic theo field. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
19:         BaseExpansionMm = ContractGuard.NonNegative(baseExpansionMm, nameof(baseExpansionMm));
20:         PadExpansionMm = ContractGuard.NonNegative(padExpansionMm, nameof(padExpansionMm));
21:         BeddingMortarMm = ContractGuard.NonNegative(beddingMortarMm, nameof(beddingMortarMm));
22:         BaseHaunch = ContractGuard.NonNegative(baseHaunch, nameof(baseHaunch));
23:         TopHaunch = ContractGuard.NonNegative(topHaunch, nameof(topHaunch));
24:         NeckHeight = ContractGuard.NonNegative(neckHeight, nameof(neckHeight));
25:         RoofOpening = ContractGuard.Required(roofOpening);
26:         PipeConnections = ContractGuard.Copy(pipeConnections);
27:         BaseBottomMesh = ContractGuard.Required(baseBottomMesh);
28:         BaseTopMesh = ContractGuard.Required(baseTopMesh);
29:         CoverMm = ContractGuard.NonNegative(coverMm, nameof(coverMm));
30:         ElevationMode = ContractGuard.EnumValue(elevationMode);
31:         BaseElevationM = baseElevationM;
32:         PipeInvertReference = ContractGuard.EnumValue(pipeInvertReference);
33:         HookBaseBottom = ContractGuard.EnumValue(hookBaseBottom);
34:         HookBaseTop = ContractGuard.EnumValue(hookBaseTop);
```

## G11 — Đường legacy bỏ Catch.M và dựng catch steel sai kích thước/frame

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:490](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S4; NR08; Catch.M/Protect.I |
| Hành vi và điều kiện kích hoạt | CatchRebarSpec cố định D8@150 cover25; truyền clearHeight vào FloorDepth; không consumer CatchMesh và không transform host thật. |
| Gói chịu trách nhiệm | W07 |
| Cách sửa | Lấy Ø/pitch/cover từ input; depth đúng trường; compose floor/side/front theo frame body/neck và rule đã duyệt. |
| Đóng bằng phép kiểm nào | Đổi Catch.M, Protect.I, depth khác height, đổi tường và offset: mỗi path/length/count đúng support và nguồn. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
488:         }
489: 
490:         if (spec.Input.CatchBasinSpec is not null && spec.Input.CatchBasinSpec.Reinforced && geometry.CatchBasin is not null)
491:         {
492:             var rebarSpec = new CatchRebarSpec(spec.Input.CatchBasinSpec.ChamberClearWidthMm, spec.Input.CatchBasinSpec.ChamberClearHeightMm, 8, 150, 25);
493:             var catchRebar = CatchBasinRebarPlanner.PlanCatch(geometry.CatchBasin, rebarSpec, policy);
494:             if (!Add(catchRebar)) return new(false, null, diagnostics);
495:         }
496:         var duplicate=bars.GroupBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault(x=>x.Count()>1);
497:         if(duplicate is not null)
498:             return new(false,null,[new Diagnostic("COMPOSITION_DUPLICATE_OCCURRENCE",DiagnosticSeverity.Block,DiagnosticScope.Manhole,
499:                 spec.Input.Id,null,duplicate.First().Sources.FirstOrDefault(),"unique ID",duplicate.Key,"Consumer output contains duplicate physical occurrences.")]);
500:         return new(true,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,
501:             partial || bars.Any(x=>x.LengthStatus==LengthStatus.NeedsRule)?LayoutStatus.Partial:LayoutStatus.Complete,angles),diagnostics);
502: 
503:         bool StackFits(WallOrNeckRebarSpec? outer,WallOrNeckRebarSpec? inner,double thickness,string member)
```

## G12 — Tắt thép tăng cường cống làm Protect.H mất tác dụng

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Domain/Rebar/WallRebarPlanner.cs:137](../../../../DBTool/DBTool.Domain/Rebar/WallRebarPlanner.cs) |
| Spec bị vi phạm | S4; NR01; Protect.H |
| Hành vi và điều kiện kích hoạt | Pipe.D OFF khiến Accessories.Pipe=null; clipping mesh dùng cover tường thay Protect.H. |
| Gói chịu trách nhiệm | W07, W06 |
| Cách sửa | Tách clearance lỗ của mesh khỏi activity phụ kiện; dùng surface cover Protect.H + D/2 theo NR01. |
| Đóng bằng phép kiểm nào | Giữ Pipe.D=0, thay riêng Protect.H: endpoint mesh phải đổi đúng q; ON/OFF phụ kiện không đổi clearance mesh. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
135:                 : spec.Input.H + spec.Input.MainRebar.RoofCoverMm + rebarSpec.VerticalDiameterMm / 2d;
136: 
137:         double openingClearance = spec.Input.MainRebar?.Accessories?.Pipe?.Cover ?? rebarSpec.CoverMm;
138:         int vertIdx = 0;
139:         foreach (double stationOffset in vertical.Value!.PositionsMm)
140:         {
141:             double station = tangentMin + stationOffset;
142:             Point3Mm start = alongX ? new(station, face, verticalStart) : new(face, station, verticalStart);
143:             Point3Mm end = alongX ? new(station, face, wall.ZRange.End - rebarSpec.CoverMm - rebarSpec.VerticalDiameterMm / 2d) : new(face, station, wall.ZRange.End - rebarSpec.CoverMm - rebarSpec.VerticalDiameterMm / 2d);
144:             var bar = Make($"{spec.Input.Id}/{wall.Id}/{surface}/Vertical/{bars.Count:000}", wall,
145:                 surface == RebarSurface.Inner ? Layer.Bottom : Layer.Top, alongX ? Axis.X : Axis.Y,
146:                 RebarDirection.Vertical, rebarSpec.VerticalDiameterMm, start, end, wall.Id, rebarSpec.Sources.FirstOrDefault() ?? spec.Sources.FirstOrDefault(), rebarSpec.Sources,
147:                 vertIdx++, station);
148:             var clipped = ClipIfRequired(bar, wall, model, openingClearance);
149:             if (!clipped.Success) return new(false, null, clipped.Diagnostics);
150:             bars.AddRange(clipped.Value!.Retained);
```

## D01 — S6 chỉ tạo ba view cửa thu

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:765](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S6; EXPAND_VIEW_ROLE_TABLE |
| Hành vi và điều kiện kích hoạt | Execute chỉ gọi CatchAssemblyProjector; case không catch trả CatchPlan/11/22 required=false, không có body/base/wall/roof. |
| Gói chịu trách nhiệm | W10, W13 |
| Cách sửa | Expand toàn bộ role table và chiếu physical components/members; catch là nhánh thêm, không thay kết cấu. |
| Đóng bằng phép kiểm nào | Probe P-STRUCTURE-VIEWS hết tái hiện; role và target coverage khớp chính xác cho cả 3 profile. |
| Loại bằng chứng | ExecutedCounterexampleAndSource |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
763:     {
764:         ArgumentNullException.ThrowIfNull(input);
765:         var projector = new CatchAssemblyProjector();
766:         var projection = projector.Project(
767:             input.effective,
768:             input.geometry,
769:             input.rebar,
770:             input.groups,
771:             input.assets,
772:             input.rules);
773:         return new S6StateV2(
774:             input.context,
775:             input.request,
776:             input.assets,
777:             input.rules,
778:             input.raw,
```

## D02 — S9 không đặt bảng thống kê và hình uốn từ S8

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:831](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S9; EXACT_VIEW_BUNDLES; F41 |
| Hành vi và điều kiện kích hoạt | Bundles chỉ duyệt projection.views; Schedule/Bending mới trong S8 không được placement; coverage cũng chỉ so S6. |
| Gói chịu trách nhiệm | W11 |
| Cách sửa | Hợp required view IDs của projection và S8; đặt mỗi bundle đúng một lần và kiểm cả heading/summary/caption. |
| Đóng bằng phép kiểm nào | Multiset required IDs == placements; mỗi TK/bending có frame và native ink nằm trong frame sau reopen. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
829:         }
830: 
831:         var bundles = proj.views.Select(v => {
832:             var ink = viewInkMap.TryGetValue(v.id, out var b) ? b : new Bounds2V2(0, 0, 1000, 1000);
833:             return new {
834:                 view_id = v.id,
835:                 view = v,
836:                 ink = ink,
837:                 sheet_role = ResolveSheetRole(v.id, e.profile)
838:             };
839:         }).ToList();
840: 
841:         var recipe = rules.entries.FirstOrDefault(r => r.id == "NR-22")
842:             ?? rules.entries.FirstOrDefault(r => r.id.Contains("PAGINATION", StringComparison.OrdinalIgnoreCase))
843:             ?? (rules.entries.Length > 0 ? rules.entries[0] : null);
844: 
```

## D03 — Binder khác tên enum trong spec và âm thầm để trống

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:309](../../../../DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs) |
| Spec bị vi phạm | S8; TagBindingV2.operation |
| Hành vi và điều kiện kích hoạt | Code nhận RoleMean/RawSumM/MassKg, spec dùng MeanRole/RawSumMetres/Mass; Pitch/FixedRole/SizePair thiếu; unknown trả literal/empty. |
| Gói chịu trách nhiệm | W04, W09 |
| Cách sửa | Dùng đúng closed operation set; nhánh unknown raise; bind typed raw/unit/source trước format. |
| Đóng bằng phép kiểm nào | Mỗi operation hợp lệ có raw/text đúng; unknown Block; reopen ATTRIB đủ tag và giá trị, không ô rỗng thay số. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
307:                         value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Diameter");
308:                         break;
309:                     case "RoleMean":
310:                         var roleVal = (group.mean_roles ?? []).FirstOrDefault(r => string.Equals(r.role, b.role, StringComparison.OrdinalIgnoreCase));
311:                         raw = roleVal?.value_mm ?? 0.0;
312:                         unit = "mm";
313:                         value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "Leg");
314:                         break;
315:                     case "MeanLength":
316:                         raw = group.mean_mm;
317:                         unit = "mm";
318:                         value = SchematicPresentationPlanner.FormatValue(raw, unit, rules, "DAI");
319:                         break;
320:                     case "Count":
321:                         raw = group.count;
322:                         unit = "count";
```

## D04 — Thiếu asset thì tự tạo block/schema/hash

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:564](../../../../DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs) |
| Spec bị vi phạm | S7/S8; exact asset gate |
| Hành vi và điều kiện kích hoạt | ExactAsset sinh handle0/default_hash/default_label_hash và extents; schematic tự tạo glyph bounds; chọn shape bằng heuristic. |
| Gói chịu trách nhiệm | W04, W09, W10 |
| Cách sửa | Chỉ exact asset/recipe có nguồn; thiếu active asset/field phải Block, không dựng definition giả. |
| Đóng bằng phép kiểm nào | Xóa từng TK/BLT/glyph active phải báo tên/source đúng trước native write; không có fake handle/hash trong output. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
562:     }
563: 
564:     private static AssetV2 ExactAsset(AssetsV2 assets, string name)
565:     {
566:         var found = (assets.definitions ?? []).FirstOrDefault(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase));
567:         if (found != null) return found;
568: 
569:         // Fallback default definition if template did not contain named asset
570:         return new AssetV2(
571:             name: name,
572:             definition_handle: "0",
573:             content_hash: "default_hash",
574:             base_point: new Point3V2(0, 0, 0),
575:             bounds: new Bounds2V2(0, -500, 12500, 0),
576:             role: "ScheduleAsset",
577:             child_names: [],
```

## D05 — Ink được ước tính bằng số ký tự

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:517](../../../../DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs) |
| Spec bị vi phạm | S7/S8; H12; NR16 |
| Hành vi và điều kiện kích hoạt | Nhãn/bảng/caption dùng text.Length×hằng số; bỏ glyph/font/kerning/escape/alignment/rotation. |
| Gói chịu trách nhiệm | W04, W09, W10 |
| Cách sửa | Đo glyph và native style thật; transform đầy đủ ink/strokes; missing metric phải lỗi. |
| Đóng bằng phép kiểm nào | Các chữ tiếng Việt, %%c, căn giữa/phải, quay/mirror và font khác: ink tính và native quan sát khớp tolerance nguồn. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
515:     }
516: 
517:     private static Bounds2V2 MeasureAssetInk(AssetV2 asset, List<BoundAttributeV2> attributes)
518:     {
519:         var box = asset.bounds ?? new Bounds2V2(0, 0, 400.0, 200.0);
520:         foreach (var a in attributes)
521:         {
522:             double w = (a.text?.Length ?? 0) * 120.0;
523:             var aBox = new Bounds2V2(0, 0, w, 200.0);
524:             box = UnionBounds(box, aBox);
525:         }
526:         return box;
527:     }
528: 
529:     private static AssetV2 ExactAsset(AssetsV2 assets, string name)
530:     {
```

## D06 — Hết vị trí hợp lệ vẫn trả nhãn chồng lấn

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:429](../../../../DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs) |
| Spec bị vi phạm | S7; H28; NR22; NR-IO-011 |
| Hành vi và điều kiện kích hoạt | Ba lane cố định; thất bại thì dùng anchor+(300,200) không kiểm; recipe luôn Right và port bị coi là insertion. |
| Gói chịu trách nhiệm | W04, W10 |
| Cách sửa | Duyệt hữu hạn lane/extension từ source, đúng side/extent/gap và port; hết phải LAYOUT_ANNOTATION_NO_FIT. |
| Đóng bằng phép kiểm nào | Che kín mọi lane phải NoFit với rejected candidates; case có chỗ phải đúng min gap và native port transform. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
427:                 return (insertion, port, ink, [routePoints]);
428:             }
429:         }
430: 
431:         // Fallback placement: offset from primary anchor
432:         var fallbackPort = new Point2V2(primaryAnchor.x_mm + 300.0, primaryAnchor.y_mm + 200.0);
433:         var fallbackInk = TransformBounds(nativeInk, fallbackPort, scale);
434:         return (fallbackPort, fallbackPort, fallbackInk, [[primaryAnchor, fallbackPort]]);
435:     }
436: 
437:     private static List<LaneDefinition> LanesFromExplicitRecipe(
438:         ViewV2 view,
439:         List<Point2V2> anchorPoints,
440:         LabelRecipe? recipe)
441:     {
442:         var lanes = new List<LaneDefinition>();
```

## D07 — Collector/tick/BLTC không gắn đúng giao điểm thanh thật

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:167](../../../../DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs) |
| Spec bị vi phạm | S7; H11; physical target association |
| Hành vi và điều kiện kích hoạt | Một leader từ anchor đầu nhưng mọi target được khai là giao điểm; BLTC occurrence_id=group.id; view ink thiếu route/tick. |
| Gói chịu trách nhiệm | W04, W10 |
| Cách sửa | Tính analytic intersections trên member đúng group; tie join physical occurrence; union ink đầy đủ. |
| Đóng bằng phép kiểm nào | Mỗi claimed point nằm trên collector và member; BLTC ID là tie thật; routes/ticks đều nằm trong certified envelope. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
165:                     string rid = Encode(labelId, bIdx.ToString(CultureInfo.InvariantCulture));
166:                     routeIds.Add(rid);
167: 
168:                     var intersections = new List<MemberIntersectionV2>();
169:                     foreach (var target in groupTargets.Where(t => t.point != null))
170:                     {
171:                         intersections.Add(new MemberIntersectionV2(
172:                             occurrence_id: target.source_id,
173:                             point: target.point!,
174:                             collector_curve_id: rid
175:                         ));
176: 
177:                         if (string.Equals(group.family, "MainMesh", StringComparison.OrdinalIgnoreCase))
178:                         {
179:                             ticks.Add(new TickV2(
180:                                 id: Encode(rid, target.source_id, "Tick"),
```

## D08 — Boolean không thực hiện CSG và arc biến thành chord

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:488](../../../../DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs) |
| Spec bị vi phạm | S6; H06/H26; F37/F38 |
| Hành vi và điều kiện kích hoạt | Union nối list; Difference thêm nguyên inner loop kể cả vượt biên; projection biến arc thành line endpoint. |
| Gói chịu trách nhiệm | W03, W10 |
| Cách sửa | Analytic split/classify/stitch, union/difference đúng và giữ line/arc; lỗi capability phải rõ. |
| Đóng bằng phép kiểm nào | Union hình chồng, void cắt mép, seam cùng vật liệu, lỗ tròn: topology/area/orientation theo tolerance nguồn và native/PDF. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
486:     }
487: 
488:     private static List<LoopV2> RegionBoolean(IEnumerable<LoopV2> loops, string operation)
489:     {
490:         var list = loops.ToList();
491:         if (list.Count <= 1) return list;
492: 
493:         // Simplify and union loops
494:         var simplified = new List<LoopV2>();
495:         foreach (var loop in list)
496:         {
497:             if (Math.Abs(loop.signed_area_mm2) > AreaTol)
498:             {
499:                 simplified.Add(loop);
500:             }
501:         }
```

## D09 — Tổng con bằng 0 do nhận owner qua tiền tố sai

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:434](../../../../DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs) |
| Spec bị vi phạm | S8; NR14 physical owner buckets |
| Hành vi và điều kiện kích hoạt | StartsWith(Base/Wall/Roof/Neck) trên ID đã scope như MH1:Base đều false; TONG vẫn cộng mass. |
| Gói chịu trách nhiệm | W04, W09 |
| Cách sửa | Join exact component/assembly ownership từ registry, không parse tên/prefix gần giống. |
| Đóng bằng phép kiểm nào | Đổi tên/prefix ga tùy ý: DAY/TUONG/TRAN/CO và TONG phải khớp raw rows cho cả hai bucket Ø. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
432:                     value = groups.Where(g => g.host_id.StartsWith("Base", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
433:                 else if (tag.StartsWith("TUONG", StringComparison.OrdinalIgnoreCase))
434:                     value = groups.Where(g => g.host_id.StartsWith("Wall", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
435:                 else if (tag.StartsWith("TRAN", StringComparison.OrdinalIgnoreCase))
436:                     value = groups.Where(g => g.host_id.StartsWith("Roof", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
437:                 else if (tag.StartsWith("CO", StringComparison.OrdinalIgnoreCase))
438:                     value = groups.Where(g => g.host_id.StartsWith("Neck", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) <= 10).Sum(g => g.mass_kg ?? 0.0);
439:                 else
440:                     value = small;
441:             }
442:             else if (tag.EndsWith("2", StringComparison.Ordinal))
443:             {
444:                 // Large diameter bucket (> 10mm)
445:                 if (tag.StartsWith("DAY", StringComparison.OrdinalIgnoreCase))
446:                     value = groups.Where(g => g.host_id.StartsWith("Base", StringComparison.OrdinalIgnoreCase) && (g.diameter_mm ?? 0) > 10).Sum(g => g.mass_kg ?? 0.0);
447:                 else if (tag.StartsWith("TUONG", StringComparison.OrdinalIgnoreCase))
```

## D10 — Đường chạy thật mang nhưng không dùng S5 registry

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/ViewBuilder.cs:39](../../../../DBTool/DBTool.Drawing/ViewBuilder.cs) |
| Spec bị vi phạm | S5; F49; shared registry |
| Hành vi và điều kiện kích hoạt | ViewBuilder tiếp tục markByOccurrence canonical; FromPlannedGroups không caller; PlanPreDrawing gộp ComponentKind và tự mass D²×0.006165. |
| Gói chịu trách nhiệm | W08, W09, W10, W13 |
| Cách sửa | Nối một registry nhóm exact xuyên annotation/TK/bending; mass theo source basis; bỏ đường dựng danh tính riêng. |
| Đóng bằng phép kiểm nào | Trace từ command thật; group marks khác canonical vẫn xuất nhất quán; host/panel/opening scopes không bị trộn. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
37:         if(geometry.PurchasedCovers.Any(c=>c.NativeViewPlacement is not null && c.Scale!=1))
38:             return Fail("COVER_NATIVE_SCALE_INVALID",source,"source-native purchased cover at unit physical scale","fit scaling");
39:         if (!new[] { "Pad", "Base", "Roof" }.All(id => geometry.Components.Any(c => c.Id == id)))
40:             return Fail("DRAWING_GEOMETRY_MISSING", source, "Pad, Base, Roof", "incomplete geometry");
41:         if (layout.Occurrences.IsEmpty)
42:             return Fail("DRAWING_LAYOUT_EMPTY", source, "S1 base occurrences", "empty layout");
43: 
44:         var occurrences = layout.Occurrences.ToDictionary(o => o.Id, StringComparer.Ordinal);
45:         var markByOccurrence = new Dictionary<string, string>(StringComparer.Ordinal);
46:         foreach (var row in schedule.Rows)
47:             foreach (var id in row.OccurrenceIds)
48:             {
49:                 if (!occurrences.ContainsKey(id))
50:                     return Fail("DRAWING_MARK_UNRESOLVED", source, "schedule occurrence in layout", id);
51:                 if (!markByOccurrence.TryAdd(id, row.Mark))
52:                     return Fail("DRAWING_MARK_DUPLICATE", source, "one schedule row per occurrence", id);
```

## D11 — Thiếu recipe/prefix hoặc sai scope vẫn có nhóm hợp lệ giả

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:114](../../../../DBTool/DBTool.Drawing/PresentationGroupPlanner.cs) |
| Spec bị vi phạm | S5; F49; scoped aliases |
| Hành vi và điều kiện kích hoạt | Thiếu basis mặc định Fabrication; search sau bỏ profile/component/family; prefix 1..8/D1; global alias không chứng minh member set. |
| Gói chịu trách nhiệm | W03, W08 |
| Cách sửa | Validate exact scope/basis/prefix và alias member-set evidence; thiếu phải Block. |
| Đóng bằng phép kiểm nào | Recipe sai profile/thiếu prefix/alias sai member set phải lỗi; cùng chữ mark ở host khác không cross-join. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
112: 
113:             string compKind = ResolveComponentKind(b.host_id, geometry);
114:             var recipe = FindShapeRecipe(rules, b.shape_recipe_id, effective.profile, compKind, b.family);
115: 
116:             string presBasis = recipe?.presentation_length_basis ?? "Fabrication";
117:             double li;
118:             if (string.Equals(presBasis, "Fabrication", StringComparison.OrdinalIgnoreCase))
119:             {
120:                 li = b.fabrication_length_mm.Value;
121:             }
122:             else if (string.Equals(presBasis, "Schedule", StringComparison.OrdinalIgnoreCase))
123:             {
124:                 li = b.schedule_length_mm ?? b.fabrication_length_mm.Value;
125:             }
126:             else
127:             {
```

## D12 — Catch view phụ thuộc nhầm vào thép cổ

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/ViewBuilder.cs:398](../../../../DBTool/DBTool.Drawing/ViewBuilder.cs) |
| Spec bị vi phạm | S6 active Catch; W12 replacement gate |
| Hành vi và điều kiện kích hoạt | Nhánh catch động nằm trong điều kiện có Neck và view /Neck/; catch active với cổ không BTCT mất cả 3 view. |
| Gói chịu trách nhiệm | W10, W12 |
| Cách sửa | Tách predicate catch khỏi neck reinforcement; chiếu chung assembly cửa thu theo activity đúng. |
| Đóng bằng phép kiểm nào | Cross-product catch ON/OFF, neck0/có và BTCT/không: các view bắt buộc có đủ hoặc diagnostic có nguồn. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
396:                 }
397: 
398:                 if (geometry.Components.Any(c => c.Id == "Neck" || c.Kind == ComponentKind.Neck) && views.Any(v => v.Id.Contains("/Neck/")))
399:                 {
400:                     var neckDetails=SheetFourNeckDetailer.TryBuild(inputs,markByOccurrence,presentation);
401:                     if(!neckDetails.Success) return new(false,null,neckDetails.Diagnostics);
402:                     var neckPlan=neckDetails.Value!.Plan;var neckAA=neckDetails.Value.AA;var neckBB=neckDetails.Value.BB;
403: 
404:                     primitives.RemoveAll(p => p.ViewId.Contains("Neck", StringComparison.Ordinal));
405:                     primitives.AddRange(neckPlan);
406:                     primitives.AddRange(neckAA);
407:                     primitives.AddRange(neckBB);
408: 
409:                     views.RemoveAll(v => v.Id.Contains("Neck"));
410:                     views.Add(new ViewDefinition(SheetFourViews.PlanViewId, ViewPurpose.Plan, new Point2Mm(0, 0), PrimitiveBounds(neckPlan), presentation.ScaleDenominator));
411:                     views.Add(new ViewDefinition(SheetFourViews.AAViewId, ViewPurpose.Section, new Point2Mm(0, 0), PrimitiveBounds(neckAA), presentation.ScaleDenominator));
```

## D13 — Sheet hash bỏ placement/scale/ink

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P2 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:994](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | S9; F46 |
| Hành vi và điều kiện kích hoạt | Chỉ hash frame.id và placements.Length, nên thay vị trí/scale/bounds vẫn cùng hash. |
| Gói chịu trách nhiệm | W11 |
| Cách sửa | Canonical HASH_STATE của payload sheet đầy đủ, không hash count thay nội dung. |
| Đóng bằng phép kiểm nào | Đổi riêng scale, translation, bounds, ink phải đổi hash; replay cùng state/culture phải giữ hash. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
992:         }
993: 
994:         var sheetHashBytes = Encoding.UTF8.GetBytes(string.Join(";", framesOut.Select(f => $"{f.id}:{f.placements.Length}")));
995:         string sheetHash = Convert.ToHexString(SHA256.HashData(sheetHashBytes));
996: 
997:         var sheetOut = new SheetV2(
998:             framesOut.ToArray(),
999:             input.rebar.fabrication_geometry_hash,
1000:             input.rebar.physical_hash,
1001:             sheetHash);
1002: 
1003:         return new S9StateV2(
1004:             input.context,
1005:             input.request,
1006:             input.assets,
1007:             input.rules,
```

## N01 — Entrypoint sản phẩm không nối production pipeline mới

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool/App/HoGaCommands.cs:167](../../../../DBTool/DBTool/App/HoGaCommands.cs) |
| Spec bị vi phạm | S0–S11 composition; W13 B3.07 |
| Hành vi và điều kiện kích hoạt | Command/palette/headless dùng CalculationPipeline cũ; dispatcher/batch/verifier không caller sản phẩm; chưa có plot/review/session/context ports thật. |
| Gói chịu trách nhiệm | W13, W05 |
| Cách sửa | Composition root thực sự dùng một request qua S0–S11 và đủ native/plot/evidence ports; giữ UI chính. |
| Đóng bằng phép kiểm nào | Transcript một run command/palette thật có đủ stage/source/build IDs, native handles, save/reopen, plot và row receipts. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
165:                 var presentation = new DBTool.Drawing.PresentationRequest(30, DBTool.Domain.ElevationMode.Symbolic, null, "VHG-Dark", true);
166:                 string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(excelPath)));
167:                 var calculated=new PaletteDrawingCalculation().CalculateAsync(excelPath,profile,profile.ToString(),
168:                     result.Manholes.Select(m=>m.Name),hash,presentation,default).GetAwaiter().GetResult();
169:                 if(!calculated.Success)throw new InvalidOperationException(PaletteDrawingCalculation.FormatDiagnostics(calculated.Diagnostics).Blocking);
170:                 var batch=calculated.Value!;
171:                 var roles=batch.Manholes.SelectMany(m=>m.Package.DrawingPackage!.Primitives).OfType<DBTool.Drawing.BlockPrimitive>()
172:                     .GroupBy(p=>p.BlockRole,StringComparer.Ordinal).Select(g=>new DBTool.Application.Templates.TemplateRequirement(
173:                         g.Key,DBTool.Application.Templates.AssetKind.Block,g.SelectMany(p=>p.Attributes).Select(a=>a.Key).Distinct(StringComparer.OrdinalIgnoreCase))).ToArray();
174:                 string templatePath=TemplateManager.ResolveTemplatePath();
175:                 string templateHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(templatePath)));
176:                 var manifest=new DBTool.AutoCAD.Template.TemplateManifestReader().Read(new(templatePath,templateHash,roles,"VHGpro 3.2/Template/VHG-Template.dwg"));
177:                 if(!manifest.Success)throw new InvalidOperationException(string.Join("; ",manifest.Diagnostics.Select(d=>d.Code+": "+d.Message)));
178:                 var validated=new DBTool.Application.Templates.TemplateContractValidator().Validate(manifest.Value!,roles,DBTool.Application.Templates.TemplateValidationMode.ProductionRender);
179:                 if(!validated.Success)throw new InvalidOperationException(string.Join("; ",validated.Diagnostics.Select(d=>d.Code+": "+d.Message)));
180:                 var repository=new DBTool.AutoCAD.Generation.GenerationRepository();
```

## N02 — Native port chỉ ghi manifest, không render entity

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.AutoCAD/CadGenerationWriter.cs:95](../../../../DBTool/DBTool.AutoCAD/CadGenerationWriter.cs) |
| Spec bị vi phạm | S10 write/readback |
| Hành vi và điều kiện kích hoạt | handleMap rỗng, không dựng native intents hoặc TagOwned; có thể xóa Replace rồi không tạo hình thay thế; vẫn readback Passed. |
| Gói chịu trách nhiệm | W13, W05 |
| Cách sửa | Flatten/render đủ intents trong transaction; ownership/handle coverage; reopen đo trước khi Passed. |
| Đóng bằng phép kiểm nào | Input S9 có curve/label/TK/frame phải ra entity thật đúng coverage; sửa một entity/ATTRIB phải NATIVE_READBACK_MISMATCH. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
93:         }
94: 
95:         var handleMap = new List<HandleV2>();
96:         IReadOnlyList<string> beforeIds = [];
97:         IReadOnlyList<string> beforeHashes = [];
98: 
99:         try
100:         {
101:             using (var docLock = _document.LockDocument())
102:             using (var tr = _document.Database.TransactionManager.StartTransaction())
103:             {
104:                 _repository.EnsureSchema(_document.Database, tr);
105: 
106:                 var before = _repository.ReadUntouchedGenerationManifests(_document.Database, tr, M);
107:                 beforeIds = before.ids;
108:                 beforeHashes = before.hashes;
```

## N03 — Thiếu port vẫn sinh Committed/Passed/Reopened giả

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/StageAdapters.cs:1074](../../../../DBTool/DBTool.Application/StageAdapters.cs) |
| Spec bị vi phạm | H00; S0/S1/S10; no simulated Passed |
| Hành vi và điều kiện kích hoạt | Default S10 trả SIMULATED_SAVED_NATIVE_HASH, handles[] và hash input; S0/S1 mặc định cũng tự dựng dữ liệu rỗng. Test hiện tại bắt phải có trạng thái giả này. |
| Gói chịu trách nhiệm | W13 |
| Cách sửa | Production không có fallback fake; thiếu port/reader trả Blocked/NotRun; fakes chỉ ở test assembly. |
| Đóng bằng phép kiểm nào | Probe P-DEFAULT-NATIVE hết tái hiện; không có DWG thật không thể Passed; command composition test không chỉ IsAssignableFrom. |
| Loại bằng chứng | ExecutedCounterexampleAndSource |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
1072:             native = _nativePort.Write(input, CancellationToken.None);
1073:         }
1074:         else
1075:         {
1076:             native = new NativeV2(
1077:                 saved_dwg_path: input.request.target_dwg_path,
1078:                 saved_dwg_sha256: "SIMULATED_SAVED_NATIVE_HASH",
1079:                 handles: [],
1080:                 normalized_geometry_hash: input.geometry.physical_hash,
1081:                 normalized_rebar_hash: input.rebar.physical_hash,
1082:                 repository_version: "generation-xdata/v1",
1083:                 write_status: "Committed",
1084:                 readback_status: "Passed",
1085:                 unchanged_manhole_ids: [],
1086:                 unchanged_before_hashes: [],
1087:                 unchanged_after_hashes: [],
```

## N04 — Readback chỉ kiểm handle tồn tại, không so field đo thật

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.AutoCAD/NativeReadbackVerifier.cs:105](../../../../DBTool/DBTool.AutoCAD/NativeReadbackVerifier.cs) |
| Spec bị vi phạm | S10 actual native equality |
| Hành vi và điều kiện kích hoạt | Không so geometry/ATTRIB/OCS/style/scale/owner với expected; thiếu attributes lấy dữ liệu caller; hash copy input; handles rỗng vẫn pass. |
| Gói chịu trách nhiệm | W05, W13 |
| Cách sửa | Đọc lại DB đã đóng/mở, đủ native fields và actual normalized hash; kiểm coverage và ownership thật. |
| Đóng bằng phép kiểm nào | Di chuyển vertex, đổi block/tag/style/owner/rotation/scale hoặc thiếu handle: fail kèm expected/actual/unit/handle. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
103:                     }
104: 
105:                     actualHandles.Add(new HandleV2(
106:                         semantic_id: h.semantic_id,
107:                         entity_handle: h.entity_handle,
108:                         block_name: actualBlockName ?? h.block_name,
109:                         attribute_tags: actualTags.Count > 0 ? actualTags.ToArray() : h.attribute_tags,
110:                         attribute_values: actualValues.Count > 0 ? actualValues.ToArray() : h.attribute_values,
111:                         owner_manhole_id: h.owner_manhole_id
112:                     ));
113:                 }
114:             }
115: 
116:             tr.Commit();
117:         }
118:         catch (StageFault)
```

## N05 — Expected được gán lại thành actual; dung sai bị tự điền

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs:331](../../../../DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs) |
| Spec bị vi phạm | S11; NR18; F48 |
| Hành vi và điều kiện kích hoạt | Dim/group/opening/owner dùng cùng DTO cho hai phía; GetUnitTolerance thay null bằng literal. |
| Gói chịu trách nhiệm | W05, W13 |
| Cách sửa | Actual chỉ từ readback/vector PDF có handle/hash; thiếu measurement NotRun, thiếu tolerance Blocked. |
| Đóng bằng phép kiểm nào | Native sai số thật phải được báo đúng actual và Failed; null tolerance không thể Passed; không tự lấy expected làm actual. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
329:             if (handleMap.TryGetValue(subjectId, out var h))
330:             {
331:                 actualVal = expectedVal; // measured dimension
332:                 nHandles = [h.entity_handle];
333:             }
334:             list.Add(new AcceptanceSubjectItem(
335:                 Name: name,
336:                 SubjectId: subjectId,
337:                 Expected: expectedVal,
338:                 Actual: actualVal,
339:                 ExpectedText: null,
340:                 ActualText: null,
341:                 Unit: "mm",
342:                 SourceIds: e.source_ids ?? [],
343:                 NativeHandles: nHandles,
344:                 EvidencePaths: [input.native.saved_dwg_path]));
```

## N06 — Visual/reference Passed không có chứng minh đúng PDF và vùng xem

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs:153](../../../../DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs) |
| Spec bị vi phạm | S11; mandatory DucBao review |
| Hành vi và điều kiện kích hoạt | Chỉ kiểm reference IDs không rỗng; copy visual_status, không bắt reviewer, PDF hash/frame và required regions; chưa port thật. |
| Gói chịu trách nhiệm | W05, W13 |
| Cách sửa | Khóa reference identity và PDF hash/page/frame; mọi vùng phải được DucBao xem; validate file/plot transcript trước verdict. |
| Đóng bằng phép kiểm nào | Đổi PDF hash/frame/reference, thiếu reviewer/vùng hoặc chưa xem: không visual Passed/Complete. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
151:                     }
152: 
153:                     PlotPageV2? reviewed = null;
154:                     if (_visualReviewReader != null)
155:                     {
156:                         reviewed = _visualReviewReader.ReadReview(result, input.request.reference_bundle_ids);
157:                     }
158: 
159:                     string visualStatus = "NotRun";
160:                     if (reviewed != null)
161:                     {
162:                         visualStatus = reviewed.visual_status;
163:                     }
164: 
165:                     var page = result with
166:                     {
```

## N07 — Thiếu bảo vệ output và kiểm stale target trước Replace

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.AutoCAD/CadGenerationWriter.cs:90](../../../../DBTool/DBTool.AutoCAD/CadGenerationWriter.cs) |
| Spec bị vi phạm | S10 output guard/Create/Replace |
| Hành vi và điều kiện kích hoạt | Chỉ chặn path rỗng; SaveAs có thể ghi nguồn/ngoài output; không đọc expected_generation/input/document token; mode lạ vẫn đi tiếp. |
| Gói chịu trách nhiệm | W13 |
| Cách sửa | Canonical output allowlist, cấm source; exact mode và stale target checks trước transaction; Create chống trùng. |
| Đóng bằng phép kiểm nào | Path nguồn/ngoài root, stale Replace/mode lạ/duplicate Create phải fail trước mutation; Replace B giữ A nguyên vẹn. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
88: 
89:         string targetDwg = request.target_dwg_path;
90:         if (string.IsNullOrWhiteSpace(targetDwg))
91:         {
92:             throw new StageFault("OUTPUT_PATH_SOURCE_CONFLICT", M, ["target_dwg_path"], "output copy", "empty");
93:         }
94: 
95:         var handleMap = new List<HandleV2>();
96:         IReadOnlyList<string> beforeIds = [];
97:         IReadOnlyList<string> beforeHashes = [];
98: 
99:         try
100:         {
101:             using (var docLock = _document.LockDocument())
102:             using (var tr = _document.Database.TransactionManager.StartTransaction())
103:             {
```

## N08 — Một row lỗi làm mất các row tốt và row receipts

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool/App/PaletteDrawingCalculation.cs:90](../../../../DBTool/DBTool/App/PaletteDrawingCalculation.cs) |
| Spec bị vi phạm | S11 batch row isolation; H17 |
| Hành vi và điều kiện kích hoạt | Loop có receipt nhưng any failure trả Value=null; command gate cả workbook; calculation Complete bị hiểu như delivery; export không xử failed rows. |
| Gói chịu trách nhiệm | W13, W05 |
| Cách sửa | Giữ một final receipt cho mỗi row; tiếp tục rows tốt khi host khỏe; tách CalculationReady với DeliveryComplete. |
| Đóng bằng phép kiểm nào | Batch A tốt/B lỗi/C tốt: A,C có artifact; A/B/C có final receipt; cancel/savefail giữ bằng chứng đã commit và không mất dòng. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
88:                 rowReceipts.Add(new PaletteRowReceipt(name, "Complete", result.Value, rowDiags));
89:             }
90:             bool allSuccess = packages.Count == selected.Length && !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block);
91:             return new(allSuccess, allSuccess ? new PaletteDrawingBatch(packages.AsReadOnly(), rowReceipts.AsReadOnly()) : null, diagnostics.Distinct());
92:         }
93:         catch (OperationCanceledException) { return Fail("OPERATION_CANCELLED", "Đã hủy tính toán."); }
94:         catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
95:         { return Fail("PALETTE_CALCULATION_FAILED", ex.Message); }
96:     }
97: 
98:     public static PresentationRequest Presentation => new(30, ElevationMode.Symbolic, null, "VHG-Dark", true);
99: 
100:     public static PaletteDiagnosticReport FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
101:     {
102:         var items=diagnostics.ToArray();
103:         string Group(DiagnosticSeverity severity)=>string.Join("\n",items.Where(d=>d.Severity==severity).Select(d=>
```

## N09 — Scale/elevation được khai báo nhưng UI/command chưa dùng đúng

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P2 / Open |
| Code | [DBTool/DBTool/UI/MainViewModel.cs:159](../../../../DBTool/DBTool/UI/MainViewModel.cs) |
| Spec bị vi phạm | W13 shared request; preserve main UI |
| Hành vi và điều kiện kích hoạt | XAML không bind các field mới; RunBatch cố định 30/Symbolic; Relative không thuộc enum, fallback Symbolic; CLI chỉ 20/30/40 và một ID. |
| Gói chịu trách nhiệm | W13 |
| Cách sửa | Map từ controls/request hiện có, không đổi hình thức UI; enum/scale đúng nguồn; command documentation trùng parser thật. |
| Đóng bằng phép kiểm nào | Trace từng lựa chọn đến immutable request; giá trị unsupported phải lỗi; script hiện tại chỉ truyền đúng một workbook path. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
157:         };
158: 
159:         private double _drawingScale = 30.0;
160:         public double DrawingScale
161:         {
162:             get => _drawingScale;
163:             set { _drawingScale = value; OnPropertyChanged(); }
164:         }
165: 
166:         public ObservableCollection<double> AvailableScales { get; } = new() { 20, 25, 30, 40, 50 };
167: 
168:         private string _scaleMode = "Fixed";
169:         public string ScaleMode
170:         {
171:             get => _scaleMode;
172:             set { _scaleMode = value; OnPropertyChanged(); }
```

## N10 — Bảng TK vẫn fallback text và số thiếu thành 0

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.AutoCAD/CadRenderer.cs:298](../../../../DBTool/DBTool.AutoCAD/CadRenderer.cs) |
| Spec bị vi phạm | S8/S10 exact native TK |
| Hành vi và điều kiện kích hoạt | Thiếu block hoặc mark rơi xuống AppendText; length/count/total/mass/diameter thiếu dùng 0. |
| Gói chịu trách nhiệm | W13, W09 |
| Cách sửa | Preflight toàn row/block/tag/raw value; thiếu active TK/number phải Block, không text thay native INSERT. |
| Đóng bằng phép kiểm nào | Xóa required block/number phải lỗi trước write; mỗi row hợp lệ reopen đúng block và ATTRIB, không DBText rời. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
296:         }
297: 
298:         if (row.Role == "Schedule/Row" && !string.IsNullOrEmpty(row.ScheduleMark))
299:         {
300:             var schedAsset = FindBlockAsset(manifest, row.ScheduleMark) ?? FindBlockAsset(manifest, $"ScheduleRow/{row.Mark}");
301:             if (schedAsset != null && sink.HasBlock(schedAsset.Name))
302:             {
303:                 var cells = row.Cells.ToDictionary(c => c.ColumnKey);
304:                 double singleLen = cells.TryGetValue("SingleLengthMm", out var slCell) && slCell.Number.HasValue ? slCell.Number.Value :
305:                                    cells.TryGetValue("LengthMm", out var lCell) && lCell.Number.HasValue ? lCell.Number.Value : 0;
306:                 double qty = cells.TryGetValue("Quantity", out var qCell) && qCell.Number.HasValue ? qCell.Number.Value : 0;
307:                 double totalLen = cells.TryGetValue("TotalLengthMm", out var tlCell) && tlCell.Number.HasValue ? tlCell.Number.Value : 0;
308:                 double mass = cells.TryGetValue("MassKg", out var mCell) && mCell.Number.HasValue ? mCell.Number.Value : 0;
309:                 double diam = cells.TryGetValue("DiameterMm", out var dCell) && dCell.Number.HasValue ? dCell.Number.Value : 0;
310: 
311:                 var attributes = ScheduleNativeAttributes.BindScheduleRow(
```

## N11 — Manifest bị ghi đè mất provenance và lỗi persist bị nuốt

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P2 / Open |
| Code | [DBTool/DBTool.Application/BatchGenerationOrchestrator.cs:248](../../../../DBTool/DBTool.Application/BatchGenerationOrchestrator.cs) |
| Spec bị vi phạm | S11/H17 artifact manifest |
| Hành vi và điều kiện kích hoạt | Assembler ghi nguồn/receipts rồi orchestrator ghi raw BatchV2 cùng tên; catch persist rỗng; session/context exception có thể mất row receipt. |
| Gói chịu trách nhiệm | W05 |
| Cách sửa | Một finalizer/schema manifest đầy đủ; I/O failure rõ; bảo đảm accounting/cleanup mọi target kể cả pre-row fault. |
| Đóng bằng phép kiểm nào | Inject lỗi store/session/context: receipt thất bại đúng và cleanup; manifest cuối giữ đủ source/build/rule/receipt/artifact hashes. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
246:                 finalDwgSha256);
247: 
248:             PersistManifest(manifestPath, finalBatch);
249: 
250:             return finalBatch;
251:         }
252: 
253:         private void PersistManifest(string manifestPath, BatchV2 batch)
254:         {
255:             try
256:             {
257:                 string dir = Path.GetDirectoryName(manifestPath) ?? "";
258:                 if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
259:                 {
260:                     Directory.CreateDirectory(dir);
261:                 }
```

## N12 — TagOwned ghi schema mới nhưng ReadMetadata đọc schema cũ

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P2 / Open |
| Code | [DBTool/DBTool.AutoCAD/GenerationRepository.cs:131](../../../../DBTool/DBTool.AutoCAD/GenerationRepository.cs) |
| Spec bị vi phạm | S10 versioned ownership |
| Hành vi và điều kiện kích hoạt | XData mới generation/semantic/run/build/version bị decoder hiểu semantic là InputHash, run là TemplateHash, build là Version. |
| Gói chịu trách nhiệm | W13 |
| Cách sửa | Schema có version discriminator rõ; round-trip/upgrade explicit; không giải nghĩa lại index âm thầm. |
| Đóng bằng phép kiểm nào | Ghi/đọc/save/reopen cả schema được hỗ trợ; unknown version phải Block; owner/input/template/semantic/run/build không lẫn. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
129:     }
130: 
131:     internal static GenerationMetadata? ReadMetadata(DBObject databaseObject)
132:     {
133:         using var data = databaseObject.XData;
134:         if (data is null) return null;
135:         var values = data.AsArray();
136:         if (values.Length < 5 || values[0].Value is not string app || !string.Equals(app, RegAppName, StringComparison.Ordinal))
137:             return null;
138:         if (values[1].Value is not string generationId || values[2].Value is not string inputHash ||
139:             values[3].Value is not string templateHash || values[4].Value is not string snapshot)
140:             return null;
141:         return new GenerationMetadata(generationId, inputHash, templateHash, snapshot);
142:     }
143: 
144:     public void TagOwned(DBObject databaseObject, string generationId, string semanticId, string runId, string buildHash)
```

## R01 — Physical manifest thiếu schema vẫn NoDifference; tọa độ vật lý bị coi LayoutOnly

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P1 / Open |
| Code | [DBTool/DBTool.Drawing/SemanticDiff.cs:52](../../../../DBTool/DBTool.Drawing/SemanticDiff.cs) |
| Spec bị vi phạm | W01 evidence integrity; physical semantic diff |
| Hành vi và điều kiện kích hoạt | {} và {} thành snapshot rỗng rồi NoDifference=true; Start.X line đổi10mm bị phân loại LayoutOnly. Hai counterexamples đã chạy thật. |
| Gói chịu trách nhiệm | W01 |
| Cách sửa | Validate schema/expected entity coverage và semantic ID trước diff; phân biệt world geometry với sheet placement bằng kind/frame/space. |
| Đóng bằng phép kiểm nào | P-EMPTY-PHYSICAL-MANIFEST và P-PHYSICAL-COORDINATE-CLASSIFICATION hết tái hiện; đổi physical endpoint không được là LayoutOnly. |
| Loại bằng chứng | ExecutedCounterexampleAndSource |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
50:     public static SemanticSnapshot FromPhysicalManifest(string documentIdentity, string generationIdentity, string manifestJson)
51:     {
52:         using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
53:         var entities = ImmutableArray.CreateBuilder<SemanticEntity>();
54:         var root = doc.RootElement;
55:         var array = root.ValueKind == System.Text.Json.JsonValueKind.Array
56:             ? root.EnumerateArray()
57:             : root.TryGetProperty("Entities", out var eNode) && eNode.ValueKind == System.Text.Json.JsonValueKind.Array
58:                 ? eNode.EnumerateArray()
59:                 : root.TryGetProperty("entities", out var eLower) && eLower.ValueKind == System.Text.Json.JsonValueKind.Array
60:                     ? eLower.EnumerateArray()
61:                     : Enumerable.Empty<System.Text.Json.JsonElement>();
62: 
63:         foreach (var item in array)
64:         {
65:             var id = item.TryGetProperty("SemanticId", out var idProp) ? idProp.GetString()! :
```

## R02 — Ledger thay đổi file chưa khép kín

| Trường | Kết luận |
|---|---|
| Mức / trạng thái | P2 / Open |
| Code | [DBTool/DBTool.Drawing/StageContractsV2/Drawing.ContractsV2.cs:1](../../../../DBTool/DBTool.Drawing/StageContractsV2/Drawing.ContractsV2.cs) |
| Spec bị vi phạm | B3.12 evidence/BeforeHash chain |
| Hành vi và điều kiện kích hoạt | Drawing DTO current hash khác W02 AfterHash (nội dung khác whitespace, schema103 vẫn khớp); StageAdapters W07 BeforeHash khác W06 AfterHash. |
| Gói chịu trách nhiệm | W02, W07 |
| Cách sửa | Bổ sung amendment có hash/source/actor/thời điểm cho thay đổi chưa ghi; không sửa receipts cũ làm gãy dependency hashes. |
| Đóng bằng phép kiểm nào | Hash snapshot hiện tại và chuỗi sửa được giải thích đầy đủ; 19 dependency links hiện khớp không thay thế bằng chứng execution order. |
| Loại bằng chứng | StaticSourceReview |

Đoạn source đóng băng (phần hàm đầy đủ ở frozen/source):

```text
1: #nullable enable
2: using DBTool.Domain.StageContractsV2;
3: namespace DBTool.Drawing.StageContractsV2;
4: 
5: public sealed record GroupV2(
6:     string id /* — */,
7:     string host_id /* — */,
8:     string face /* — */,
9:     string layer /* — */,
10:     string direction /* — */,
11:     string family /* — */,
12:     string region_role /* — */,
13:     string[] opening_ids /* — */,
14:     string shape_recipe_id /* — */,
```

Review gốc theo nhánh: [Geometry](agents/geometry-review.md), [Drawing](agents/drawing-review.md), [Native](agents/native-review.md). [Counterexamples thực thi](probes/results/counterexamples.json). Các review nhánh là source review; root thực thi và đối chiếu riêng, không gọi chúng là native chứng nhận.
