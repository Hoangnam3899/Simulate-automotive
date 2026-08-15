# Todo: Vector Hardware Gateway & CAN Simulation Backend

> Trạng thái ban đầu: PLAN ONLY — chưa triển khai code.
>
> UI LOCK: Không sửa `App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs` hoặc file UI/XAML nào nếu chưa có yêu cầu và cho phép rõ ràng từ người dùng.
>
> Coordinator status (2026-08-15): Backend Tasks 0–15 DONE tại commit `b0e753b`; branch local đang trước origin 1 commit. UI-01 hiện `DEBUG_RETURN` do user báo lỗi baudrate liên tầng. Route hiện tại: **Terra xhigh** chốt/fix binding-state contract trước, sau đó **Sol ultra** xử lý Vector/native bitrate; không thay visual UI và không mở UI-02.

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
| 9 | UI-01→UI-10 — binding từng khu vực | Sol/Terra/Luna theo matrix | Strict user debug gate sau từng khu vực |

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

**Implementation status (2026-08-14):** DONE — `Luna xhigh` implementation/fix PASS và `Sol xhigh` independent re-review PASS; chưa commit/push.

**Acceptance criteria:**
- [x] Test đầy đủ Classic và FD mode ở integration contract level.
- [x] Soak 50 vòng connect/start/schedule/stop/disconnect không để receive worker/scheduler/session còn hoạt động và không phát frame sau **engine stop khi session vẫn mở**.
- [x] Typed transmit failure dừng engine, giữ ownership session rõ ràng và cho phép **cùng driver instance** mở/reconnect session mới thành công.

**Verification:**
- [x] Focused `SimulationIntegrationTests`: 4/4 PASS; soak lặp 10/10, tương đương 500 vòng.
- [x] `dotnet test Simulate.sln --no-build --no-restore`: 158/158 PASS.
- [x] `dotnet build Simulate.sln --no-restore`: 0 warning, 0 error.
- [x] Targeted formatter/CRLF và `git diff --check` PASS.
- [x] UI/XAML/code-behind/project/solution diff bằng không.

**Dependencies:** Task 13
**Files likely touched:** integration test files, không sửa production UI
**Estimated scope:** M
**Model allocation:** Lead `Luna xhigh`; review `Sol xhigh`
**Skills khi triển khai:** `test-driven-development`, `observability-and-instrumentation`

**Luna xhigh work log (2026-08-14):**

- [x] Slice Classic: discovery/open in-memory session → engine start → PassThrough/Block/Inject → engine stop → session disconnect; counter outcomes được kiểm chứng qua `GatewayStatistics`.
- [x] Slice CAN FD: discovery/open FD session → scheduler one-shot Inject → kiểm tra FD format/DLC 12/BRS/payload → stop scheduler/gateway/disconnect.
- [x] Slice soak/reconnect: 50 vòng trong một test và lặp test 10 lần; xác nhận `IsRunning`, `IsScheduling`, `IsOpen` đều false sau cleanup, enqueue sau engine stop được nhận nhưng không TX rồi bị từ chối sau disconnect, typed transmit failure không làm hỏng reconnect cùng driver.
- [x] Không sửa UI/XAML/code-behind hoặc project/solution configuration.
- [x] `Sol xhigh` review: formatter/build/test/UI scope PASS nhưng trả 2 Required test gaps; Task 14 chưa DONE.
- [x] `Luna xhigh` đã sửa hai Required finding bằng test seam; focused/full/build/formatter/scope PASS.
- [x] `Sol xhigh` re-review PASS: hai Required finding được đóng đúng contract, không có finding Critical/Required mới.
- [x] Re-validation độc lập: formatter PASS; build 0/0; focused 4/4; full 158/158; `git diff --check` và UI/XAML/code-behind/project/solution scope PASS.
- [x] Task 14 DONE; chưa commit/push. **Next action:** Task 15 — lead `Sol ultra`, review `Luna high` + `Terra xhigh`.

**Sol xhigh independent review (2026-08-14):**

- [x] Axis Standards PASS: test dùng public driver/session/engine seams, code dễ đọc, không có production/UI/package/project change; formatter, build 0/0, focused 4/4, full 158/158 và scope gate PASS.
- [x] **Required — post-stop proof fixed by Luna:** giữ session mở sau `engine.StopAsync`, enqueue thành công nhưng không có transmission/counter tăng trong bounded window; sau đó mới disconnect.
- [x] **Required — reconnect proof fixed by Luna:** fail-once/transient test driver dùng cùng `ICanHardwareDriver` instance cho session lỗi và session khỏe thứ hai.
- [x] Sol xhigh re-review PASS sau fix: không còn finding Critical/Required.

## Task 15: Final review và hardware verification

**Description:** Review spec/standards, chạy full verification và kiểm tra hardware thật theo checklist có rollback/cleanup.

**Implementation status (2026-08-15):** DONE — backend/review closure đã hoàn tất qua `Sol ultra` lead, `Luna high` Axis Spec và `Terra xhigh` Axis Standards/security. S2 graceful-close vẫn `UI_GATED`; hardware Vector thật vẫn `NEEDS_VERIFY` và không bị suy diễn là PASS.

**Acceptance criteria:**
- [x] Review correctness, readability, architecture, security và performance không còn blocker trong backend/review scope đã được phép.
- [x] Hardware checklist phân biệt rõ PASS/FAIL/NEEDS_VERIFY tại [`task15-review-and-hardware-status.md`](task15-review-and-hardware-status.md) và hai bench checklist.
- [x] PLAN/todo và work log phản ánh đúng trạng thái thực, không đánh dấu hoàn thành theo suy đoán.

**Verification:**
- [x] Full build/test sạch: build 0 warning/0 error; full suite 158/158 PASS.
- [x] UI diff bằng không.
- [x] Không có secret/binary log mới và không có commit/push trong Task 15 lead review.

**Dependencies:** Task 14
**Files likely touched:** tài liệu/checklist; production code chỉ khi review phát hiện lỗi được phê duyệt trong phạm vi
**Estimated scope:** S
**Model allocation:** Lead `Sol ultra`; spec review `Luna high`; standards review `Terra xhigh`
**Skills khi triển khai:** `code-review`, `security-and-hardening`, `documentation-and-adrs`

### Sol ultra lead work log — 2026-08-14

- [x] Axis Spec/Standards, native trust boundary, supplied DBC set và reference shutdown behavior đã được review; báo cáo chi tiết nằm ở [`task15-review-and-hardware-status.md`](task15-review-and-hardware-status.md).
- [x] Build 0/0; full 158/158; engine/scheduler/integration 350/350 qua 10 vòng; Task 14 targeted formatter PASS.
- [x] NuGet vulnerability audit, tracked-text secret scan, binary/log scan, `git diff --check` trước tài liệu và UI/XAML/code-behind scope đều PASS.
- [x] Không sửa production code, UI/XAML/code-behind, package/project/solution; không commit/push.
- [x] **Required S1:** engine contract và ViewModel projection đã PASS qua Terra xhigh implementation cùng Luna xhigh review ở slices 15.1–15.2.
- [ ] **Required S2 — `UI_GATED`:** graceful window-close cleanup chưa có; reference có closing hook để tránh leak port.
- [x] **Required Q1/Q2:** Terra xhigh implementation đã sửa typed invalid-configuration mapping và giữ first cleanup failure khi cancel-after-open; Sol xhigh review PASS.
- [x] **Required Q3:** DBC duplicate message identity/signal name nay lỗi tại parse boundary; Luna xhigh review độc lập PASS.
- [x] Cross-review **Luna high** đã kiểm tra Axis Spec và xác nhận S1/S2; không sửa production/UI.
- [x] Cross-review **Terra xhigh** đã kiểm tra Axis Standards/security, xác nhận Q1/Q2/Q3 Required; Q4/Q5 Optional.
- [x] **Sol ultra** đã thiết kế remediation S1 và sequence implementation Q1–Q3; shutdown close hook vẫn `UI_GATED`.
- [x] **Sol xhigh** đã triển khai slice 15.1 bằng RED→GREEN, không sửa UI/ViewModel behavior.
- [x] **Terra xhigh** review độc lập slice 15.1 PASS; không có finding Critical/Required.
- [x] Slice 15.2 đã hoàn tất bằng RED→GREEN và Luna xhigh review PASS.
- [x] **Sol xhigh** review độc lập slice 15.3 PASS; không có finding Critical/Required.
- [x] Sol ultra slice 15.5 closure gate cùng Luna high và Terra xhigh cross-review đã hoàn tất; Task 15 backend/review scope DONE, chưa commit/push.

### Terra xhigh Axis Standards/security work log — 2026-08-14

- [x] Review độc lập Axis Standards theo correctness, readability, architecture, security và performance; kết luận chi tiết tại [`task15-review-and-hardware-status.md`](task15-review-and-hardware-status.md).
- [x] Q1: `CanGatewayOptions` validation (same/overlapping channel, zero bitrate, CAN FD overflow) hiện bị map sai thành `Unexpected`; Required phải giữ `InvalidConfiguration` và thêm regression tests.
- [x] Q2: cancel-after-open gọi cleanup nhưng bỏ qua typed `StopAsync` failure; Required phải dispose best-effort đồng thời giữ first typed cleanup failure và có test deterministic.
- [x] Q3: DBC duplicate message/signal không bị chặn ở parser boundary; Required phải trả parse issue có line/context, trước các lookup `ToDictionary`/`Single` downstream.
- [x] Q4/Q5 giữ Optional: giới hạn resource/regex phải được thiết kế trước DBC file-input UI phase; repo formatter baseline không được bulk-normalize vì đụng file UI khóa.
- [x] Verification độc lập: `dotnet build Simulate.sln --no-restore` PASS 0 warning/0 error; `dotnet test Simulate.sln --no-build --no-restore` PASS 158/158; NuGet vulnerability audit và tracked-source secret-pattern scan PASS.
- [x] Không sửa production/UI/XAML/code-behind/package/project/solution, không commit/push.
- [x] Remediation contract/sequence đã chuyển cho **Sol ultra** và được chốt trong design record.

### Sol ultra remediation design work log — 2026-08-14

- [x] Chọn API additive: `ISimulationEngine.LastFailure` và `GatewayStatistics.LastRoutingLatency`; không tạo runtime snapshot type trùng lặp.
- [x] `LastFailure` giữ first typed root failure của mỗi run, vẫn observable sau worker stop và chỉ reset khi một run mới bắt đầu hợp lệ.
- [x] `LastRoutingLatency` là `TimeSpan?`, dùng monotonic `TimeProvider`, chỉ đo successful software gateway route; physical bus/analyzer latency vẫn `NEEDS_VERIFY`.
- [x] ViewModel sẽ project typed failure/statistics và refresh trong `finally` trên faulting stop paths; không thêm XAML, command binding hay code-behind.
- [x] Chia remediation thành 15.1–15.5 với dependency, lead/reviewer và gate rõ ràng; chi tiết tại [`task15-review-and-hardware-status.md`](task15-review-and-hardware-status.md).
- [x] Baseline `dotnet build Simulate.sln --no-restore`: PASS 0 warning/0 error. Không sửa production/UI/package/project, không commit/push.

### Slice 15.1 — S1 engine telemetry contract

**Implementation status (2026-08-15):** DONE — `Sol xhigh` implementation PASS; independent `Terra xhigh` review PASS.

**Description:** Thêm durable typed root-failure state và software gateway-routing latency vào public engine seam mà không thay đổi exception/lifecycle ownership hiện hữu.

**Acceptance criteria:**
- [x] `LastFailure` giữ failure đầu tiên của run, observable trước/sau `StopAsync`; emergency cleanup không ghi đè root cause.
- [x] `GatewayStatistics.LastRoutingLatency` là immutable `TimeSpan?`, reset về `null` khi start run mới và chỉ cập nhật sau successful routed transmit.
- [x] Latency dùng monotonic `TimeProvider`; blocked/echo/scheduled frame không cập nhật và không bị mô tả như physical-bus latency.

**Verification:**
- [x] RED→GREEN tests cho durable transmit/receive failure, root precedence, deterministic latency và reset.
- [x] Focused engine/scheduler/integration 39/39; full build 0/0; full suite 162/162; stress 390/390; targeted formatter/diff/secret/UI/project scope PASS.

**Dependencies:** Sol ultra remediation design
**Files likely touched:** `GatewayStatistics.cs`, `ISimulationEngine.cs`, `SimulationEngine.cs`, engine/integration tests và compile-only fake seam updates
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Terra xhigh`
**Skills:** `test-driven-development`, `incremental-implementation`, `api-and-interface-design`

### Sol xhigh slice 15.1 implementation work log — 2026-08-15

- [x] Thêm additive `ISimulationEngine.LastFailure` và `GatewayStatistics.LastRoutingLatency`; không tạo snapshot type mới.
- [x] First typed root được capture từ receive stream, gateway/scheduler transmit và emergency cleanup; `Interlocked.CompareExchange` giữ failure đầu tiên khi cleanup thứ cấp cũng lỗi.
- [x] Valid `StartAsync` reset failure/latency sau lifecycle/session validation; invalid restart không xóa failure cũ.
- [x] Routing latency dùng engine monotonic `TimeProvider`, bắt đầu trước shared transmit gate và kết thúc sau successful session acceptance; block/echo/scheduler/transmit failure không cập nhật.
- [x] TDD RED được quan sát cho missing API, scheduler capture, cleanup-only capture, native receive capture và latency API; tất cả chuyển GREEN.
- [x] Verification: focused 39/39; build 0 warning/0 error; full 162/162; high-risk stress 10 vòng = 390/390; targeted formatter, `git diff --check`, secret scan và UI/XAML/code-behind/ViewModel/project/solution scope PASS.
- [x] Chỉ cập nhật compile-only fake `ISimulationEngine` trong `SimulationViewModelTests`; không sửa `SimulationViewModel` behavior, UI, package/project/solution, commit hoặc push.
- [x] `Terra xhigh` review gate PASS: không finding Critical/Required; contract/concurrency/test coverage và independent verification đều đạt.

### Terra xhigh slice 15.1 review work log — 2026-08-15

- [x] Axis Spec PASS: first-root failure, valid/invalid restart, latency interval/reset và block/echo/scheduler/failure exclusion đúng frozen contract.
- [x] Axis Standards PASS: atomic `Volatile`/`Interlocked` operations phù hợp; session ownership và exception behavior không đổi; API additive không phá seam cũ.
- [x] Native transmit contract trả `HardwareOperationResult`; receive stream direct `HardwareOperationException` được capture. Tests dùng public seam và `ManualTimeProvider`, không phụ thuộc wall-clock.
- [x] Independent verification: build 0/0; focused 39/39; full 162/162; stress 10 vòng = 390/390; targeted formatter/diff/secret/UI/XAML/code-behind/ViewModel/project/solution scope PASS.
- [x] Không finding Critical/Required, không sửa file, không commit/push. Slice 15.1 DONE.

### Slice 15.2 — S1 ViewModel projection

**Implementation status (2026-08-15):** DONE — `Terra high` implementation PASS; independent `Luna xhigh` review PASS.

**Implementation evidence:** observable engine failure projection; unconfigured safe defaults; `finally` refresh on both stop fault paths; focused ViewModel tests 9/9, build 0/0, full suite 165/165, targeted formatter/diff/UI scope PASS. No commit or push.

**Description:** Project durable failure và latency-bearing statistics qua `SimulationViewModel` mà không bind hoặc thay đổi UI.

**Acceptance criteria:**
- [x] `SimulationViewModel.LastFailure` phản chiếu engine; unconfigured state trả `null` và `GatewayStatistics.Empty`.
- [x] `StopAsync`/`StopSchedulingAsync` refresh state trong `finally`, giữ caller synchronization context và exception gốc.
- [x] Binding paths hiện hữu và UI/XAML/code-behind không đổi.

**Verification:**
- [x] RED→GREEN projection/fault-path/synchronization-context tests; targeted + full build/test và UI scope PASS.

**Dependencies:** Slice 15.1
**Files likely touched:** `SimulationViewModel.cs`, `SimulationViewModelTests.cs`
**Estimated scope:** S
**Model allocation:** Lead `Terra high`; review `Luna xhigh`
**Skills:** `test-driven-development`, `incremental-implementation`

### Luna xhigh slice 15.2 independent review work log — 2026-08-15

- [x] Axis Spec PASS: `LastFailure` projection, unconfigured defaults, latency-bearing statistics projection, `finally` refresh, original exception identity, and no-UI scope match the frozen contract.
- [x] Axis Standards PASS: public seam remains thin, no new ownership or async-context violation, fault tests are outcome-based, and no security/performance finding applies.
- [x] Independent verification: ViewModel 9/9; full suite 165/165; targeted formatter, `git diff --check`, and UI/XAML/project/solution scope PASS.
- [x] No Critical/Required finding. Slice 15.2 is DONE; no production file was changed during review, and no commit/push was performed.

### Slice 15.3 — Q1/Q2 connection boundary

**Implementation status (2026-08-15):** DONE — `Terra xhigh` implementation PASS; independent `Sol xhigh` review PASS.

**Description:** Giữ typed invalid-configuration error và typed cleanup failure khi cancellation thắng sau session open.

**Acceptance criteria:**
- [x] Zero bitrate, same channel, overlapping mask và CAN FD bitrate overflow trả `InvalidConfiguration`; driver open không bị gọi.
- [x] Cancel-after-open luôn stop + dispose best-effort, giữ failure đầu tiên (`StopFailed` ưu tiên hơn dispose exception) và không set connected state.
- [x] Normal disconnect behavior, command availability và UI binding không đổi.

**Verification:**
- [x] RED→GREEN deterministic tests cho bốn invalid-config cases và cancel-after-open cleanup failure; targeted + full gates PASS.

**Dependencies:** Sol ultra remediation design
**Files likely touched:** `ConnectionViewModel.cs`, `ConnectionViewModelTests.cs`
**Estimated scope:** S
**Model allocation:** Lead `Terra xhigh`; review `Sol xhigh`
**Skills:** `test-driven-development`, `debugging-and-error-recovery`

### Terra xhigh slice 15.3 implementation work log — 2026-08-15

- [x] Q1 root cause: option-construction `ArgumentException`/`OverflowException` bị map nhầm thành `Unexpected`. Chúng nay trở thành typed `InvalidConfiguration`; cả bốn invalid inputs bị từ chối trước `OpenGatewaySessionAsync`.
- [x] Q2 root cause: cancel-after-open bỏ qua `StopAsync` result, rồi dispose exception thay thế root cause. Cleanup nay luôn stop rồi dispose best-effort, trả first failure và ưu tiên typed `StopFailed` hơn dispose exception; cancellation không set connected state.
- [x] RED→GREEN public-seam tests: zero bitrate, matching channel, overlapping masks, CAN FD bitrate overflow, và cancel-after-open với `StopFailed` + dispose exception. Existing normal disconnect/command tests tiếp tục PASS.
- [x] Verification: focused ConnectionViewModel 11/11; stress 110/110 qua 10 runs; build 0 warning/0 error; full suite 170/170; targeted formatter, `git diff --check`, secret và UI/XAML/project/solution scope PASS. Không commit/push.

### Sol xhigh slice 15.3 independent review work log — 2026-08-15

- [x] Axis Spec PASS: bốn invalid options giữ `InvalidConfiguration` và chặn driver-open; cancel-after-open luôn stop + dispose, ưu tiên typed `StopFailed`, không publish connected session.
- [x] Axis Standards PASS: cleanup first-failure logic rõ ràng, ViewModel/session ownership không đổi, public-seam tests deterministic, không có security/performance regression.
- [x] Independent verification tuần tự: build 0/0; focused 11/11; full 170/170; stress 110/110; targeted formatter, diff/secret/UI/project scope PASS.
- [x] Một gate chạy song song từng gây transient `CS5001` do formatter/build/test tranh generated WPF `obj`; source/App/XAML không đổi và build tuần tự lập tức PASS. Các gate .NET tiếp theo phải chạy tuần tự.
- [x] No Critical/Required finding. Slice 15.3 DONE; review không sửa production code, không commit/push.

### Slice 15.4 — Q3 duplicate DBC boundary

**Implementation status (2026-08-15):** DONE — `Terra high` implementation PASS; independent `Luna xhigh` review PASS.

**Description:** Từ chối duplicate normalized message identity và duplicate signal name ngay tại parser trust boundary.

**Acceptance criteria:**
- [x] Thêm typed parse issue code cho duplicate message/signal với exact duplicate line/context; document trả `null` khi có lỗi.
- [x] Message key là `(normalized identifier, isExtended)`; signal name so sánh `Ordinal` trong từng message.
- [x] Không còn đường duplicate đi tới `FirstOrDefault`/`ToDictionary`/`Single`; tám DBC supplied vẫn parse PASS.

**Verification:**
- [x] RED→GREEN parser tests cho hai duplicate cases; focused DBC corpus + full build/test/security/scope gates PASS.

**Dependencies:** Sol ultra remediation design
**Files likely touched:** `DbcDocument.cs`, `DbcParser.cs`, `DbcParserTests.cs`
**Estimated scope:** M
**Model allocation:** Lead `Terra high`; review `Luna xhigh`
**Skills:** `test-driven-development`, `security-and-hardening`

### Terra high slice 15.4 implementation work log — 2026-08-15

- [x] RED: thêm public-seam tests cho duplicate normalized extended message identity và duplicate `SG_` name; contract yêu cầu error typed, exact line/context và `Document == null`.
- [x] GREEN: thêm `DuplicateMessageIdentifier`/`DuplicateSignalName`; parser index message theo `(uint Identifier, bool IsExtendedIdentifier)` và signal theo dictionary `StringComparer.Ordinal`.
- [x] Loại `FirstOrDefault` khỏi lookup `VAL_` và `FindSignal` tại parser boundary; standard/extended cùng normalized ID vẫn là hai message hợp lệ.
- [x] Verification tuần tự: DbcParser 13/13 (bao gồm corpus 8 DBC); build 0 warning/0 error; full suite 173/173; targeted formatter, `git diff --check`, NuGet vulnerability audit, secret scan và UI/XAML/project/solution scope PASS.
- [x] Không sửa UI/XAML/code-behind/package/project/solution, không commit/push.

### Luna xhigh slice 15.4 independent review work log — 2026-08-15

- [x] Axis Spec PASS: duplicate normalized message identity và duplicate signal name bị chặn tại parser trust boundary; standard/extended cùng normalized ID vẫn hợp lệ; lỗi giữ exact line/context và làm `Document == null`.
- [x] Axis Standards/security PASS: tuple key và `StringComparer.Ordinal` đúng contract; duplicate không đi vào document collection; lookup `VAL_`/signal dùng index; không có Critical/Required security, correctness hoặc performance finding.
- [x] Independent verification: DBC parser 13/13; build 0/0; full suite 173/173; targeted formatter, `git diff --check`, NuGet vulnerability audit, secret và UI/project scope PASS.
- [x] Không sửa production code/UI trong review. Slice 15.4 DONE; Task 15 còn slice 15.5 và S2 `UI_GATED`.

### Slice 15.5 — final re-review and closure gate

**Lead status (2026-08-15):** DONE — `Sol ultra` lead PASS; `Luna high` Axis Spec PASS; `Terra xhigh` Axis Standards/security PASS. Task 15 backend/review scope hoàn tất; S2 vẫn `UI_GATED`.

**Acceptance criteria:**
- [x] S1/Q1/Q2/Q3 không còn Critical/Required finding; S2 có explicit user decision và không bị sửa ngầm.
- [x] Build/full tests/high-risk stress/formatter/supply-chain/secret/UI scope đều PASS.
- [x] Hardware matrix tiếp tục phân biệt software `PASS`, static `FAIL` và physical `NEEDS_VERIFY`.

**Dependencies:** Slices 15.1–15.4
**Model allocation:** Lead `Sol ultra`; cross-review `Luna high` + `Terra xhigh`

### Sol ultra slice 15.5 lead closure work log — 2026-08-15

- [x] Re-review S1/Q1/Q2/Q3 theo Spec/Standards: không còn finding Critical/Required sau các implementation và independent review của slices 15.1–15.4.
- [x] S2 có explicit user decision từ UI lock: không sửa XAML/code-behind khi chưa có yêu cầu và approval riêng. Graceful window-close cleanup giữ `FAIL (UI_GATED)`, không bị triển khai ngầm và không bị gọi là software PASS.
- [x] Hardware matrix giữ software/fake evidence `PASS`, known static shutdown gap `FAIL (UI_GATED)` và toàn bộ physical bench claims `NEEDS_VERIFY`.
- [x] Verification tuần tự: build 0 warning/0 error; full suite 173/173; high-risk Task 15 stress 720/720 qua 10 vòng; targeted formatter PASS; NuGet vulnerability audit PASS; `git diff --check`, tracked-project secret assignment scan và UI/XAML/code-behind/project/solution scope kể từ Task 14 PASS.
- [x] Broad secret pattern chỉ bắt ví dụ minh họa trong `.agents/skills`; scan project sau khi loại đúng nguồn hướng dẫn PASS. Không có credential dự án bị phát hiện.
- [x] Không sửa production code/UI trong lead closure, không commit/push.

### Luna high slice 15.5 Axis Spec cross-review work log — 2026-08-15

- [x] Axis Spec PASS: S1 durable failure/latency, Q1 typed configuration, Q2 first cleanup failure, Q3 duplicate DBC boundary đều khớp acceptance và có test evidence; không có Critical/Required gap.
- [x] S2 PASS về traceability: user decision giữ UI lock được ghi rõ; graceful close không bị gọi là hoàn tất runtime và vẫn là `FAIL (UI_GATED)`.
- [x] Hardware matrix PASS về phân loại: software/fake `PASS`, static shutdown gap `FAIL`, physical Vector/bench `NEEDS_VERIFY`; không suy diễn physical evidence từ fake tests.
- [x] Independent focused cross-review suite: 72/72; không sửa production code/UI, không commit/push. Terra xhigh Axis Standards/security cross-review sau đó PASS.

### Terra xhigh slice 15.5 Axis Standards/security cross-review work log — 2026-08-15

- [x] Axis Standards PASS: parser giữ normalized message identity bằng tuple `(identifier, isExtendedIdentifier)` và signal name bằng `StringComparer.Ordinal`; duplicate bị dừng ở trust boundary, không rơi vào document/lookup downstream mơ hồ.
- [x] Security PASS: DBC text được coi là input ngoài; duplicate identity/name, configuration failure và cancel cleanup đều có typed, deterministic boundary behavior. Q4 document-size/regex bound vẫn Optional vì chưa có DBC file-input UI; phải được thiết kế trước phase UI đó.
- [x] Independent verification tuần tự: build 0 warning/0 error; full suite 173/173; targeted formatter; NuGet vulnerability audit; `git diff --check`; tracked-project secret scan; UI/XAML/code-behind/project/solution scope đều PASS.
- [x] Không có finding Critical/Required. Task 15 và Checkpoint E backend/review scope DONE; S2 graceful close giữ `FAIL (UI_GATED)` và physical Vector bench giữ `NEEDS_VERIFY`.
- [x] Task 15 đã commit tại `b0e753b`; push vẫn cần lệnh riêng. Next phase là UI binding plan, không tự cấp quyền sửa UI.

## Phase UI binding 1–10 — strict user debug gates

### Quy tắc điều phối bắt buộc

- [x] Lập inventory: UI hiện có 41 bindings, chỉ 3 command bindings (Refresh/Connect/Disconnect),
  3 window click handlers và nhiều runtime placeholder/hardcode ở panels 2–10.
- [x] Chốt `binding-only`: chỉ nối property/command/state vào control hiện hữu; không thay visual tree,
  layout, style, resource, màu, font, label, icon, kích thước hoặc thứ tự control.
- [x] Chốt strict sequence UI-01→UI-10; UI-01 đang `IMPLEMENTING` sau approval binding-only, UI-02→10 đều khóa.
- [x] Chốt state gate: `IMPLEMENTING → AGENT_REVIEW_PASS → WAITING_USER_DEBUG → USER_ACCEPTED`.
- [x] Khi user báo lỗi, task trở về `DEBUG_RETURN`; coordinator phải đề xuất model phù hợp và không
  được mở task sau.
- [x] Mỗi task phải build/test/diff/review PASS nhưng vẫn không được DONE nếu user chưa nói PASS/cho qua.
- [x] Không commit/push tự động; không sửa source UI trong lượt planning này.

### Transition protocol — bắt buộc trước mỗi UI mới

- [x] Coordinator phải ghi trạng thái hiện tại vào PLAN/TODO/Handoff trước khi mở panel tiếp theo.
- [x] Thông báo chuyển trạng thái phải nói rõ: tên UI, mục đích/hành vi, dependency, files/seam được phép,
  lead/reviewer model, automated gate và manual debug gate.
- [x] Nếu chưa có user PASS cho panel trước, panel sau giữ `LOCKED` dù agent review đã PASS.
- [x] Nếu user báo lỗi, ghi `DEBUG_RETURN`, mô tả lỗi và model đề xuất; không tự đánh dấu DONE/đi tiếp.

### UI-01 — Connection / Setup

**Status:** `DEBUG_RETURN` — user báo lỗi baudrate TX/RX và CAN FD data-rate; cần **Terra xhigh** xử lý contract/state trước, rồi **Sol ultra** xử lý Vector/native configuration. UI-02 vẫn khóa.

**Description:** Hoàn thiện binding của panel 1 trên control hiện hữu, thống nhất session ownership cho
phase simulation và xử lý cancel/disconnect/graceful close mà không đưa Vector logic vào code-behind.
Chia nội bộ 01A binding/state và 01B session/lifecycle để mỗi slice nhỏ, review được.

**UI-01 làm gì:**

- `01A Connection state`: bind Interface/Driver, TX/RX Channel, CAN FD, bitrate và Refresh/Connect/
  Disconnect; trạng thái Connected chỉ được phản ánh khi session mở thành công.
- `01B Session handoff`: chuyển `ICanGatewaySession` qua composition seam với một owner duy nhất để
  các phần simulation sau dùng được, không leak Vector API vào UI.
- `01C Cleanup`: cancel pending operation, Disconnect và graceful window close stop/dispose idempotent,
  không double-open, không stale state và không block UI thread.
- `01D User test`: user kiểm tra refresh → chọn RX/TX → connect → disconnect, cancel và đóng/mở lại app.
  Chưa có PASS của user thì UI-02 vẫn LOCKED.

**Acceptance criteria:**
- [x] Refresh/interface/TX/RX/CAN FD/bitrate và Connect/Disconnect phản chiếu typed state thật; status
  indicator không tuyên bố Connected khi session chưa mở thành công.
- [x] Session mở được handoff qua composition seam cho engine về sau nhưng chỉ một owner chịu trách
  nhiệm stop/dispose; connect nhanh lặp/cancel không tạo double-open hoặc stale connected state.
- [x] Đóng cửa sổ khi đang discover/connect/connected thực hiện cleanup hữu hạn và idempotent; chỉ
  lifecycle hook tối thiểu ở code-behind sau approval exact, không sửa visual UI.

**Verification:**
- [x] Focused `ConnectionViewModelTests` và lifecycle tests PASS; full build/test và binding/UI-scope diff PASS.
- [ ] **USER DEBUG GATE:** user tự Refresh→Connect→Disconnect, đóng app khi đang connected, mở lại và
  reconnect; chỉ user mới được đổi status thành `USER_ACCEPTED`.

**Dependencies:** Backend Task 15; explicit approval cho UI-01.
**Likely files (split 01A/01B):** `MainWindow.xaml` (binding-only), `MainWindow.xaml.cs` (close lifecycle-only),
`ConnectionViewModel.cs`, `ApplicationComposition.cs`/`MainViewModel.cs`, focused tests.
**Debug routing:** native/close/race → **Sol ultra**; binding/CanExecute/state → **Terra xhigh**;
reproduction fixture → **Luna xhigh**.

**User không cần tự chọn agent:** chỉ cần gửi panel, thao tác, expected/actual, lỗi/log hoặc screenshot và khả
năng tái hiện. Coordinator sẽ phân loại, cập nhật `DEBUG_RETURN`, báo rõ model cần chuyển và chỉ tiếp tục sau
khi model đó sửa/review/build/test xong. Nếu chưa đủ evidence, coordinator hỏi bổ sung thay vì đoán model.
Nếu user báo lỗi native/close/race thì chuyển **Sol ultra**; binding/CanExecute/state thì **Terra xhigh**;
reproduction/fixture thì **Luna xhigh**; parser/resource/domain edge thì **Sol xhigh** phối hợp **Terra xhigh**.

#### UI-01 DEBUG_RETURN — baudrate incident — 2026-08-15

- [x] User evidence: Baudrate TX và RX đang hiển thị chung `250000`; đổi TX channel tự ghi đè baudrate theo
  `DefaultBaudrate`; CAN FD data bitrate bị ép bằng `Baudrate * 4` và chưa có state/điều khiển độc lập.
- [x] Coordinator classification: cross-layer binding/domain/native configuration; không phải yêu cầu đổi layout
  hay style. UI-01 giữ nguyên `DEBUG_RETURN`, chưa được chuyển panel.
- [x] Additional user evidence: CAN FD đang là cờ On/Off toàn cục; Vector FD timing dùng SJW/TSEG cố định;
  không hỗ trợ gateway Classic-CAN ↔ CAN-FD khác loại; protocol mode bị cố định ISO, thiếu Bosch Non-ISO.
- [x] Additional classification: đây là mở rộng contract CAN/CAN FD Automotive, không phải lỗi hiển thị đơn lẻ.
  Per-side mode/bitrate/timing/protocol cần **Terra xhigh** định nghĩa trước; Vector timing/native mapping cần
  **Sol ultra** triển khai sau. ComboBox/control mới là `UI_SHAPE_GATED`, cần user approval riêng.
- [x] **Terra xhigh slice 1:** `OnSelectedTxChanged` không còn ghi đè lựa chọn bitrate của user bằng
  `HardwareChannel.DefaultBaudrate`; giá trị observed của channel vẫn được thêm vào list để user tự chọn.
  Regression test RED (500000 bị đổi thành 250000) → GREEN. Build 0/0; focused Connection 19/19; full 183/183;
  targeted formatter và `git diff --check` PASS. Không sửa XAML/UI, không commit/push.
- [x] **Terra technical assessment:** Non-ISO đã có mapping nội bộ trong `VectorXlApi`, nhưng service luôn truyền
  ISO nên chưa có capability công khai. Fixed SJW/TSEG là thiếu linh hoạt, nhưng không đủ evidence để áp chung
  timing table 80 MHz/80% cho mọi Vector/Virtual channel; cần target hardware/clock có căn cứ.
- [ ] **Terra xhigh — bước hiện tại:** tách `BaudrateTx`/`BaudrateRx`, giữ lựa chọn người dùng khi đổi channel,
  bổ sung contract cho CAN FD data bitrate, per-side CAN/FD mode, timing profile và ISO/Non-ISO protocol state;
  cập nhật binding-only/test seam. User chuyển sang Terra xhigh.
- [ ] **Sol ultra — bước kế tiếp sau Terra:** áp contract độc lập vào `CanGatewayOptions`/`VectorHardwareService`,
  cấu hình đúng TX/RX mask/bitrate, heterogeneous Classic↔FD, nominal/data bitrate, bit timing và protocol mode;
  chỉ chạy sau khi Terra slice được review.
- [ ] **UI approval gate:** chưa thêm Data Bitrate ComboBox hoặc per-channel CAN/FD mode control; đây là thay đổi
  control/UI shape và chỉ được làm khi user cho phép rõ ràng.
- [ ] **Hardware decision gate:** trước khi tính/tự gán timing profile, xác nhận target Vector/device controller
  clock và profile sample point được yêu cầu; không suy diễn chung từ Virtual CAN hay một thiết bị khác.
- [ ] **User retest:** TX/RX khác baudrate, đổi channel không tự ghi đè, CAN FD data bitrate đúng lựa chọn, rồi
  kiểm tra per-side Classic/FD, timing/protocol profile, connect/disconnect và close/reopen. Chỉ user mới được
  xác nhận `UI-01 PASS`.

#### UI-01 Sol ultra implementation work log — 2026-08-15

- [x] Existing Refresh/Connect/Disconnect và Connected LED/text tiếp tục dùng binding thật; sáu control
  Interface/TX/RX/CAN FD/bitrate chỉ được bổ sung `IsEnabled="{Binding Connection.CanEditConnectionSettings}"`.
- [x] `ConnectionViewModel.ActiveGatewaySession` là borrowed composition seam; ViewModel vẫn là owner duy
  nhất stop/dispose. Disconnect/shutdown cùng dùng cleanup best-effort và giữ typed failure đầu tiên.
- [x] Shutdown đánh dấu terminal state nguyên tử, cancel và await discover/open đang chạy, không publish
  session nếu shutdown đã thắng race, cleanup idempotent và khóa mọi command/settings sau đó.
- [x] `MainViewModel.ShutdownAsync()` dừng simulation đã cấu hình trước khi giải phóng session; connection
  vẫn được cleanup trong `finally` nếu simulation stop lỗi. `MainWindow.xaml.cs` chỉ delegate lifecycle.
- [x] TDD public seams: RED→GREEN cho borrowed session, editability state, idempotent shutdown, active
  refresh/open cancellation, cleanup failure precedence và engine-before-session shutdown order.
- [x] Verification: focused Connection/Simulation ViewModel 29/29; lifecycle stress 10/10 vòng; build
  0 warning/0 error; full suite 182/182; targeted formatter, `git diff --check`, secret assignment scan,
  protected project/config scope và XAML binding-only diff PASS.
- [x] Không thêm package, không sửa `.csproj`/`.sln`, không commit/push. Không thay control/layout/style/
  resource/content UI; UI-02 vẫn `LOCKED_BY_UI-01_USER_ACCEPTANCE`.
- [x] Independent review **Terra xhigh** PASS: Axis Spec xác nhận state/binding, borrowed-session ownership,
  cancel/open/close cleanup và thứ tự simulation-before-session; Axis Standards xác nhận MVVM boundary,
  code-behind lifecycle-only, cleanup off Dispatcher, public seams/tests và không có package/project/solution/
  secret/UI-design regression. Không có finding Critical/Required.
- [x] Terra independent verification: build 0 warning/0 error; focused 29/29; full suite 182/182; lifecycle
  stress 5 test × 10 vòng = 50 executions PASS; targeted formatter, `git diff --check`, protected config,
  secret assignment scan và exact six-binding XAML scope PASS.
- [ ] `NEEDS_VERIFY`: user chạy Vector/manual flow Refresh→Connect→Disconnect, close khi connected hoặc
  đang open, mở lại và reconnect; chỉ user PASS mới mở UI-02.

### UI-02 — DBC Management

**Status:** `LOCKED_BY_UI-01_USER_ACCEPTANCE`.

**Description:** Bind Load/Unload, filename/valid state và message/node/signal counts vào DBC document
thật; file picker chỉ là UI boundary, parse/validation ở service/ViewModel.

**Acceptance criteria:**
- [ ] DBC hợp lệ load một lần và project đúng filename/counts; unload xóa document/projection/engine
  composition an toàn; lỗi parse giữ typed line/context và không publish partial state.
- [ ] Trước khi mở interactive file input, Q4 được xử lý bằng document-size cap và finite regex strategy/
  timeout; tám DBC supplied vẫn PASS.
- [ ] Không thay control hoặc giao diện panel 2; chỉ binding/command/state trên control hiện hữu.

**Verification:**
- [ ] Parser/corpus + DBC ViewModel tests, full build/test, resource/security/diff gates PASS.
- [ ] **USER DEBUG GATE:** load valid/invalid/duplicate DBC, unload/reload và đối chiếu ba count.

**Dependencies:** UI-01 `USER_ACCEPTED`; explicit approval UI-02.
**Likely files:** `MainWindow.xaml` binding-only, DBC/application ViewModel/composition, `DbcParser.cs`, focused tests.
**Models:** lead **Terra xhigh**; review **Sol xhigh**; corpus support **Luna high**.

### UI-03 — TX Message List

**Status:** `LOCKED_BY_UI-02_USER_ACCEPTANCE`.

**Description:** Bind message rows và top-row actions vào DBC-backed editable simulation draft; immutable
`SimulationPlan` chỉ được tạo sau validation, không mutate trực tiếp từ grid.

**Acceptance criteria:**
- [ ] Columns map đúng `Id/Name/Dlc/Cycle/GatewayMode/SendType/SignalCount/IsEnabled/LastSent`, không
  tiếp tục dùng `Cycle`/`Dlc` cho cột sai nghĩa.
- [ ] Add/Delete/Delete All/Move Up/Move Down thao tác deterministic trên selected row và giữ unique
  normalized message identity.
- [ ] Các glyph trong cell hiện là `TextBlock`: không được gọi là actionable nếu binding-only không thể
  gắn behavior an toàn; giữ `UI_SHAPE_GATED` cho tới khi user cho phép đổi control/behavior cụ thể.

**Verification:**
- [ ] Draft/projection/command tests và full build/test/diff PASS.
- [ ] **USER DEBUG GATE:** user kiểm tra selection, enable, add/delete/reorder và từng cột dữ liệu.

**Dependencies:** UI-02 `USER_ACCEPTED`; explicit approval UI-03.
**Likely files:** `MainWindow.xaml` binding-only, `SimulationViewModel.cs`/draft projection, `MainViewModel.cs`, focused tests.
**Models:** lead **Terra xhigh**; review **Luna xhigh**.

### UI-04 — Live Signal Monitor

**Status:** `LOCKED_BY_UI-03_USER_ACCEPTANCE`.

**Description:** Thêm bounded live-signal telemetry seam tối thiểu từ engine tới ViewModel rồi bind
search/message filter/pause/clear và grid hiện hữu; không polling/sleep trên Dispatcher.

**Acceptance criteria:**
- [ ] Raw/physical/unit/message/status/updated dùng frame thật + DBC decode, cập nhật trên caller UI
  context với coalescing/bound để traffic cao không tăng memory vô hạn.
- [ ] Search/filter/pause/clear chỉ thay projection; pause monitor không làm dừng gateway engine.
- [ ] Stop/disconnect/unload ngắt subscription sạch, không có late update vào ViewModel đã đóng.

**Verification:**
- [ ] Engine telemetry + ViewModel context/backpressure tests, stress/full build/test/diff PASS.
- [ ] **USER DEBUG GATE:** user chạy traffic, lọc, pause/resume/clear và quan sát freeze/memory/update.

**Dependencies:** UI-03 `USER_ACCEPTED`; explicit approval UI-04.
**Likely files:** `ISimulationEngine.cs`, `SimulationEngine.cs`, `SimulationViewModel.cs`, `MainWindow.xaml` binding-only, focused tests.
**Models:** lead **Sol ultra**; review **Terra xhigh**.

### UI-05 — Fault Configuration

**Status:** `LOCKED_BY_UI-04_USER_ACCEPTANCE`.

**Description:** Bind selected message/signal, fault/mode/timing fields và Add to Queue vào typed draft
configuration. Không invent fault type/backend semantics mà engine chưa hỗ trợ.

**Acceptance criteria:**
- [ ] Selected label và các field hiện hữu dùng state typed; time/repeat/value parse invariant và invalid
  input không crash hoặc tạo partial queue item.
- [ ] Add to Queue tạo đúng một validated rule/override row, giữ replacement/restore semantics đã được
  backend hỗ trợ và chống duplicate ngoài contract.
- [ ] Unsupported option giữ disabled/unavailable typed state; không thêm option/control để “làm đủ”.

**Verification:**
- [ ] Validation/queue command tests và full build/test/diff PASS.
- [ ] **USER DEBUG GATE:** user nhập valid/invalid values, add queue, kiểm tra row và error behavior.

**Dependencies:** UI-04 `USER_ACCEPTED`; explicit approval UI-05.
**Likely files:** `MainWindow.xaml` binding-only, fault/draft state trong ViewModel, plan validation, focused tests.
**Models:** lead **Terra xhigh**; review **Luna high**.

### UI-06 — Signal Value Configuration

**Status:** `LOCKED_BY_UI-05_USER_ACCEPTANCE`.

**Description:** Bind signal search/filter, editable physical value, min/max/step và override state vào
`ReplaceSignalOverrides`; `VAL_` label/key phải map raw→physical theo contract Task 11.

**Acceptance criteria:**
- [ ] Numeric và typed `VAL_` selection tạo finite physical value trong min/max; invalid edit không cập
  nhật engine snapshot.
- [ ] Show Only Overridden/search giữ selected row ổn định; enable/clear override cập nhật queue/draft
  và payload qua cùng codec/E2E path.
- [ ] Edit state observable và UI-context safe; không mutate immutable DBC metadata.

**Verification:**
- [ ] Codec/override/ViewModel tests, payload golden vectors và full build/test/diff PASS.
- [ ] **USER DEBUG GATE:** user đổi numeric/label value, filter override và đối chiếu payload phù hợp.

**Dependencies:** UI-05 `USER_ACCEPTED`; explicit approval UI-06.
**Likely files:** `MainWindow.xaml` binding-only, `SimulationViewModel.cs`, override draft/codec seam, focused tests.
**Models:** lead **Terra xhigh**; review **Sol xhigh**.

### UI-07 — Execution Control

**Status:** `LOCKED_BY_UI-06_USER_ACCEPTANCE`.

**Description:** Bind Start/Stop/Pause toggle/Clear Queue và runtime status vào engine thật; command
availability khóa double action và tôn trọng session/DBC/plan prerequisites.

**Acceptance criteria:**
- [ ] Start chỉ chạy khi connected + valid configured plan; Stop/Pause/Resume/Clear có deterministic
  lifecycle và observable status/queue/running state.
- [ ] Rapid clicks, faulting worker và cancellation giữ first typed failure, không transmit sau stop và
  cleanup đúng owner.
- [ ] Panel hiện không có Emergency button: không repurpose `Stop` thành emergency và không tuyên bố
  emergency UI complete; giữ `UI_SHAPE_GATED` đến khi user yêu cầu thay UI.

**Verification:**
- [ ] Scheduler/engine/ViewModel command tests + stress/full build/test/diff PASS.
- [ ] **USER DEBUG GATE:** user start/pause/resume/stop/clear, click nhanh và kiểm tra state/cleanup.

**Dependencies:** UI-06 `USER_ACCEPTED`; explicit approval UI-07.
**Likely files:** `MainWindow.xaml` binding-only, `SimulationViewModel.cs`, application coordinator/composition, focused tests.
**Models:** lead **Sol ultra**; review **Terra xhigh**.

### UI-08 — Log / Output

**Status:** `LOCKED_BY_UI-07_USER_ACCEPTANCE`.

**Description:** Thay demo log bằng bounded observable log thật; bind level filter, Clear và Export vào
control hiện hữu, không log payload/credential nhạy cảm ngoài nhu cầu chẩn đoán.

**Acceptance criteria:**
- [ ] Connect/DBC/config/run/stop/failure tạo timestamped typed log; collection/text projection có hard
  cap và thread-safe UI dispatch.
- [ ] Level filter và Clear deterministic; Export dùng explicit user-selected path, finite snapshot và
  typed failure khi I/O lỗi.
- [ ] Không còn chuỗi log demo là runtime truth, không thêm panel/control/style.

**Verification:**
- [ ] Log bound/filter/export tests, full build/test/secret/diff PASS.
- [ ] **USER DEBUG GATE:** user tạo events, filter/clear/export và đối chiếu file output.

**Dependencies:** UI-07 `USER_ACCEPTED`; explicit approval UI-08.
**Likely files:** `MainWindow.xaml` binding-only, bounded log ViewModel/service, `MainViewModel.cs`, focused tests.
**Models:** lead **Terra high**; review **Luna high**.

### UI-09 — Bus Monitor / Health

**Status:** `LOCKED_BY_UI-08_USER_ACCEPTANCE`.

**Description:** Bind chart/counters vào bounded health history và typed runtime/native evidence. Metric
không được SDK/runtime cung cấp phải hiện unavailable, không suy diễn thành số đẹp.

**Acceptance criteria:**
- [ ] Received/transmitted/dropped/echo/latency và error/overrun evidence có source rõ; bus-load nếu
  tính toán phải ghi đúng là software estimate, không gọi là analyzer truth.
- [ ] Polyline history bounded, update throttled và reset đúng lifecycle; không block receive/Dispatcher.
- [ ] Physical-only metric tiếp tục `NEEDS_VERIFY`; không hardcode `24.7%`, `0`, `1` làm state thật.

**Verification:**
- [ ] Health telemetry/window/bound tests, engine stress/full build/test/diff PASS.
- [ ] **USER DEBUG GATE:** user chạy traffic/fault/soak và đối chiếu counter/graph với available evidence.

**Dependencies:** UI-08 `USER_ACCEPTED`; explicit approval UI-09.
**Likely files (split telemetry/projection):** health model/statistics, engine/session adapter, `MainViewModel.cs`,
`MainWindow.xaml` binding-only, focused tests.
**Models:** lead **Sol ultra**; review **Terra xhigh**.

### UI-10 — Status Overview

**Status:** `LOCKED_BY_UI-09_USER_ACCEPTANCE`.

**Description:** Bind aggregate connection/driver/bus/DBC/health/CAN FD state và existing footer vào một
read-only projection; panel 10 không sở hữu hardware, parser hay engine.

**Acceptance criteria:**
- [ ] Overview phản ánh nhất quán state của UI-01→09 và không còn `CANoe 17.0`, filename/count/health/
  session demo làm runtime truth.
- [ ] Aggregate update trên UI context, không giữ stale state sau unload/disconnect/stop/error.
- [ ] Không thêm hoặc bố trí lại status field; unavailable field dùng typed fallback đã thống nhất.

**Verification:**
- [ ] Aggregate projection tests, full build/test/formatter/secret/diff + Sol ultra final lifecycle review PASS.
- [ ] **FINAL USER DEBUG GATE:** user chạy connect→load DBC→configure→inject→stop→disconnect và nói
  UI-10/toàn phase PASS trước khi đóng phase.

**Dependencies:** UI-09 `USER_ACCEPTED`; explicit approval UI-10.
**Likely files:** `MainWindow.xaml` binding-only, `MainViewModel.cs`/status projection, focused tests.
**Models:** lead **Terra xhigh**; review **Luna high**; final lifecycle review **Sol ultra**.

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

## Work log — 2026-08-15 (CAN / CAN FD Bitrate Independence & UI-01 Hardware Fix)

- [x] Tách độc lập `BaudrateTx` và `BaudrateRx` trong `ConnectionViewModel` (Nominal bitrates: 125k, 250k, 500k, 1M; default 500k).
- [x] Bổ sung dải Data Bitrates độc lập `DataBaudrateTx` và `DataBaudrateRx` (500k, 1M, 2M, 4M, 5M, 8M; default 2M).
- [x] Cho phép CAN FD hoạt động với Nominal 500k / Data 500k mà không báo lỗi hoặc tự ý ép hệ số ×4.
- [x] Đảm bảo chọn kênh trong `OnSelectedTxChanged` / `OnSelectedRxChanged` không tự động ghi đè giá trị baudrate do người dùng chọn.
- [x] Cập nhật `MainWindow.xaml` binding `Baudrate TX` tới `Connection.BaudrateTx` và `Baudrate RX` tới `Connection.BaudrateRx` theo đúng phân tích và cho phép từ người dùng.
- [x] Cập nhật `VectorHardwareService` cấu hình độc lập `TxChannel` và `RxChannel` khi nominal hoặc data bitrate giữa hai phía khác nhau.
- [x] Thêm 5 unit tests mới kiểm thử cấu hình bitrate độc lập, bảo vệ giá trị người dùng chọn khi đổi kênh, và validation CAN FD.
- [x] Định dạng lại tên hiển thị channel thành `{HardwareTypeName} Channel {Index}` (ví dụ: `VN1640A Channel 1`, `VN1640A Channel 2`), giúp hiển thị trực quan, phân biệt rõ cổng vật lý mà không bị tràn/cắt ngắn ComboBox.
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 190/190 tests.
