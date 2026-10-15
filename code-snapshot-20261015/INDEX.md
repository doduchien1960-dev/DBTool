# Code snapshot DBTool — 2026-10-15

Snapshot mã nguồn tại thời điểm leader đang trực tiếp sửa (R05 InProgress).
Chỉ gồm source (.cs/.csproj/.sln/.md) — loại trừ bin/obj và test assets (dwg/dxf/pdf/xlsx...).
Danh sách đầy đủ 615 file: xem [FILELIST.txt](FILELIST.txt).

## File trọng điểm (dính findings trong biên bản nghiệm thu)

- [DBTool/DBTool.Application/StageAdapters.cs](DBTool/DBTool.Application/StageAdapters.cs) — 24 findings (S2/S3/S4)
- [DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs](DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs) — bảng thống kê thép
- [DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs](DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs) — xếp nhãn
- [DBTool/DBTool.Drawing/ViewBuilder.cs](DBTool/DBTool.Drawing/ViewBuilder.cs) — dựng view
- [DBTool/DBTool.AutoCAD/TemplateManifestReader.cs](DBTool/DBTool.AutoCAD/TemplateManifestReader.cs)
- [DBTool/DBTool.AutoCAD/CadGenerationWriter.cs](DBTool/DBTool.AutoCAD/CadGenerationWriter.cs) — ghi DWG
- [DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs](DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs) — bằng chứng nghiệm thu
- [DBTool/DBTool.AutoCAD/NativeReadbackVerifier.cs](DBTool/DBTool.AutoCAD/NativeReadbackVerifier.cs)
- [DBTool/DBTool.AutoCAD/GenerationRepository.cs](DBTool/DBTool.AutoCAD/GenerationRepository.cs)
- [DBTool/DBTool.AutoCAD/CadRenderer.cs](DBTool/DBTool.AutoCAD/CadRenderer.cs)
- [DBTool/DBTool.Application/BatchGenerationOrchestrator.cs](DBTool/DBTool.Application/BatchGenerationOrchestrator.cs)
- [DBTool/DBTool.Drawing/PresentationGroupPlanner.cs](DBTool/DBTool.Drawing/PresentationGroupPlanner.cs)
- [DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs](DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs)
- [DBTool/DBTool.Drawing/SemanticDiff.cs](DBTool/DBTool.Drawing/SemanticDiff.cs)
- [DBTool/App/HoGaCommands.cs](DBTool/App/HoGaCommands.cs)
- [DBTool/App/PaletteDrawingCalculation.cs](DBTool/App/PaletteDrawingCalculation.cs)
- [DBTool/UI/MainViewModel.cs](DBTool/UI/MainViewModel.cs)
