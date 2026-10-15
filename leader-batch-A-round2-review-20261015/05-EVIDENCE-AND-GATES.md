# SỔ BẰNG CHỨNG / HASH / GATE

Mỗi kết luận phải nối được command→log→expected/actual (unit,tolerance)→verdict. Exit 0 của observation harness chỉ nói harness đã hoàn tất; không thay verdict của từng predicate. Snapshot, receipt, source và log được giữ riêng trước/sau patch.

| Gate | Execution | Acceptance | Giới hạn |
| --- | --- | --- | --- |
| Native compile NEW-A02 | Executed | Đạt predicate compile | 0 errors SDK 2026; không đồng nghĩa G08/G09 Closed |
| W02 native harness nhận bàn giao | Executed | Chưa đủ coverage | 12 checks có phạm vi hẹp; không save imported target/reopen |
| Leader independent native R03 | Executed | Failed | Glyph ink và geometry/dependency hash sai; exit 0 là run completed |
| Full pipeline NativeGate | NotRun | Chưa nghiệm thu | Chưa có generated DWG/PDF integration của pipeline |
| Candidate leader pure guards | Executed | Đạt phạm vi NEW-A01/D11 candidate | Chưa apply live; không đóng G08/D08 |
| Candidate native build | Executed | Compile đạt | Chưa có native functional round-trip của candidate |
| Candidate apply guard | Executed | Đạt trên copy | Live CheckOnly; không ghi đè workspace |

## Hash receipt và diff code thực

| Gói | Receipt SHA256 nhận bàn giao | AfterHash khớp file thật | BeforeHash so R02 |
| --- | --- | --- | --- |
| W01 | 87cf9555151fc1a4b45fb894806f61f9ac78cc705fba9fdbf6a0199976964e14 | 3/3 | 0/3 khớp R02 snapshot |
| W02 | f6d677e4d14ed4d2597924a3a035b72803f6a5b8800102001def223413e7446f | 17/17 | 0/17 khớp R02 snapshot |
| W03 | def1b40b672b05c06f2b8e0a7276f5a9bd82bc5389e9ecb64bc6cc2b8a996091 | 3/3 | 3/3 khớp R02 snapshot |

23/23 AfterHash khớp source, nhưng không chứng minh từng BeforeHash chính là trạng thái ngay trước vòng 2. Một số receipt dùng cumulative BeforeHash từ vòng gốc; W02 PreviousReceiptHash vẫn trỏ receipt gốc, bỏ qua receipt R02. Không tự ghép chronology bằng file mtime. Mọi Before/After/R02 hash giữ tại [audit 23 file](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/data/receipt-hash-audit.json>) và [diff R02→R03](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/diffs>). Đây là một phần R02 ledger còn mở.

## ID không được tráo

NEW-A01 canonical là registry/provenance; NEW-A02 canonical là native compile Layer. File closure mới của implementer đặt tên new-a01 cho typography và new-a02 cho registry không làm đổi ID gốc. Bảng R03 dùng ID canonical; tên test registry cũ được sửa trong candidate.

## Sample unsupported và artifact sai phạm vi

Closure manual ghi A advance 48mm, ink maxY 60mm không thể được converter hiện tại sinh ra: ref height 100, A regular 56/A bold 70 và ink maxY 100. Fresh B-I và native N-RUN giữ actual riêng. Không kết luận về động cơ gian lận; kết luận là sample không có executable evidence hỗ trợ.

Scratch round-trip baseline có SHA256 trùng nguyên bản VHG-Template.dwg:ABF4B2A60EB51DE311401F7C92CF159DD578D5109C113AA10045308DDFB6C234. Đây là copy input, không phải imported target save/reopen. Template nguồn trước/sau native R03 không đổi SHA256.

## A-I

    dotnet run --project 'C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/W01Independent.csproj' -- 'C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/results.json' 'C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/source/DBTool/TestOutput/Session-09/host-20260930T075502510/actual-semantic.json'

Exit code đã quan sát: 0. 

Log/output: [probes/r03-w01/probe.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/probe.log>).

Expected/actual records: [probes/r03-w01/results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/results.json>).
## A-OWN

    dotnet run --project 'C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/owned-fixtures/OwnedProbe.csproj' -- 'C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/owned-results.json'

Exit code đã quan sát: 0. 

Log/output: [probes/r03-w01/owned-probe.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/owned-probe.log>).

Expected/actual records: [probes/r03-w01/owned-results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w01/owned-results.json>).
## B-I

    dotnet run --project "C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/ContractProbe.csproj" -- "C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/contract-observations.json"

Exit code đã quan sát: 0. 

Log/output: [probes/r03-w02/contract-probe.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/contract-probe.log>).

Expected/actual records: [probes/r03-w02/contract-observations.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/contract-observations.json>).
## B-HASH

    Get-FileHash -Algorithm SHA256 -LiteralPath "C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/source/DBTool/DBTool.Drawing/StageContractsV2/Drawing.ContractsV2.cs","C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261014-batch-review-02/probes/batch-a-integrity-assets/Drawing.ContractsV2.historic-exact.txt","C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/packages/W02/rework/receipt.prev.json","C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/frozen/packages/W02/rework/scratch-roundtrip-baseline.dwg","C:/Users/DucBao/Desktop/Dbtool/Ver 1/VHGpro 3.2/Template/VHG-Template.dwg"

Exit code đã quan sát: 0. 

Log/output: [probes/r03-w02/r02-and-dwg-hashes.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/r02-and-dwg-hashes.json>).

Expected/actual records: [probes/r03-w02/r02-and-dwg-hashes.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/r02-and-dwg-hashes.json>).
## B-DIFF

    git diff --no-index -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261014-batch-review-02\probes\batch-a-integrity-assets\Drawing.ContractsV2.historic-exact.txt" "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\frozen\source\DBTool\DBTool.Drawing\StageContractsV2\Drawing.ContractsV2.cs"

Exit code đã quan sát: 1. Exit 1 cho biết hai file khác nhau, chỉ khác một dòng trống cuối; không phải lỗi thực thi.

Log/output: [probes/r03-w02/r02-historic-current.diff](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w02/r02-historic-current.diff>).
## C-I

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\probes\r03-w03\R03W03.csproj"

Working directory: C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\probes\r03-w03.

Exit code đã quan sát: 0. 

Log/output: [probes/r03-w03/run.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w03/run.log>).

Expected/actual records: [probes/r03-w03/results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w03/results.json>).
## C-OWN

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\probes\r03-w03\WorkerRound2.csproj"

Working directory: C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\probes\r03-w03.

Exit code đã quan sát: 0. 

Log/output: [probes/r03-w03/worker-round2.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/probes/r03-w03/worker-round2.log>).
## N-BUILD

    dotnet build "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\probes\root-native\IndependentNativeProbe.csproj" "-p:AutoCADManagedPath=C:/Program Files/Autodesk/AutoCAD 2026" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\native-before\probe-build" --nologo -v quiet

Exit code đã quan sát: 0. Build project probe tham chiếu AutoCAD/Application/Drawing/Domain từ snapshot R03; SDK AutoCAD 2026, target net8.0-windows. 0 errors; warnings được giữ trong log.

Log/output: [native-before/native-probe-build.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/native-probe-build.log>).
## N-RUN

    "C:/Program Files/Autodesk/AutoCAD 2026/accoreconsole.exe" /i "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\native-before\host\active-run.dwg" /s "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\native-before\host\run.scr" /l en-US

Exit code đã quan sát: 0. Sentinel R03_NATIVE_EXECUTED. Exit 0 chỉ chứng minh đã thực thi; các predicate độc lập ghi lỗi.

Log/output: [native-before/host/native-readable.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/native-readable.log>).

Expected/actual records: [native-before/host/independent-native.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/independent-native.json>).

Run metadata/hash: [native-before/host/execution.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/native-before/host/execution.json>).
## P-RED1

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\LeaderGuardProbe.csproj" -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\red-results.json"

Exit code đã quan sát: 1. Candidate trước khi sửa có cùng source byte với frozen. Hỏng đúng predicate cần sửa.

Log/output: [leader-patches/probe/red.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red.log>).

Expected/actual records: [leader-patches/probe/red-results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-results.json>).
## P-RED2

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\LeaderGuardProbe.csproj" -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\red-approval-results.json"

Exit code đã quan sát: 1. Thêm kiểm tra approval/source trước khi sửa các guard đó; 8 nhánh lỗi được tái hiện.

Log/output: [leader-patches/probe/red-approval.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-approval.log>).

Expected/actual records: [leader-patches/probe/red-approval-results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-approval-results.json>).
## P-GREEN

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\LeaderGuardProbe.csproj" -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\green-delivery-results.json"

Exit code đã quan sát: 0. 

Log/output: [leader-patches/guard-delivery.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/guard-delivery.log>).

Expected/actual records: [leader-patches/probe/green-delivery-results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/green-delivery-results.json>).
## P-APPLY

    C:\Users\DucBao\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\verify-apply.py"

Exit code đã quan sát: 0. Chạy apply thật trên bản sao; preimage sai phải chặn toàn bộ, không ghi live.

Log/output: [leader-patches/apply-verification.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/apply-verification.json>).

Expected/actual records: [leader-patches/apply-verification.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/apply-verification.json>).
## T-baseline-DBTool.Domain.Tests

    dotnet test "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\frozen\source\DBTool\DBTool.Domain.Tests\DBTool.Domain.Tests.csproj" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\test-artifacts\baseline\DBTool.Domain.Tests" --nologo -v quiet

Exit code đã quan sát: 0. Kiểm hồi quy code; không dùng số test để đóng finding hay ký DWG/PDF.

Log/output: [leader-patches/baseline-DBTool.Domain.Tests.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/baseline-DBTool.Domain.Tests.log>).
## T-candidate-DBTool.Domain.Tests

    dotnet test "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-candidate\DBTool\DBTool.Domain.Tests\DBTool.Domain.Tests.csproj" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\test-artifacts\candidate\DBTool.Domain.Tests" --nologo -v minimal

Exit code đã quan sát: 0. Kiểm hồi quy code; không dùng số test để đóng finding hay ký DWG/PDF.

Log/output: [leader-patches/candidate-DBTool.Domain.Tests-delivery.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/candidate-DBTool.Domain.Tests-delivery.log>).
## T-baseline-DBTool.Drawing.Tests

    dotnet test "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\frozen\source\DBTool\DBTool.Drawing.Tests\DBTool.Drawing.Tests.csproj" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\test-artifacts\baseline\DBTool.Drawing.Tests" --nologo -v quiet

Exit code đã quan sát: 0. Kiểm hồi quy code; không dùng số test để đóng finding hay ký DWG/PDF.

Log/output: [leader-patches/baseline-DBTool.Drawing.Tests.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/baseline-DBTool.Drawing.Tests.log>).
## T-candidate-DBTool.Drawing.Tests

    dotnet test "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-candidate\DBTool\DBTool.Drawing.Tests\DBTool.Drawing.Tests.csproj" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\test-artifacts\candidate\DBTool.Drawing.Tests" --nologo -v minimal

Exit code đã quan sát: 0. Kiểm hồi quy code; không dùng số test để đóng finding hay ký DWG/PDF.

Log/output: [leader-patches/candidate-DBTool.Drawing.Tests-delivery.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/candidate-DBTool.Drawing.Tests-delivery.log>).
## T-baseline-DBTool.Application.Tests

    dotnet test "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\frozen\source\DBTool\DBTool.Application.Tests\DBTool.Application.Tests.csproj" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\test-artifacts\baseline\DBTool.Application.Tests" --nologo -v quiet

Exit code đã quan sát: 0. Kiểm hồi quy code; không dùng số test để đóng finding hay ký DWG/PDF.

Log/output: [leader-patches/baseline-DBTool.Application.Tests.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/baseline-DBTool.Application.Tests.log>).
## T-candidate-DBTool.Application.Tests

    dotnet test "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-candidate\DBTool\DBTool.Application.Tests\DBTool.Application.Tests.csproj" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\test-artifacts\candidate\DBTool.Application.Tests" --nologo -v minimal

Exit code đã quan sát: 0. Kiểm hồi quy code; không dùng số test để đóng finding hay ký DWG/PDF.

Log/output: [leader-patches/candidate-DBTool.Application.Tests-delivery.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/candidate-DBTool.Application.Tests-delivery.log>).
## P-RED3

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\LeaderGuardProbe.csproj" -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\red-deep-host-results.json"

Exit code đã quan sát: 1. RED cho deep immutability và Angle host; source ở trạng thái trước hai thay đổi hẹp tương ứng.

Log/output: [leader-patches/probe/red-deep-host.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-deep-host.log>).

Expected/actual records: [leader-patches/probe/red-deep-host-results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-deep-host-results.json>).
## P-RED4

    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\LeaderGuardProbe.csproj" -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\red-json-order-results.json"

Exit code đã quan sát: 1. RED cho thứ tự JSON gốc của SourceV2 trước khi thêm property order.

Log/output: [leader-patches/probe/red-json-order.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-json-order.log>).

Expected/actual records: [leader-patches/probe/red-json-order-results.json](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/probe/red-json-order-results.json>).
## P-NATIVE-BUILD

    dotnet build "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-candidate\DBTool\DBTool.AutoCAD.Tests\DBTool.AutoCAD.Tests.csproj" "-p:AutoCADManagedPath=C:/Program Files/Autodesk/AutoCAD 2026" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\native-build" --nologo -v quiet

Exit code đã quan sát: 0. 

Log/output: [leader-patches/candidate-native-build-delivery.log](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/candidate-native-build-delivery.log>).

## Phạm vi kiểm thử

Đã so baseline và candidate bằng cùng các suite liên quan đến thay đổi (Domain, Drawing, Application). Không dựa tổng test pass để đóng finding: source trước patch cũng xanh trong khi phản ví dụ thất bại. Log initial candidate Application failure do expectation 8 char/signature hash cũ được giữ tại [initial failure](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/candidate-DBTool.Application.Tests.log>). Không chạy UI/Excel/PDF visual acceptance vì patch không sửa các tầng đó; native pipeline/PDF gate vẫn NotRun.
