# Read-only review W02 / W06 / W07 — 2026-10-14

Reviewer scope: all 45 distinct ChangedFiles in receipts W02, W06, W07; StageAdapters.cs ownership restricted to S2/S3/S4 and relevant legacy composition. Source-only review, no product/receipt/spec edits, no build/test/AutoCAD launch. Findings below are static executable-path evidence, not a native acceptance certificate. All absolute source paths use workspace `C:/Users/DucBao/Desktop/Dbtool/Ver 1/`.

Spec base: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/docs/vhgpro-standard-20261012/leader-architecture-20261013/`. `05-ARCHITECTURE.md` line numbers below are exact. Work-package sections B3.01/B3.09/B3.10 prohibit missing-as-zero, fixture defaults, Ready data on binding failure, and count-only acceptance. The 23 approved proposals are acknowledged; none of these findings requests reapproval.

## Findings

### G01 — P1 — S2 replaces absent/invalid engineering inputs with fabricated dimensions and can select another row

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:70` and `:90`–`:123`.
- Trigger: selected row is absent, or a required Length/Width/Height/thickness cell is blank, malformed, an unverified formula, or not numerically parseable.
- Actual: `FirstOrDefault(row_index) ?? raw.rows[0]`; `GetNum` ignores RawCell kind/formula/cache_verified/activity and returns supplied defaults. Empty raw cells produce clear 1000×1000×1500, base200/wall150/roof150/base expansion100, neck wall125; code can select NapGaV.700, symbolic zero datum and body scale30. No field diagnostic or binding failure is returned.
- Violated spec: S2 lines7546–7557 UNIQUE exact row, topological field/activity parsing, and `no constructor defaults`; lines7567–7583 exact cover product/aperture contract; H14 field parser and B3.09. These values are not derived from the approved proposals.
- Impact: a normal typed stage result can represent the wrong row and engineering dimensions absent from the workbook. Existing `ManholeValidator.Validate` does not protect this path: Execute does not call it.
- Fix owner: W06. Implement exact row lookup and typed field parsing with current culture/cache/activity/source gates; missing required input must create the prescribed Block diagnostic/receipt before S2.
- Verify: missing selected row, empty cells, malformed number, formula without verified cache, comma-decimal policy and valid-zero optional fields; assert exact named Block and no S2 output. Valid values must round-trip from source.

### G02 — P1 — S2 silently drops all steel, pipe, catch and panel requests

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:166`–`:179`.
- Trigger: real raw row/config enables any main mesh, pipe pair, Catch.C..M or BTCT panels; valid sources/bindings may be supplied.
- Actual: EffectiveV2 is constructed with `members=[]`, `pipes=[]`, `catch=null`, `panel=null`, `source_ids=[]`, `parsed_fields=[]` unconditionally. NeckSize and NeckPosition are not resolved: aperture is min(clear dimension,700), neck center is always(0,0); purchased-cover kind recognizes only literal `Purchased`, not specified Gang/composite. Product/contact bindings and FinishedCoverTop are never resolved.
- Violated spec: S2 lines7567–7612 exact cover enum/product/position/datums/panel/catch; lines7613ff pipe pair/catalog expansion; H14 lines5736–5764 member-binding expansion. W06 B3.03 requires downstream effective typed products/feature states and no re-reading workbook.
- Impact: standard S1→S2→S3→S4 loses active features before any capability gate; S4 receives an empty member set and cannot report missing requested steel. This is silent suppression, not an explicit GeometryOnly request.
- Fix owner: W06, coordinated W07. Populate each required effective field and feature from the exact field-binding tables; retain inactive raw provenance and Block active incomplete bindings.
- Verify: one input per main member/accessory family, four pipe walls, type3 body/neck catch, purchased Gang and BTCT panels, nine neck positions; assert exact effective values/source IDs and matching output or explicit Block.

### G03 — P1 — S3 omits physical assemblies and does not register roof access void with its host

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:223`–`:305` (notably `:273`–`:284`).
- Trigger: direct valid S2 contains active pipes/catch/panels/haunches/frame/seat/collar or a roof access aperture.
- Actual: Execute makes only pad/base/four walls/mortar/blank roof/neck and a generic cover placement. No iteration over e.pipes, e.catch, e.panel or haunch values; no frame100 solid, seat/collar, native product-contract lookup, shell/mortar or opening ownership partition. MakePrism always assigns `void_ids=[]`; RoofAccess is added to geometry.openings but Roof.void_ids remains empty. Joint.dowel_required is hardcoded false. No missing feature failure occurs.
- Violated spec: S3 lines8173–8177 register access ID once on roof; lines8193–8213 required frame/cover/collar/panel/haunch/pipe/catch construction; lines8155–8160 HasDowel/profile gating; lines8214–8218 occupied/void checks. Type3 with catch=null specifically must raise CATCH_REQUIRED_BUT_INACTIVE.
- Impact: incorrect concrete topology and missing 2D source geometry even if G02 is repaired; current direct stage execution returns a success envelope for incomplete assemblies.
- Fix owner: W06. Compose the declared domain builders/typed payload faithfully, retain exact host/void IDs and joint rules; unimplemented active components must Block.
- Verify: each active component and owner receives expected contour/depth; access subtraction once; pad0 absent; type2/3 joint/mortar retained; type3 absent catch blocked; each pipe shape true contour and owners verified.

### G04 — P1 — S4 manufactures repeated straight bars and declares every family Ready without shape/support/clip bindings

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:572`–`:599`.
- Trigger: valid S3 with any active non-Dowel MemberSpec (MainMesh, Tie, Lifting, Catch, etc.).
- Actual: every non-Angle family uses `count??5`, `run=requested_pitch??1000`, diameter `??10`, station i×150, actual_gap150, and exactly the same path from(0,0,0) to(run,0,0) for every i. Face/layer/direction/host cover do not transform the path. Fabrication/schedule/run are all set to run, length_status Known, opening_ids empty, region Uncut. No recipe lookup, required_rule_ids gate, support/station distribution, clipping, hooks, C/U/Z shaping, panel expansion or provenance ledger is performed. Angle emits an unrelated 100mm line, fixed unit mass4 and mass16 while claiming length4×pitch.
- Violated spec: S4 lines8943–9008 requires exact recipes, support, real stations, opening offsets/union/complement, role values, hooks and MAKE_OCCURRENCE; NR01–08/19/21 and 05B-F32/F36. All active paths must satisfy support/cover/void constraints, B3.10.
- Impact: duplicate coincident physical steel with wrong lengths/counts/mass can flow into canonical/group/schedule/drawing output under Ready. Good legacy `Plan`/domain helper tests do not validate this Execute path.
- Fix owner: W07. Wire the actual typed physical planners and required rule/data gates into Execute, preserving parent/station/cut interval records and length bases.
- Verify: exact host/frame paths, cover, independent station positions, anisotropic pitch/diameter deltas, multiopening/tangent/full-removal conservation, each tie/hook family and panel multiplicity. Missing recipe/radius/support binding must Block and never assign Known.

### G05 — P1 — S4 dowel pairs invent dimensions, overlap at origin and succeed without a joint

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:611`–`:635`.
- Trigger: active Dowel MemberSpec; absent approved NR06 embed/depth/station data or absent dowel-required joint.
- Actual: first joint selected optionally; missing joint replaced by string Joint. count defaults4; all bars run from origin to z=-500; stations i×200 only metadata. Hole center origin, diameter25, depth100 and range0..100 are fixed for every pair. No DowelLength/DowelHoleDia, RoofBottom/WallTop, rule recipe, support or catch collision check. Feature is Ready even though all hole/bar locations coincide and no joint may exist.
- Violated spec: H09 lines5313–5353 and 05B-F34 require exact approved joint/station/embed/depth, source length equality and bar+hole collision checks. NR06 approval explicitly does not supply missing embeddings/hole depth.
- Impact: physically impossible/misplaced dowels and holes, unsafe fabrication metadata and false pair coverage; S3 currently sets every joint.dowel_required=false yet S4 still accepts dowels.
- Fix owner: W07 (joint flag source W06). Consume BUILD_DOWEL_PAIRS logic and reject missing joint/bindings.
- Verify: distinct approved stations, 1:1 exact matching bar/hole centers, input length and hole diameter, hole begins at actual RoofBottom, union support including mortar, catch collision IDs, missing joint/data Block.

### G06 — P1 — BindingMissing in S4 still returns a normal S4 output

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:565`–`:569` and `:662`–`:665`.
- Trigger: active MemberSpec points to a host absent from g.components.
- Actual: adds FeatureState BindingMissing and continues, without DiagnosticV2, needsrule IDs or failure receipt; builds canonical and returns S4StateV2. No final all-active-feature gate exists.
- Violated spec: S4 lines9031–9036 persist PartialPreview and raise REBAR_REQUIRED_FEATURE_BLOCKED when any feature is BindingMissing/Unsupported/Invalid; B3.09 no Ready data on failure.
- Impact: downstream stage receives incomplete steel as usable output; missing host/feature is not propagated as a row Block.
- Fix owner: W07. Accumulate exact diagnostics and fail before accounting/output whenever required active feature is unresolved.
- Verify: absent host plus another valid member, missing rule plus valid member, fully removed legitimate mesh and deliberate GeometryOnly; distinguish Block from approved suppression and preserve partial preview evidence.

### G07 — P1 — Physical hashes ignore the physical paths and geometry that they claim to freeze

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:307`–`:316` and `:649`–`:660`.
- Trigger: keep IDs/z/thickness fixed but alter L/B, neck position/aperture, any contour/frame/product XY/rotation; or change steel path/face/station/basis without changing ID/diameter/schedule length.
- Actual: geometry hash includes only id/kind/material/z_min/z_max/thickness, opening id/kind/depth, joint id/z, product id/contract/z. No XY loops/curves, frames, void ownership, product transform. Rebar hash includes only id/diameter/schedule length and angle id/unitmass; fabrication geometry hash includes hole id/dia/depth but no center/axis/range. String interpolation also depends on current culture and separator concatenation is not canonical JSON.
- Violated spec: 05B-F46 (`05B-FORMULAS.md:58`) canonical invariant round-trip JSON including ordered physical payload; S3 line8220 and S4 lines9040–9041. Rebar hash explicitly freezes physical paths/identity/basis.
- Impact: large geometry changes can keep exactly the same hash. Update/reopen/freshness comparisons cannot prove untouched/consistent physical output and may miss change.
- Fix owner: W06 geometry hash and W07 rebar/fabrication-hole hash; reuse the spec canonical serialization protocol.
- Verify: changing every relevant physical leaf changes hash; display name/layout/handle-only changes do not; culture en-US/vi-VN invariant; test endpoints, reversed ordered path, hole center/axis and product rotation separately.

### G08 — P1 — Template asset conversion fabricates all ATTDEF metrics and removes required native metadata

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:315`–`:360`.
- Trigger: call public ToAssetsV2 with a legitimately inspected native manifest.
- Actual: every asset has bounds(0,0,0,0), definition_handle empty, dependencies empty, layer/style empty; every ATTDEF height200,width1,insertion/alignment=block basepoint, rotation0 and Left/Base regardless native value. Products/glyphs/kerning/layers/font_styles all empty. source_ids gets source-file Sha256 rather than an exact SourceV2 descriptor ID.
- Violated spec: W02 B3.03 reader must export actual definition hash/basepoint/ATTDEF/style/dependencies; `05-ARCHITECTURE.md` AssetV2/AttributeDefV2 at1635–1687 and AssetsV2 at1702ff require native metrics, registered source IDs and approved product contracts. Native schema e.g. TK.THKL.CT has width0.8, distinct align points/modes and actual height200.0000326 (`05C-IMPLEMENTATION-CONTRACTS.md:949`).
- Impact: V2 adapter result cannot support trustworthy ink/label placement or exact product fit. The converter has no callers in present source, so this is a broken exported W02 boundary and missing wiring, not evidence that it already altered a DWG.
- Fix owner: W02. Extract native metrics within transaction into typed records with exact provenance; required unreadable metadata must cause explicit Block rather than synthesizing neutral-looking values.
- Verify: at least two different blocks/styles/ATTDEF alignments; compare every exported leaf against readback, resolve every source_id in context registry, preserve actual extents/dependencies; absent metrics cannot return a usable AssetsV2.

### G09 — P1 — New mandatory template roles can never be produced by the native reader, blocking existing template load

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/VhgTemplateRoles.cs:11`–`:12`; `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.AutoCAD/TemplateManifestReader.cs:211`–`:220`.
- Trigger: existing TemplateManager.EnsureTemplateLoaded reads a normal source DWG then validates VhgTemplateRoles.Presentation (caller `DBTool/DBTool/Cad/TemplateManager.cs:26`–`:29`).
- Actual: Presentation now requires CatchSummary and Geometry/DowelHole, but MapBlockRole returns only SteelSummary, AngleScheduleRow, PanelSummary, two reinforcement roles, or `Block:<name>`. There is no mapping for either new role for any block name. TemplateContractValidator must therefore emit TEMPLATE_BLOCK_MISSING before import. CatchSummary also requires tag TONG, whereas source TK.THKL.CT exact tags are CUATHU1/CUATHU2.
- Violated spec: W02 B3.03 keep role-to-native mapping, add explicit scoped catch/dowel roles, exact tags; native schema `05C-IMPLEMENTATION-CONTRACTS.md:949`. B3.10 existing caller/interface must remain functional.
- Impact: deterministic regression in normal native template-loading path, including users not requesting catch/dowels. Native launch is unnecessary to demonstrate impossible mapping, but actual host reproduction was not performed by this reviewer.
- Fix owner: W02. Bind actual exact native definitions/tags and apply requested-role scope correctly; unavailable assets must report specific binding missing for affected feature without impossible universal requirements.
- Verify: native reader→validator→import round-trip with unchanged template, and feature-specific missing catch/dowel asset cases; test actual mapped role/tag pairs, not preconstructed synthetic manifests.

### G10 — P1 — Pad=0 passes new validator but throws in ManholeInput

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/Input/ManholeInput.cs:21`; caller `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/ManholeValidator.cs:119` and `:682`.
- Trigger: otherwise valid legacy Calculate input with CF.Ga PadThickness=0, representing the explicitly supported padOFF case.
- Actual: ManholeValidator changed padding check to NonNegative, then constructs ManholeInput, whose PadThicknessMm still uses ContractGuard.Positive. The constructor throws ArgumentOutOfRangeException rather than creating valid snapshot/no-pad geometry. No catch converts this to the intended result; pad0 never reaches drawing pad suppression policies through this path.
- Violated spec: W06 B3.03 pad nonnegative; S3 lines8125–8132 pad=0 no component/hatch/PadBottom and unchanged BaseBottom. B3.09 padOFF branch mandatory.
- Impact: valid current workbook input crashes calculation rather than honoring padOFF. V2 default pad0 in G01 is a separate path and does not fix this old caller.
- Fix owner: W06, with ManholeInput declaration owned jointly W02. Use nonnegative model invariant, and ensure ChamberGeometryBuilder also suppresses zero pad instead of creating degenerate positive-volume components.
- Verify: existing fixture with only PadThickness changed100→0 validates and builds no Pad; pad<0 yields named Block; base datums/quantities unchanged except removed pad.

### G11 — P1 — Legacy catch steel ignores parsed Catch.M and uses invented Ø/pitch/cover and wrong depth argument

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:490`–`:494`.
- Trigger: legacy FullRebarStage.Plan receives an active Reinforced CatchBasinSpec and geometry.CatchBasin, e.g. typed catch extension with Catch.M entered.
- Actual: creates CatchRebarSpec(ChamberClearWidthMm, **ChamberClearHeightMm**,8,150,25); second argument is FloorDepthMm, not height. AccessoryInputMapper parses and stores Catch.M (`AccessoryInputMapper.cs:171`–`:175`) but no consumer reads Accessories.CatchMesh. Both mesh diameter and pitch plus cover are fabricated. CatchBasinRebarPlanner also centers all its bars at world origin XY instead of transforming to catch host/wall/depth frame; current tests intentionally use origin-centered synthetic geometry and miss placement.
- Violated spec: NR08 W07 B3.04 requires Ø/pitch from M17 and one layer per actual support face; W07 B3.03 compose physical catch geometry/voids then catch steel. H07/H08 require true host support paths. Returning Partial for structural evidence does not authorize substituting source values.
- Impact: wrong steel sizes/counts/lengths and location for valid reinforced catch data; changing Catch.M has no effect. This affects legacy Plan and remains even after repairing V2 Execute.
- Fix owner: W07. Bind floor/side/front meshes from Catch.M and Protect.I with exact support transforms; depth must use actual chamber depth. Missing face-shape/anchorage data must Block while retaining raw input/partial evidence.
- Verify: vary Catch.M Ø/pitch and cover independently; depth != height; catch on all four walls, body versus translated neck and nonzero offset; every bar envelope inside actual face with correct cover; no guessed values.

### G12 — P1 — Protect.H stops controlling wall mesh clipping when opening accessory reinforcement is off

- File: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/Rebar/WallRebarPlanner.cs:137` (openingClearance assignment), coupled `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/AccessoryInputMapper.cs:148`.
- Trigger: pipe opening exists and wall mesh active; Pipe.D=0/disabled (no supplementary pipe bars); Protect.H differs from CoverWall, e.g.15 versus30.
- Actual: Opening(false) returns null when Pipe.D inactive. WallRebarPlanner uses `Accessories?.Pipe?.Cover ?? rebarSpec.CoverMm`, so ordinary wall surface cover controls pipe clipping rather than Protect.H. Changing Protect.H alone cannot affect clipped main wall pieces in this case.
- Violated spec: NR01, 05B-F36 (`05B-FORMULAS.md:48`), S4 lines8964–8969: exclusion clearance is Protect.H + Ø/2 independent of supplementary pipe reinforcement activation.
- Impact: overcut/undercut main meshes, wrong retained lengths and clearances; valid wall-cover and hole-cover policies cannot be independently honored. OpeningClipper itself adds Ø/2 correctly; the wrong input enters before it.
- Fix owner: W07 (typed input preservation W06). Carry source-backed Protect.H separately from optional VhgOpeningInput and use it for opening main-mesh exclusions even when Pipe.D=0.
- Verify: same wall/pipe grid, accessory off, CoverWall30, Protect.H15→25; independent expected interval endpoints change by appropriate analytic exclusion while wall face offsets stay fixed; both diameters and tangent/full removal checked.

## Test/evidence assessment

- Receipt W02/W06/W07 ChangedFiles contain no test files. Receipt commands cite four complete suites, but command counts are not physical/native acceptance. This reviewer did not rerun those commands or validate receipt execution logs.
- Existing HaunchCompositionTests, WallCTieCompositionTests, VhgDowelCompositionTests exercise legacy Build/Plan, not S2/S3/S4 Execute. They include meaningful synthetic geometry/count/provenance checks and should be retained, but cannot detect G01–G07. No test references IS2StageV2/IS4StageV2 Execute outside implementation were found in source search.
- PanelCatchRebarTests vary fixture pitch and assert aggregate lengths, but catch geometry is deliberately centered at origin and does not verify actual translated/rotated support placement, source Catch.M or differing chamber depth/height. They miss G11.
- ArchitectureContractTests inspects only namespaces exactly DBTool.Domain/DBTool.Drawing/DBTool.Application, excluding `.StageContractsV2`. W02 get-only-immutability receipt claim is stronger than that test: generated record DTOs have init properties and mutable arrays exactly as the supplied generated spec files do. Since implementation matches generated spec, this is a contract/assurance ambiguity rather than an implementation finding against W02.
- Domain and Application DTO files were mechanically compared against `data/stage-contracts-v2/*`: line content equal. Drawing differs only by one extra blank line. Exact DTO copying is established; it does not prove correct stage calculations or immutable runtime payloads.
- No actual reopened DWG/vector PDF/font/visual-review evidence was read in this subreview; NativeGate remains NotRun. Requested 2D is the scope; no 3D test was attempted.

## Coverage — changed source read

All 45 distinct receipt paths read in full; long files were read in numbered ranges. StageAdapters full file was inspected, but findings outside S2/S3/S4 are left to their package reviewers. DTO files also compared mechanically to embedded generated schema.

W02 (17):

1. DBTool/DBTool.Domain/Common/Diagnostic.cs
2. DBTool/DBTool.Domain/Common/SourceRef.cs
3. DBTool/DBTool.Domain/Input/ManholeInput.cs
4. DBTool/DBTool.Domain/Input/ValidatedManholeSpec.cs
5. DBTool/DBTool.Domain/Policies/CalculationPolicy.cs
6. DBTool/DBTool.Drawing/PresentationProfile.cs
7. DBTool/DBTool.Drawing/PresentationRequest.cs
8. DBTool/DBTool.Infrastructure.Excel/Vhg32Schema.cs
9. DBTool/DBTool.Infrastructure.Excel/WorkbookImporter.cs
10. DBTool/DBTool.Application/Templates/VhgTemplateRoles.cs
11. DBTool/DBTool.Application/Templates/TemplateContractValidator.cs
12. DBTool/DBTool.AutoCAD/NativeAssetSignatures.cs
13. DBTool/DBTool.AutoCAD/TemplateAssetImporter.cs
14. DBTool/DBTool.AutoCAD/TemplateManifestReader.cs
15. DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs
16. DBTool/DBTool.Drawing/StageContractsV2/Drawing.ContractsV2.cs
17. DBTool/DBTool.Application/StageContractsV2/Application.ContractsV2.cs

W06 (13):

1. DBTool/DBTool.Application/AccessoryInputMapper.cs
2. DBTool/DBTool.Application/ManholeValidator.cs
3. DBTool/DBTool.Domain/Geometry/CatchBasinBuilder.cs
4. DBTool/DBTool.Domain/Geometry/GeometryModel.cs
5. DBTool/DBTool.Domain/Input/CatchBasinSpec.cs
6. DBTool/DBTool.Drawing/SheetOneConcreteAssembly.cs
7. DBTool/DBTool.Drawing/SheetOneHatchRegions.cs
8. DBTool/DBTool.Drawing/SheetOneNeckAssembly.cs
9. DBTool/DBTool.Drawing/SheetOnePipeViews.cs
10. DBTool/DBTool.Drawing/SheetOnePlanBaseOutlinePolicy.cs
11. DBTool/DBTool.Drawing/SheetOnePlanOutlinePolicy.cs
12. DBTool/DBTool.Drawing/SheetOneSectionOutlinePolicy.cs
13. DBTool/DBTool.Application/StageAdapters.cs

W07 adds 15 distinct paths (StageAdapters already counted):

1. DBTool/DBTool.Application/FeatureSupportValidator.cs
2. DBTool/DBTool.Domain/Rebar/BarLengthResolver.cs
3. DBTool/DBTool.Domain/Rebar/BarOccurrence.cs
4. DBTool/DBTool.Domain/Rebar/CatchBasinRebarPlanner.cs
5. DBTool/DBTool.Domain/Rebar/DowelPlanner.cs
6. DBTool/DBTool.Domain/Rebar/HaunchRebarPlanner.cs
7. DBTool/DBTool.Domain/Rebar/LiftingHookPlanner.cs
8. DBTool/DBTool.Domain/Rebar/NeckRebarPlanner.cs
9. DBTool/DBTool.Domain/Rebar/NominalFabricationLength.cs
10. DBTool/DBTool.Domain/Rebar/NominalHookPlanner.cs
11. DBTool/DBTool.Domain/Rebar/OpeningClipper.cs
12. DBTool/DBTool.Domain/Rebar/PanelRebarPlanner.cs
13. DBTool/DBTool.Domain/Rebar/ShapeSignature.cs
14. DBTool/DBTool.Domain/Rebar/TiePlanner.cs
15. DBTool/DBTool.Domain/Rebar/WallRebarPlanner.cs

## Open limits / interpretation

- `NativeAssetSignatures.ReadBlock` alone omits geometry, but actual TemplateManifestReader.BlockSignature uses ReadBlocks geometry hashes for importer conflict checking. Therefore no blanket claim that native import ignores block geometry is made.
- Current product command routing into ProductionDispatcher versus legacy CalculationPipeline belongs W13 review. The Execute boundaries themselves are demonstrably wrong regardless of caller reachability; legacy path regressions G09–G12 have separately traced callers.
- W02 HasApprovedRules uses All and can return true for an empty list, but no current caller was found; not promoted to an impact finding without evidence of actual bypass.
- SheetOnePipeViews converts polygon/multicell sections to bounding rectangles and SheetOneNeckAssembly remains limited to thickness125/150 and special collar geometry. These are additional incomplete-coverage risks; no independently traced native output was obtained, so they are not used to inflate confirmed findings.
- Parent reviewer should validate runtime probes for G01/G04/G06/G07/G10 and collect exact current test runs; no runtime claims above rely on fabricated outputs.
