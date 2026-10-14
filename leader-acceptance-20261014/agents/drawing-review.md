# Drawing S5–S9 acceptance review — static source evidence

Review date: 2026-10-14. Workspace: `C:/Users/DucBao/Desktop/Dbtool/Ver 1`. Read-only source review; no build, test, AutoCAD, plot, or host commands were run. Findings below are deterministic source paths and input triggers, not claims of an observed native reproduction. No production/spec/receipt changes were made.

Authoritative specification: `docs/vhgpro-standard-20261012/leader-architecture-20261013/05-ARCHITECTURE.md`, especially S5 (9858), S6 (10777), S7 (11787), S8 (12937), S9 (14125), H06 (4836), H12 (5506), H28 (6583), and TagBindingV2 (1843). All 23 NR proposals are already approved; missing payload values remain missing.

## Findings

### D01 — P1 — S6 drops every non-catch view

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:765` (through 772), `DBTool/DBTool.Drawing/CatchAssemblyProjector.cs:49` (catchRoles), 51–76 (inactive path).
- Trigger: execute S6 with any ordinary body/base/wall/roof/neck/panel payload, whether catch is active or inactive.
- Behavior: the adapter invokes only CatchAssemblyProjector, whose entire role list is CatchPlan/Catch11/Catch22. Inactive catch returns three empty, unrequired views; active catch still emits only those three roles. The remaining physical geometry/rebar never obtains views/targets.
- Violation/impact: S6 requires EXPAND_VIEW_ROLE_TABLE and physical-member coverage; mandatory structure/steel/panel drawings disappear before S7/S9, while required-view coverage checks see only the reduced set.
- Corrective package: W10, with W13 runtime composition verification. Acceptance: execute the real S5→S6 path for all three profiles, independent catch/neck/panel activity, and assert exact role-table equality plus per-member target/frame ownership; inactive catch must not suppress body views.

### D02 — P1 — S9 never places S8 schedules or bending views

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:831` (bundles), 981–988 (coverage); `DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:65` (scheduleView/bendingView), 211–230 (schedule ink).
- Trigger: any nonempty S8 schedule whose IDs are not already in projection.views, as emitted by the existing S6.
- Behavior: schedule ink is added to a dictionary, but bundles are constructed exclusively from proj.views. New Schedule/Bending IDs are never iterated or placed, and coverage compares only proj.views.required.
- Violation/impact: S9 EXACT_VIEW_BUNDLES must include annotation and schedule ink and place every required bundle once. Nonzero TK rows, summaries, headings and bending captions silently have no sheet placement.
- Corrective package: W11. Acceptance: run S8→S9 with at least Body/Neck/Panel/Catch rows; multiset of all required projection plus S8 view IDs must equal placement IDs exactly once. Verify native entities lie in their declared frame.

### D03 — P1 — Native binder silently blanks the authoritative operations

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:309`, 325, 330, 346–347.
- Trigger: a valid source recipe uses MeanRole, RawSumMetres, Mass, Pitch, FixedRole or SizePair.
- Behavior: implementation recognizes different names RoleMean/RawSumM/MassKg and lacks the remaining operations. All unmatched operations use literal-or-empty instead of a diagnostic. Valid MeanRole/RawSumMetres/Mass therefore bind empty leg/TDAI/KL text without numeric provenance.
- Violation/impact: specification TagBindingV2.operation line 1843 explicitly lists the closed exhaustive operation set; exact native attributes and quantities become incomplete while S8 returns a schedule.
- Corrective package: W04/W09. Acceptance: table-driven public S8 cases for every authoritative enum, unknown-operation rejection, exact tag set equality against ATTDEF, raw numerical values/units, and saved/reopened ATTRIB values.

### D04 — P1 — Missing native assets and metrics are fabricated

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:564`–590; `DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:529`–552; `DBTool/DBTool.Drawing/SchematicPresentationPlanner.cs:151`–158.
- Trigger: required TK/BLT/glyph absent from the exact template or active recipe has no native asset binding.
- Behavior: ExactAsset returns invented definitions (handle 0, default_hash/default_label_hash, hardcoded schemas/extents); schematic ink uses an invented glyph rectangle. Missing recipe falls back to name heuristics such as TK.C/TK.T.
- Violation/impact: architecture no null-to-default fixture rule, exact asset signatures and RULE_BINDING_DATA_MISSING; planning succeeds with identities not present in the source template and can claim usable native layout where none exists.
- Corrective package: W04/W09/W10. Acceptance: delete one required asset or active recipe value and require typed failure with exact asset/field/source; no fabricated hash/handle/default schema may occur. Real template variants must bind their actual ATTDEF schemas.

### D05 — P1 — Character counts replace native ink measurement

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:517`–525; `DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:542`–560; `DBTool/DBTool.Drawing/SchematicPresentationPlanner.cs:163`–174.
- Trigger: any annotation/schedule/caption, especially %%c diameter escapes, Vietnamese, proportional fonts, centered/right-aligned or rotated attributes.
- Behavior: widths are text.Length×constant/height. Glyphs, kerning, text style, encoding, alignment and rotation are not measured. Native schedule attributes are assumed to extend right/up from insertion.
- Violation/impact: S7 gate and H12 require actual glyph/font metrics, escaped-text decoding, native alignment and stroke envelopes. S9 containment/collision decisions are based on false bounds and cannot establish actual native fit.
- Corrective package: W04 plus W09/W10 integration. Acceptance: native metric comparison for escapes/Vietnamese/font variants/rotation/alignment; absent glyph metric returns FONT_GLYPH_METRIC_MISSING. All plotted ink must fit the certified envelope.

### D06 — P1 — Failed label placement returns unchecked overlap

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:429`–434; 437–473; 555–558.
- Trigger: an obstacle occupies each of the three hardcoded lane candidates, or an approved recipe requires a different side/finite extent.
- Behavior: after all collision checks fail, a fixed anchor+(300,200) placement is returned without checking obstacles. FindLabelRecipe always returns Right and LanesFromExplicitRecipe does not consume recipe bounds, offsets or min gap. Port is also treated as insertion without native port offset.
- Violation/impact: H28 and S7 must exhaust valid finite candidates and raise LAYOUT_ANNOTATION_NO_FIT; NR-IO-011 requires native-port-aware ink. Overlapping labels are accepted and approved lane policy ignored.
- Corrective package: W04/W10. Acceptance: obstacles covering all approved lanes must fail with rejected-candidate evidence; positive cases must satisfy side/extent/min-gap and native port transform.

### D07 — P1 — Collector intersections and tie ownership are invented

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AnnotationLaneAllocator.cs:167`–193, 203–213, 425–426.
- Trigger: group contains multiple bars whose targets are not collinear with the single primaryAnchor→port segment; any tie group.
- Behavior: one leader segment is built from the first anchor, but every target is declared an intersection with that segment and gets a Gach tick. No geometric intersection is calculated. BLTC sets occurrence_id=group.id rather than a physical tie occurrence. View ink at 259–261 omits routes and tick ink.
- Violation/impact: S7/H11 requires collectors crossing real visible physical members and physical tie ownership; labels/ticks may point to unrelated locations and their ink may exceed the frame undetected.
- Corrective package: W04/W10. Acceptance: independently intersect emitted collectors with own visible bar curves; each claimed point must lie on both. BLTC must join exactly one tie occurrence and all route/tick ink must be included in view_ink.

### D08 — P1 — RegionBoolean does not perform CSG and arcs become chords

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/AssemblyBoundaryEmitter.cs:488`–533; 473.
- Trigger: overlapping/touching component section loops, a void partially outside a solid, a subtractive part sharing an exterior edge, or circular/arc aperture.
- Behavior: Union returns a concatenated list; Difference appends an untrimmed Inner loop, even when it extends beyond the outer boundary. TransformLoopsToView converts every CurveV2 to a line using endpoints, losing center/radius/sweep. A full circular arc with coincident endpoints becomes degenerate.
- Violation/impact: H06/H26/S6 require real analytic union/difference, face-cell ownership and shared valid outlines/hatch regions. Internal seams, invalid hatch holes, incorrect area and missing circular openings follow deterministically.
- Corrective package: W03/W10. Acceptance: analytic rectangle-overlap union, edge-crossing void subtraction, adjacent same-material seam removal, circular holes and oblique cuts with area/orientation/containment checks against approved tolerances.

### D09 — P1 — Main summary fields are zero for the actual component IDs

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/NativeSteelSchedulePlanner.cs:434`–456; `DBTool/DBTool.Application/StageAdapters.cs:219` (component ID construction).
- Trigger: generated host IDs such as MH1:Base, MH1:Wall/T1, MH1:Roof or MH1:Neck with nonzero group mass.
- Behavior: BindSummaryTags tests StartsWith("Base"/"Wall"/"Roof"/"Neck") on the fully scoped host ID. Those tests all fail, so DAY/TUONG/TRAN/CO categories are zero, while TONG still sums the same masses.
- Violation/impact: NR14 and S8 require exact physical owner buckets; summary subtotals contradict rows and grand total.
- Corrective package: W04/W09. Acceptance: several arbitrary manhole prefixes, each component and Ø<=10/>10, with nonzero expected categories; category sums and TONG must reconcile from raw physical mass.

### D10 — P1 — Legacy production path carries but never consumes S5 registry

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/CalculationPipeline.cs:175`; `DBTool/DBTool.Drawing/PresentationGroup.cs:251`–284; `DBTool/DBTool.Drawing/ViewBuilder.cs:39`–48, 134–142, 674.
- Runtime trace: palette App/PaletteDrawingCalculation.cs:37 and Headless/Program.cs:50 construct CalculationPipeline. It invokes PlanPreDrawing; DrawingStageAdapter.Build:728–737 carries its map; ViewBuilder has no reference to GroupRegistry and continues canonical markByOccurrence for labels/table/bending. FromPlannedGroups has no production caller.
- Trigger: real palette/headless drawing, including two walls with compatible bar metadata or split regions/canonical shapes within one presentation scope.
- Behavior: PlanPreDrawing groups by ComponentKind rather than exact host/assembly/opening/panel scope, invents D/N/C sequence prefixes, and recomputes mass using D²×0.006165. The carried map is then ignored by drawing generation. The exact new S5 planner does not govern these drawing identities/quantities.
- Violation/impact: W08 and S5/F49 require one exact primary group registry shared by annotation/TK/bending, raw quantities and canonical mapping. Interface-only carrying does not implement this runtime contract.
- Corrective package: W08/W09/W10/W13. Acceptance: actual user entrypoint must reach the shared S5 registry; inject deliberately different presentation/canonical marks, exact host scopes and source mass basis, and verify all native labels/rows/bending plus registry hash use those values.

### D11 — P1 — S5 accepts absent or out-of-scope recipe/prefix data

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs:114`–125, 391–402, 515–527, 583–584.
- Trigger: missing active shape recipe/presentation length basis/prefix, a same-ID recipe in the wrong profile/component, or unscoped alias.mark.
- Behavior: absent recipe defaults to Fabrication; secondary recipe search bypasses profile/component/family; missing prefixes default 1..8/D1; alias lookup accepts global mark key without proving exact member-set evidence.
- Violation/impact: S5 and F49 require exact scoped recipes and approved prefixes/alias member sets; absent binding should block. A valid-looking group can silently use the wrong basis/mark/shape or global alias.
- Corrective package: W03/W08. Acceptance: wrong-profile recipe and missing per-active-field values must yield exact typed binding/scope failure; alias allowed only with matching host/face/layer/member set and provenance. Include equal text marks on different hosts without cross-joining.

### D12 — P1 — Catch drawing replacement is gated by reinforced neck views

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/ViewBuilder.cs:398`, 414–477.
- Trigger: active type3 catch with neck absent or nonreinforced (there are no /Neck/ reinforcement views), while catch physical components/rebar exist.
- Behavior: all three dynamic catch views are created inside the condition requiring both a Neck component and existing /Neck/ views. With the neck condition false, the catch branch never executes. W12 regression fixture includes a neck bar, so it misses this case.
- Violation/impact: S6 active Catch role coverage and W12 dynamic replacement prerequisite; removal of the catch static module leaves active catch without required Plan/1-1/2-2 views for this valid input combination.
- Corrective package: W10/W12. Acceptance: catch activity and neck structure/activity cross-product; required catch views must exist from actual assembly independent of neck reinforcement, or fail explicitly when engineering binding is missing.

### D13 — P2 — S9 sheet hash ignores scale, transforms and ink

- Location: `C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/StageAdapters.cs:994`–995.
- Trigger: change scale, asset/frame bounds, ink or view translation while preserving frame IDs and placement counts.
- Behavior: sheet_hash hashes only `frame.id:placements.Length`. Different drawings have identical sheet hash.
- Violation/impact: S9 requires HASH_STATE(out); provenance/readback expectations cannot detect changed placements or geometry envelope. This is a deterministic hash collision by construction, not a cryptographic SHA collision.
- Corrective package: W11. Acceptance: independently vary every schema field that changes layout; normalized deterministic state hash must change, while identical state and ordering policy remain stable.

## Coverage and boundaries

Read all current new planner sources in W03/W04: PresentationGroupPlanner, AssemblyBoundaryEmitter, RuleApprovalRegistry, NativeSteelSchedulePlanner, SchematicPresentationPlanner, AnnotationLaneAllocator, CatchAssemblyProjector. Read W08–W12 production changes: CalculationPipeline, DrawingInputs, PresentationGroup, ScheduleNativeAttributes, StageAdapters S5–S9 and dispatcher call chain, BendingSchematicPlanner, ScheduleTablePlanner, SheetFiveViews/Placement, SheetThreeRoofViews/Detailer, RoofJointDowelProjection, ViewBuilder, DrawingPackage/Validator, Family21ManholeHelper, SheetFourViews, SheetPlan. Traced real palette/headless entrypoints, legacy SheetLayoutPlanner/SheetTwo/Three placement callers and AutoCAD CadRenderer table boundary. Read W09–W12 drawing test sources and S6–S9 application tests; Phase6GenerationCommands reviewed as a scratch-only diagnostic entrypoint, not product acceptance evidence.

Six W12 deletions verified absent: Family21SheetTwoStore, Family21SheetThreeStore, Family21SheetFourStore, Family21SheetFiveStore, SheetTwoViews, SheetFourCatchBasinViews. No remaining production references to those six names were found. Their absence alone is not a finding. Remaining Family21ManholeHelper code contains size/name-specific placements with actual legacy callers, but obsolete literal G500 strings alone were not promoted to findings.

No claim is made of native DWG/PDF output, save/reopen correctness or host acceptance. P1 findings are code-level contract failures and remain relevant even if unrelated unit tests pass. S9 origins all (0,0), hardcoded content/asset/tolerance fallbacks, auto-scale retry lacking approved next candidate, catch-only dimensions using body clear spans, and projected curve IDs based only on local CurveV2.id are additional source concerns for parent reproduction/consolidation; they were not needed to establish the findings above.
