# CODE LIVE VÀ RECEIPT — BEFORE/AFTER HASH

| Code live đã sửa | BeforeSHA256 | AfterSHA256 |
| --- | --- | --- |
| [DBTool/DBTool.Application/Templates/TemplateContractValidator.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application/Templates/TemplateContractValidator.cs>) | 8938f360ebf512c3bc3a77cb88d086315ed6c14adeefdad8aa4fa66dbd3810b2 | 2aecc8a981c060bc46cc1f9c3b881187845494a8e94abf072a3e7df9087ba713 |
| [DBTool/DBTool.Drawing/PresentationGroupPlanner.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Drawing/PresentationGroupPlanner.cs>) | c786c3d700882747ccabf33fae81bae9a1478512070a11420c031fb0d63f4a2d | 255116bf87118acc444fb8beaabd36d788caffb437b846d385981dd3b9f3ce6f |
| [DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Domain/StageContractsV2/Domain.ContractsV2.cs>) | f62ed2f5ae81415592b1c6251620ad1ab374498c29dad86c258f5f601595843f | 3e9cab6590874746659d2c03ddb40c9d439413409731269762d13de11431a58f |
| [DBTool/DBTool.Application.Tests/W02RemediationTests.cs](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/DBTool/DBTool.Application.Tests/W02RemediationTests.cs>) | 4984b85a4bf917217ac58b0adcc9aeb7595186c7c87c44a61c58ab3487ba8f8d | 4bb9141d79917ca206a0243894994c939b2427756d4d718b886babe499921ea1 |

Snapshot trước sửa: [manifest](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/snapshots/route1-before/manifest.json>); [log scriptapplygốc](<C:/Users/DucBao/Desktop/Dbtool/Ver 1/outputs/leader-acceptance/leader-field-contracts-20261013-r2/20261015-leader-live-remediation-04/evidence/route1-apply/output.log>). Script dùng --root=Ver1 vì manifest đã có tiền tốDBTool/; bốn file đích thực tế đều nằm trong Ver1/DBTool/, không có thư mụcDBTool/DBTool.Domain bị lồng sai. Không forceapply.

| Receiptlive | SHA256hiện tại | Modifier / scope |
| --- | --- | --- |
| [W01receipt](<C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W01/receipt.json>) | 7a432aaffd4a493ac75f243501aac4b32c54f47bdc7032b0fb174f4c7402a13f | leader; evidencecorrection, không sửa code |
| [W02receipt](<C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W02/receipt.json>) | fe82b388d20f5f056eeab810b5a9d6b149044293a9b94871c2f8c02b713cd590 | leader; codeRoute1 |
| [W03receipt](<C:/Users/DucBao/Desktop/Dbtool/outputs/leader-implementation/leader-field-contracts-20261013-r2/packages/W03/receipt.json>) | f87ce45f6898a4e71cb1240bd664ceca43abc17d1b8efdea82810b142120896a | leader; codeRoute1 |

## Chuỗi amendment giữ lại đầy đủ

| Gói | BeforeReceiptSHA | AfterReceiptSHA | PreviousReceiptSHA |
| --- | --- | --- | --- |
| W01 | 87cf9555151fc1a4b45fb894806f61f9ac78cc705fba9fdbf6a0199976964e14 | ad63fd6ad359940853860521e62deb19cea07ff7fdcf02430eba09033214fcf6 | 87cf9555151fc1a4b45fb894806f61f9ac78cc705fba9fdbf6a0199976964e14 |
| W02 | f6d677e4d14ed4d2597924a3a035b72803f6a5b8800102001def223413e7446f | 44c70b2fd659cb96f85bd5207666513b651d533679370168404309ecab015142 | f6d677e4d14ed4d2597924a3a035b72803f6a5b8800102001def223413e7446f |
| W03 | def1b40b672b05c06f2b8e0a7276f5a9bd82bc5389e9ecb64bc6cc2b8a996091 | ceeefde038a2090917262dde269d180d8e2a7fe0ee8d1dcfa2c1817948ab34c5 | def1b40b672b05c06f2b8e0a7276f5a9bd82bc5389e9ecb64bc6cc2b8a996091 |
| W01 | ad63fd6ad359940853860521e62deb19cea07ff7fdcf02430eba09033214fcf6 | 69a77b03e9edb820f28547dff469737acb206509bee0ecd3be9248dd398ee86c | ad63fd6ad359940853860521e62deb19cea07ff7fdcf02430eba09033214fcf6 |
| W02 | 44c70b2fd659cb96f85bd5207666513b651d533679370168404309ecab015142 | 5a7d3a10a14c48228ce8f68f33fee087d2d24637bbcd73626eca2e2251772b96 | 44c70b2fd659cb96f85bd5207666513b651d533679370168404309ecab015142 |
| W03 | ceeefde038a2090917262dde269d180d8e2a7fe0ee8d1dcfa2c1817948ab34c5 | c61ed58db82ffdd2b93a5c33327c00cbfa74ae99b4a62a445bde971414b3156f | ceeefde038a2090917262dde269d180d8e2a7fe0ee8d1dcfa2c1817948ab34c5 |
| W01 | 69a77b03e9edb820f28547dff469737acb206509bee0ecd3be9248dd398ee86c | 7a432aaffd4a493ac75f243501aac4b32c54f47bdc7032b0fb174f4c7402a13f | 69a77b03e9edb820f28547dff469737acb206509bee0ecd3be9248dd398ee86c |
| W02 | 5a7d3a10a14c48228ce8f68f33fee087d2d24637bbcd73626eca2e2251772b96 | fe82b388d20f5f056eeab810b5a9d6b149044293a9b94871c2f8c02b713cd590 | 5a7d3a10a14c48228ce8f68f33fee087d2d24637bbcd73626eca2e2251772b96 |
| W03 | c61ed58db82ffdd2b93a5c33327c00cbfa74ae99b4a62a445bde971414b3156f | f87ce45f6898a4e71cb1240bd664ceca43abc17d1b8efdea82810b142120896a | c61ed58db82ffdd2b93a5c33327c00cbfa74ae99b4a62a445bde971414b3156f |

Có hai chỉnh lý metadata trong khi biên soạn receipt: đầu tiên sửa bộ đọc kết quả W01 từ khóa pass sang predicatePassed (để không gán sai Failed cho positivecontrol); tiếp theo ràng buộc Actual với observation live, bỏ claim sourcehash cũ củaG08 vì L04 đã sửa phần đó, và phân biệt nativeR03 với R04. Không thay code hay findingstatus trong hai chỉnh lý này. Từng phiên bản trước chỉnh lý được lưu và nối hash thật; không xóa dấu vết.

Mọi receipt hiện có InterfaceGate=Blocked, NativeGate/FullPipelineNativeGate=NotRun, IndependentAcceptanceRequired=true, BatchGOIssuedByLeader=false. Các Check Passed chỉ là predicate đã thực thi; không kýPassed cho package/nativepipeline chưa chạy.
