# Phase 2 — Bộ hồ sơ cho DucBao nghiệm thu bằng mắt

**Chưa mở Phase2.** Danh sách PDF sản phẩm hiện có0 mục. `phase2/product-pdf-manifest.json` ghi `Phase1=Blocked_Preflight`, `pages=[]`, `visual_status=NotRun`. Không đưa scratch DWG, PDF mẫu hoặc ảnh unit test vào danh sách sản phẩm đã bàn giao.

## 1. Tài liệu đã chuẩn bị

| Tài liệu | Mục đích | Trạng thái |
|---|---|---|
| product-pdf-manifest.json | Exact generated PDF path/hash/page/frame/case/row và vùng bắt buộc | Rỗng, chờ Phase1 đạt. |
| product-pdf-manifest.schema.json | Schema cần có để mở review | Đã định nghĩa, không phải artifact Passed. |
| region-checklist.json | Checklist theo từng SheetRole/vùng | Đã chuẩn bị. |
| REVIEW-FORM.html / visual-review-template.json / visual-review-template.csv | Mẫu ghi nhận quyết định, reviewer và bằng chứng | Draft/NotRun; không có chữ ký sản phẩm. |
| reference-pdf-manifest.json | 71 PDF nguồn có hash để chọn đúng reference | ReferenceOnly; không phải PDF sản phẩm. |
| phase1-signoff.json | Kết luận kiểm máy và PDF manifest hash | Blocked, chưa ký ReadyForHumanReview. |

Sau Phase1, Leader sinh một dòng danh sách cho **mỗi trang thực tế** của **mọi PDF sản phẩm**. Không chọn vài trang đẹp hoặc một ga đại diện. Nội dung dòng: CaseId/variant, RunId, ManholeId/row, FrameId/SheetRole, generated PDF path/SHA256/page, reference PDF/path/hash/page, ComparisonMode, input/build/rule/template/plot hashes, required region IDs và link ảnh compare. Nếu chưa xác nhận đúng input thì reference chỉ là RecipeOnly, không dùng số trên reference để kết luận số của ga current sai.

## 2. Checklist từng vùng theo loại tờ

| SheetRole | Các vùng phải xem | Dấu hiệu không đạt |
|---|---|---|
| Structure | Mặt bằng, AA, BB; khung/tiêu đề/tỷ lệ; DIM và cao độ; nắp/khung/cổ/cống; vật liệu/hatch | Sai kích thước/khẩu độ/cao độ, view thiếu, lỗ không đồng bộ, seam giả, che DIM/chữ hoặc khung cắt ink. |
| BaseWalls12 | Thép đáy dưới/trên; T1/T2 trong/ngoài; labels/collectors/ticks; vật liệu và footprint | Thanh hoặc view mất, Gach ở chỗ không có thanh, ghi nhầm nhóm, cover/cắt lỗ không nhất quán. |
| Walls34Roof | T3/T4 trong/ngoài; thép trần trên/dưới; lỗ trần/chốt; labels/collectors | Chốt cong/đổi vị trí, hole thiếu pair, nhóm T1A/T1B/T2A/T2B sai vùng, nhãn chồng. |
| NeckAndSchedule | Cổ/mặt cắt/thép cổ; frame100; thống kê Body/Neck và tổng hợp | Cổ không theo selected cover, high datum sai, bảng/tiêu đề/summary thiếu hoặc không cùng mark. |
| NeckCatch | Cổ nếu active; CatchPlan/11/22 và thép cửa thu; sản phẩm nắp/lưới; cao độ/labels | Cửa thu giữ contour tĩnh khi input đổi, mất view khi cổ không BTCT, sai depth/height/owner. |
| ScheduleOnly | Toàn bộ hàng TK, header/owner headings, summary và các trang nối | Thiếu hàng/nhóm, tổng con trái tổng, ký tự/font sai, hàng text giả thay block, continuation mất header. |
| CoverConstruction | Mặt bằng/mặt cắt/thép panel; quantity/gap/support; sản phẩm native; Panel TK | Nhân quantity hai lần, support/gap sai, stretch sản phẩm, thiếu panel rows hoặc anchor/lifting notes. |
| Bending | Mọi fixed glyph, caption L/Ltb, leg roles, mark/Ø/count, trang overflow | Dùng schematic để quyết định chiều dài, glyph scale theo Li, caption/mark khác TK, hình uốn bị xóa để vừa trang. |

Mọi trang còn có các vùng chung: **V01 khung và role/tỷ lệ; V02 hình học/view bắt buộc; V03 DIM/cao độ; V04 nhãn/leader/tick; V05 TK/bending nếu áp dụng; V06 hatch/linetype/lineweight; V07 dấu tiếng Việt/font/readability; V08 khoảng hở và không cắt ink; V09 liên kết ID/mark giữa các tờ.** Leader phải bind từng vùng tới page/view/bounds thật trong manifest, không để junior tự đoán tọa độ vùng.

Vùng không áp dụng chỉ N/A khi predicate input và SourceId/RuleId chứng minh (ví dụ không panel); không dùng N/A vì không có hình được sinh. Mọi feature ON cần vùng/output hoặc diagnostic trước Phase1 signoff. Khi nhiều trang overflow, từng trang có checklist riêng và source-role tương ứng.

## 3. Cách DucBao xem và ghi nhận

1. Xác nhận build/input/PDF manifest hash đúng bộ Leader bàn giao; mở PDF sinh mới cạnh reference đúng mode, cùng trang/zoom. Có thể dùng PDF viewer bất kỳ, không đổi UI sản phẩm DBTool.
2. Xem lần lượt từng vùng bắt buộc, kiểm số/mark/DIM/CT, bố cục/độ đọc/leader/TK/bending và liên kết sang tờ liên quan. Ảnh overlay chỉ hỗ trợ; quyết định dựa trên PDF thật.
3. Ghi **Đạt / Không đạt / Chưa xem / N/A có nguồn** cho từng vùng. Không đạt ghi vị trí/page/region/mark, mô tả cụ thể và ảnh chú thích. Nghi số học thì trả Leader đo lại native, không sửa số trên PDF.
4. Xác nhận reviewer=DucBao, thời điểm thực, exact generated PDF SHA256/page/frame, required region IDs và quyết định cho từng vùng. Form là draft cho đến khi bằng chứng này đủ và Leader kiểm lại manifest/hash.
5. Một vùng bắt buộc Không đạt → page Failed. Một vùng bắt buộc Chưa xem → page NotRun. Chỉ mọi vùng bắt buộc Đạt, mọi N/A hợp lệ và toàn bộ artifact identity khớp mới page Passed. Mọi page/row áp dụng phải Passed mới có thể ký nghiệm thu cuối; không lấy tỷ lệ phần trăm trang đạt để chấp nhận phần còn lỗi.

Mỗi record phải có `ReviewId, Phase1SignoffHash, CaseId, RunId, ManholeId, FrameId, PdfPath, PdfSha256, PdfPage, ReferenceSha256, ComparisonMode, RegionId, RegionBounds, Required, Decision, Comment, AnnotationPath, Reviewer, ReviewedAt`. ReviewedAt để null trước khi xem. Không ký thay DucBao, không tái dùng quyết định phê duyệt23 NR làm review PDF.

## 4. Hiệu lực và quay lại sửa

PDF/hash, input, source build, rule/template/font/plot đổi → review các trang bị ảnh hưởng mất hiệu lực. Giữ review cũ để truy vết, tạo revision mới và Leader chạy lại Phase1 liên quan trước khi DucBao xem lại. Không sửa record Failed thành Passed khi chưa có bộ PDF/hash mới và lần xem thật.

Nếu cần UI nghiệm thu sau này: hai khung PDF cạnh nhau, chọn page/region, hiện hash/mode, checklist có trạng thái chưa xem, ghim ảnh lỗi, export JSON/CSV và bộ review. Công cụ này tách khỏi UI DBTool; không tự tính visual Passed hoặc tự tạo dữ liệu native. Trong lượt review hiện tại, bàn giao form draft và checklist; không tuyên bố đã có full PDF comparison app vận hành.
