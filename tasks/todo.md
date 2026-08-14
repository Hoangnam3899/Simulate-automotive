# Todo: Vector Hardware Gateway & CAN Simulation Backend

> Trạng thái ban đầu: PLAN ONLY — chưa triển khai code.
>
> UI LOCK: Không sửa `App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs` hoặc file UI/XAML nào nếu chưa có yêu cầu và cho phép rõ ràng từ người dùng.
>
> Coordinator status (2026-08-12): Task 9 đã DONE — implementation `Terra high` và independent review `Luna high` đều PASS; focused tests 17/17 và full suite 115/115, build 0 warning/0 error. Tasks 7-8 đã commit/push; Task 9 chưa commit/push theo rào chắn người dùng. UI diff bằng không, hardware thật còn `NEEDS_VERIFY`. Task completion reminder đã được bật. Bàn giao chi tiết: [`handoff.md`](../handoff.md).

## Coordinator execution order

| Thứ tự | Task | Lead model | Gate trước khi chạy |
|---|---|---|---|
| 1 | Task 0 — Approval/baseline | Luna high | Xác nhận test project/NuGet, x64 và cấu trúc module |
| 2 | Task 1 — Hardware/session seam | Sol xhigh | Task 0 hoàn tất |
| 3 | Task 2 — Mock gateway | Terra high | Contract Task 1 được review |
| 4 | Tasks 3-5 — Vector lifecycle, Classic, FD | Sol ultra | Mock contract tests pass |
| 5 | Task 6 — Async connection | Terra xhigh | Hardware session ổn định |
| 6 | Tasks 7-9 — DBC, codec/E2E, validation | Terra/Sol | Không cần UI approval |
| 7 | Tasks 10-12 — Gateway/inject/scheduler | Sol ultra/xhigh | Hardware + simulation domain pass |
| 8 | Tasks 13-15 — ViewModel/test/review | Terra/Luna/Sol | UI vẫn bị khóa |

## Task completion reminder — bắt buộc sau mỗi task

- [ ] Cập nhật checkbox/status trong PLAN và todo.
- [ ] Chạy build/test phù hợp và ghi kết quả.
- [ ] Kiểm tra `git diff --check`, UI diff và file scope.
- [ ] Ghi work log và `NEEDS_VERIFY`/approval còn thiếu.
- [ ] Cập nhật [`handoff.md`](../handoff.md) với next action và model tiếp theo.
- [ ] Báo cáo người dùng task đã xong và nhắc model/reviewer của task kế tiếp.

## Task 0: Chốt approval gates và baseline

**Description:** Ghi nhận build/test baseline, dirty worktree và xin duyệt mọi thay đổi package/project structure trước implementation.

**Acceptance criteria:**
- [x] Ghi lại kết quả `dotnet build Simulate.sln` hiện tại: PASS ngày 2026-08-10, 0 warning, 0 error.
- [x] Người dùng cho phép tạo `Simulate.Tests` và package test tương ứng ở task implementation phù hợp.
- [x] Người dùng cho phép đánh giá/áp dụng target x64 khi verification Vector DLL yêu cầu; chưa đổi target trong Task 0.

**Verification:**
- [x] `git status --short` đã được ghi nhận; thay đổi của người dùng không bị ghi đè.
- [x] UI diff bằng không.
- [x] `dotnet build Simulate.sln`: PASS, 0 warning, 0 error.
- [x] Người dùng đã xác nhận approval decisions cho test project/NuGet, target x64 và module backend.

**Dependencies:** None
**Files likely touched:** chỉ tài liệu PLAN cho đến khi có approval
**Estimated scope:** XS
**Model allocation:** Lead `Luna high`; review `Terra high`
**Skills khi triển khai:** `context-engineering`, `git-workflow-and-versioning`

## Task 1: Định nghĩa domain contracts và hardware session seam

**Description:** Thiết kế typed models cho frame, endpoint, bus mode, bitrate, connection options/result và seam tạo gateway session mà không lộ type của Vector XL.

**Acceptance criteria:**
- [x] Contract biểu diễn được CAN Classic/CAN FD, standard/extended ID và hai phía RX/TX.
- [x] `ICanHardwareDriver` có surface nhỏ cho discovery và mở session.
- [x] Session định nghĩa receive/transmit/flush/stop và cleanup idempotent.

**Verification:**
- [x] Contract review bằng deletion/depth test: PASS bởi `Terra xhigh`, không còn finding Critical/Required.
- [x] `dotnet build Simulate.sln` thành công: 0 warning, 0 error.
- [x] UI diff bằng không.

**Dependencies:** Task 0
**Files likely touched:** `Models/*.cs`, `Services/ICanHardwareDriver.cs`
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Terra xhigh`
**Skills khi triển khai:** `api-and-interface-design`, `codebase-design`

## Task 2: Nâng Mock adapter thành in-memory gateway session

**Description:** Cung cấp adapter test có hai chiều frame, fault injection cho lifecycle và state quan sát được qua cùng hardware seam.

**Acceptance criteria:**
- [x] Có thể enqueue frame từ RX/TX và quan sát frame phát tới phía đối diện.
- [x] Có thể mô phỏng lỗi discovery/open/configure/activate/transmit.
- [x] Stop/dispose gọi nhiều lần không lỗi và không phát frame sau stop.

**Verification:**
- [x] Contract tests chạy trên Mock adapter: 10/10 PASS.
- [x] Build/test thành công: 0 warning, 0 error.
- [x] UI diff bằng không.

**Dependencies:** Task 1
**Files likely touched:** `Services/MockHardwareService.cs`, model/session test files
**Estimated scope:** M
**Model allocation:** Lead `Terra high`; review `Luna high`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Task 3: Hardening Vector discovery và native lifecycle

**Description:** Sửa ownership của driver/port, kiểm tra mọi `XL_Status`, typed error và cleanup đầy đủ khi lỗi giữa chuỗi open/configure/activate.

**Implementation status:** `DONE` — `Sol ultra` implementation và `Terra xhigh` independent review đều PASS.

**Acceptance criteria:**
- [x] Không còn nhánh activation/configuration failure làm rò port/driver.
- [x] Discovery dùng `try/finally` và lọc channel CAN hợp lệ.
- [x] Error result giữ operation, `XL_Status` và message chẩn đoán.

**Verification:**
- [x] Fault-path tests bằng seam/wrapper phù hợp: 11 test Vector lifecycle mới, toàn suite 21/21 PASS.
- [x] Đối chiếu API với `XL Driver Library Manual 20.30` và wrapper `vxlapi_NET` 25.20.14.0 trong `Doc/`.
- [x] `dotnet build Simulate.sln`: PASS, 0 warning/0 error; UI diff bằng không.
- [ ] `NEEDS_VERIFY`: cắm hardware Vector thật để xác minh trạng thái native/driver thực tế ở Task 15.

**Dependencies:** Tasks 1-2
**Files likely touched:** `Services/VectorHardwareService.cs`, supporting models/tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh`
**Skills khi triển khai:** `source-driven-development`, `test-driven-development`

## Task 4: Classic CAN frame I/O hai chiều

**Description:** Mở RX/TX physical channels, nhận và phát Classic CAN frame, bảo toàn standard/extended ID và đóng session đúng thứ tự.

**Implementation status:** DONE — implementation `Sol ultra`, test review `Luna xhigh` PASS và code review `Terra xhigh` PASS.

**Acceptance criteria:**
- [x] Dùng interface V3 và Classic CAN receive/transmit APIs đúng wrapper hiện có.
- [x] RX/TX channel không được trùng; permission/configuration failure trả typed error.
- [x] Receive/transmit/flush/cancel hoạt động qua session seam.

**Verification:**
- [x] Contract/integration tests bằng Mock và fake Vector SDK boundary.
- [x] Manual Vector checklist đã tạo tại [`tasks/vector-classic-can-hardware-checklist.md`](vector-classic-can-hardware-checklist.md); mục chưa cắm hardware ghi `NEEDS_VERIFY`.
- [x] Build/test sạch, UI diff bằng không tại checkpoint implementation.

**Dependencies:** Task 3
**Files likely touched:** Vector adapter/session files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; test review `Luna xhigh`; code review `Terra xhigh`
**Skills khi triển khai:** `source-driven-development`, `test-driven-development`

## Task 5: CAN FD frame I/O hai chiều

**Description:** Thêm mode CAN FD dùng interface V4, nominal/data bitrate, DLC-length mapping và transmit/receive event phù hợp.

**Implementation status:** `DONE` — implementation `Sol ultra`, independent code review `Terra xhigh` PASS.

**Acceptance criteria:**
- [x] Cấu hình arbitration/data bitrate được validate và kiểm tra status.
- [x] Payload length/DLC hợp lệ cho CAN FD; classic path không bị thay đổi hành vi.
- [x] Session cleanup/cancellation đạt cùng contract với Classic CAN.

**Verification:**
- [x] Golden tests cho toàn bộ DLC `0..15` và payload length tương ứng.
- [x] Contract tests chạy cho cả Classic V3 và CAN/CAN FD V4 modes.
- [x] `dotnet build Simulate.sln --no-restore`: PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore`: PASS 65/65; UI diff bằng không.
- [x] Manual checklist đã tạo tại [`tasks/vector-can-fd-hardware-checklist.md`](vector-can-fd-hardware-checklist.md).
- [ ] `NEEDS_VERIFY`: Vector hardware thật, CAN FD mode/bit timing, timestamp/latency và soak 50 chu kỳ.

**Dependencies:** Task 4
**Files likely touched:** Vector adapter/session, CAN FD mapping và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh`
**Skills khi triển khai:** `source-driven-development`, `test-driven-development`

## Task 6: Async connection orchestration

**Description:** Chuyển discovery/connect/disconnect sang async command, busy state và cancellation trong ViewModel, giữ nguyên binding paths hiện tại.

**Implementation status:** `DONE` — Terra xhigh đã sửa finding Required, Luna high re-review PASS; đã commit, chưa push.

**Acceptance criteria:**
- [x] Constructor không gọi hardware blocking trên Dispatcher.
- [x] Connect/refresh/disconnect không chạy đồng thời và có state/error rõ ràng.
- [x] `Connection.*` bindings và public command names hiện tại được giữ nguyên.

**Verification:**
- [x] ViewModel tests với Mock adapter và public session seam: 6 Task 6 tests.
- [x] Build/test sạch: 0 warning/0 error, 71/71 PASS.
- [x] Không sửa XAML hoặc code-behind UI.

**Dependencies:** Tasks 2, 4, 5
**Files likely touched:** `ViewModels/ConnectionViewModel.cs`, `ViewModels/MainViewModel.cs`, tests
**Estimated scope:** M
**Model allocation:** Lead `Terra xhigh`; review `Luna high`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Work log — 2026-08-11 (Task 6 Terra xhigh implementation)

- [x] `ConnectionViewModel` giờ dùng trực tiếp deep seam `ICanHardwareDriver`; constructor không còn discovery/call native blocking.
- [x] Giữ nguyên public command names/binding hiện hữu `RefreshInterfacesCommand`, `ConnectCommand`, `DisconnectCommand`; các command nay async, bị khóa theo `IsBusy`, serialize bằng atomic operation gate và có `CancelPendingOperation()`.
- [x] Session gateway được ViewModel sở hữu: open thành công mới đặt `IsConnected`; disconnect luôn stop/dispose session và giữ typed `LastFailure` cho lỗi hardware/cleanup.
- [x] Xóa transitional `ICanConnectionDriver` và legacy sync path khỏi Vector/Mock adapters; `VectorHardwareService` chạy discovery/open native trên worker thread nhưng giữ nguyên `ICanHardwareDriver` contract.
- [x] Thêm ViewModel seam tests: constructor không discovery, refresh/select mặc định, connect/disconnect, typed open failure và cancellation/command concurrency.
- [x] Verification: targeted formatter PASS; `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 70/70; `git diff --check` và UI/XAML/code-behind/project scope diff PASS.
- [x] Đã sửa finding Required: stop/dispose được schedule ngoài Dispatcher cho disconnect và cancel-after-open; regression test chặn đồng bộ chứng minh command trả control trước native cleanup.
- [x] Luna high re-review PASS: acceptance criteria đạt, Required finding đã được giải quyết; Task 6 DONE. Đã commit, push vẫn chờ lệnh người dùng.

## Work log — 2026-08-11 (Task 6 Luna high review)

- [x] Axis Spec: đạt constructor không discovery, async command names/binding giữ nguyên, busy/serialization/cancellation, typed error, session ownership và ViewModel seam tests.
- [x] Axis Standards: boundary `ICanHardwareDriver` sạch, Vector open/discovery chạy worker thread, không secret/package/project/UI diff; build 0 warning/0 error và test 70/70 PASS.
- [x] **Required resolved:** disconnect cleanup từng gọi trực tiếp `session.StopAsync()`/`DisposeAsync()` trong khi `VectorCanGatewaySession` thực thi `_resources.Cleanup()` đồng bộ; Terra fix đưa cleanup ra worker task và thêm regression test seam.
- [x] Luna high re-review PASS: không còn finding Critical/Required; Task 6 DONE.

## Work log — 2026-08-11 (Task 6 Terra xhigh Required-finding fix)

- [x] `ConnectionViewModel` đưa `ICanGatewaySession.StopAsync()` và `DisposeAsync()` ra worker task ở disconnect và cancel-after-open; không đổi session/interface public hay binding/UI.
- [x] Regression test `Disconnect_returns_control_while_session_cleanup_runs` dùng public session seam có cleanup chặn đồng bộ: test RED trước fix, GREEN sau fix; đồng thời xác nhận disconnect hoàn tất và session dispose.
- [x] Verification: targeted formatter PASS; `dotnet test Simulate.sln --no-restore` PASS 71/71; `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `git diff --check` và UI/XAML/code-behind/project scope diff PASS.
- [x] Luna high re-review xác nhận finding Required đã được giải quyết; Task 6 DONE, đã commit, chưa push.

## Task 7: DBC domain và parser tối thiểu

**Description:** Parse các phần DBC cần cho simulation: node/message/signal, CAN ID, DLC, start bit, length, endian, signedness, factor, offset, min/max và unit.

**Input data available:** 8 DBC files đã được copy nguyên trạng vào `DBC/`: 4 CAN Classic (`DBC/CAN class`) và 4 CAN FD (`DBC/CAN FD`). SHA-256 đã đối chiếu khớp với `C:\Users\Hnam\Downloads\DBC`.

**Implementation status:** `DONE` — Terra xhigh đã hoàn tất implementation/fix; Luna xhigh re-review PASS.

**Acceptance criteria:**
- [x] Parser trả typed result kèm lỗi line/context; không phụ thuộc WPF.
- [x] Standard và extended CAN ID được chuẩn hóa nhất quán với `CanFrame`.
- [x] Unsupported construct được báo rõ, không silently đoán.

**Verification:**
- [x] Fixture tests gồm file hợp lệ, malformed và boundary values.
- [x] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 1
**Files likely touched:** DBC model/parser files và tests
**Estimated scope:** M
**Model allocation:** Lead `Terra xhigh`; review `Luna xhigh`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Work log — 2026-08-11 (Task 7 Terra xhigh implementation)

- [x] Thêm glossary `CONTEXT.md` cho DBC document, node, message, signal, normalized CAN identifier và parse issue.
- [x] Thêm domain bất biến `DbcDocument`/`DbcNode`/`DbcMessage`/`DbcSignal` cùng `DbcParseResult`/`DbcParseIssue`; parser là pure static seam `DbcParser.Parse(string)` không phụ thuộc WPF hay hardware.
- [x] Parse core `BU_`, `BO_`, `SG_`: node/message/signal, CAN ID, payload length, start bit, bit length, endian, signedness, factor, offset, min/max, unit và receivers.
- [x] Chuẩn hóa raw extended DBC identifier theo cờ bit 31 thành `Identifier` 29-bit + `IsExtendedIdentifier`, đúng boundary của `CanFrame`.
- [x] Core malformed definitions trả error có severity/code/line/context; metadata và multiplexing ngoài phạm vi Task 7 trả warning rõ ràng, không tạo signal partial hoặc silently diễn giải.
- [x] Thêm 7 test parser: valid typed metadata, standard/extended ID, ID/payload boundaries, malformed ID, multiplex warning, unsupported statement warning và đọc cả 8 DBC thật.
- [x] Verification: formatter targeted PASS; `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-restore` PASS 78/78; `git diff --check` và UI/XAML/code-behind/project scope diff PASS.
- [x] `Luna xhigh` independent review và re-review đã hoàn tất; Task 7 chưa commit/push theo rào chắn người dùng.

## Work log — 2026-08-11 (Task 7 Luna xhigh review)

- [x] Axis Spec: core node/message/signal, normalized standard/extended ID, physical signal metadata, typed line/context issues và explicit unsupported warnings đều khớp Task 7.
- [x] **Required finding đã phát hiện:** `DbcParser` chỉ kiểm tra `startBit < payloadBits`, chưa kiểm tra toàn bộ signal span theo `bitLength` và endian. Ví dụ `BO_ ...: 8` + `SG_ Bad : 63|2@1+ ...` đang được nhận dù signal vượt payload; đã bổ sung regression test và validation.
- [x] Axis Standards: pure boundary không phụ thuộc WPF/hardware, immutable result, no UI/project/package diff; formatter PASS, build 0 warning/0 error, full test 78/78 PASS.
- [x] Terra xhigh đã sửa finding: little-endian kiểm tra span liên tiếp; big-endian kiểm tra DBC sawtooth bit order; hai regression tests `63|2@1+` và `56|2@0+` trong payload 8 byte đều trả `InvalidSignalLayout`.
- [x] Verification sau fix: DBC parser tests 9/9 PASS; `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-restore` PASS 80/80; formatter targeted và `git diff --check` PASS.
- [x] Luna xhigh re-review PASS: signal-span validation đã xử lý little-endian liên tiếp và big-endian DBC sawtooth; không còn finding Critical/Required. Task 7 DONE.

## Task 8: Signal codec và E2E protection

**Description:** Tạo pure module pack/unpack signal và E2E counter/CRC dùng payload hiện hữu làm baseline.

**Implementation status:** `DONE` — Sol xhigh implementation và Luna xhigh independent review đều PASS.

**Acceptance criteria:**
- [x] Hỗ trợ endian/signedness/scale đã chốt ở Task 7.
- [x] Không sửa bit ngoài signal target.
- [x] E2E chỉ áp dụng khi enabled và có bounds validation.

**Verification:**
- [x] Golden vectors độc lập cho pack/unpack và CRC/counter.
- [x] Boundary tests cho min/max, overflow và payload ngắn.
- [x] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 7
**Files likely touched:** signal codec/E2E files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Luna xhigh`
**Skills khi triển khai:** `test-driven-development`, `source-driven-development`

## Work log — 2026-08-11 (Task 8 Sol xhigh implementation)

- [x] Đối chiếu `SignalEncoder.cs`/`E2EHelper.cs` của dự án tham chiếu và AUTOSAR CRC/E2E source; giữ CRC-8/SAE-J1850 + alive counter nhưng không tuyên bố full AUTOSAR Profile vì Task 8 chưa có Data ID/Profile mode.
- [x] Thêm pure `SignalCodec` pack/unpack physical value cho little-endian liên tiếp và big-endian DBC sawtooth; hỗ trợ signed two's-complement, factor/offset, min/max, raw range và payload tối đa 64 byte.
- [x] Mọi validation pack chạy trước mutation; golden vectors xác nhận các bit ngoài signal target được giữ nguyên và payload invalid không bị sửa dở.
- [x] Thêm immutable `E2eProtectionConfiguration`/result, `Crc8SaeJ1850` và stateless `E2eProtector`; disabled là no-op, enabled validate index/range/mask/counter trước khi ghi counter rồi CRC.
- [x] Task 8 focused tests 18/18 PASS; full suite 98/98 PASS. Build Debug qua isolated output và Release đều PASS 0 warning/0 error; targeted formatter và `git diff --check` PASS; UI/XAML/project diff bằng không.
- [x] Luna xhigh independent review hai trục spec/standards PASS: không có finding Critical/Required; các biên endian/signed/range/mutation/E2E được đối chiếu với acceptance và test vector.
- [x] Task 8 DONE; commit `7ad4e72` đã được push. Next action: Task 9 — lead `Terra high`, review `Luna high`.

## Task 9: Simulation configuration và validation

**Description:** Thay model placeholder bằng typed simulation plan gồm message rule, gateway mode, overrides, send type, timing và E2E config.

**Implementation status:** `DONE` — Terra high implementation và Luna high independent review đều PASS.

**Acceptance criteria:**
- [x] Mode/send type dùng enum hoặc discriminated model, không dùng string làm logic.
- [x] Validate duplicate CAN ID, invalid timing, missing DBC reference và out-of-range override.
- [x] Model không phụ thuộc control/WPF type.

**Verification:**
- [x] Validation tests cho happy path và invalid combinations.
- [x] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 7-8
**Files likely touched:** simulation model/validation files và tests
**Estimated scope:** M
**Model allocation:** Lead `Terra high`; review `Luna high`
**Skills khi triển khai:** `domain-modeling`, `test-driven-development`

## Work log — 2026-08-12 (Task 9 Terra high implementation)

- [x] Thêm immutable `SimulationPlan`, `SimulationMessageRule`, `SimulationTiming` và `SignalOverride`; `GatewayMode`/`SimulationSendType` là enum, model chỉ tham chiếu DBC/E2E domain và không có WPF/Vector/UI type.
- [x] Validation tại public boundary chặn enum không xác định, duplicate CAN ID theo cặp normalized ID + extended state, message/signal không tồn tại trong DBC, duplicate override, override rỗng/non-finite/out-of-range và timing/schedule mâu thuẫn.
- [x] TDD qua public `SimulationPlan` seam: happy path và 17 focused assertions PASS; full suite 115/115 PASS, build 0 warning/0 error, targeted formatter và `git diff --check` PASS; UI/XAML/project diff bằng không.
- [x] Self-review hai trục spec/standards: ambiguity OneShot repeat khác 1 đã được fix bằng regression tests; không còn finding Critical/Required.
- [x] Luna high independent review hai trục spec/standards PASS: identity DBC, timing, duplicate/missing references, override bounds và pure boundary đều đạt; không có finding Critical/Required.
- [x] Task 9 DONE; chưa commit/push theo rào chắn người dùng. Next action: Task 10 — lead `Sol ultra`, review `Terra xhigh`.

## Task 10: Gateway hai chiều với PassThrough và Block

**Description:** Chạy receive loop cancellable cho RX→TX và TX→RX; áp rule pass-through/block và thống kê observable.

**Implementation status:** `DONE` — `Sol ultra` implementation và `Terra xhigh` independent review đều PASS.

**Acceptance criteria:**
- [x] Frame không có enabled rule được pass-through theo policy đã chốt.
- [x] Block không phát frame và tăng dropped counter đúng một lần.
- [x] Stop hoàn tất hữu hạn thời gian và không để background task sống sót.

**Verification:**
- [x] Bidirectional integration tests bằng in-memory session.
- [x] Cancellation/race tests và idle-loop test không busy-spin quá mức.
- [x] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 2, 4, 5, 9
**Files likely touched:** simulation engine files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Work log — 2026-08-12 (Task 10 Sol ultra implementation)

- [x] Thêm public seam `ISimulationEngine`/`SimulationEngine`: engine nhận một `ICanGatewaySession` đã mở và immutable `SimulationPlan`; caller tiếp tục sở hữu session, nên `StopAsync` chỉ cancel/await receive loop và không disconnect hardware ngầm.
- [x] Receive loop async xử lý chung cả RX→TX và TX→RX, lookup enabled rule theo normalized CAN ID + extended state; frame không có enabled rule, rule disabled và `PassThrough` đều được chuyển nguyên payload.
- [x] Enabled `Block` không gọi transmit và tăng `DroppedFrames` đúng một lần; immutable `GatewayStatistics` công khai received/transmitted/passed/dropped counters bằng atomic reads.
- [x] Lifecycle được serialize, hỗ trợ caller cancellation, concurrent/idempotent stop và async dispose; không dùng polling hoặc `Thread.Sleep`. Đối chiếu reference `MitmEngine` nhưng giữ hardware/UI/Vector type ngoài engine.
- [x] TDD qua 7 focused integration/lifecycle tests: unknown/disabled/enabled pass-through, block + exact counters, hai chiều, idle stop, cancellation/concurrent stop và idle enumeration.
- [x] Verification: focused 7/7 PASS; full suite 122/122 PASS; `dotnet build Simulate.sln --no-restore` 0 warning/0 error; targeted formatter và `git diff --check` PASS; UI/XAML/code-behind/project/solution diff bằng không.
- [x] Sol ultra self-review hai trục spec/standards: không có finding Critical/Required. `Inject` vẫn forward nguyên frame ở lát Task 10; override/E2E/echo filtering thuộc Task 11.
- [x] `Terra xhigh` independent review hai trục PASS: không có finding Critical/Required; build 0 warning/0 error, full suite 122/122 PASS, focused lifecycle suite lặp 10/10 PASS, formatter/diff/UI scope sạch. Task 10 DONE; chưa commit/push nếu chưa có lệnh người dùng. FYI cho Task 13: statistics là các atomic counter độc lập, không hứa cross-counter snapshot transactionally consistent.

## Task 11: Inject/override, live baseline và echo filtering

**Description:** Với Inject mode, clone payload live, pack signal override, áp E2E rồi phát; ngăn loopback/echo hai chiều.

**Design note bắt buộc trước implementation:** Tham chiếu `SignalItemModel` có cả `PhysicalValue`, `LiveValue`, `Override` và lựa chọn `VAL_` description. Backend mới phải giữ rõ ranh giới: UI chọn giá trị ở boundary, engine chỉ nhận typed override snapshot; không đưa WPF binding vào engine.

**Acceptance criteria:**
- [x] Signal không override giữ nguyên dữ liệu live baseline.
- [x] Extended ID và payload length được bảo toàn.
- [x] Echo filtering có timeout/bounds và không drop frame thật sau cửa sổ echo.
- [x] Numeric UI input được hiểu là physical value; validate finite/min-max/raw representability trước khi publish, input invalid không thay đổi override đang chạy.
- [x] Nếu DBC có `VAL_`, UI chọn theo label nhưng boundary map key raw value sang physical value bằng `raw * factor + offset`; không truyền raw key trực tiếp vào `SignalOverride.PhysicalValue` khi factor/offset khác mặc định.
- [x] Khi UI đổi override lúc engine đang chạy, publish immutable snapshot nguyên tử; mỗi frame dùng đúng một snapshot và signal không override vẫn giữ nguyên bit live.
- [x] `VAL_` metadata phải được parse/lưu typed hoặc Task 11 phải ghi rõ numeric-only fallback; không được tạo dropdown từ metadata chưa tồn tại.

**Verification:**
- [x] Golden injection tests gồm physical numeric, raw `VAL_` → physical mapping, factor/offset, signed/unsigned, signal không override và payload bit preservation.
- [x] Echo-loop integration tests.
- [x] Concurrency tests khi update override lúc engine đang chạy.
- [x] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 8-10
**Files likely touched:** simulation engine/override store và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh` + `Luna xhigh`
**Skills đã áp dụng:** `api-and-interface-design`, `test-driven-development`, `incremental-implementation`, `code-review`; `diagnosing-bugs` dành cho failure/race tái hiện được nếu reviewer phát hiện.

## Work log — 2026-08-12 (Task 11 Sol ultra implementation)

- [x] Đối chiếu reference `SignalItemModel`/`MitmEngine`: backend giữ numeric input là physical value; `VAL_` được parse thành typed `DbcValueDescription` gồm raw/label/physical và `SignalOverride.FromValueDescription(...)` luôn dùng physical đã map, không đưa WPF binding vào engine.
- [x] Thêm `ISimulationEngine.ReplaceSignalOverrides(...)`: validate toàn replacement trước khi clone-and-swap snapshot; input invalid/duplicate/missing/out-of-range hoặc raw không representable không mutate active snapshot, empty replacement xóa override.
- [x] Inject clone live payload, chỉ pack signal active, giữ nguyên bit/signal còn lại; áp E2E sau override; bảo toàn standard/extended ID, Classic/FD format, BRS, DLC và payload length. Statistics bổ sung injected/filtered-echo counters.
- [x] Echo filter hai chiều dùng exact frame + expected source, one-shot consume, monotonic timeout mặc định 10 ms và hard bound 32 entry; options/time provider cho deterministic tests. Echo của payload sau Inject được match theo frame thực sự đã phát.
- [x] TDD bao phủ 8 DBC thật có typed `VAL_`, raw→physical factor/offset, signed/unsigned, bit preservation, runtime update/clear/rollback, E2E ordering, atomic concurrency, echo hai chiều/timeout/bound/modified payload.
- [x] Sol ultra self-review hai trục đã sửa 2 finding Required trước gate: `VAL_` malformed không còn publish metadata một phần; enum issue code mới được append để không đổi numeric value của member public cũ. Không còn finding Critical/Required trong self-review.
- [x] Verification cuối: `dotnet build Simulate.sln --no-restore` 0 warning/0 error; full suite 134/134 PASS; targeted formatter và `git diff --check` PASS; UI/XAML/code-behind/project/solution diff bằng không; secret scan sạch.
- [x] `Terra xhigh` independent two-axis review PASS: không có finding Critical/Required. Re-run build 0 warning/0 error, full suite 134/134 PASS, `SimulationEngineTests` lặp 10 lần đều 17/17 PASS; formatter/diff/UI scope sạch. FYI: echo exact-frame có false positive không thể phân biệt trong chính cửa sổ 10 ms, nên Vector hardware echo/latency vẫn `NEEDS_VERIFY`; `VAL_` raw hiện là `long`, chưa cover enum unsigned 64-bit vượt `Int64.MaxValue` (không có trong 8 DBC supplied, reference cũng dùng `long`).
- [x] `Luna xhigh` independent review cuối PASS: không có finding Critical/Required. Đối chiếu reference và hai trục spec/standards xác nhận raw `VAL_` → physical, atomic snapshot, live baseline, E2E-after-pack, metadata Classic/FD/BRS/DLC và echo one-shot/timeout/bound. Re-run build 0 warning/0 error, full suite 134/134 PASS, `git diff --check` và UI/project scope sạch. Task 11 DONE tại commit `dd60366`; Vector hardware echo/latency thực vẫn `NEEDS_VERIFY`.

## Task 12: Scheduler, pause/resume và emergency stop

**Description:** Thêm One-shot/Cyclic/Event với cancellation, debounce, start delay, repeat và emergency stop an toàn.

**Implementation status:** `DONE` — `Sol xhigh` implementation/self-review, `Sol ultra` Required-finding fix và `Terra xhigh` re-review đều PASS. Chưa commit/push.

**Acceptance criteria:**
- [x] Không dùng blocking sleep trên UI thread.
- [x] Pause chỉ dừng schedule theo contract; gateway vẫn route RX↔TX và giữ session mở. Send đến hạn trong lúc pause phát đúng một lần sau resume, không catch-up burst.
- [x] Emergency stop hủy/await receive, cyclic/one-shot và event trigger đang chạy trước khi cleanup session theo thứ tự an toàn.

**Verification:**
- [x] 14 deterministic/tolerance-bounded scheduler tests với `TimeProvider` giả lập, blocking transmit seam và transmit-fault seam.
- [x] Stop/pause/resume, concurrent Event transmit và Emergency Stop race/fault tests PASS; focused suite lặp 20 lần đều 14/14 PASS.
- [x] Build 0 warning/0 error, full suite 148/148 PASS, formatter/diff check sạch, UI/XAML/project diff bằng không.

**Dependencies:** Task 11
**Files likely touched:** scheduler/engine files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Terra xhigh`
**Skills khi triển khai:** `test-driven-development`, `diagnosing-bugs`

## Work log — 2026-08-12 (Task 12 Sol xhigh implementation)

- [x] Giữ `StartAsync` là lifecycle gateway; thêm scheduler lifecycle riêng `StartSchedulingAsync`/`StopSchedulingAsync`, typed state `IsScheduling`/`IsSchedulingPaused` và không tự nối vào UI.
- [x] One-shot/Cyclic dùng start delay, cycle interval và repeat count (`0` = unbounded); Event dùng typed trigger result và debounce mặc định 50 ms. Scheduler luôn phát về TX side như reference.
- [x] Scheduled send dùng latest real RX frame làm live baseline; nếu chưa có frame thì tạo zero baseline hợp lệ theo Classic/FD DLC, giữ extended ID/FD/BRS metadata, rồi dùng chung override + E2E path Task 11.
- [x] Pause chỉ khóa scheduled sends, gateway vẫn route; Stop Scheduling hủy/await schedule nhưng giữ session mở; Emergency Stop hủy/await toàn bộ receive/schedule/Event trigger rồi mới gọi `session.StopAsync`.
- [x] TDD đã phát hiện và sửa 2 finding Required nội bộ: mapping DBC payload length sang FD DLC không được cast trực tiếp; Emergency Stop phải drain Event transmit ngoài scheduler worker trước khi cleanup session. Hardware transmission được serialize để bảo vệ E2E counter/session.
- [x] `GatewayStatistics` bổ sung `ScheduledFrames`; input invalid tại scheduling/event boundary trả exception rõ ràng, event debounce trả typed `Transmitted`/`Debounced`.
- [x] Verification: scheduler tests 12/12 PASS và lặp 10 vòng đều PASS; full suite 146/146 PASS; build 0 warning/0 error; targeted formatter, `git diff --check`, UI/project scope PASS.
- [x] `Terra xhigh` independent review hoàn tất: build 0/0, full suite 146/146, focused scheduler 12/12 lặp 10 vòng, formatter/diff/UI scope PASS; phát hiện 2 finding Required bên dưới. Vector hardware scheduler timing/latency và emergency cleanup thực vẫn `NEEDS_VERIFY` ở Task 15.

## Work log — 2026-08-12 (Task 12 Terra xhigh independent review)

- [x] **Required fixed — pause gate race:** scheduled dispatch nay re-check pause dưới cùng lifecycle lock sau khi lấy `_transmitGate`; hardware call được khởi phát trong boundary này rồi mới await ngoài lock. Regression test giữ gateway transmit, pause, release và xác nhận Event chỉ phát sau Resume đã RED trước fix/GREEN sau fix.
- [x] **Required fixed — emergency cleanup after worker/scheduler fault:** `EmergencyStopAsync()` luôn gọi `session.StopAsync()` trong `finally`, đồng thời giữ nguyên typed `HardwareOperationException` gốc. Regression test `TransmitFailed` đã RED với session còn mở/GREEN với session đóng sau fix.
- [x] Các phần còn lại đạt: lifecycle scheduler tách gateway, timing/debounce/live-zero baseline/FD DLC, serialized transmit, Event drain, API typed và không có UI/XAML/project/solution diff.
- [x] Verification độc lập: `dotnet build Simulate.sln --no-restore` PASS (0 warning/0 error); full suite 146/146 PASS; `SimulationSchedulerTests` 12/12 lặp 10 vòng PASS; targeted formatter, `git diff --check`, task-scope secret scan và UI scope PASS.
- [x] `Sol ultra` đã sửa hai Required finding bằng hai vòng RED→GREEN riêng; self-review không còn finding Critical/Required. Focused scheduler 14/14 lặp 20 vòng PASS, full suite 148/148 PASS, build 0/0 và scope gates sạch.
- [x] `Terra xhigh` re-review PASS: hai Required finding đã được giải quyết đúng contract. Pause/transmit decision được tuyến tính hóa dưới lifecycle lock; Emergency Stop luôn cleanup sau worker drain, kể cả khi root fault được giữ nguyên. Không có finding Critical/Required.
- [x] Re-validation độc lập: focused scheduler 14/14 lặp 20 vòng PASS (280/280 lượt); `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; full suite 148/148 PASS; formatter/diff/secret/UI/XAML/project scope PASS.
- [x] Task 12 DONE; chưa commit/push theo rào chắn người dùng. **Next action:** Task 13 — lead `Terra xhigh`, review `Sol xhigh`; vẫn UI-gated.

## Task 13: SimulationViewModel và projection sang bindings hiện hữu

**Description:** Tạo ViewModel orchestration cho engine, project state vào `Messages`, `Signals`, `FaultQueue` mà không sửa UI hoặc tự kích hoạt thao tác chưa có binding.

**Implementation status (2026-08-14):** DONE — `Terra xhigh` implementation/fix PASS và `Sol xhigh` independent re-review PASS; chưa commit/push.

**Acceptance criteria:**
- [x] ViewModel không gọi Vector API trực tiếp.
- [x] Existing public binding paths tiếp tục compile và cung cấp dữ liệu typed thay placeholder.
- [x] Commands chưa thể nối vào UI được ghi `UI_GATED`, không dùng code-behind workaround.

**Verification:**
- [x] ViewModel tests với fake engine/session: focused 6/6 PASS.
- [x] Build/test sạch: build 0 warning/0 error; full suite 154/154 PASS.
- [x] UI/code-behind diff bằng không.

**Dependencies:** Tasks 6, 9-12
**Files likely touched:** `ViewModels/MainViewModel.cs`, new simulation ViewModel/model adapters, tests
**Estimated scope:** M
**Model allocation:** Lead `Terra xhigh`; review `Sol xhigh`
**Skills khi triển khai:** `incremental-implementation`, `test-driven-development`

**Sol xhigh independent review (2026-08-14):**

- [x] Axis Spec PASS: ownership, typed projection, binding forwarding và `UI_GATED` đúng contract; UI/XAML/code-behind/project/solution diff bằng không.
- [x] **Required fixed — repository format:** targeted formatter đã chuẩn hóa 5 file C# Task 13; `dotnet format --verify-no-changes` PASS với CRLF theo `.editorconfig`.
- [x] **Required fixed — WPF continuation context:** bỏ mọi `ConfigureAwait(false)` trong `SimulationViewModel`; fake engine asynchronous + queueing synchronization context chứng minh regression RED→GREEN, focused 6/6 PASS.
- [x] Re-run: build 0/0, full 154/154, `git diff --check`, formatter và UI scope PASS.
- [x] `Sol xhigh` re-review PASS: hai Required finding được đóng; không có finding Critical/Required mới.
- [x] Re-validation độc lập: targeted formatter/CRLF PASS; build 0 warning/0 error; focused 6/6; full 154/154; `git diff --check` và UI/XAML/code-behind/project/solution scope PASS.
- [x] Task 13 DONE; không mở rộng UI, chưa commit/push. **Next action:** Task 14 — lead `Luna xhigh`, review `Sol xhigh`.

## Task 14: Integration và soak tests

**Description:** Kiểm chứng end-to-end backend qua in-memory hardware: connect → start gateway → pass/block/inject/schedule → stop/disconnect.

**Acceptance criteria:**
- [ ] Test đầy đủ Classic và FD mode ở contract level.
- [ ] Soak test không tăng task/handle giả lập và không phát frame sau stop.
- [ ] Error path trả state ổn định để reconnect.

**Verification:**
- [ ] `dotnet test Simulate.sln` pass 100%.
- [ ] `dotnet build Simulate.sln` 0 warning, 0 error.
- [ ] UI diff bằng không.

**Dependencies:** Task 13
**Files likely touched:** integration test files, không sửa production UI
**Estimated scope:** M
**Model allocation:** Lead `Luna xhigh`; review `Sol xhigh`
**Skills khi triển khai:** `test-driven-development`, `observability-and-instrumentation`

## Task 15: Final review và hardware verification

**Description:** Review spec/standards, chạy full verification và kiểm tra hardware thật theo checklist có rollback/cleanup.

**Acceptance criteria:**
- [ ] Review correctness, readability, architecture, security và performance không còn blocker.
- [ ] Hardware checklist phân biệt rõ PASS/FAIL/NEEDS_VERIFY.
- [ ] PLAN/todo và work log phản ánh đúng trạng thái thực, không đánh dấu hoàn thành theo suy đoán.

**Verification:**
- [ ] Full build/test sạch.
- [ ] UI diff bằng không.
- [ ] Không có secret/binary log mới và không có commit/push ngoài yêu cầu.

**Dependencies:** Task 14
**Files likely touched:** tài liệu/checklist; production code chỉ khi review phát hiện lỗi được phê duyệt trong phạm vi
**Estimated scope:** S
**Model allocation:** Lead `Sol ultra`; spec review `Luna high`; standards review `Terra xhigh`
**Skills khi triển khai:** `code-review`, `security-and-hardening`, `documentation-and-adrs`

## UI-gated backlog — không triển khai khi chưa được phép

- [ ] Bind Load DBC command.
- [ ] Bind Start/Stop/Emergency Stop commands.
- [ ] Bind Add/Remove/Clear fault queue.
- [ ] Bind signal override editing và live monitor controls.
- [ ] Thêm status/error/counter bindings nếu UI hiện tại không có điểm gắn.

Mỗi mục trên cần một yêu cầu và approval UI riêng từ người dùng trước khi chạm XAML/code-behind.

## Work log — 2026-08-10 (Planning only)

- [x] Đã đối chiếu dự án hiện tại với implementation Vector/MITM tham chiếu.
- [x] Đã tạo mới PLAN và todo sau khi nội dung cũ bị xóa.
- [x] Đã khóa phạm vi UI/XAML/code-behind trong toàn bộ kế hoạch.
- [x] Đã phân bổ Luna/Terra/Sol và reasoning level cho từng task.
- [x] Verification: `dotnet build Simulate.sln` PASS, 0 warning, 0 error.
- [ ] Tests: chưa chạy vì solution hiện chưa có test project.
- [ ] `NEEDS_APPROVAL`: test project/NuGet, target x64, cấu trúc module mới và mọi UI integration.

## Work log — 2026-08-10 (Coordinator readiness)

- [x] Đã bổ sung coordinator execution order và lead/reviewer model cho toàn bộ task.
- [x] Đã tạo [`handoff.md`](../handoff.md) với next action, rào chắn và suggested skills.
- [x] Verification: `git diff --check` PASS; UI/XAML/code-behind diff bằng không.
- [x] Verification: `dotnet build Simulate.sln` PASS, 0 warning, 0 error.
- [x] Approval gate đã được gỡ cho test project/NuGet, target x64 có điều kiện và cấu trúc module backend.

## Work log — 2026-08-10 (Task 0 coordinator update)

- [x] Task 0 baseline được xác nhận: build PASS, 0 warning, 0 error.
- [x] Đã thêm task completion reminder vào PLAN/todo/handoff.
- [x] UI/XAML/code-behind không thay đổi.
- [x] Task 0 hoàn tất; next action là Task 1 — `Sol xhigh`, review `Terra xhigh`.

## Work log — 2026-08-10 (Task 1 implementation)

- [x] `Sol xhigh` đã thêm `CanFrame`, RX/TX source metadata, Classic/FD connection options và typed hardware result/error.
- [x] `ICanHardwareDriver` chỉ còn hai trách nhiệm: discovery và mở gateway session; `ICanGatewaySession` định nghĩa receive/transmit/flush/stop/dispose.
- [x] Native type của Vector không xuất hiện trong domain/session contract.
- [x] Luồng đồng bộ hiện tại được giữ qua `ICanConnectionDriver` tạm thời; không đổi binding hoặc hành vi UI.
- [x] Verification: `dotnet build Simulate.sln` PASS, 0 warning, 0 error; `git diff --check` PASS; UI diff bằng không.
- [ ] Tests: chưa chạy vì solution chưa có test project; contract tests được tạo ở Task 2 cùng in-memory adapter.
- [x] Terra review đã xử lý: `CanFrame` giữ frame format/DLC/BRS; channel-mask overlap bị chặn; `StopAsync` không còn cancellable giữa cleanup.
- [x] Task 1 hoàn tất; next action là Task 2 — lead `Terra high`, review `Luna high`.

## Work log — 2026-08-10 (Task 2 implementation)

- [x] Đã tạo `Simulate.Tests` bằng MSTest và thêm vào solution theo approval đã được xác nhận ở Task 0.
- [x] `MockHardwareService` hiện triển khai `ICanHardwareDriver`, mở `MockCanGatewaySession` in-memory với queue receive/transmit tách biệt.
- [x] Thêm fault plan cho discovery, open driver/session, configure, activate và transmit; từng điểm trả `HardwareFailure` typed.
- [x] Contract tests chứng minh RX→TX, TX→RX, fault mapping, Classic/FD boundary, flush và stop/dispose idempotent.
- [x] Verification: `dotnet build Simulate.sln` PASS, 0 warning, 0 error; `dotnet test Simulate.sln` PASS, 10/10; `git diff --check` PASS; UI diff bằng không.
- [x] Luna high review Task 2 PASS, không còn finding Critical/Required.
- [x] Task 2 hoàn tất; next action là Task 3 — lead `Sol ultra`, review `Terra xhigh`.

## Work log — 2026-08-10 (Task 3 implementation)

- [x] `Sol ultra` đã tách boundary `IVectorXlApi`; type của Vector XL không lọt ra domain/session contract công khai.
- [x] Discovery luôn đóng driver bằng `try/finally`, lọc theo `XL_BUS_ACTIVE_CAP_CAN` và kiểm tra channel mask theo global channel index.
- [x] Open/configure/activate giữ ownership driver/port trong session; mọi failure path đều chạy cleanup theo thứ tự deactivate → close port → close driver.
- [x] Classic dùng interface V3; CAN FD lifecycle dùng interface V4; `permissionMask` được kiểm tra trước cấu hình.
- [x] Typed failure giữ `HardwareOperation`, `HardwareErrorCode`, numeric `XL_Status`, enum name và API gây lỗi.
- [x] Fault-path tests bao phủ discovery, open port, thiếu init access, Classic/FD configuration, activation, legacy connect, stop/dispose idempotent và cleanup status.
- [x] Source verification: manual local 20.30; `Doc/vxlapi_NET.dll` và `Simulate/lib/vxlapi_NET.dll` cùng version 25.20.14.0 và cùng SHA-256.
- [x] Verification: `dotnet build Simulate.sln` PASS, 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 21/21; `git diff --check` PASS; UI diff bằng không.
- [x] Self-review bằng `code-review` và review độc lập `Terra xhigh`: không còn finding Critical/Required.
- [ ] `NEEDS_VERIFY`: hardware Vector thật chưa được cắm; receive/transmit/flush native vẫn thuộc Task 4/5.

## Work log — 2026-08-10 (Task 3 independent review)

- [x] `Terra xhigh` review độc lập hai trục spec/standards: PASS, không có finding Critical/Required.
- [x] Spec: ownership/cleanup ở failure path, `try/finally` discovery, CAN channel filter, init access và typed native error đều đạt acceptance criteria.
- [x] Standards: boundary `IVectorXlApi` giữ type Vector nội bộ; không có UI diff, secret, package/project configuration change hoặc file ngoài scope.
- [x] Source verification độc lập: `XL Driver Library Manual 20.30` pp.37, 39, 43-44, 48, 53, 61, 85-86, 103-104; DLL dùng trong app và `Doc/` cùng version/hash.
- [x] Verification độc lập: `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 21/21; `git diff --check` PASS; UI diff bằng không.
- [ ] `NEEDS_VERIFY`: Vector hardware thật và close-status của driver còn cần manual checklist ở Task 15; đây không chặn Task 3.
- [x] Task 3 hoàn tất; next action là Task 4 — lead `Sol ultra`, test review `Luna xhigh`, code review `Terra xhigh`.

## Work log — 2026-08-10 (Task 4 implementation)

- [x] `Sol ultra` đã mở Classic frame I/O qua interface V3 và API chính thức `XL_Receive`, `XL_CanTransmit`, `XL_FlushReceiveQueue`, `XL_CanFlushTransmitQueue`.
- [x] Một native port sở hữu combined RX/TX mask; `chanIndex` ánh xạ frame về `CanGatewaySide.Rx` hoặc `CanGatewaySide.Tx`, destination transmit dùng đúng physical mask.
- [x] Standard/extended ID, DLC 0..8 và payload được bảo toàn; queue overrun, invalid DLC và native RX/TX/flush status trả typed failure.
- [x] Receive chạy theo batch giới hạn 256 ngoài caller thread, có delay khi queue rỗng, caller cancellation và stop pending receive hữu hạn.
- [x] Flush luôn xử lý cả receive queue và transmit queue; stop/dispose tiếp tục cleanup idempotent theo deactivate → close port → close driver.
- [x] Source verification: Vector manual 20.30 pp.47, 49, 75-76, 79-80, 90-94; wrapper DLL `25.20.14.0` trong `Doc/XLDriver.txt`, `Doc/XLClass.txt`, `Doc/XLDefine.txt`.
- [x] Verification implementation: `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 35/35; `git diff --check` PASS; self-review không còn finding Critical/Required.
- [x] Targeted whitespace verification cho 5 file C# thuộc Task 4 PASS. Repo-wide formatter còn baseline EOL/charset ngoài phạm vi, gồm file UI; các file đó không bị sửa.
- [x] UI/XAML/code-behind và binding `Connection.*`, `Messages`, `Signals`, `FaultQueue` không thay đổi.
- [ ] `NEEDS_VERIFY`: chưa cắm Vector hardware thật; toàn bộ mục runtime được ghi tại [`tasks/vector-classic-can-hardware-checklist.md`](vector-classic-can-hardware-checklist.md).

## Work log — 2026-08-11 (Task 4 Luna xhigh test review)

- [x] Spec/acceptance review PASS: Classic V3, RX/TX channel distinct, session seam, receive/transmit/flush/cancellation và typed native failures đều có bằng chứng từ test/fake SDK.
- [x] Edge-case review PASS: standard/extended ID, payload/DLC, queue overrun, invalid DLC, CAN FD rejection, destination mask, pending receive stop và repeated cleanup đều được kiểm tra.
- [x] Independent verification: `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 35/35; `git diff --check` PASS; targeted whitespace verification PASS.
- [x] UI/XAML/code-behind, binding `Connection.*`/`Messages`/`Signals`/`FaultQueue`, project và solution configuration không có diff.
- [x] Luna xhigh review không còn finding Critical/Required.
- [ ] `NEEDS_VERIFY`: Vector hardware thật chưa được cắm; runtime checklist vẫn ở [`tasks/vector-classic-can-hardware-checklist.md`](vector-classic-can-hardware-checklist.md).

## Work log — 2026-08-11 (Task 4 Terra xhigh code review)

- [x] Two-axis review PASS, không có finding Critical/Required.
- [x] Trục spec: interface V3 và wrapper `XL_Receive`/`XL_CanTransmit`/flush khớp source local; combined RX/TX mask, `chanIndex`, standard/extended ID, DLC/payload, typed failure và cleanup đều đạt Task 4.
- [x] Trục standards: Vector types vẫn bị cô lập sau `IVectorXlApi`; native I/O và cleanup được tuần tự hóa bằng cùng lock; batch receive giới hạn 256, queue-empty delay/cancellation hữu hạn; không có secret, package/project configuration hoặc UI thay đổi.
- [x] Independent verification: `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 35/35; `git diff --check` PASS; targeted whitespace verification PASS.
- [x] UI/XAML/code-behind, binding `Connection.*`/`Messages`/`Signals`/`FaultQueue`, project và solution configuration không có diff.
- [x] Task 4 hoàn tất; chưa commit/push theo rào chắn người dùng.
- [ ] `NEEDS_VERIFY`: Vector hardware thật, timestamp/latency và soak 50 chu kỳ vẫn cần thực hiện theo [`tasks/vector-classic-can-hardware-checklist.md`](vector-classic-can-hardware-checklist.md).
- [x] Next action completed: Task 5 independent code review `Terra xhigh` PASS.

## Work log — 2026-08-11 (Task 5 Sol ultra implementation)

- [x] `VectorXlApi` dùng interface V4 với `XL_CanTransmitEx`/`XL_CanReceive`; TX tạo đúng event tag, extended-ID bit, DLC và EDL/BRS flags; RX chỉ nhận exact `RX_OK` tag và phát hiện `XL_CAN_QUEUE_OVERFLOW`.
- [x] V4 session hỗ trợ cả Classic frame và CAN FD frame; source được ánh xạ bằng `channelIndex`, destination transmit dùng đúng RX/TX physical mask.
- [x] DLC `0..15` ánh xạ độc lập sang `0..8,12,16,20,24,32,48,64`; event có DLC/EDL/BRS không hợp lệ trả typed receive failure.
- [x] Native RX/TX status, `messageCounterSent != 1`, queue overflow và flush failure giữ operation/error/native diagnostics phù hợp.
- [x] Receive batch vẫn giới hạn 256, queue-empty polling có cancellation; FD caller cancellation và stop pending receive đạt cùng contract với Classic.
- [x] Source verification: Vector manual 20.30 CAN FD flow pp.103-104; wrapper DLL `25.20.14.0` và `Doc/XLDriver.txt`, `Doc/XLClass.txt`, `Doc/XLDefine.txt`.
- [x] Self-review đã loại bỏ lựa chọn legacy `NO_ISO` bị ẩn từ Task 3: orchestration nay truyền `VectorCanFdProtocolMode.Iso`, adapter map rõ thành `options=0`; test khóa quyết định mặc định này.
- [x] Verification implementation: formatter targeted PASS; `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 65/65.
- [x] `git diff --check` và UI/XAML/code-behind diff PASS; không đổi binding, package, project hoặc solution configuration.
- [x] Independent code review `Terra xhigh` PASS; không có finding Critical/Required. Task 5 đã DONE và đã commit; chưa push.
- [ ] `NEEDS_VERIFY`: thực hiện toàn bộ runtime bench tại [`tasks/vector-can-fd-hardware-checklist.md`](vector-can-fd-hardware-checklist.md); đặc biệt xác nhận ISO CAN FD mặc định và bit timing tương thích peer.

## Work log — 2026-08-11 (Task 5 Terra xhigh code review)

- [x] Trục spec PASS: V4 dùng đúng `XL_CanTransmitEx`/`XL_CanReceive`, exact RX/TX tag, EDL/BRS, extended ID, DLC `0..15`, Classic-over-V4, nominal/data bitrate, typed error, flush và cleanup/cancellation đều đạt Task 5.
- [x] Trục standards PASS: Vector types vẫn cô lập sau `IVectorXlApi`; receive batch giới hạn 256, queue-empty wait có cancellation, native I/O/cleanup tuần tự dưới cùng lock; không có secret, UI, binding, package hay project/solution configuration thay đổi.
- [x] Independent verification: `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 65/65; targeted formatter và `git diff --check` PASS.
- [x] Không có finding Critical/Required. Optional/FYI: timestamp/latency và CAN FD ISO mode/bit timing cần bench thật; đã được quản lý bởi checklist, không suy diễn từ fake SDK.
- [x] Task 5 DONE; đã commit, chưa push. Next action: Task 6 — lead `Terra xhigh`, review `Luna high`.
