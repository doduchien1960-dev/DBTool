# SPEC THAY ĐỔI CÁCH TIẾP CẬN — ĐƯỜNG 2

Đây là phương án và spec mới do leader bàn giao, chưa phải chức năng đã triển khai. Không được dùng bất kỳ mục nào trong tài liệu này để đóng finding chỉ vì đã viết spec. Các thay đổi giao thức là quyết định kỹ thuật của leader ngày 2026-10-15, trong quyền điều chỉnh phạm vi được người dùng cho phép. Không sửa 23 quyết định NR đã duyệt; mọi số liệu kỹ thuật còn thiếu phải Block, kèm source/field.

## L01 — Một canonical snapshot và một gate dùng chung

Áp dụng R01, NEW-A03, NEW-R03-W01-COMPAT. Ngõ cụt cũ là thêm allowlist và kiểm tra “có ít nhất một field hình học” trong helper, trong khi native consumer vẫn gọi Compare trực tiếp. Thay cách này bằng dữ liệu có kiểu, adapter không mất dữ liệu và validator chạy trước mọi phép so sánh.

### Contract CanonicalPhysicalSnapshotR03

| Field lá | Kiểu / đơn vị | Nguồn / quy tắc bắt buộc |
| --- | --- | --- |
| schema_version | string | Protocol mới của leader: DBTOOL-PHYSICAL-R03; đúng một property; không nhận missing/null/duplicate/array root |
| run_id | string | ID riêng của lần capture; không dùng timestamp của file làm ID bằng chứng lịch sử |
| units | enum mm | Snapshot chuẩn hóa vật lý về mm; unit binding có nguồn; thiếu binding phải Block |
| source_dwg_sha256 | SHA256 | Bytes của đúng DWG được capture |
| expected_contract_sha256 | SHA256 | Contract coverage bên ngoài, khóa trước khi capture actual; actual không tự sinh chuẩn |
| entities[].semantic_id | string | Duy nhất, không rỗng; ID ổn định từ semantic owner; không dùng native handle làm semantic ID |
| entities[].native_kind | string | Giữ CLR kind gốc để truy vết; không bỏ khi normalize |
| entities[].kind | enum | Line/Circle/Arc/Polyline/Text/MText/Block/Dimension/Hatch; unknown phải có diagnostic |
| entities[].space | enum World/Paper | Producer khai báo; không suy từ tên field |
| entities[].frame_id | string | Resolve duy nhất trong frames; không rỗng |
| entities[].source_ids[] | string | Resolve đúng immutable registry và SHA256 nguồn |
| entities[].physical_payload | tagged union | Các field bắt buộc theo bảng dưới; không dùng một dictionary tùy ý |
| entities[].placement.transform | số hữu hạn | Sheet transform hoặc native placement được khai báo riêng; không trộn với physical shape |
| frames[].id | string | Duy nhất |
| frames[].origin_xyz_mm | vector3 | Có nguồn; cả 3 tọa độ đều bắt buộc |
| frames[].basis_u_xyz / basis_v_xyz / normal_xyz | vector3 vô thứ nguyên | Hữu hạn, basis hợp lệ; unit/orthogonality theo tolerance nguồn |
| coverage.required_ids[] | string | Pass-through từ expected contract; không tính từ actual entities |
| coverage.required_kinds_by_id | mapping | Khóa từ expected contract; thiếu/extra phải có rule explicit cho extra, không bỏ qua |

Bảng physical_payload là hợp đồng canonical mới; adapter phải truyền đủ các thành phần. Chỉ được mở rộng danh sách cùng với schema revision và cả producer lẫn consumer.

| Kind | Field lá bắt buộc | Domain check |
| --- | --- | --- |
| Line | start.xyz_mm,end.xyz_mm | Hữu hạn; field thừa/chưa tiêu thụ phải báo lỗi; degeneracy dùng tolerance có nguồn |
| Circle | center.xyz_mm,normal.xyz,radius_mm | radius>0; normal hợp lệ |
| Arc | center.xyz_mm,normal.xyz,radius_mm,start_angle_rad,sweep_rad,start.xyz_mm,end.xyz_mm | Theo arc-domain L03; EndAngle native được chuyển không mất dữ liệu sang sweep; giữ raw angle để audit |
| Polyline | vertices[].xyz_mm,bulge,start_width_mm,end_width_mm; closed,elevation_mm,normal.xyz | Đủ tuple cho mọi vertex; không chấp nhận VertexBogus hoặc một tọa độ đơn lẻ |
| Text | text.raw,text.decoded,encoding_rule_id,position.xyz_mm,alignment.xyz_mm,height_mm,width_factor,rotation_rad,horizontal_mode,vertical_mode,style_id,mirror_x,mirror_y | Encoding/style có nguồn; height và width factor hữu hạn, dương; text rỗng vẫn là giá trị hợp lệ, không đồng nghĩa field bị thiếu |
| MText | contents.raw,encoding_rule_id,location.xyz_mm,text_height_mm,width_mm,rotation_rad,attachment,style_id,normal.xyz | Không flatten formatting khi chưa có adapter native; unknown controls phải báo lỗi kèm raw data |
| Block | asset_id,asset_content_hash,insertion.xyz_mm,scale.xyz,rotation_rad,normal.xyz,attributes[].tag/value/position/alignment/style/height/width/rotation/modes,dependency_ids[] | Native identity và dependency hash đầy đủ; không dùng Space-only làm geometry |
| Dimension | dimension_kind,measurement_mm,definition_points[].role/xyz_mm,text_position.xyz_mm,text_override,style_id,style_hash,normal.xyz,rotation_rad nếu kind cần | Giữ cả measurement và override; không suy actual measurement từ text override |
| Hatch | pattern_id,pattern_scale,pattern_angle_rad,loops[].curves[],looprole,nesting_parent_id,associative,owner_ids[] | Loop analytic khép kín, đủ readback; PendingGuiHost không được nâng thành complete |

Native mapping tối thiểu bắt buộc: Circle.Numbers.Radius→radius_mm; DBText→Text; MText→MText; RotatedDimension/AlignedDimension→Dimension với dimension_kind gốc; Line/Arc/Polyline/BlockReference/Hatch có adapter theo các field trên. Mapping từ producer thật phải liệt kê mọi field producer ghi ra, đánh dấu Consumed hoặc ExplicitlyIgnoredNonPhysical kèm lý do; field chưa phân loại phải báo lỗi SNAPSHOT_FIELD_UNMAPPED. Không chỉ bọc capture cũ bằng schema rồi coi là đã normalize.

### Thuật toán và classifier

    NormalizeNativeCapture(raw, expectedContract, unitBinding):
        reject root không phải object, schema missing/null/duplicate/unknown
        parse đầy đủ native kind và field; unknown → SNAPSHOT_FIELD_UNMAPPED
        chuyển đơn vị đúng unitBinding; kiểm tra hữu hạn/domain cho từng field
        resolve semantic ID, frame và nguồn; duplicate → SNAPSHOT_ID_DUPLICATE
        kiểm tra mọi requiredID từ expectedContract; thiếu → SNAPSHOT_COVERAGE_MISSING
        validate từng taggedpayload; thiếu → SNAPSHOT_PAYLOAD_INCOMPLETE
        nếu native readback còn PendingGuiHost → SNAPSHOT_CAPTURE_INCOMPLETE
        return canonical snapshot chỉ khi tất cả gate trên đạt

    CompareCanonical(expected, actual):
        validate BOTH snapshots bằng cùng validator
        ID/kind hoặc World↔Paper/frame basis thay đổi → CoordinateBasisMismatch
        so physicalshape/content/dimension/assetidentity trong cùng frame
        physicalpayload khác → NumericalMismatch hoặc SemanticMismatch theo taggedfield
        nếu payload giữ nguyên AND cả hai cùng Paper AND cùng frame:
            placementtransform khác → LayoutOnly
            placementtransform bằng → NoDifference
        ELSE nếu cả hai World và geometry/transform giữ nguyên → NoDifference
        ELSE thay World placement làm đổi vị trí vật lý → NumericalMismatch
        không được trả NoDifference từ emptycapture trái expectedcoverage

Frame ID đổi nhưng có transform-equivalence được duyệt chỉ được normalize khi expected contract có nguồn equivalence explicit; thiếu equivalence phải báo CoordinateBasisMismatch, không đoán. Paper text đổi Point.X trong cùng sheet frame nhưng nội dung/shape giữ nguyên là LayoutOnly; World endpoint đổi 10mm là NumericalMismatch; World→Paper không bao giờ là LayoutOnly chỉ vì một phía là Paper.

Cập nhật đồng thời SemanticSnapshotReader, SemanticDiff, DrawingRegressionCommand, ReferenceComparisonService và ReleaseEvidence. Public Compare không còn lối bypass: hoặc nhận validatedCanonical type mà chỉ validator tạo được, hoặc tự validate và yêu cầu expected contract. Overload legacy chỉ dùng để đọc capture lưu trữ, không được ký acceptance.

Gate L01: native Circle/DBText/RotatedDimension phải được truyền không mất dữ liệu; tất cả angle/coordinate/content mutation phải còn trong diff; malformed root/schema/payload/coverage không được coi là equality. Capture 9 entity cũ có HatchPendingGuiHost chỉ là fixture tương thích, không phải acceptance đã hoàn tất native. Rủi ro: baseline cũ sẽ cần migration có version rõ ràng; cấm đổi baseline để xóa khác biệt thực.

## L02 — Native asset identity, full-text measurement và round-trip thật

Áp dụng G08/G09 và R03-W02-N01/N02/N03. Leader sở hữu toàn bộ thay đổi, không gửi thêm một lệnh “đọc native metrics” chung chung cho implementer cũ.

### Tách dữ liệu theo việc có thể chứng minh

S0 chỉ lập inventory native resource và xác minh source/style/font/unit. Nội dung chữ annotation/schedule chỉ hoàn chỉnh sau grouping và formatting. Do đó, fixed glyph catalog tại S0 không thể đại diện cho mọi chuỗi Excel bất kỳ. Luồng mới:

    S0 native inventory + font context + exact source registry
        → grouping/recipe/format text drafts
        → đo native toàn bộ actual text drafts
        → allocator dùng measurement đã được verified
        → emit native entity
        → save target + reopen + independent readback

Đây là thay đổi port giữa W02/W04/W08 và các consumer W09/W10/W11/W13. Không được âm thầm coi DTO public V2 là đã chứa metrics mới: tạo revision asset-context-R03 và native-text-measurement-R03, khóa receipt dependency theo revision/hash. Legacy V2 chỉ Ready nếu cung cấp đủ metrics thật cho toàn bộ actual request; không tạo glyph giả để đủ mảng.

| NativeAssetContext field lá | Kiểu / đơn vị | Nguồn và validation |
| --- | --- | --- |
| revision | string | asset-context-R03; quyết định protocol của leader |
| bundle_sha256 | SHA256 | Exact DWG bytes |
| source_ids[] | string | Registry immutable; SourceV2.sha256 là bytes nguồn, không phải signature hash |
| units.metadata_insunits | native enum | Đọc DWG; chỉ là metadata |
| units.coordinate_unit | approved enum mm/in/ft/... | UnitBinding được duyệt cho exact bundle hash; thiếu phải Block |
| units.scale_to_mm | rational numerator/denominator | Chuyển đơn vị theo binding/source; không infer từ schematic |
| units.source_id | string | Quyết định hoặc nguồn unit có thẩm quyền |
| definitions[].asset_id / handle / content_hash | string | ID chính xác; content hash riêng; handle có DWG parent |
| definitions[].origin_xyz_mm / bounds_min_xyz_mm / bounds_max_xyz_mm | vector3 | Native đọc và normalize đúng một lần; bounds chỉ là metric, không phải identity |
| definitions[].entity_payload_hash | SHA256 | Canonical đầy đủ geometry/text/dimension/visibility |
| definitions[].child_asset_ids[] / child_content_hashes[] | strings | Resolve recursive; cyclic/missing phải có diagnostic |
| definitions[].dynamic_properties[].name/type/value | typed | Native authoritative; không bỏ nội dung dynamic nếu có ảnh hưởng đến geometry |
| definitions[].attributes[].tag/default_text/style_id/height_mm/width_factor/position_xyz_mm/alignment_xyz_mm/rotation_rad/horizontal_mode/vertical_mode | typed fields | Mỗi field native đều required; không default 0/Left/Base; kiểm tra finite và supported enum |
| definitions[].entity_layers[] | exact strings | Danh sách layer thật; MultiLayer:... chỉ là description, không phải layer_id dùng để vẽ |
| definitions[].insertion_layer_id | exact string | Binding explicit cho layer đặt INSERT; thiếu phải Block, không đoán layer0 |
| styles[].style_id / native_style_hash | string/SHA256 | Đúng TextStyleTableRecord; hash style không thay hash font |
| styles[].font_path / font_file_sha256 | path/SHA256 | Resolve file thật, hash bytes; file missing→FONT_SOURCE_MISSING |
| styles[].bigfont_path / bigfont_sha256 | nullable path/hash | Both null khi native không dùng; không cho phép một trường null và một trường có giá trị |
| styles[].native_width_factor / oblique_rad / mirrored_x / mirrored_y / vertical / typeface / bold / italic | typed | Đọc native; không dựa vào tên chứa Bold |
| styles[].encoding_rule_id / normalization_rule_id | string | Rule có nguồn cho Unicode/TCVN3/VNI/SHX; thiếu→FONT_ENCODING_UNRESOLVED |
| styles[].provider_version / fallback_detected | string/bool | Engine/version thật; fallback true→FONT_SUBSTITUTION_BLOCKED |
| layers[].id/native_lineweight_enum/effective_stroke_paper_mm/source_ids[] | typed/mm | Resolve native ByLayer/ByBlock/default từ DB/plot binding có nguồn; không mặc định 0.25 |

Font tồn tại không chứng minh engine đang dùng đúng font; provider phải xác định font thực tế và fallback. SHX/bigfont/legacy encoding phải có binding được duyệt, không dán nhãn Unicode/NFC cho tất cả. Nếu host không thể chứng minh actual font/coverage, trả typed fault; không dùng tỷ lệ gần đúng.

### Port đo toàn chuỗi

| TextMeasureRequest field lá | Kiểu / đơn vị | Quy tắc |
| --- | --- | --- |
| id / run_id / context_sha256 | string/hash | Ràng buộc exact context và run |
| text_raw / text_decoded | string | Raw từ formatted schedule/annotation; decoded theo explicit encoding rule |
| entity_kind | enum DBText/MText/Attribute | Đúng loại đối tượng native cuối |
| style_id / font_file_sha256 / bigfont_sha256 | string/hash nullable | Khớp verified context |
| height_mm / width_factor / rotation_rad | double | Từ field nguồn/recipe/style đã chốt; kiểm tra finite/domain |
| horizontal_mode / vertical_mode / attachment | native enum/nullable | Theo entity kind; không dùng blank default |
| mirrored_x / mirrored_y / vertical | bool | Native binding thực |
| wrapping_width_mm | double nullable | Bắt buộc cho MText wrapping; null cho DBText; không đoán giới hạn |
| source_ids[] | strings | Nguồn text/style/format/geometry đầy đủ |

| TextMeasureResult field lá | Kiểu / đơn vị | Tính từ đâu |
| --- | --- | --- |
| request_id / request_sha256 / context_sha256 | strings/hash | Pass-through/hashed exact request |
| ink.min_x_mm/min_y_mm/max_x_mm/max_y_mm | double/mm | Native geometry của whole text sau align/rotation/mirror/wrap |
| has_ink | bool | Native result; space-only có thể advance>0 nhưng ink empty |
| advance_x_mm / advance_y_mm | double/mm | Native text advance thực nếu provider có API tin cậy; không đổi tên ink width thành advance |
| resolved_font_hashes[] / engine_version / encoding_rule_id | hash/string | Actual native font và rule đã dùng |
| native_raw_measurement | typed metric record | Giữ readback gốc trước khi chuyển frame, làm oracle độc lập |
| source_ids[] / diagnostic_codes[] | arrays | Missing metric phải Block, không partial Ready |
| measurement_sha256 | SHA256 | Canonical request+actual result+font identity |

Native API đã xác minh: [TextStyle.ExtentsBox](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_GraphicsInterface_TextStyle_ExtentsBox_string__MarshalAsUnmanagedType_U1__bool__MarshalAsUnmanagedType_U1__bool_WorldDraw.html) trả extents của chuỗi, có tham số includeSpaces/raw/context. API này không tự chứng minh advance/encoding/font bytes. Dùng native DBText/MText/Attribute trong target DB để đối chiếu GeometricExtents và alignment cuối; đo trong transaction của bản sao; restore WorkingDatabase bằng finally. Không lấy ink width làm advance nếu API chưa chứng minh. Consumer đo whole text sẽ dùng ink bắt buộc và typed advance-unavailable nếu thật sự cần advance.

### Signature và dependency

Canonical block content hash phải gồm geometry entity theo loại và đầy đủ thuộc tính ảnh hưởng đến output, native text/dimension properties, layer/linetype/style dependency hash, INSERT child hashes và dynamic geometry/state. Không đưa handle/ObjectId allocation vào nội dung; handle giữ trong provenance. Không bỏ line/arc/vertex payload vì extents không đổi. Không bỏ unknown entity rồi hash phần còn lại; phải báo ASSET_ENTITY_UNSUPPORTED.

Traversal DFS lưu stack asset IDs; cycle→ASSET_DEPENDENCY_CYCLE kèm cycle path; missing child→ASSET_DEPENDENCY_MISSING. Hash source và target bằng cùng canonical serializer, culture invariant, field order stable, không F3 rounding. ImportIgnore chỉ sau khi hai content hash bằng nhau hoặc target chưa có; target cùng tên khác content→TEMPLATE_TARGET_CONFLICT.

Sản phẩm: inventory cover block không tự thành approved ProductContract. Contract phải có exact plan/section AA/section BB asset, physical outer/aperture/contact/frame height/support cùng nguồn phê duyệt. Bounds block không phải aperture; native preview height không tự là frame height; thiếu product binding→PRODUCT_CONTRACT_INCOMPLETE. Không điền AA/BB empty, aperture[], contacts[] rồi requiresSource=true để coi là đã đủ.

### UnitBinding

UnitBinding khóa bundle_sha256, coordinate_unit, intended_output_unit, scale rational, approved_source_ids, approver và approval time. Với source thực sự dùng ft: 100ft=30480mm; đây là chuyển đơn vị chuẩn, không suy rằng template legacy có INSUNITS Feet đang dùng feet thật. Undefined hoặc metadata trái coordinate binding phải Block nếu chưa có bản chốt cho exact bundle. Không lấy kích thước schematic hoặc fixture để đoán unit. Import target mm; normalize geometry/ATTDEF/product/dimension một lần; native INSERT physical scale theo approved product contract; giữ units receipt kèm hash.

### Active-feature plan và round-trip

Baseline phải dùng toàn bộ VhgTemplateRoles.Presentation đã được audit nguồn; không thay bằng TemplateRoleCatalog.RequiredRoles chỉ có SteelSummary/PanelSummary. Các con số style/frame trong VhgTemplateRoles là contract hiện có, phải truy nguồn asset/quyết định khi ký; không coi việc nằm trong code là nguồn kỹ thuật.

| Feature | Role / tag bắt buộc | Khi inactive |
| --- | --- | --- |
| Presentation baseline | Toàn bộ Presentation, gồm Khung/layer/style/dimstyle/linetype và SteelSummary/PanelSummary | Không được tắt để né lỗi |
| Reinforced catch active | CatchSummary:CUATHU1,CUATHU2; Layer:VHG-HoThu và dependency thật | Không bắt catch asset ngoài scope |
| Dowel hole requested | Geometry/DowelHole và VHG-BT,VHG-VL cùng dependency | Không bắt geometry role toàn cục nếu không request |
| Product requested | Exact approved product plan/AA/BB/contact/aperture | Không dựng placeholder |

Baseline positive: đọc source → validate active plan → kiểm tra unit/font/content → clone vào target mới → insert actual active blocks/attributes đúng contract → save GeneratedTarget.dwg → đóng mọi DB → reopen saved target → raw readback độc lập từng field → compare từng field. Không được chuẩn bị target bằng cách copy Standard/Defpoints từ source để che dependency conflict; nếu built-in conflict phải có migration và receipt rõ ràng.

Negative cases: xóa từng active asset/tag; đổi rotation/modes/height/XScale/lineweight; đổi interior line cùng bounds; đổi child geometry/style; missing font; metadata unit không có binding; conflicting target. Mỗi ca phải có expected diagnostic cụ thể và không Ready. Nếu native catch/Dowel asset thật chưa có thì feature đó Block; không xóa requirement hoặc tắt feature để lấy Passed.

Runner mới sinh unique run folder; giữ command argv, start/end UTC, exact DLL/source/font/input hashes, expected field+actual field+unit+tolerance/source+conclusion. Chỉ exit0 nếu có fresh run ID/sentinel, mọi required field comparison đạt và artifact saved target reopen tồn tại. Nonzero host exit, timeout, stale JSON, thiếu check hoặc bất kỳ false→gate Failed. ActualDWGPath phải là output target thật; source copy không được khai là round-trip output.

Gate L02: independent measurement đúng whole text thực, không fixed activeChars; native valid 2 blocks khác alignment/styles được so từng field; toàn bộ active-feature plan đúng; saved reopen verified; source IDs resolve; field missing/nonfinite/unit/font/dependency có diagnostic đúng. Rủi ro: thiếu font/encoding/unit/product source sẽ làm Block nhiều ca hơn hiện tại; đó là trạng thái thật, không được hạ gate.

## L03 — Complete occupied-region kernel, không vá thêm flat loops

Áp dụng D08 và R03-W03-N01..N05. Candidate native adapter dùng [Region.CreateFromCurves](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Region_CreateFromCurves_DBObjectCollection.html) và [Region.BooleanOperation](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Region_BooleanOperation_BooleanOperationType_Region.html). Việc API tồn tại không chứng minh adapter đã triển khai hoặc đạt; trong R03 chỉ native probe W02 được Executed.

Theo tài liệu Autodesk của CreateFromCurves, các curve đầu vào phải được mở ở ForRead, không phải ForWrite; vi phạm điều kiện này có thể làm AutoCAD crash. Caller chịu trách nhiệm quản lý các Region trả về: bàn giao vào Database hoặc dispose khi không còn dùng.

Không đưa Autodesk types vào Domain/Drawing. Interface IPlanarOccupiedRegionKernel nhận/trả DTO analytic có nguồn; adapter AutoCAD được inject ở composition root. CatchAssemblyProjector/AssemblyBoundaryEmitter/W10 chỉ dùng port; không fallback ngầm sang engine flat loop nếu native provider không có.

| Field lá request | Kiểu / đơn vị | Quy tắc |
| --- | --- | --- |
| request_id / assembly_id / material_id / view_id | string | Ownership exact; không gộp chỉ vì tên tương tự |
| component_regions[].component_id | string | Resolve unique geometry component |
| component_regions[].frame.origin_xyz_mm/basis_u/basis_v/normal | typed frame | Hữu hạn và orientation nhất quán |
| component_regions[].operations[].sequence | int | Thứ tự do source quyết định; không sort theo tên |
| operations[].kind | enum Union/Subtract/Intersect | Exact; unknown phải fault |
| operations[].loops[].id / parent_loop_id / role | string/null/Outer/Inner | Ownership/nesting rõ ràng; không coi mọi Inner là cutter toàn cục |
| loops[].closed / curves[].kind/start/end/center/normal/radius/sweep | analytic typed | Validate toàn bộ curves/domain/closed consumption trước kernel |
| operations[].depth_min_mm/depth_max_mm | double/mm | Của prism part; min<max trừ explicit Empty |
| openings[].host_id/frame/loops/depth/source_ids | typed | Cutter chỉ thuộc host đúng; không trừ all openings khỏi all components |
| view.frame / section_offset_mm / cut_kind | typed | Phân biệt Projection/Section; không chiếu arc nghiêng thành chord |
| tolerance.linear_mm/angular_rad/area_mm2/source_ids | positive finite | Resolve approved rule; missing→CSG_TOLERANCE_UNRESOLVED |
| source_ids[] | strings | Component/operation/view/tolerance đầy đủ |

| Field lá result | Kiểu / đơn vị | Cách tính |
| --- | --- | --- |
| request_id/view_id/assembly_id/material_id | string | Pass-through identity |
| is_empty | bool | Kết quả topology thực; empty hợp lệ không làm sống lại basis |
| regions[].component_sources[] | sourceIDs | Union provenance của occupied component |
| regions[].outer_loop.id/curves[]/signed_area_mm2 | analytic loop/mm² | Native/topology readback; dấu đúng theo orientation |
| regions[].holes[].id/parent_id/curves[]/signed_area_mm2 | analytic loop/mm² | Owned inner boundaries; không bỏ nested topology |
| regions[].islands[] | region tree | Hoặc vùng rời riêng có parent nhất quán; signed occupancy được giữ đúng |
| total_occupied_area_mm2 | double/mm² | Sum outer−holes+islands thực; không sum absolute areas |
| input_edge_count/output_edge_count/unconsumed_edge_ids[] | int/int/IDs | Audit complete consumption; unmatched không được partial success |
| native_region_hash/canonical_geometry_hash/source_ids[] | hashes/IDs | Content identity và provenance |
| diagnostics[].code/subject/field/source/actual/expected | typed | Block nếu unknown capability; không Ready partial |

    BuildSection(request):
        validate tất cả source, frame, curve, loop, depth, tolerance
        for each component (exact ownership):
            occupied = EmptyRegion
            for operation in source sequence:
                create complete owned region with outer/holes/islands
                apply Union/Subtract/Intersect bằng kernel
                failure → fault với componentID, operationID, sources; no partial return
            apply only openings with matching host/depth/frame
            keep component occupied even khi Empty
        union complete occupied regions chỉ trong cùng assembly/material/view scope
        export analytic boundaries+nested ownership
        validate closedness, orientation, area and complete edge consumption
        return Empty success hoặc complete result; không return basis khi Difference empty

Native BooleanOperation có thể tiêu thụ ShapeManager của otherRegion khi thành công; adapter phải clone operand khi cần tái sử dụng và dispose đúng ở mọi nhánh. Không dựa vào một region đã consumed cho operation kế tiếp. Disjoint/tangent components cần giữ multiple returned regions; không chọn FirstOrDefault.

Curve-domain sửa đổi: kind Arc yêu cầu finite radius>0, finite nonzero sweep với 0<abs(sweep)≤2π; endpoints/center/radius/sweep/normal phải nhất quán trong tolerance có nguồn. abs(sweep)=2π là closed circle được support; zero/multiturn/inconsistent→CURVE_DOMAIN_INVALID trước CSG. Native adapter normalize full circle thành Circle hoặc representation closed analytic tương đương; one-arc/two-arc circle phải có cùng occupied area/topology.

Arc frame: transform oriented curve, cả tangent/normal và domain; không giữ scalar positive sweep khi thay basis làm đổi orientation. Lấy point midpoint+tangent từ source analytic, transform qua frame, dựng target arc rồi kiểm tra target midpoint/tangent nằm đúng miền. Nếu phép chiếu không song song, arc chiếu nghiêng thành ellipse phải có analytic ellipse support đã chốt hoặc báo CSG_PROJECTION_UNSUPPORTED; không đổi thành Line. Vertical prism section không phải phép chiếu arc: giao analytic footprint với đường cut để tạo occupied intervals, nhân với part depth, rồi union các section regions qua cùng kernel. Không mở rộng phạm vi sản phẩm 3D.

Tolerance: không Round(x,3). Sort intersection theo section parameter, cluster bằng linear tolerance có nguồn, giữ signed crossing events, quyết định occupied interval từ region membership. Không dùng area tolerance làm linear tolerance; không sửa source tolerance để cho fixture pass.

Gate L03 dùng analytic oracles và public Emit: 10000−1600=8400; cutter 400 nằm trong void vẫn 8400; donut∪island 400=8800; half disk r10=50π; two full circle disks=200π; reflected lower semicircle midpoint y=-10; width .0004×depth 100=.04mm² trong phản ví dụ tolerance đã ghi; input thiếu edge phải fault và không có partial output. Sau đó, W10 native hatch/section phải save/reopen và đối chiếu PDF mẫu bằng tolerance đã duyệt. Chưa chạy thì NotRun.

Rủi ro: coupling host/tolerance và topology readback; native kernel không tự giải quyết source/ownership. Headless không có native provider sẽ Block production planar boolean, không đi đường silent approximation. Có thể triển khai analytic provider khác nếu cũng đạt toàn bộ port contract; đổi provider cần recorded decision và oracle độc lập, không tessellate arc để né.

## L06 — G10 bridge do leader sở hữu, không tạo vòng gate

Spec ownership sửa đổi: G10 gồm G10-W02-InputInvariant, G10-Leader-LegacyConsumer, G10-W06-V2Consumer, G10-NativePadEvidence. W02 local invariant đã đạt; overall G10 chưa Closed. Đây là phân công nghĩa vụ, không phải waiver.

Leader cập nhật ChamberGeometryBuilder legacy: input pad<0→field diagnostic; input pad=0→không gọi Extrusion/không tạo component Pad; pad>0→dựng với độ dày input. Mọi consumer hatch/PadBottom phải derive từ actual component existing, không từ bool tự tạo. Base physical datum phải giữ nguyên theo explicit base geometry source; chỉ pad feature thay đổi.

Gate bridge: cùng actual input, chỉ đổi pad positive→0; validation thành công, legacy Build và V2 không exception; Pad component/hatch/PadBottom count=0; base bounds/datum bằng case positive trong tolerance nguồn; negative pad trả code+field+ExcelSource. Save/reopen native đếm đúng entities và tọa độ. W02 model accepted không thay consumer/native evidence.

L06 được leader làm trên nhánh bridge trước khi mở B triển khai tiếp; không đòi implementer W02 sở hữu file W06 và không mở B chỉ bằng cách tách ID. Do R01/G08/D08 còn P1, chưa giới hạn được rủi ro, đợt A hiện tại NO-GO.

## L07 — Ledger có giới hạn chứng minh rõ ràng

R02-current-byte: giữ historic/current DTO bytes, SHA256 và diff một blank line. Không khẳng định actor/time W11 nếu không có retained transcript. R02-historical-chain: yêu cầu exact W06 After/W07 Before byte versions và lệnh thực tế chứa actor/run ID/time có nguồn. Current dependency hash khớp không chứng minh chronology.

ForensicAmendmentR03 fields: id; finding_id; snapshot_paths[]; snapshot_sha256[]; previous_receipt_sha256; next_receipt_sha256; actor(nullable); time_utc(nullable); raw_evidence_paths[]; certainty enum Verified/Unverified/IrrecoverableGap; explanation; approved_exception_source_id(nullable). Mỗi actor/time không verified phải null và ghi đúng trạng thái. Không fill lại bằng filesystem mtime hoặc guessed chat.

Nếu tìm được historic transcripts thì hash/attach và kiểm tra chain. Nếu không tìm được, ghi IrrecoverableGap, dựng forward-only frozen baseline và immutable changelog từ R03. Historical R02 vẫn StillOpen đến khi chủ nhiệm chấp nhận explicit exception hoặc có evidence phục hồi; việc đồng ý 23 NR cũ không tự là đồng ý waiver lịch sử này. Không cần hỏi để hoàn tất biên bản NO-GO hiện tại.

Gate L07: verified transitions có đủ command/log/hash/actor/time thật; mọi unknown được liệt kê; không sửa old receipt/review; forward receipt hash chain đầy đủ. Lưu riêng local byte Closed và historical StillOpen, không dùng chữ Passed chung.

## Quy tắc nghiệm thu lại và downstream

Mỗi L01/L02/L03/L06/L07 phải bàn giao actual diff+before/after hash, commanded run+raw log, independent expected/actual field/unit/tolerance/source và conclusion. Không chọn “code chạy đúng” hoặc đếm test.

W04/W08 phải consume verified font/asset/recipe context mới; W06/W07 kiểm tra source unit/datums và pad bridge; W09/W10 kiểm tra canonical capture/kernel/actual asset dependency; W11/W12 kiểm tra layout inks và release evidence; W05 port kiểm tra revision khớp; W13 integration chỉ chạy khi các đợt tương ứng đúng GO.

Đợt D=W09/W10 xong mới review; D GO mới W11/W12. Đợt E GO mới W05 port; port GO mới W13. Đợt F cuối bắt buộc native pipeline Executed cùng generated DWG/PDF readback; không lấy native probe W02 của R03 để gán trạng thái đó.

