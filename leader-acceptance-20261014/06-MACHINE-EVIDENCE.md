# Bằng chứng máy và giới hạn quan sát

Các file dưới đây được tạo trong review, tách khỏi13 receipt tự khai. Không dùng số test pass để nghiệm thu sản phẩm.

| Hoạt động | Observed | Log/artifact |
|---|---|---|
| Build solution | Exit0; 0 errors, 5 MSB3277 warnings | [Build log](logs/build.log) · [Binary log](logs/build.binlog) |
| Software tests | 1508/1508 qua trong5 suites;0 failed/skip | [TRX index](data/fresh-test-results.json) |
| AutoCAD.Tests project | dotnet test chỉ restore; không có test SDK/runner, không host test | [Log](logs/DBTool.AutoCAD.Tests.log) |
| DTO field/type | 103 expected ==103 actual; mismatch[] | [Schema comparison](data/dto-schema-comparison.json) |
| Counterexamples | 6 CounterexampleObserved; không có ProbeError ở run cuối | [JSON](probes/results/counterexamples.json) · [Run log](logs/counterexamples.log) |
| Core Console load-only | Exit0; NETLOAD/VEHOGABATCH executed; missing workbook rejected | [Transcript](native-probe/transcript-utf8.log) · [Result](data/host-load-result.json) |
| Source protection | Nguồn scratch DWG giữ hash; input copy của probe đã bị Core thay đổi | [Input hashes](native-probe/input-manifest.json); chỉ bản sao nằm trong review output. |
| Dependency hashes | 19 links đúng, mtime tương thích;1 file-chain gap và1 current hash mismatch | [Receipt chain](data/receipt-dependency-chain.json) · [Inventory](data/receipt-inventory.json) |
| Native product | NotRun; không có generated product DWG/PDF; Phase2 pages=[] | [Review summary](data/review-summary.json) · [PDF manifest](phase2/product-pdf-manifest.json) |

## Giải thích host và units

Probe dùng executable AutoCAD2026 (25.1.164.0.0), transcript engine W.164.0.0 và một source-DWG copy; log cho thấy có AEC/Civil modules được load theo bản vẽ/profile hiện có. Đây không phải smoke palette desktop hoặc một seed mm sạch. INSUNITS=2 là Feet; mm là4 theo [Autodesk INSUNITS2026](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Core/files/GUID-A58A87BB-482B-4042-A00A-EEF55A2B4FD8.htm). Không suy từ biến này rằng mọi geometry đã scale đúng: nó là insertion-unit boundary phải kiểm cùng native geometry/block units.

Core Console nạp được DLL có WPF/AcMgd references ở máy này là quan sát thực tế, không chứng minh mọi API/UI/plot hoạt động trong Core. Desktop palette/undo và numerical product run chưa thực hiện vì preflight không đạt.

## Các phép tái hiện chạy lại

```powershell
dotnet run --project outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261014-review-01/probes/AcceptanceProbe/AcceptanceProbe.csproj -- <thư-mục-output-chẩn-đoán-mới>
```

Probe chỉ đọc và gọi các adapter/semantic diff trong source hiện tại; sử dụng synthetic negative data từ test factory, không phải bộ input nghiệm thu. Không sửa production source. `never-written.dwg` phải không tồn tại trước chạy; nếu chạy lại vào thư mục khác, ghi log/run ID mới. Hai log attempt01/02 là lỗi dựng harness đã sửa (relative ProjectReference và tên property), không phải lỗi build sản phẩm; giữ để truy vết, không dùng làm finding.

## Code và nguồn trong review

Snapshot source/receipts ở frozen; spec frozen là căn cứ của lượt này. Các reviewer nhánh không chạy build/test/host; root chạy các bước có log nêu trên. Không có native geometric mutation test, vector PDF comparison hoặc visual DucBao review bị giả là đã chạy.
