# HỒ SƠ BẰNG CHỨNG VÀ HASH AUDIT

Review read-only production. Source được chụp trước phân tích; mọi probe compile/run trên frozen source hoặc bản copy có hash tương đương. Diff từ review-01 giữ tại diffs/Wxx. Không coi receipt là bằng chứng thực thi.

| Command key | Command đã chạy | Exit | Log |
|---|---|---|---|
| NATIVE | dotnet build C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\frozen\source\DBTool\DBTool.AutoCAD\DBTool.AutoCAD.csproj -p:AutoCADManagedPath=C:/Program Files/Autodesk/AutoCAD 2026 --artifacts-path C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\root-confirmation\artifacts | 1 | probes\root-confirmation\native-build.log |
| A | dotnet run --project C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\batch-a-integrity-assets\IntegrityProbe.csproj -- C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\root-confirmation\integrity-results.json | 0 | probes\root-confirmation\integrity.log |
| AC | dotnet run --project C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\batch-ac-grouping-csg\BatchAcProbe.csproj | 0 | probes\root-confirmation\csg-grouping.log |
| C | dotnet run --project C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\batch-c-presentation\runner\Probe.csproj | 0 | probes\root-confirmation\presentation.log |
| B | dotnet run --project C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\batch-b\BatchBProbe.csproj -- C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\batch-b\results.json | 0 | probes\batch-b\run.log |
| LEDGER | powershell -File C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261014-batch-review-02/probes/batch-a-integrity-assets/LedgerAudit.ps1 | 0 | probes/batch-a-integrity-assets/ledger-audit-v2.log |

NATIVE exit1 là compile failure đã root tái hiện; không phải host NativeGate Executed. A/B/AC/C exit0 nghĩa observation harness hoàn tất, không nghĩa sản phẩm Passed. Root đã chạy lại A,AC,C để đối chiếu kết quả reviewer và tự viết/chạy B; những phản chứng trọng yếu được xác nhận độc lập.

## Receipt evidence không được chấp nhận chỉ theo lời khai

- W07 RemediatedFindings ghi8 Closed, Evidence là file:dòng; Commands chỉ test projects và code dirs. Không có command-output/predicate measurement liên kết đủ để đóng từng finding. Probes B tái hiện nguyên lỗi dù receipt Passed.
- W06 JSON evidence có expected/actual nhưng thiếu liên kết log/command per predicate; nhiều record không unit/tolerance/source. G12 expected delta25, actual25.43209201606703 vẫn Passed, chưa có sourced tolerance được chứng minh. Quan sát legacy này không chứng minh V2 S4 consumer: B12 cho paths không đổi.
- W04 D05 ghi actual bounds[0,-15,120,35], cùng executable synthetic fixture cho[0,0,1080,150]; thiếu native measurement/log tương ứng. Chưa đủ chứng cứ gán lại Passed.
- W08 coverage test tự ném StageFault trong test body không chứng minh production validator ném lỗi; invalid registry duplicate membership vẫn qua probe độc lập.
- W01/W02/W03 và các gói khác đều có cải thiện local được ghi nhận; synthetic signatures/fixtures và test names không thay native reader/template round-trip hoặc required behavior ngoài fixture.

## Bốn món bắt buộc

Mỗi closure: command thực đã chạy + output/log gắn cùng source/input hashes + independent expected/actual/unit/tolerance/source + kết luận đúng phạm vi. Before/After và dependency hashes cũng bắt buộc. Numerical tolerance chưa nguồn phải Blocked; categorical/hash/protocol predicates dùng exact equality/N/A có lý do, không tự cấp engineering epsilon.

## Phiên bản source và hash

data/receipt-hash-audit.json có63 ChangedFiles records:59 current matches;4 current differences thuộc StageAdapters ba phiên bản và WallRebarPlanner một phiên bản. Ba StageAdapters AfterHash đã được reconstruct khớp exact; WallRebarPlanner được giải thích bằng W06After→W07Before. Không gán mismatch do downstream sửa hợp lệ thành corruption.

data/receipt-version-dependency-audit.json:9 dependency links match;7 PreviousReceiptHash giải được tới previous snapshots. probes/batch-ac-grouping-csg/reconstruction có hash candidates và class-preservation diff. R02 vẫn PartiallyClosed vì present version chain không chứng minh historic mutation actor/time.

data/source-snapshot.json khóa1048 source/config/project files. data/preservation-final.json sẽ ghi frozen integrity và live drift tại lúc bàn giao. Live drift do W09 đang chạy phải được ghi rõ, không sửa frozen snapshot để giả cập nhật.

Chưa thực hiện product native generation/plot/PDF/visual approval. NativeGate=NotRun. Không dùng load DLL hoặc dotnet test không có host runner để đổi thành Executed. Không chạy full suite lặp lại vì đã có native compile blocker và counterexamples; targeted checks/probes được giữ log, không dùng tổng số pass làm căn cứ.
