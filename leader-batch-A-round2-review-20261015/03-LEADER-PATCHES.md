# CODE SỬA TRỰC TIẾP — ĐƯỜNG 1

Leader đã viết và kiểm chứng bản sửa cho NEW-A01, D11 và blocker liên quan R03-W03-N06 trong bản sao độc lập. Chưa ghi đè code live và chưa sửa receipt của implementer. Bảng nghiệm thu vẫn mô tả đúng code nhận bàn giao trước patch.

Bản sửa không giải quyết G08 typography, G09 full native round-trip, D08 CSG, unit/product binding hay historical ledger. Vì vậy, apply patch này chưa mở khóa đợt B.

## Nội dung bản sửa

- Registry: source ID chứa toàn bộ SHA256; kiểm cả 11 field của SourceV2 khi trùng ID. Khác kind/sheet/cell/page/raw_text/parent_ids/formula_id phải fault, không chỉ so sha256/relative_path/handle.
- CadHandle source.sha256 trở về hash của bytes DWG; content signature vẫn ở AssetV2.hash. Giữ tách biệt identity nguồn và identity nội dung.
- SourceV2.parent_ids: copy khi constructor/init và copy khi getter; caller sửa mảng không làm thay đổi snapshot hoặc run khác. Giữ 11 public field/constructor, JSON field names và thứ tự cũ; không đổi input/output DTO public.
- Recipe: chỉ nhận rule Approved có approver/time/source không rỗng; parent scope và nested scope không rỗng; wildcard chỉ được dùng khi có ký tự * explicit; shape source là bắt buộc.
- Host: cả round bar và Angle phải có đúng một geometry component với exact host ID và kind rõ ràng; không đoán Base/Wall từ tên gần giống. Angle vẫn dùng recipe component Angle, không nhầm với kind của concrete host.
- Cập nhật một test hiện có: tên canonical NEW-A01, full hash và source byte hash theo spec gốc. Lần chạy full suite đầu phát hiện test cũ đóng đinh 8 ký tự hash và signature hash; giữ nguyên log thất bại, sửa expectation có lý do, không xóa test. Các probe lỗi độc lập chứng minh guard trước/sau.

| File thay | Before SHA256 | After SHA256 |
| --- | --- | --- |
| [DBTool/DBTool.Application/Templates/TemplateContractValidator.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/changed-files/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs>) | 8938f360ebf512c3bc3a77cb88d086315ed6c14adeefdad8aa4fa66dbd3810b2 | 2aecc8a981c060bc46cc1f9c3b881187845494a8e94abf072a3e7df9087ba713 |
| [DBTool/DBTool.Drawing/PresentationGroupPlanner.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/changed-files/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs>) | c786c3d700882747ccabf33fae81bae9a1478512070a11420c031fb0d63f4a2d | 255116bf87118acc444fb8beaabd36d788caffb437b846d385981dd3b9f3ce6f |
| [DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/changed-files/DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs>) | f62ed2f5ae81415592b1c6251620ad1ab374498c29dad86c258f5f601595843f | 3e9cab6590874746659d2c03ddb40c9d439413409731269762d13de11431a58f |
| [DBTool/DBTool.Application.Tests/W02RemediationTests.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/changed-files/DBTool/DBTool.Application.Tests/W02RemediationTests.cs>) | 4984b85a4bf917217ac58b0adcc9aeb7595186c7c87c44a61c58ab3487ba8f8d | 4bb9141d79917ca206a0243894994c939b2427756d4d718b886babe499921ea1 |

[Diff đầy đủ](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/leader-direct-fixes.patch>); [manifest machine-readable](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/patch-manifest.json>); [script apply có hash guard](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/leader-patches/apply-leader-patch.py>). Bốn file thay nguyên văn nằm trong changed-files; không cần implementer viết lại hàm hoặc suy luận thêm.

## Kiểm chứng trước và sau

| Predicate | Trước sửa | Sau sửa | Unit/tolerance / evidence |
| --- | --- | --- | --- |
| Full hash của hai bundle cùng 8 ký tự đầu | Conflict thay vì coexistence | Hai descriptor đúng và riêng biệt, không overwrite | Exact identity; B-I / P-RED1 / P-GREEN |
| Descriptor kind/parent_ids/sheet/cell/page/raw_text/formula_id thay đổi | Nhiều thay đổi không bị chặn | SOURCE_DESCRIPTOR_CONFLICT với cả existing/incoming | Exact field identity; P-RED1 / P-GREEN |
| Source constructor/init/getter array mutation | Nguồn thay theo mảng bên ngoài | Nguồn giữ nguyên | Exact source chain; P-RED3 / P-GREEN |
| JSON SourceV2 property order | Phiên candidate đầu đổi formula_id/parent_ids | Đúng thứ tự gốc | Exact order; P-RED4 / P-GREEN |
| Rejected/blank recipe →Plan/S5 | Tạo 1 group N2 hợp lệ giả | SHAPE_RECIPE_BINDING_MISSING | Approval/scope exact; C-I / P-RED1 / P-GREEN |
| Thiếu approval/source →Plan/S5 | Không fault | SHAPE_RECIPE_BINDING_MISSING | Exact; P-RED2 / P-GREEN |
| Angle host missing/duplicate | Plan/S5 vẫn tạo nhóm | GROUP_HOST_BINDING_MISSING | Exact count ID; P-RED3 / P-GREEN |
| Valid recipe + explicit wildcard | N2 hợp lệ | Vẫn N2 hợp lệ | Count 2 exact; P-GREEN |
| Apply sai preimage | Không áp dụng | PATCH_PREIMAGE_MISMATCH, mọi file giữ nguyên | Byte hash exact; P-APPLY |

Review chéo: [W02 candidate](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/agents/candidate-w02-review.md>); [W03 candidate](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-batch-A-round2-review-03/agents/candidate-w03-review.md>). Cả log phản biện đầu và log sau sửa được giữ riêng; không thay lịch sử bằng một receipt Passed.

Các suite Domain/Drawing/Application và native build đã được chạy trên candidate; kết quả ở sổ lệnh. Đây là kiểm hồi quy code, không phải nghiệm thu sản phẩm DWG/PDF. Native round-trip của candidate vẫn NotRun; native probes trước patch thuộc snapshot R03 nhận bàn giao.

## Áp dụng nguyên văn và kiểm live

Chạy khối PowerShell dưới đây một lần khi implementer tiếp nhận. Script kiểm preimage của cả bốn file trước khi ghi; tạo backup và rollback nếu ghi lỗi; không ghi nếu upstream đã đổi. Nếu hash mismatch thì phải rebase bằng diff trên source mới và review lại, không bỏ guard.

    & "C:\Users\DucBao\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\apply-leader-patch.py" --root "C:\Users\DucBao\Desktop\Dbtool\Ver 1" --apply
    if ($LASTEXITCODE -ne 0) { throw 'PATCH_APPLY_FAILED' }
    dotnet run --project "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\probe\LeaderGuardProbe.csproj" "-p:LeaderSourceRoot=C:\Users\DucBao\Desktop\Dbtool\Ver 1\DBTool" --artifacts-path "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\live-replay-build" -- "C:\Users\DucBao\Desktop\Dbtool\Ver 1\outputs\leader-acceptance\leader-field-contracts-20261013-r2\20261015-batch-A-round2-review-03\leader-patches\live-replay-results.json"
    if ($LASTEXITCODE -ne 0) { throw 'LEADER_GUARD_REPLAY_FAILED' }

Sau replay, hash các file live phải khớp manifest; lưu actual command/log/result và previous receipt SHA256, tạo receipt version mới; không sửa receipt/review cũ. Nếu cần khôi phục, dùng đúng backup được script trả; không reset repository. Trong vòng review này leader chỉ chạy CheckOnly trên live và apply trên bản sao kiểm chứng.

## Diff reviewable

    --- a/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs
    +++ b/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs
    @@ -131,8 +131,25 @@
             {
                 if (registry.TryGetValue(source.id, out var existing))
                 {
    -                if (existing.sha256 != source.sha256 || existing.relative_path != source.relative_path || existing.handle != source.handle)
    -                    throw new InvalidOperationException($"Conflicting source registration for ID '{source.id}': existing sha256={existing.sha256}, new sha256={source.sha256}.");
    +                bool identical =
    +                    existing.id == source.id &&
    +                    existing.kind == source.kind &&
    +                    existing.relative_path == source.relative_path &&
    +                    existing.sha256 == source.sha256 &&
    +                    existing.sheet == source.sheet &&
    +                    existing.cell == source.cell &&
    +                    existing.page == source.page &&
    +                    existing.handle == source.handle &&
    +                    existing.raw_text == source.raw_text &&
    +                    existing.formula_id == source.formula_id &&
    +                    existing.parent_ids is not null &&
    +                    source.parent_ids is not null &&
    +                    existing.parent_ids.SequenceEqual(source.parent_ids, StringComparer.Ordinal);
    +                if (!identical)
    +                    throw new InvalidOperationException(
    +                        $"SOURCE_DESCRIPTOR_CONFLICT: id='{source.id}', " +
    +                        $"existing={System.Text.Json.JsonSerializer.Serialize(existing)}, " +
    +                        $"incoming={System.Text.Json.JsonSerializer.Serialize(source)}");
                 }
                 else
                 {
    @@ -140,7 +157,12 @@
                 }
             }
     
    -        string bundlePrefix = manifest.SourceFileHash[..Math.Min(8, manifest.SourceFileHash.Length)];
    +        if (string.IsNullOrWhiteSpace(manifest.SourceFileHash) ||
    +            manifest.SourceFileHash.Length != 64 ||
    +            !manifest.SourceFileHash.All(Uri.IsHexDigit))
    +            throw new InvalidOperationException(
    +                $"SOURCE_HASH_INVALID: expected=SHA256 hex, actual='{manifest.SourceFileHash}'.");
    +        string bundlePrefix = manifest.SourceFileHash.ToUpperInvariant();
             string bundleSourceId = $"SRC-DWG-{bundlePrefix}";
             var bundleSource = new DBTool.Domain.StageContractsV2.SourceV2(
                 bundleSourceId,
    @@ -228,7 +250,7 @@
                         descId,
                         "CadHandle",
                         a.VerificationSource?.RelativePath ?? "template.dwg",
    -                    a.Signature?.Hash ?? manifest.SourceFileHash,
    +                    manifest.SourceFileHash,
                         null,
                         null,
                         null,
    --- a/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs
    +++ b/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs
    @@ -218,6 +218,8 @@
             // 2. Process angle steel occurrences
             foreach (var a in angles)
             {
    +            // Host identity must be proven even though the steel recipe's component kind is Angle.
    +            _ = ResolveComponentKind(a.host_id, geometry);
                 var angleRecipe = FindShapeRecipe(rules, a.shape_recipe_id, effective.profile, "Angle", "Angle");
                 if (angleRecipe == null)
                 {
    @@ -443,15 +445,17 @@
     
         private static string ResolveComponentKind(string hostId, GeometryV2 geometry)
         {
    -        var comp = geometry.components?.FirstOrDefault(c => string.Equals(c.id, hostId, StringComparison.OrdinalIgnoreCase));
    -        if (comp != null && !string.IsNullOrWhiteSpace(comp.kind)) return comp.kind;
    -
    -        if (hostId.Contains("Base", StringComparison.OrdinalIgnoreCase)) return "Base";
    -        if (hostId.Contains("Wall", StringComparison.OrdinalIgnoreCase)) return "Wall";
    -        if (hostId.Contains("Roof", StringComparison.OrdinalIgnoreCase)) return "Roof";
    -        if (hostId.Contains("Neck", StringComparison.OrdinalIgnoreCase)) return "Neck";
    -        if (hostId.Contains("Pad", StringComparison.OrdinalIgnoreCase)) return "Pad";
    -        return hostId;
    +        var matches = (geometry.components ?? [])
    +            .Where(c => string.Equals(c.id, hostId, StringComparison.Ordinal))
    +            .ToArray();
    +        if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].kind))
    +            throw new DrawingStageFaultException(
    +                "GROUP_HOST_BINDING_MISSING",
    +                hostId,
    +                ["geometry.components[].id", "geometry.components[].kind"],
    +                "exactly one component with this host ID and an explicit component kind",
    +                $"matches={matches.Length}; kinds=[{string.Join(",", matches.Select(c => c.kind))}]");
    +        return matches[0].kind;
         }
     
         private static ShapeRecipeV2? FindShapeRecipe(
    @@ -467,31 +471,39 @@
     
             foreach (var r in rules.entries)
             {
    +            if (!string.Equals(r.status, "Approved", StringComparison.Ordinal) ||
    +                string.IsNullOrWhiteSpace(r.approver) ||
    +                string.IsNullOrWhiteSpace(r.approved_at_utc) ||
    +                r.source_ids is null || r.source_ids.Length == 0 ||
    +                r.source_ids.Any(string.IsNullOrWhiteSpace))
    +                continue;
                 if (r.shapes == null || r.shapes.Length == 0) continue;
                 bool profileMatch = r.profiles != null && (r.profiles.Contains(profile) || r.profiles.Contains("*"));
                 if (!profileMatch) continue;
     
    -            bool parentCompMatch = r.components == null || r.components.Length == 0 ||
    -                                   r.components.Contains("*") ||
    -                                   r.components.Any(c => string.Equals(c, componentKind, StringComparison.OrdinalIgnoreCase));
    +            bool parentCompMatch = r.components != null && r.components.Length > 0 &&
    +                                   (r.components.Contains("*") ||
    +                                    r.components.Any(c => string.Equals(c, componentKind, StringComparison.OrdinalIgnoreCase)));
                 if (!parentCompMatch) continue;
     
    -            bool parentShapeKindMatch = r.shape_kinds == null || r.shape_kinds.Length == 0 ||
    -                                        r.shape_kinds.Contains("*") ||
    -                                        r.shape_kinds.Any(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase));
    +            bool parentShapeKindMatch = r.shape_kinds != null && r.shape_kinds.Length > 0 &&
    +                                        (r.shape_kinds.Contains("*") ||
    +                                         r.shape_kinds.Any(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase)));
                 if (!parentShapeKindMatch) continue;
     
                 foreach (var s in r.shapes)
                 {
                     if (!string.Equals(s.id, recipeId, StringComparison.Ordinal)) continue;
    -
    -                bool compMatch = s.component_kind == "*" ||
    -                                 string.IsNullOrEmpty(s.component_kind) ||
    -                                 string.Equals(s.component_kind, componentKind, StringComparison.OrdinalIgnoreCase);
    -
    -                bool famMatch = s.family == "*" ||
    -                                string.IsNullOrEmpty(s.family) ||
    -                                string.Equals(s.family, family, StringComparison.OrdinalIgnoreCase);
    +                if (s.source_ids is null || s.source_ids.Length == 0 ||
    +                    s.source_ids.Any(string.IsNullOrWhiteSpace)) continue;
    +
    +                bool compMatch = !string.IsNullOrWhiteSpace(s.component_kind) &&
    +                                 (s.component_kind == "*" ||
    +                                  string.Equals(s.component_kind, componentKind, StringComparison.OrdinalIgnoreCase));
    +
    +                bool famMatch = !string.IsNullOrWhiteSpace(s.family) &&
    +                                (s.family == "*" ||
    +                                 string.Equals(s.family, family, StringComparison.OrdinalIgnoreCase));
     
                     if (compMatch && famMatch)
                     {
    --- a/DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs
    +++ b/DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs
    @@ -45,8 +45,21 @@
         string? handle /* — */,
         string raw_text /* — */,
         string[] parent_ids /* — */,
    -    string? formula_id /* — */
    -);
    +    [property: System.Text.Json.Serialization.JsonPropertyOrder(10)] string? formula_id /* — */
    +)
    +{
    +    // A source snapshot must not change when a caller mutates the array it supplied or received.
    +    private readonly string[] _parentIds = parent_ids?.ToArray()
    +        ?? throw new ArgumentNullException(nameof(parent_ids));
    +
    +    [System.Text.Json.Serialization.JsonPropertyOrder(9)]
    +    public string[] parent_ids
    +    {
    +        get => _parentIds.ToArray();
    +        init => _parentIds = value?.ToArray()
    +            ?? throw new ArgumentNullException(nameof(parent_ids));
    +    }
    +}
     
     public sealed record DiagnosticV2(
         string code /* — */,
    --- a/DBTool/DBTool.Application.Tests/W02RemediationTests.cs
    +++ b/DBTool/DBTool.Application.Tests/W02RemediationTests.cs
    @@ -366,7 +366,7 @@
         }
     
         [Fact]
    -    public void NEW_A02_SourceRegistry_IsImmutablePerRun_IncludesBundleHashInIds_AndCoexistsWithoutOverwritingProvenance()
    +    public void NEW_A01_SourceRegistry_IsImmutablePerRun_IncludesFullBundleHash_AndPreservesSourceBytes()
         {
             string hashA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
             string hashB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    @@ -405,23 +405,23 @@
             var bundleA = TemplateAssetsV2Converter.ConvertToAssetsBundle(manifestA);
             var bundleB = TemplateAssetsV2Converter.ConvertToAssetsBundle(manifestB, existingSources: bundleA.Sources);
     
    -        // Descriptor IDs must contain bundle hash prefix
    -        string idA = $"SRC-BLK-{hashA[..8]}-1A0";
    -        string idB = $"SRC-BLK-{hashB[..8]}-1A0";
    +        // Source identity includes the entire SHA256, as required by the rework order.
    +        string idA = $"SRC-BLK-{hashA}-1A0";
    +        string idB = $"SRC-BLK-{hashB}-1A0";
     
             Assert.True(bundleB.Sources.ContainsKey(idA), $"Sources must contain {idA}");
             Assert.True(bundleB.Sources.ContainsKey(idB), $"Sources must contain {idB}");
             Assert.NotEqual(idA, idB);
     
             // Both block descriptors coexist without overwriting provenance
    -        Assert.Equal(sig.Hash, bundleB.Sources[idA].sha256);
    +        Assert.Equal(hashA, bundleB.Sources[idA].sha256);
             Assert.Equal("TemplateA.dwg", bundleB.Sources[idA].relative_path);
    -        Assert.Equal(sig.Hash, bundleB.Sources[idB].sha256);
    +        Assert.Equal(hashB, bundleB.Sources[idB].sha256);
             Assert.Equal("TemplateB.dwg", bundleB.Sources[idB].relative_path);
     
             // Bundle sources also exist with their DWG file hashes
    -        string dwgIdA = $"SRC-DWG-{hashA[..8]}";
    -        string dwgIdB = $"SRC-DWG-{hashB[..8]}";
    +        string dwgIdA = $"SRC-DWG-{hashA}";
    +        string dwgIdB = $"SRC-DWG-{hashB}";
             Assert.True(bundleB.Sources.ContainsKey(dwgIdA));
             Assert.True(bundleB.Sources.ContainsKey(dwgIdB));
             Assert.Equal(hashA, bundleB.Sources[dwgIdA].sha256);
    @@ -437,6 +437,6 @@
     
             var exConflict = Assert.Throws<InvalidOperationException>(() =>
                 TemplateAssetsV2Converter.ConvertToAssetsBundle(manifestA, existingSources: conflictingDict));
    -        Assert.Contains("Conflicting source registration", exConflict.Message);
    +        Assert.Contains("SOURCE_DESCRIPTOR_CONFLICT", exConflict.Message);
         }
     }
