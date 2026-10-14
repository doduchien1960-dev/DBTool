# Đánh giá lại đủ 59 check records

Reported là lời ghi trong receipt; Adjudicated là kết luận review hiện tại. Không có check nào trong bảng này được dùng để chứng nhận DWG/PDF. File code tồn tại hoặc unit suite xanh chỉ hỗ trợ phạm vi software tương ứng.

| W/check | Tên | Reported | Adjudicated | Finding / lý do |
|---|---|---|---|---|
| W01/1 | ReferenceComparison_InputMismatch_Separation | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W01/2 | ReleaseEvidence_VerdictIntegrity | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W01/3 | SemanticDiff_PhysicalManifestAndClassification | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu R01. |
| W02/1 | StageDTO_Schema_Exactness | Passed | VerifiedStructuralOnly | 103 record và từng field/type khớp spec; đây chỉ là schema, không chứng minh dữ liệu runtime. |
| W02/2 | TemplateContract_RoleAndTagValidation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G08, G09. |
| W02/3 | CalculationPolicy_ApprovedRulesGating | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W02/4 | ExcelImporter_FieldContractMapping | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G02. |
| W02/5 | Architecture_DependenciesAndImmutability | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W03/1 | PresentationGroupPlanner_MemberCoverage | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D11. |
| W03/2 | AssemblyBoundaryEmitter_CSG_Regions | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D08. |
| W03/3 | RuleApprovalRegistry_ApprovedSeed_23Proposals | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W03/4 | Architecture_DependenciesAndImmutability | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W04/1 | CatchAssemblyProjector_ThreeViews | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D01. |
| W04/2 | SchematicPresentationPlanner_CaptionsAndGlyphs | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D04, D05. |
| W04/3 | NativeSteelSchedulePlanner_RowsAndSummaries | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D03, D04, D05, D09. |
| W04/4 | AnnotationLaneAllocator_SharedInkAndRouting | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D05, D06, D07. |
| W04/5 | Architecture_TestSuiteCoverage | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W05/1 | BatchGenerationOrchestrator_Signature_And_Isolation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N01, N11. |
| W05/2 | NativeReadbackVerifier_Save_Reopen_Validation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N04. |
| W05/3 | AcceptanceEvidenceAssembler_S11_Records | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N05, N06, N11. |
| W05/4 | Architecture_DependenciesAndImmutability | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W06/1 | StageAdapters_S2_S3_Implementation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G01, G02, G03, G07. |
| W06/2 | SheetOneDrawingPolicies_Preservation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G10. |
| W06/3 | AccessoryAndCatchBasin_Extensions | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G02, G03. |
| W06/4 | Architecture_DependenciesAndTestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W07/1 | StageAdapters_S4_FullRebarStage_Implementation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G04, G05, G06, G07. |
| W07/2 | RebarPlanners_PhysicalProvenance_And_CanonicalSignatures | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G11, G12. |
| W07/3 | FeatureSupport_RebarCapabilities | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G02, G06. |
| W07/4 | Architecture_DependenciesAndTestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W08/1 | PresentationGroup_S5_PreDrawing_Planning | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D10. |
| W08/2 | ScheduleNativeAttributes_21_Exact_Block_Schemas | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D03, D09. |
| W08/3 | DrawingStageAdapter_S5_Carrier | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D10. |
| W08/4 | CalculationPipeline_PreDrawing_Integration | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D10. |
| W08/5 | Architecture_TestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W09/1 | BendingSchematic_RecipeId_And_GlyphAsset | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D04, D05. |
| W09/2 | ScheduleTablePlanner_OmitGrid_OnVhgPath | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W09/3 | SheetFiveViews_And_Placement_DynamicBending | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D02. |
| W09/4 | SteelScheduleStageAdapter_IS8StageV2_Wiring | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D03, D09. |
| W09/5 | Architecture_TestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W10/1 | DrawingStageAdapter_IS6_IS7_Wiring | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D01, D05, D06, D07. |
| W10/2 | SheetThreeRoofViews_RetainSemanticIds_DropMockBounds | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W10/3 | RoofJointDowelProjection_PreserveHole1to1_AndScalePaperMetrics | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu G05. |
| W10/4 | SheetThreeRoofDetailer_TargetMemberFromMetadata | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D10. |
| W10/5 | Architecture_TestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W11/1 | DrawingStageAdapter_IS9_LayoutPagination | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D02, D13. |
| W11/2 | DrawingPackage_ExpandedContract | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W11/3 | DrawingPackageValidator_ValidatePackageCompleteness | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D01, D02. |
| W11/4 | SheetFourViews_RetainSemanticIds_DropMockBounds | Passed | PartiallySupported_NotNative | Có thay đổi/guard trong source và software evidence; chưa chứng minh toàn bộ predicate bằng dữ liệu thật hoặc native. |
| W11/5 | SheetPlan_FrameRegistry_And_PageLedger | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D02. |
| W11/6 | Architecture_TestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W12/1 | Family21Store_And_StaticViews_PermanentRemoval | Passed | VerifiedStructuralOnly | Đã kiểm deletion/archive/no production symbol; phần thay thế chức năng vẫn chưa đạt D12 và D01. |
| W12/2 | ZeroActiveReferences_To_RemovedModules | Passed | VerifiedStructuralOnly | Đã kiểm deletion/archive/no production symbol; phần thay thế chức năng vẫn chưa đạt D12 và D01. |
| W12/3 | CallerUpdates_SheetFourViews_SheetFiveViews_ViewBuilder | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu D12. |
| W12/4 | ArchiveManifest_Created_With_Hashes | Passed | VerifiedStructuralOnly | Đã kiểm deletion/archive/no production symbol; phần thay thế chức năng vẫn chưa đạt D12 và D01. |
| W12/5 | Architecture_TestSuiteZeroRegression | Passed | SoftwareEvidenceOnly | Số unit tests/architecture checks không chứng minh pipeline thật hoặc DWG/PDF; đã chạy lại, vẫn có counterexamples. |
| W13/1 | ProductionPipeline_S0_Through_S11_Wiring | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N01, N03. |
| W13/2 | CadGenerationWriter_NativeGenerationPort_Implementation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N02, N04, N07, N12. |
| W13/3 | Palette_PresentationRequest_Configurability | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N09. |
| W13/4 | Headless_Scale_And_Identity_Validation | Passed | ContradictedOrIncomplete | Mệnh đề Passed rộng hơn hành vi code đã kiểm; đối chiếu N09. |

Đối với mọi check yếu: thay bằng observed input→output qua đúng public entrypoint, có source/hash, expected độc lập và negative mutation. Native checks phải trỏ tới DWG/PDF/handle thật; không dùng tên thư mục project thay log hoặc artifact.
