# 04 — Quyết định chủ nhiệm và NeedsRule hợp nhất

**23/23 đề xuất đã được chủ nhiệm DucBao chấp thuận**, hiệu lực 13/10/2026, phiên bản `owner-20261013-r2`. Nguồn là chỉ thị trực tiếp: “23 needs rule: tôi đồng ý toàn bộ đề xuất của bạn, bạn hãy cập nhật mọi thứ liên quan”. Không yêu cầu duyệt lại 23 đề xuất. Bản trước khi duyệt được giữ trong history.

ApprovedProposal xác nhận phương án được chọn. Runtime vẫn phải kiểm các field dữ liệu số, sản phẩm và hình dạng bắt buộc cùng SourceRef. Thiếu dữ liệu báo `RULE_BINDING_DATA_MISSING` kèm field cụ thể; không trả `RULE_UNAPPROVED` cho 23 đề xuất đã ký hoặc tự tạo số fixture mặc định. Phê duyệt hồ sơ không phải nghiệm thu DWG/PDF.

| ID | Vấn đề | Đề xuất đã duyệt nguyên văn | Trạng thái / scope |
|---|---|---|---|
| NR-01 | Khoảng bảo vệ lỗ: tim hay bề mặt | Chốt Protect.H là surface cover hoặc nominal centerline riêng. Đề xuất giữ physical surface cover+Ø/2, cho presentation nominal adapter chỉ khi owner xác nhận; không đổi H17=15 về10. | ApprovedProposal; S4 cut mesh và Li |
| NR-02 | C năm đoạn/R8 và chiều dài thống kê | Chốt nominal/tangent/centerline cho 5 cạnh, R8 là inner hay centerline và công thức DAI; đề xuất lưu riêng path fabrication và nominal schedule, không dùng DAI190 thay path. | ApprovedProposal; S4 C;S8 C quantity |
| NR-03 | Ties Đều/Lệch với pitch bất kỳ | Chốt quy tắc chọn station khi pitch hai phương khác, pha hàng/cột và tiePitch không bằng2meshPitch. Đề xuất chọn station có ràng buộc maximum pitch, không snapping âm thầm; cần sơ đồ owner phê chuẩn. | ApprovedProposal; S4 tie station |
| NR-04 | Shape C/U/Z theo đáy/tường/trần/cổ/panel | Chốt bảng cạnh fixed/input/derived cùng lớp đỡ từng component×shape. Đề xuất đầu/giữa lấy J/K/L đúng role, chiều nối lớp lấy stack có cover; shape chưa có nguồn không fallback. | ApprovedProposal; S4 tie shape;S8 binding |
| NR-05 | Thép vạt và neo phân bố | Cần typed spec Ø/phân bố/neo/bend/basis cho đáy/trần; đề xuất giữ occurrences hiện có Partial và kiểm toàn path trong slab+haunch+wall union trước cấp fabrication length. | ApprovedProposal; S4 haunch |
| NR-06 | Chốt: tròn50, phân neo và lỗ sâu | Chốt midpoint rounding50, điểm đầu/cuối, chiều neo mỗi assembly và holeDepth. Đề xuất pair station chung, không tự chia length/2 nếu không nguồn; collision trả ID, không tự xóa/dời. | ApprovedProposal; S4 dowel;S6 hole |
| NR-07 | Móc nâng trần/panel: hình và length basis | Chốt role A/B/C, bend radii/anchorage cho hai scope; đề xuất roof0/2/4, panel1/2/4 giữ khác nhau; dùng TK.MC khi shape được xác minh, không TK.M.C. | ApprovedProposal; S4 lifting;S8 |
| NR-08 | Cửa thu: physical shape1..4 và TK.CT | Chốt mapping marks1..4 tới floor/side/front, bend và TK.CT.N/T. Đề xuất dựng mesh một lớp theo từng mặt support, annotate theo GroupId; số/Ø/pitch từ M17, không Ø10@150 mẫu. | ApprovedProposal; S4 catch;S8 CT |
| NR-09 | Sản phẩm cửa thu bị co trong nguồn | SCR Y=0.6470588235294118, NAPCUATHU2 X=0.71875; đề xuất chọn sản phẩm/catalog extension đúng aperture, giữ1:1. Owner chốt cho source schematic exception nếu muốn; tuyệt đối không tự stretch vật lý. | ApprovedProposal; S2 catch product;S6 |
| NR-10 | C3 ở nhãn, C3A ở TK | Đề xuất alias explicit theo Neck/role/face và source handles sau khi xác nhận cùng members; owner chốt alias hoặc sửa lỗi source. Trước đó giữ hai identities, không fuzzy join. | ApprovedProposal; S5 neck mark;S8 |
| NR-11 | Alias tấm nắp tại685CE/685D5 | Owner xác nhận target và SH/Ø của hai nhãn BB so với1A/2A. Đề xuất regenerate nhãn từ members/Ø đã resolve, lưu source exception thay sao lỗi hoặc rename toàn cục. | ApprovedProposal; S7 panel source-equivalence |
| NR-12 | Đoạn giữa nhiều lỗ và suffix mở rộng | Chốt DisplayMark cho Middle/MultipleOpenings và nhiều vùng cùng role. Đề xuất key chứa OpeningRegionSet và ordinal theo tọa độ ổn định, chỉ đặt suffix sau phê chuẩn; không T1C/D tự duyệt. | ApprovedProposal; S5 multi-opening naming |
| NR-13 | DAI, TDAI, KL: rounding midpoint và precision | Nguồn chứng minh nhiều DAI nguyên và TDAI/KL hai số lẻ, chưa phân biệt midpoint. Đề xuất mm0, m/kg2, AwayFromZero lúc hiển thị; owner phải chốt, raw sums không đổi. | ApprovedProposal; S8 final formatting |
| NR-14 | Mass policy và bucket thang/chốt | Chốt D²/162 hay density, quantity basis và component bucket của thang/chốt/lifting; header Ø<=10/>10 đã rõ. Đề xuất physical owner bucket, explicit legacy mapping nếu cần nguồn; không cộng formatted cells. | ApprovedProposal; S8 KL/summaries |
| NR-15 | Template canonical và migration cùng tên | Chốt một bundle definition/symbol canonical. Đề xuất acad-original giữ signature gốc và migration version có hash; native-current có khác font/style nên không trộn chỉ vì cùng tên. | ApprovedProposal; S0 production import |
| NR-16 | Font/encoding và glyph có dấu trong nguồn | Original Regular SHX, native-current bảng HTML Arial; PDF012 có glyph hiện dấu hỏi. Đề xuất FontPolicy khai báo nguồn/alias/encoding, output Việt đúng literal, đo ink; owner chốt tương đương font thay thế, không âm thầm sửa shared style. | ApprovedProposal; S0/S10 visual |
| NR-17 | Hatch default khi thiếu pattern | Note cho fallback nhưng không đặt tên default. Đề xuất chỉ fallback được named trong project profile đã duyệt; thiếu cả default thì Block, log requested/resolved/pattern scale. | ApprovedProposal; S0/S7 material |
| NR-18 | Plot profile và dung sai nghiệm thu | Chốt profile CTB/STB và numerical/visual tolerances theo đơn vị; phân biệt nguồn hairline và profile cải thiện. Đề xuất exact semantic dims theo Excel và numeric policy tách paper glyph/endpoint; chưa ký tolerance không gọi toàn diện Passed. | ApprovedProposal; S10/S11 acceptance |
| NR-19 | Thứ tự lớp chồng/móc/neo khi va biên | Policy hiện ghi TwoDirectionsTouchingTestOnly/UniformBaseTestOnly, chưa là rule chủ nhiệm. Đề xuất bảng stack theo component/layer/Ø và mặt đỡ; thiếu không tự thu ngắn hook hoặc đổi cover. | ApprovedProposal; S4 physical stack |
| NR-20 | Cống đôi: web, contour và overlap assembly | CF.Cong cho width/thickness/chamfer/DoubleFlag; khi web khác thickness hoặc opening cắt qua nhiều host cần entry/spec có nguồn. Đề xuất reject thông số chưa đủ và giải ownership theo contour thật, không gán tường tùy ý. | ApprovedProposal; S2/S3 complex openings |
| NR-21 | Vị trí ladder/chốt va cửa thu hoặc cống | Chốt dời/loại/thay cấu tạo cho station va. Đề xuất Block đúng station+SourceRef cho tới khi có relocation rule; vẫn xử lý stations độc lập, không blanket loại3 unsupported và không tự bỏ thanh. | ApprovedProposal; S4 collision |
| NR-22 | Khoảng hở chữ và scale tự chọn | Source offsets5/1mm chỉ scoped recipe; chốt min ink gap, scale auto candidates và overflow single-view. Đề xuất fixed scale phân trang giữ role, view đơn quá lớn trả LAYOUT_SINGLE_VIEW_NO_FIT, chỉ auto đổi scale khi request cho phép. | ApprovedProposal; S7/S9 large pages |
| NR-23 | Phạm vi khóa clear cho nắp BTCT tự thiết kế | Owner chốt cách chọn nắp trước khi khóa clear nếu BTCT panel tự thiết kế chưa có sản phẩm/aperture catalog độc lập. Đề xuất selected PanelAssemblyContract có aperture/quantity/support được duyệt; không lấy rawE11 làm authoritative âm thầm để né khóa clear. | ApprovedProposal; S2 BTCT custom panel |

## 4.0a. Mismatch mới và disposition liên quan

| ID | Mâu thuẫn phát hiện | Cách xử lý cụ thể | Status / nguồn |
|---|---|---|---|
| NR-IO-001 | Stage payload/05C chưa cùng schema;hai nhánh S7/S8 thiếu carrier rõ | Dùng cumulative immutable stage envelopes; Application adapters wrap leaf payload, Drawing planners không tham chiếu Application. Output N==Input N+1 by generated schema. | ResolvedArchitecture; Source05C/StageAdapters;leader software decision |
| NR-IO-002 | 205 input fields thiếu Material.G vữa đệm | Thêm F-206:CF.Ga!G34 loại2,!G36 loại3;only active BeddingMortar;không lấy Material.F hoặc M75 mẫu. | ResolvedSourceCorrection; Original CF.Ga material C:G headers |
| NR-IO-003 | W/bậc cổ/khung100/FinishedTop chưa khép kín cao độ | Giữ FrameBottom=NeckTop,FrameHeight100;contact equations của selected NR23 contract phải xác định NeckTop/PanelBottom/FinishedTop;thiếu field→DATUM_GRAPH_DISCONNECTED. | ApprovedPolicyBindingRequired; CONTRACT C01;NR23 accepted selected contact contract |
| NR-IO-004 | Legacy cửa thu dùng clearHeight như externalHeight | floorTop=hostTop-clearHeight;floorBottom=floorTop-base;source clearHeight note NOTE0411,PDF040 clear250/base125 chỉ evidence. | ResolvedSourceCorrection; CF.Ga G16/G17 type3;PDF040 |
| NR-IO-005 | Panel formula cũ luôn n+1 gaps;Cover=None chưa có aperture contract | Tách Recess/OverOuter theo CoverPanelBuilder;NoCover chỉ dùng explicit selected no-cover aperture/contact,không tự rawE11 authoritative. | ResolvedContractWithBinding; CoverPanelBuilder;NR23 approved proposal |
| NR-IO-006 | LadderTop legacy nhiều cao độ;note chỉ đỉnh ga | NR19 binding ladder_top_surface_id+min_z_expr bắt buộc;missing→RULE_BINDING_DATA_MISSING;không tự RoofTop/FinishedTop. | ApprovedPolicyBindingRequired; EX083;LadderPlanner;NR19 |
| NR-IO-007 | Haunch scalar chưa chứng minh b=h;C/R8 chưa chứng minh radius basis | Bắt buộc width/height expressions NR05;recipe fabrication centreline and nominal schedule separately NR02;missing payload blocks active feature. | ApprovedPolicyBindingRequired; EX020/026;NR02/05 approved proposals |
| NR-IO-008 | Catch Left/Right legacy sign so FRAME-001 khác observer | Center invariant;Neck forced Center;Body Left/Right requires named side_basis in NR19 placement binding;both signs explicitly implemented. | ApprovedPolicyBindingRequired; WallFrame FRAME001;CatchBasinBuilder;EX177 |
| NR-IO-009 | Draft box outer contour dùng offset Euclidean của inner chamfer | Outer là rectangle W+2t,H+2t;cut rectangle thêm2m;chamfer chỉ inner cells;surface cover clipping vẫn Euclidean offset riêng. | ResolvedSourceCorrection; PipeCrossSectionBuilder.BuildBox |
| NR-IO-010 | Draft activity F13/G13 gắn nhầm BTCT panel | EX042 chỉ Gang/Purchased;G13 là collar phía trên cổ. Active collar phải có selected source-backed profile/contact expressions;không thay Frame100 hoặc bỏ feature. | ResolvedSourceWithBinding; EX042/043;ManholeValidator collar branch;NR23 contact contract |
| NR-IO-011 | S7 cần anchor title/cao độ nhưng registry S6 chưa khai báo; lane thiếu side và nhầm port với insertion | S6 đăng ký Target2V2 ViewTitle/Elevation có ID/frame/point từ biểu thức nguồn. S7 join đúng target, local lane có side; tính vùng cấm theo ink tương đối với port sau scale/rotation. Nhánh accessory không sinh Gach của lưới; helper finite allocator có thân đầy đủ. | ResolvedArchitectureWithSourceBinding; 05C Target2V2;CONTRACT C06;NR16/22 đã duyệt;leader software correction |

## 4.1. Các xung đột có quyết định rõ hoặc đã sửa nghiên cứu

| ID | Mâu thuẫn | Kết luận có nguồn | Nguồn |
| --- | --- | --- | --- |
| R-01 | Khung80 so owner100 | Owner13/10 chốt100 cho khung riêng, không sản phẩm | CONTRACT C01 |
| R-02 | Một row=một canonical so nhóm mean | Owner13/10 cho presentation nhiều canonical, giữ invariant fabrication | OWNER §3/4 |
| R-03 | T1A/B direction suy luận cũ | Owner13/10 T1A/B trái/phải,T2A/B trên/dưới theo convention | OWNER §3 |
| R-04 | 337 notes chưa đọc | 528 occurrence-191 unique=337 duplicate occurrence, không phải 337 rule mới | EXCEL-NOTES/EXCEL-RULES-REVIEW + full raw equality |
| R-05 | 12 block phụ từng NeedsRule | Vai trò/dependency đã được audit; lựa chọn sản phẩm còn NR09/15 | catalog/CONDITIONAL-ASSETS-AUDIT |
| R-06 | PDF056/064 ghép ngược | Sửa research join056→F024,064→F031, giữ glyph pending | CAD-PDF-JOIN-REVIEW |
| R-07 | M15 là phần trăm | Note rõ tỷ lệ chi tiết1:M15, không phần trăm | EX-059 |
| R-08 | Cổ cửa thu Left/Right | HDSD p13 chốt cổCenter bất kể rawLeft/Right; body vẫnLeft/Center/Right | HDSD p13;EX-177 |
| R-09 | Hairline luôn lỗi | Nguồn loại 2/3 có hairline; profile cải thiện cần chốt riêng NR18 | pdf/plot-policy-observed.json |
| R-10 | LWPOLYLINE78DD3 thiếu route trái | Chuyển OCS theo extrusion riêng cho 43/43 labels trên route; không lỗi renderer DBTool | PDF040 deep evidence/path_records |

## 4.2. Đối soát mọi chỗ NeedsRule cũ

[data/needsrule-source-mentions.json](data/needsrule-source-mentions.json) giữ 60 đoạn HTML tìm được với source/element/text, có disposition cho từng đoạn. Các nhắc chung “không gọi Final”, “chưa native reproduced”, “chưa đọc từng glyph” là yêu cầu bằng chứng, không tạo thêm quyết định kết cấu. Nhiệm vụ kiểm Li/membership của nhóm chưa audit thuộc phase06; alias/mass/basis tương ứng NR02/10/14.

Không dùng bảng này để gắn mọi feature hiện experimental thành approved. Tại runtime, RuleBinding không có source/approval vẫn bị gate kể cả khi NR không nhắc tên riêng đó; phải bổ sung register với case/field khi phát hiện trong implementation. Không được tự đổi ExperimentalSoftwareFixture thành Approved.

## 4.3. Disposition sau khi đọc từng đoạn nguồn

60 đoạn là các vị trí nguồn, có đoạn lặp; không phải 60 quyết định mở. Bảng này giải thích cách gom vào register 23 mục phía trên hoặc đưa thành công việc kiểm chứng. Nội dung nguyên văn và vị trí element vẫn ở JSON liên kết trong 4.2. Mapping dưới đây đã được rà theo nghĩa, thay cho candidate từ tìm từ khóa.

| Vị trí | Nguồn | Register / disposition | Lý do và công việc tiếp theo |
|---|---|---|---|
| MENTION-001 | BAO-CAO-TIEN-DO-VA-BAN-GIAO-20261013.html | NR-01, NR-13 | Quyết định mở đã gom theo đúng topic tại NR-01, NR-13. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-002 | BAO-CAO-TIEN-DO-VA-BAN-GIAO-20261013.html | NR-10, NR-14 | Quyết định mở đã gom theo đúng topic tại NR-10, NR-14. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-003 | catalog/CONDITIONAL-ASSETS-AUDIT.html | ResolvedSourceRole_R05 | 12 definition đã có audit vai trò; giữ resolution R-05, không tạo câu hỏi owner mới. |
| MENTION-004 | CAU-TAO-UNION-CHOT-VAT-CUA-THU.html | NR-05 | Quyết định mở đã gom theo đúng topic tại NR-05. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-005 | CAU-TAO-UNION-CHOT-VAT-CUA-THU.html | NR-08 | Quyết định mở đã gom theo đúng topic tại NR-08. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-006 | CAU-TAO-UNION-CHOT-VAT-CUA-THU.html | NR-06, NR-21 | Quyết định mở đã gom theo đúng topic tại NR-06, NR-21. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-007 | CAU-TAO-UNION-CHOT-VAT-CUA-THU.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Yêu cầu capability có consumer hoàn chỉnh; implementation S2/S4/S6/S8, không parameter kết cấu riêng. |
| MENTION-008 | CAU-TAO-UNION-CHOT-VAT-CUA-THU.html | NR-05 | Quyết định mở đã gom theo đúng topic tại NR-05. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-009 | CURRENT-CODE-FINDINGS.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Trạng thái và transcript phải kiểm chính run; acceptance S10/S11, không suy Final từ tên thư mục. |
| MENTION-010 | CURRENT-CODE-FINDINGS.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-011 | CURRENT-CODE-FINDINGS.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-012 | EXCEL-CONSUMER-AUDIT.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Hit tĩnh chưa là consumer proof; field-consumer trace tại P1/P3/P6. |
| MENTION-013 | OWNER-RULES-THONG-KE-NHOM-20261013.html | NR-10 | Quyết định mở đã gom theo đúng topic tại NR-10. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-014 | pages/G500-CUT-MEMBERS-AUDIT.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-015 | pages/G500-CUT-MEMBERS-AUDIT.html | NR-13 | Quyết định mở đã gom theo đúng topic tại NR-13. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-016 | pages/PDF-001-PHAN-TICH-CHI-TIET.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Entity-purpose VHG-150 cần kiểm handle/role ở S3/S6; đây là research/verification pending, không phải plot tolerance NR-18. |
| MENTION-017 | pages/PDF-002-PHAN-TICH-CHI-TIET.html | NR-10 | Quyết định mở đã gom theo đúng topic tại NR-10. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-018 | pages/PDF-002-PHAN-TICH-CHI-TIET.html | NR-02 | Quyết định mở đã gom theo đúng topic tại NR-02. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-019 | pages/PDF-005-COLLECTOR-MAP.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Collector association không chứng minh physical members; kiểm S4/S5/S7, không tự tạo owner rule. |
| MENTION-020 | pages/PDF-007-PHAN-TICH-CHI-TIET.html | NR-10, NR-14 | Quyết định mở đã gom theo đúng topic tại NR-10, NR-14. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-021 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Gate chống tắt feature/số giả; đã thành invariant S0–S11. |
| MENTION-022 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Issue-status/run-manifest là implementation evidence, không owner parameter. |
| MENTION-023 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | NR-02, NR-10 | Quyết định mở đã gom theo đúng topic tại NR-02, NR-10. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-024 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Trạng thái và thứ tự triển khai đã giữ trong 06; không quyết định kết cấu mới. |
| MENTION-025 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | NR-10 | Quyết định mở đã gom theo đúng topic tại NR-10. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-026 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Shape-role recipe thiếu phải review glyph/native source tại S8. Quy tắc kết cấu tương ứng dùng NR-02/04/05/07/08 khi có liên quan; không fallback. |
| MENTION-027 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | NR-01, NR-13, NR-19 | Quyết định mở đã gom theo đúng topic tại NR-01, NR-13, NR-19. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-028 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-029 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Một registry trước ba exporter là owner rule đã rõ; recipe chưa verify là việc S8, không mặc định câu hỏi C-shape NR-02. |
| MENTION-030 | PROMPT-THUC-THI-CAP-NHAT-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Các phép biến thiên/identity/trace thuộc acceptance matrix 06, không owner parameter mới. |
| MENTION-031 | PROMPT-THUC-THI-CHUAN-HOA-DRAFT.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Consumer/code/native/visual/batch còn pending ở source; đã đưa dependency và acceptance 06, không tuyên bố hoàn tất phần mềm. |
| MENTION-032 | PROMPT-THUC-THI-CHUAN-HOA-DRAFT.html | NR-02, NR-03, NR-04, NR-05, NR-06, NR-07, NR-08, NR-19, NR-21 | Quyết định mở đã gom theo đúng topic tại NR-02, NR-03, NR-04, NR-05, NR-06, NR-07, NR-08, NR-19, NR-21. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-033 | PROMPT-THUC-THI-QUY-TRINH-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Tham chiếu asset inventory và TK schema; nguồn đã giữ trong 01, không câu hỏi mới. |
| MENTION-034 | PROMPT-THUC-THI-QUY-TRINH-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Quy tắc nguồn ưu tiên và gate đã có owner; áp dụng 01/04/05. |
| MENTION-035 | PROMPT-THUC-THI-QUY-TRINH-DBTOOL.html | NR-05 | Quyết định mở đã gom theo đúng topic tại NR-05. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-036 | PROMPT-THUC-THI-QUY-TRINH-DBTOOL.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-037 | PROMPT-THUC-THI-QUY-TRINH-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Yêu cầu báo cáo trạng thái thực; 06.4/6.7. |
| MENTION-038 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Nguồn ưu tiên và xung đột không tự chọn phe; 01/04/05.2. |
| MENTION-039 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Đây là header bảng assembly, không chứa quyết định mở độc lập. |
| MENTION-040 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Thứ tự mesh/clip/hook/accessories đã rõ; 05.7 giữ dowel sau hook. |
| MENTION-041 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | NR-04 | Quyết định mở đã gom theo đúng topic tại NR-04. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-042 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-043 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | NR-12 | Quyết định mở đã gom theo đúng topic tại NR-12. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-044 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | NR-08, NR-14 | Quyết định mở đã gom theo đúng topic tại NR-08, NR-14. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-045 | QUY-TRINH-CODE-BIEN-THIEN-VA-SUA-DBTOOL.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Giới hạn evidence/hồ sơ không chứng minh product; acceptance 06. |
| MENTION-046 | SO-TAY-CACH-SUA-CHI-TIET-20261013.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Quy tắc issue-status và tiếp tục việc độc lập; 06.3. |
| MENTION-047 | SO-TAY-CACH-SUA-CHI-TIET-20261013.html | NR-02, NR-04, NR-19 | Quyết định mở đã gom theo đúng topic tại NR-02, NR-04, NR-19. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-048 | SO-TAY-CACH-SUA-CHI-TIET-20261013.html | NR-13 | Quyết định mở đã gom theo đúng topic tại NR-13. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-049 | SO-TAY-CACH-SUA-CHI-TIET-20261013.html | NR-02, NR-10 | Quyết định mở đã gom theo đúng topic tại NR-02, NR-10. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-050 | SO-TAY-CACH-SUA-CHI-TIET-20261013.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-051 | SOURCE-CONFLICTS.html | NR-10 | Quyết định mở đã gom theo đúng topic tại NR-10. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-052 | SOURCE-CONFLICTS.html | NR-02 | Quyết định mở đã gom theo đúng topic tại NR-02. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-053 | SOURCE-CONFLICTS.html | NR-11 | Quyết định mở đã gom theo đúng topic tại NR-11. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-054 | SOURCE-CONFLICTS.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Association nguồn không phải physical membership; kiểm S4/S5/S8. Không lấy DAI từ collector/Gach. |
| MENTION-055 | SOURCE-CONFLICTS.html | ImplementationOrVerificationRequirement_NotAdditionalOwnerRule | Nhắc lại đoạn source cùng nội dung; giữ vị trí gốc, cùng disposition với đoạn trước. |
| MENTION-056 | SOURCE-CONFLICTS.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-057 | SOURCE-CONFLICTS.html | NR-01 | Quyết định mở đã gom theo đúng topic tại NR-01. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-058 | THONG-KE-THEP-NATIVE-CACH-SUA.html | NR-02 | Quyết định mở đã gom theo đúng topic tại NR-02. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-059 | THONG-KE-THEP-NATIVE-CACH-SUA.html | NR-04, NR-05, NR-07, NR-08 | Quyết định mở đã gom theo đúng topic tại NR-04, NR-05, NR-07, NR-08. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
| MENTION-060 | THONG-KE-THEP-NATIVE-CACH-SUA.html | NR-02, NR-04, NR-05, NR-07, NR-08 | Quyết định mở đã gom theo đúng topic tại NR-02, NR-04, NR-05, NR-07, NR-08. Các kiểm native/member/visual đi cùng vẫn thuộc phase triển khai, chưa được đánh Passed. |
