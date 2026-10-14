# W05/W13 independent read-only native and acceptance review

Reviewer scope: W05 and W13 receipt ChangedFiles, production call graph, native writer/readback, plot/acceptance, command/palette/headless inputs. Review date: 2026-10-14. No source, spec or receipt edits; no builds, tests or AutoCAD calls. This report is static evidence, not an executed native result.

Verdict: **NOT READY to satisfy native AutoCAD 2026 phase 1 or product acceptance.** An exploratory host run can diagnose the existing command, but cannot establish the promised S0–S11/save/reopen/plot pipeline. Mandatory DucBao visual phase 2 remains NotRun. Both receipts correctly say NativeGate=NotRun and have no actual DWG/PDF paths; their ImplementationComplete claims are contradicted by the defects below. Approval of NR-01..23 is retained; missing engineering binding values must not be manufactured.

All relative spec citations below refer to `C:/Users/DucBao/Desktop/Dbtool/Ver 1/docs/vhgpro-standard-20261012/leader-architecture-20261013/`. Source paths are absolute.

## Findings

### N01 — P1: User entrypoints never invoke the new production/acceptance chain

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool/App/HoGaCommands.cs:167`; companion `.../DBTool/DBTool/UI/MainPaletteControl.xaml.cs:103`, `.../DBTool/DBTool.Headless/Program.cs:53`.
- Trigger: VEHOGABATCH, the palette draw button, or HeadlessRunner.
- Observed: all use the old CalculationPipeline/CalculationPackage route. Command and palette call GenerateDrawingUseCase -> CadGenerationWriter.WriteAsync -> CadWriteSession.Run; that route commits entities but never saves/reopens or calls NativeReadbackVerifier, AcceptanceEvidenceAssembler, or BatchGenerationOrchestrator. Repository-wide C# references to ProductionDispatcher and BatchGenerationOrchestrator are declarations/tests only. NativeReadbackVerifier has only its declaration. No class implements IPlotEvidencePort, IPlotSessionPort, IVisualReviewReader or IRunContextFactory in source. Default S11 therefore cannot plot actual PDFs.
- Spec: W13-P8.txt:24–36 requires shared batch request, dispatcher and production wiring. 05-ARCHITECTURE.md:15309–15370 and 16517–16558 require actual S10 and S11.
- Impact: tests of the new adapters do not prove the shipped AutoCAD commands use them; command success has no saved native or PDF acceptance evidence.
- Fix owner: W13 production composition/entrypoints, W05 ports; retain the existing main UI.
- Verifiable check: trace one real palette/command run through instrumented S0..S11 with source/build/request identity; save and reopen actual output; collect actual native handles, PDF bytes, transcript, per-row checks. A code search must show real composition and implementations, not only tests.

### N02 — P1: INativeGenerationPort.Write saves only a manifest and calls it Passed

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/CadGenerationWriter.cs:95` and :123–180.
- Trigger: inject CadGenerationWriter into S10StageAdapter and call Write(S9StateV2).
- Observed: handleMap is initialized empty and never appended. The transaction only ensures repository schema, optionally erases old entities, stores input hashes and commits. There is no rendering/intent flattening, native entity creation or TagOwned call in this method. It saves the active database, copies/reopens bytes, compares other NOD manifest strings and returns input geometry/rebar hashes with readback_status="Passed" and commit_state="Reopened".
- Spec: 05-ARCHITECTURE.md:15315–15354 explicitly requires every native intent written, exact counts/tags, reopened fields compared and actual normalized native hashes.
- Impact: a blank or unrelated drawing can receive a passing native receipt. Replace can erase existing owned output and write no replacement geometry.
- Fix owner: W13 native writer, W05 native verification.
- Verifiable check: inject real S9 curves, labels, schedules and frames into an empty output drawing; verify expected entity count and semantic handle map after reopen. Change one actual entity/attribute and require NATIVE_READBACK_MISMATCH.

### N03 — P1: Missing native port produces simulated committed/readback success

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:1074–1092`; default dispatcher at :1183; fabricated S0/S1 at :1197–1243.
- Trigger: new S10StageAdapter() or default ProductionDispatcher.
- Observed: absent native port returns "SIMULATED_SAVED_NATIVE_HASH", no handles, "Committed", "Passed", "Reopened", and copies domain hashes. Missing S0/S1 similarly invent empty assets/rules/raw with default hash strings instead of blocking. W13StageAdapterTests.cs:24–35 explicitly asserts this fake success; the dispatcher test at :75–79 checks only interface membership.
- Spec: W13 prompt forbids skeleton/default fixture; 05-ARCHITECTURE.md:19115 requires real saved/reopened evidence and NotRun when absent.
- Impact: default production-named adapters provide false success evidence; green unit tests institutionalize it.
- Fix owner: W13 StageAdapters/composition and its tests.
- Verifiable check: absent mandatory production ports and readers must yield typed Blocked/NotRun with no fake native artifact/hash; fakes must exist only in test code. An integration test should exercise the actual command composition.

### N04 — P1: NativeReadbackVerifier checks existence, not native field equality

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/NativeReadbackVerifier.cs:105–111` and :157–170.
- Trigger: verify a DWG whose handles still exist but geometry, block attributes, owner metadata or style changed; or verify an empty handle list.
- Observed: no expected native intent is built or compared. Only handle format/existence/entity kind are added to failure checks. Actual attributes are read but never compared with expected values; empty actual attributes fall back to the caller's expected tags/values. Owner remains h.owner_manhole_id, geometry/rebar hashes remain caller hashes, and before/after arrays are compared from the caller rather than reread from the reopened database. Empty handles succeed. No geometry, OCS, rotation, scale, style, alignment, ink, dimensions or tolerance comparison exists.
- Spec: 05-ARCHITECTURE.md:15345–15354; native leaves at :17679–17689 require actual ownership and normalized 2D readback.
- Impact: changed or missing attributes, wrong geometry and forged caller hashes pass; the W05 receipt claim of OCS/tolerance verification is unsupported.
- Fix owner: W05 NativeReadbackVerifier, W13 invocation.
- Verifiable check: real saved/reopened mutation cases for moved vertex, wrong block name, removed/changed ATTRIB, ownership XData, rotation and scale must fail with exact expected/actual/unit/handle. Empty required handle coverage must fail/NotRun.

### N05 — P1: Acceptance compares expected values to themselves and invents missing tolerances

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs:331`, :372, :393, :413, :292–298.
- Trigger: a dimension has a matching semantic handle, any group/opening exists, or an approved numeric tolerance value is absent.
- Observed: AddDim assigns actualVal=expectedVal solely on handle-map membership; group mean, opening invert and handle owner checks use the same DTO value for expected and actual. Groups/openings have no native handles. GetUnitTolerance fills missing mm/mm2/kg/paper/pdf/rad/basis values with literals, so its intended missing-tolerance Blocked path is unreachable for these units.
- Spec: 05-ARCHITECTURE.md:16528–16536 and :17699 explicitly prohibit guessing actual from requested value; NR-18/W13:12 requires missing binding diagnostics instead of invented numbers.
- Impact: geometrically wrong drawing can produce Passed numerical checks, and absence of approved tolerance bindings is hidden.
- Fix owner: W05 AcceptanceEvidenceAssembler, W13 actual measured evidence producer.
- Verifiable check: fixture expected 1000 mm/native 1200 mm must report actual=1200 and Failed; absent native measurement remains NotRun. Missing approved tolerance must produce RULE_BINDING_DATA_MISSING/Blocked, with source/rule identifiers.

### N06 — P1: Visual review and reference identity can be accepted without proof

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs:43–51` and :153–170.
- Trigger: a review reader returns visual_status="Passed" for an unrelated PDF/frame or with null reviewer and empty regions; request contains any nonempty reference bundle ID.
- Observed: reference validation only tests nonempty IDs. reviewed.visual_status is copied without requiring reviewed PDF hash/frame identity, actual reviewer, or required reviewed regions. Result PDF path/hash/transcript existence/content are not checked by the assembler. PDF check only compares supplied scales. The interface has no demonstrated production reader/plot implementation.
- Spec: 05-ARCHITECTURE.md:16525, :16542–16549 and :17729–17731 require exact profile/reference identity, actual PDF hash and required regions all reviewed Passed. User requires mandatory DucBao phase 2.
- Impact: external review metadata can upgrade a page to Passed without proving review of the delivered PDF or required scope. Missing reader correctly remains NotRun; that correct fallback does not close this validation gap.
- Fix owner: W05 evidence assembly, W13 plot/review integration; DucBao alone performs the required human visual phase.
- Verifiable check: mismatch PDF hash/frame/reference, null reviewer, missing required regions and unreviewed region must never yield visual Passed or row Complete. Record DucBao's actual reviewed named regions against exact produced PDF hash after phase 1.

### N07 — P1: Native write ignores source-output safety and stale replacement target

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/CadGenerationWriter.cs:90–93`, :110–121, :135.
- Trigger: target_dwg_path points to an original/source DWG or outside the approved output directory; Replace references stale generation/hash; invalid generation_mode supplied.
- Observed: only a blank output path is rejected, then SaveAs writes it. No canonical path/source conflict/output-root guard. No selected TargetV2 expected_generation_id, expected_input_hash, expected_document_token or requested_generation_id is read. Replace erases by context manhole ID without staleness validation. Any unrecognized mode falls through to manifest write. Create checks entities only via ReadGeneration; N02 creates only dictionary manifests, so repeating Create can overwrite a manifest unnoticed.
- Spec: 05-ARCHITECTURE.md:15318–15331 requires output-copy guard, exact target, Create/Replace validation and generation/hash staleness checks.
- Impact: source overwrite and stale deletion risks; duplicate generation identities can be silently accepted.
- Fix owner: W13 CadGenerationWriter/GenerationRepository and request composition.
- Verifiable check: source/outside-root output rejection before any mutation; stale Replace and invalid mode rejection; two consecutive Creates of same target rejected; replace B preserves A entities plus NOD/XData exactly.

### N08 — P1: Failed row discards successful rows and all row receipts

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool/App/PaletteDrawingCalculation.cs:90–91`; command :150–154/:169 and palette :105–114.
- Trigger: selected rows A valid, B invalid and C valid.
- Observed: calculation loops and creates per-row receipts, but any failure returns Value=null, discarding successful packages and receipts. Both callers abort all drawing. Command has an earlier whole-workbook blocking gate. Receipts marked Complete at :88 mean only calculation, before native write, PDF or visual acceptance. SchedulePackageExport.cs:16–19 contains an empty failed-row conditional and accepts no per-row quantity-basis/status contract.
- Spec: W13-P8.txt:25–27 requires row receipts instead of whole-batch failure; H17/05-ARCHITECTURE.md:17691–17696 requires exactly one final receipt per selected row, including failures.
- Impact: good rows are never delivered; failures cannot be audited through their final structured receipts; calculation status is confused with completed delivery.
- Fix owner: W13 palette/batch/export integration with W05 orchestration.
- Verifiable check: mixed batch preserves A/C outputs and produces A/B/C final receipts with appropriate failed-row state; cancellation/host unhealthy cases likewise preserve prior committed evidence and cover every selected row.

### N09 — P2: Scale/elevation/batch inputs remain disconnected from actual UI and command

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool/UI/MainViewModel.cs:159–186`; companion command :165/:184–187 and palette :100–101.
- Trigger: user wants a different page scale/elevation/anchor or multiple Headless IDs.
- Observed: MainPaletteControl.xaml has no DrawingScale, AvailableScales, ScaleMode, ElevationMode or AvailableElevationModes binding. New VM fields therefore remain defaults in ordinary UI use; ScaleMode has no reader outside its property. ElevationModes includes "Relative" although domain enum supports only Absolute/Symbolic; parsing invalid strings silently falls back Symbolic. RunBatch is still fixed 30/Symbolic and 0,0 anchor; it reads no sheet/baseX/baseY despite XML documentation claiming them. Headless validates 20/30/40 only, forces Symbolic, rejects more than one ID and writes software JSON/Excel, not native delivery.
- Spec: W13-P8.txt:24–29/:36 requires shared request, selected rows/scale/elevation/anchor and batch/page scales while preserving main UI.
- Impact: W13 receipt's RunBatch-from-ViewModel claim is false; scale/elevation capabilities are not reachable as promised and CLI diverges.
- Fix owner: W13 existing UI bindings/request adapters and CLI.
- Verifiable check: retain main UI structure and prove selection of each approved scale/mode actually reaches identical immutable request fields in command/palette/headless; reject unsupported enum values explicitly; command's documented syntax must match real inputs.

### N10 — P1: Schedule rows still degrade to free text and missing numbers to zero

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/CadRenderer.cs:298–331`.
- Trigger: a Schedule/Row's schedule block is missing/unresolved, ScheduleMark absent, or canonical numeric table cells missing.
- Observed: native block path is conditional; when asset or block is absent it falls through to per-cell sink.AppendText at :331. Native branch uses zero defaults for missing length/count/total/mass/diameter (:304–309). Preflight checks angle rows and BlockPrimitive assets, not general round TableRowPrimitive schedule block/tag coverage.
- Spec: W13-P8.txt:33 requires native schedule blocks and no free-text fallback; receipt claims this fallback was removed.
- Impact: invalid native schedule schema becomes plausible-looking text/zero quantities rather than an explicit block; required editable tagged block evidence disappears.
- Fix owner: W13 CadRenderer, upstream S8 canonical schedule contract.
- Verifiable check: missing block/mandatory numeric cell must block before mutation; all valid schedule rows reopen as the exact approved block with expected tags/values, never substituted DBText rows.

### N11 — P2: Final manifest overwrites provenance and failures are swallowed

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/BatchGenerationOrchestrator.cs:248–275`; assembler :423–458.
- Trigger: a pipeline runs S11 and later the batch finalizer writes the same delivery-manifest.json; or directory/store write fails.
- Observed: assembler writes {batch,sources,receipts,generated_at_utc}; orchestrator then writes raw BatchV2 to the same filename, removing sources and receipts. Both persistence catch blocks silently ignore all failures while returning a manifest path and potentially Complete status. OpenHealthySession (:52) and contextFactory.Create (:78) are outside the row try/catch, so their exceptions also leave missing row receipts/session cleanup.
- Spec: 05-ARCHITECTURE.md:16557–16558 requires actual artifact manifest with source/receipt evidence and H17 requires every selected target final receipt.
- Impact: even otherwise good execution loses provenance or advertises a nonexistent/stale acceptance manifest; session/precheck errors bypass final accounting.
- Fix owner: W05 BatchGenerationOrchestrator and AcceptanceEvidenceAssembler.
- Verifiable check: final artifact retains source/build/rule/receipt and actual artifact hashes for every row; inject artifact-store/session/context faults and require explicit failed/not-run receipts and correct cleanup, never silent Complete.

### N12 — P2: New ownership XData format is decoded with the old schema

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/GenerationRepository.cs:131–141` and :144–157.
- Trigger: any entity tagged using the new TagOwned method is later inspected by ReadGeneration/ReadMetadata.
- Observed: TagOwned writes [app,generation,semantic,run,build,version]; ReadMetadata expects [app,generation,inputHash,templateHash,version], takes semantic as InputHash, run as TemplateHash and build hash as SnapshotVersion. It never validates the real version at index 5. TagOwned currently has no callers (N02), but the required native implementation would activate the inconsistency.
- Spec: W13 persistence contract requires actual versioned namespace ownership and stale-input protection; 05-ARCHITECTURE.md:17679/:17682.
- Impact: schema/version proof is wrong and stale replacement/hash checks cannot work consistently across old/new ownership records.
- Fix owner: W13 GenerationRepository/native writer.
- Verifiable check: round-trip both supported versioned ownership formats after save/reopen with explicit input/template/semantic/run/build fields; unsupported schema must block rather than silently reinterpret.

## Command syntax and host interpretation

The real AutoCAD registration is `[CommandMethod("VEHOGABATCH")]` at HoGaCommands.cs:120. RunBatch calls Editor.GetString exactly once (:129), with AllowSpaces=true, strips surrounding quotes/spaces, and reads that path. It does **not** read the XML-comment optional sheetName/baseX/baseY arguments (:118). A script must provide separate input lines, conceptually:

```text
VEHOGABATCH
"C:\absolute\path\actual-workbook.xlsx"
```

NETLOAD must load the **DBTool command/UI assembly**, not just DBTool.AutoCAD.dll (the latter contains no command registration). Exact built DLL path and host load outcome are for the root's actual phase 1. The command project targets net8.0-windows/x64 with WPF and AutoCAD 2026 references; AutoCAD adapter is also net8.0-windows/x64 and requires AutoCADManagedPath. That is configuration compatibility evidence only, not successful host loading. The comment claiming accoreconsole support is unverified here; WPF/AcMgd command assembly dependencies must be proved by a real load if core console is used.

Providing extra sheet/base coordinate lines would not set those values and could become subsequent unrelated commands. Current RunBatch always reads detected profile sheet, all detected manholes, fixed 1:30/Symbolic, Create mode and origin X=0/Y=0 descending by bounds. It does not save, reopen, plot or generate a delivery manifest. A manual QSAVE/PDF after this command is a separate test action, not proof that production W13 executes the promised pipeline.

## Complete changed-source coverage

Base for every path below: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/`.

| Receipt | Source inspected | Result |
|---|---|---|
| W05 | DBTool/DBTool.Application/BatchGenerationOrchestrator.cs | N01, N11; fixed literal Auto candidates also ignore source-backed candidate binding; no actual use site |
| W05 | DBTool/DBTool.AutoCAD/NativeReadbackVerifier.cs | N01, N04 |
| W05 | DBTool/DBTool.Application/AcceptanceEvidenceAssembler.cs | N05, N06, N11; absent plot stays NotRun correctly |
| W13 | DBTool/DBTool/App/HoGaCommands.cs | N01, N08, N09; exact command syntax traced |
| W13 | DBTool/DBTool/App/PaletteDrawingCalculation.cs | N01, N08, N09 |
| W13 | DBTool/DBTool/App/SchedulePackageExport.cs | N08; failure conditional empty; no native/acceptance evidence |
| W13 | DBTool/DBTool/UI/MainPaletteControl.xaml.cs | N01, N08, N09; modeless native mutation marshalled to command context |
| W13 | DBTool/DBTool/UI/MainViewModel.cs | N09; actual XAML bindings checked as dependency |
| W13 | DBTool/DBTool.Application/ExportCalculationUseCase.cs | Added quantity NotCalculated gate inspected; it does not provide production batch/native acceptance; no separate proven regression raised |
| W13 | DBTool/DBTool.Application/GenerateDrawingUseCase.cs | Added nonempty group guard inspected; still old DrawingPackage port; N01 |
| W13 | DBTool/DBTool.AutoCAD/BlockAttributeWriter.cs | Emptying unused template values is implemented; same-object text comparison at61 is not save/reopen/style/alignment evidence (N04) |
| W13 | DBTool/DBTool.AutoCAD/CadGenerationWriter.cs | N01, N02, N07; both overloads traced |
| W13 | DBTool/DBTool.AutoCAD/CadRenderer.cs | N10; general rendering and block path inspected |
| W13 | DBTool/DBTool.AutoCAD/GenerationRepository.cs | N07, N12; untouched hash comparison only measures persisted input-manifest strings, not actual other entity state |
| W13 | DBTool/DBTool.Headless/Program.cs | N01, N09; software artifacts explicitly labeled ExperimentalSoftwareFixture |
| W13 | DBTool/DBTool.Application/StageAdapters.cs | W13-owned S10/S11/ProductionDispatcher inspected in detail; N03. Other stage implementations read for context; their package owners review engineering correctness. CancellationToken.None is passed into native and assembler at1072/1124, so in-progress cancellation cannot reach these ports |
| W13 | DBTool/DBTool.Application.Tests/W13StageAdapterTests.cs | N03; fake defaults asserted; no real app wiring/native save-reopen-PDF coverage |

Dependencies also inspected: CadWriteSession.cs (old route commits with random GUID generation IDs, no save/reopen); Application.ContractsV2.cs (ports/target expectations); SharedEnums.cs (valid elevation values); MainPaletteControl.xaml; actual command and AutoCAD csproj; changed-file receipt inventories; W05/W13 prompts and architecture S10/S11 leaf contracts.

No executed test/build/native assertion is made by this report. Main UI preservation, 2D-only intent, source input preservation, NR approval boundary and mandatory DucBao actual visual phase remain acceptance constraints.
