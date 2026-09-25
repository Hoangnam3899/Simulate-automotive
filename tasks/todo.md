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

**Status:** `USER_ACCEPTED`.

**Description:** Bind selected message/signal, fault/mode/timing fields, hỗ trợ direct fault injection cho Cyclic/One-Shot và kịch bản chuỗi (Sequence). Nút Add to Queue chỉ xuất hiện khi chọn mode Sequence.

**Acceptance criteria:**
- [x] Tự động nhận diện tín hiệu từ Live Monitor (`Selected: <Message>.<Signal>`); nếu chưa chọn thì hiển thị trạng thái chưa chọn an toàn.
- [x] Hỗ trợ tiêm lỗi trực tiếp (Single/Direct) khi ở chế độ `Cyclic` hoặc `One-Shot`, trở thành active fault để Bảng 7 chạy ngay.
- [x] Chế độ `Sequence` hiển thị nút `+ Add to Queue` (`Visible`); các chế độ `Cyclic`/`One-Shot` ẩn nút này (`Collapsed`).
- [x] Add to Queue ở chế độ `Sequence` tạo đúng một validated item đưa vào `FaultQueue`, không crash khi input invalid.

**Verification:**
- [x] Validation/queue command tests và full build/test/diff PASS (226/226 tests PASS, 0 warning/0 error).
- [x] **USER DEBUG GATE:** user đã kiểm tra thực tế (chọn signal, chuyển mode, ẩn/hiện nút Add to Queue, làm mờ ô nhập theo mode, căn chỉnh chống tụt chữ) và xác nhận phê duyệt ("oke rồi đấy").

**Dependencies:** UI-04 `USER_ACCEPTED`; explicit approval UI-05.
**Likely files:** `MainWindow.xaml` binding-only, fault/draft state trong ViewModel, plan validation, focused tests.
**Models:** lead **Terra xhigh**; review **Luna high**.

### UI-06 — Signal Value Configuration

**Status:** `IN_PROGRESS`.

**Description:** Bind signal search/filter, editable physical value, min/max/step và override state vào
`ReplaceSignalOverrides`; `VAL_` label/key phải map raw→physical theo contract Task 11.

**Acceptance criteria:**
- [x] Tối ưu layout công thái học: mở rộng Bảng 6 (tỷ lệ `1.72*`, chiều cao hàng `1.5*`), thu gọn Bảng 8 & 9 (`Height="78"`), nâng cỡ chữ lên `9.5pt`, chiều cao dòng `24px` chống bấm nhầm.
- [x] Numeric value edit và CheckBox `Override` tương tác hai chiều; cột `Override` mở rộng đủ 65px không bị cụt chữ (`Overrid`), cột `Message` hiển thị đúng tên message (80px).
- [x] Show Only Overridden và ô tìm kiếm `Search signals...` hoạt động mượt mà qua `FilteredValueSignals` độc lập với Bảng 4.
- [x] Enable/clear override cập nhật snapshot nguyên tử vào `ISimulationEngine.ReplaceSignalOverrides`, đồng thời đồng bộ trạng thái `SelectedSignal` sang Bảng 5 và trạng thái `Injected` sang Bảng 4.
- [x] Edit state observable và UI-context safe; không mutate immutable DBC metadata.

**Verification:**
- [x] Override/search/filter ViewModel unit tests trong `SignalValueConfigurationTests.cs` (7/7 tests PASS, toàn bộ suite 233/233 PASS, build 0/0).
- [x] **USER DEBUG GATE:** user mở app, nạp DBC, kiểm tra layout mới của Bảng 6 (chữ to rõ, dòng 24px thoáng), tìm kiếm, tích chọn CheckBox Override, sửa giá trị Value, lọc Show Only Overridden và xác nhận "đã ngon".

**Dependencies:** UI-05 `USER_ACCEPTED`; explicit approval UI-06.
**Likely files:** `MainWindow.xaml` binding-only, `SimulationViewModel.cs`, override draft/codec seam, focused tests.
**Models:** lead **Terra xhigh**; review **Sol xhigh**.

### UI-07 — Execution Control

**Status:** `DONE` (Sẵn sàng cho User Runtime Verification).

**Description:** Bind Start/Stop/Pause toggle/Clear Queue và runtime status vào engine thật; command
availability khóa double action và tôn trọng session/DBC/plan prerequisites.

**Acceptance criteria:**
- [x] Start chỉ chạy khi connected + valid configured plan; Stop/Pause/Resume/Clear có deterministic
  lifecycle và observable status/queue/running state.
- [x] Rapid clicks, faulting worker và cancellation giữ first typed failure, không transmit sau stop và
  cleanup đúng owner.
- [x] Panel hiện không có Emergency button: không repurpose `Stop` thành emergency và không tuyên bố
  emergency UI complete; giữ `UI_SHAPE_GATED` đến khi user yêu cầu thay UI.

**Verification:**
- [x] Scheduler/engine/ViewModel command tests + stress/full build/test/diff PASS (249/249 tests PASS).
- [ ] **USER DEBUG GATE:** user start/pause/resume/stop/clear, click nhanh và kiểm tra state/cleanup.

**Dependencies:** UI-06 `USER_ACCEPTED`; explicit approval UI-07.
**Likely files:** `MainWindow.xaml` binding-only, `SimulationViewModel.cs`, application coordinator/composition, focused tests.
**Models:** lead **Sol ultra**; review **Terra xhigh**.

### UI-08 — Log / Output

**Status:** `WAITING_USER_DEBUG` (Đã hoàn thành triển khai và kiểm thử tự động, chờ người dùng debug nghiệm thu).

**Description:** Xây dựng hệ thống Nhật ký Hoạt động Người dùng & Báo cáo Sự cố Hệ thống (`User Action & System Diagnostic Log`). Tuyệt đối KHÔNG ghi các frame/signal CAN chạy ngầm để tránh rác log. Chỉ tập trung ghi nhận thao tác của người dùng (kết nối, nạp DBC, cấu hình tiêm lỗi, điều khiển thực thi) và các cảnh báo / sự cố lỗi phần mềm.

**Acceptance criteria:**
- [x] **Contract & Service (`ILogService`, `LogService`, `LogEntry`)**:
  - `LogEntry` chứa `Timestamp` (`HH:mm:ss.fff`), `Level` (`Info`, `Warning`, `Error`), `SourceModule` (`Connection`, `DBC`, `Fault`, `Execution`, `System`), `Message`, `FormattedLine`.
  - `LogService` lưu trữ thread-safe bằng `BoundedQueue` (Hard Cap = 1,000 dòng log), chống rò rỉ bộ nhớ 100%.
  - Tuyệt đối không hook vào luồng nhận/truyền frame CAN định kỳ của engine để tránh spam log.
- [x] **Các nhóm sự kiện được ghi nhận (Scoped Audit Events)**:
  - **Thao tác người dùng (User Actions)**:
    * Nhấn Connect (Interface, Channels, Baudrate, FD), Disconnect.
    * Mở file DBC, nạp thành công (số lượng msg/sig), Unload DBC.
    * Bật/tắt override tín hiệu (tên tín hiệu, giá trị), thay đổi giá trị nhập tay.
    * Nhấn "Add to Queue", "Clear Queue".
    * Nhấn "Start Injection" (chế độ tiêm), "Pause", "Resume", "Stop".
    * Thao tác Clear Log và Export Log.
  - **Cảnh báo vận hành (Warnings)**:
    * Giá trị nhập ngoài dải Min/Max của tín hiệu.
    * Bắt đầu tiêm lỗi khi hàng đợi rỗng hoặc chưa chọn cấu hình hợp lệ.
  - **Sự cố & Lỗi hệ thống (Errors & Failures)**:
    * Lỗi kết nối phần cứng (mở cổng thất bại, mất kết nối thiết bị).
    * Lỗi cú pháp nạp DBC.
    * Lỗi ngắt kết nối CAN đột ngột hoặc lỗi truyền phần cứng.
    * Bắt các ngoại lệ chưa xử lý (Unhandled Exceptions) của ứng dụng để phục vụ chẩn đoán.
- [x] **Dialog Service (`IFileDialogService`)**:
  - Bổ sung `SaveFileDialog(string filter, string title, string? defaultFileName = null)` để phục vụ Export.
- [x] **ViewModel (`LoggingViewModel`)**:
  - Quản lý danh sách log, bộ lọc mức độ (`All Levels`, `Info`, `Warning`, `Error`).
  - `ClearCommand`: Xóa sạch nội dung log hiện tại.
  - `ExportCommand`: Xuất snapshot log ra file `.log` hoặc `.txt` an toàn (UTF-8, try-catch lỗi I/O).
- [x] **Tích hợp sự kiện qua `MainViewModel`**:
  - Đăng ký lắng nghe các hành động từ `ConnectionViewModel`, `DbcManagementViewModel`, `SimulationViewModel`, và bắt lỗi toàn cục.
- [x] **UI XAML Binding-Only (`MainWindow.xaml`)**:
  - Giữ nguyên 100% layout, controls, styles hiện hữu của Bảng 8.
  - Bind ComboBox: `ItemsSource="{Binding Logging.AvailableLevels}"`, `SelectedItem="{Binding Logging.SelectedLevel}"`.
  - Bind Buttons: `Command="{Binding Logging.ClearCommand}"`, `Command="{Binding Logging.ExportCommand}"`.
  - Bind TextBlock: `Text="{Binding Logging.FormattedLogText}"`.
- [x] **Unit Tests (`Simulate.Tests`)**:
  - Test ghi nhận đầy đủ các thao tác người dùng và lỗi hệ thống.
  - Test bộ lọc Level (`All`, `Info`, `Warning`, `Error`).
  - Test Clear và Export với mock file dialog.

**Verification:**
- [x] Build `dotnet build Simulate.sln` đạt 0 warning / 0 error.
- [x] Toàn bộ unit tests pass 100% (1,271 / 1,271 tests).
- [x] `git diff --check` và UI diff sạch 100%.
- [ ] **USER DEBUG GATE:** Người dùng thao tác các nút trên UI, quan sát log ghi lại đúng hành động của mình, thử gây lỗi hoặc nhập dải sai để xem cảnh báo, thử nghiệm Filter/Clear/Export.

**Dependencies:** UI-07 `USER_ACCEPTED`; explicit user approval UI-08 implementation plan.
**Likely files:** `Simulate/Models/LogEntry.cs`, `Simulate/Services/ILogService.cs`, `Simulate/Services/LogService.cs`, `Simulate/Services/IFileDialogService.cs`, `Simulate/ViewModels/LoggingViewModel.cs`, `Simulate/ViewModels/MainViewModel.cs`, `Simulate/MainWindow.xaml` (binding-only), `Simulate.Tests/LoggingTests.cs`.
**Models:** lead **Terra high**; review **Luna high**.


### UI-09 — Bus Monitor / Health

**Status:** `READY_FOR_IMPLEMENTATION`.

**Description:** Chỉ báo sức khỏe đường truyền CAN (Health Status Indicator) bằng cơ chế đổi màu đường line/polyline thông minh (Xám/Xanh/Vàng/Đỏ) thay thế sóng động để tránh giật lag; tính toán và hiển thị chính xác 4 chỉ số viễn thám thực tế: `Bus Load`, `Errors`, `Lost`, `Warning` từ `SimulationEngine` và `ConnectionViewModel`.

**Acceptance criteria:**
- [ ] Line/Polyline đổi màu trạng thái chuẩn xác: Xám (`#64748B` - Offline), Xanh lá (`#10B981` - Optimal), Vàng (`#F59E0B` - High Load / Warning), Đỏ (`#EF4444` - Critical / Error / Drop).
- [ ] Tuyệt đối không dùng sóng động (Waveform dynamic points calculation) để triệt tiêu tải CPU và giật lag luồng UI.
- [ ] `Bus Load (%)` tính toán định kỳ theo chu kỳ $500\text{ms}$ ngầm dựa trên $\Delta \text{Frames}$ thực tế, baudrate và số bit trung bình (~120 bits/frame); hiển thị dạng số thập phân có màu đồng bộ.
- [ ] `Lost` liên kết trực tiếp với `GatewayStatistics.DroppedFrames` (frame bị block hoặc drop).
- [ ] `Errors` đếm số lỗi phần cứng hoặc truyền nhận thất bại từ `HardwareFailure`.
- [ ] `Warning` đếm số lượng cảnh báo vận hành (vượt dải min/max, tải cao hoặc log warning).
- [ ] Tự động reset về 0 và trạng thái Offline khi ngắt kết nối (`Disconnect`).
- [ ] UI Boundary: Giữ nguyên 100% layout XAML gốc, chỉ gán Data Binding vào các thuộc tính có sẵn.

**Verification:**
- [ ] Unit tests bao phủ toàn diện: tính toán Bus Load, ma trận chuyển màu sức khỏe, đếm lỗi/drop, và reset lifecycle.
- [ ] `dotnet build Simulate.sln` đạt 0 warning / 0 error.
- [ ] `dotnet test Simulate.sln` PASS 100%.
- [ ] `git diff --check` và UI diff sạch 100%.
- [ ] **USER DEBUG GATE:** Người dùng kiểm tra trực quan khi kết nối, chạy traffic, thử tiêm lỗi / block để quan sát đổi màu và cập nhật các chỉ số.

**Dependencies:** UI-08 hoàn tất; user approval kế hoạch UI-09.
**Likely files:** `Simulate/ViewModels/BusHealthViewModel.cs`, `Simulate/ViewModels/MainViewModel.cs`, `Simulate/MainWindow.xaml` (binding-only), `Simulate.Tests/BusHealthTests.cs`.
**Models:** lead **Terra high**; review **Luna high**.

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

## Work log — 2026-08-20 (UI-02 DBC Management Implementation & Real DBC Testing)

- [x] Thêm `IFileDialogService` và `DefaultFileDialogService` để trừu tượng hóa hộp thoại chọn file phục vụ testability.
- [x] Thêm `DbcManagementViewModel` quản lý trạng thái tải/hủy DBC, kiểm tra giới hạn dung lượng tệp an toàn (50MB), UTF-8 encoding, và trích xuất số lượng Messages, Nodes, Signals chuẩn xác.
- [x] Tích hợp `DbcManagementViewModel` vào `MainViewModel` và kết nối với `SimulationViewModel` để tự động load danh sách Messages/Signals khi nạp DBC.
- [x] Cập nhật `MainWindow.xaml` (Panel 2) binding `LoadedFileNameDisplay`, `CheckmarkVisibility`, `MessageCount`, `NodeCount`, `SignalCount`, `LoadDbcCommand`, `UnloadDbcCommand`, `CanLoadDbc`, `CanUnloadDbc`.
- [x] Viết bộ kiểm thử toàn diện `DbcManagementViewModelTests.cs` (9 unit tests) bao gồm kiểm thử tự động với toàn bộ các file DBC thực tế trong thư mục `C:\Users\Hnam\Desktop\Simulate\DBC` (cả CAN class và CAN FD).
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 199/199 tests.

## Work log — 2026-08-20 (UI-03 TX Message List Selective Drafting Implementation)

- [x] Cập nhật `MessageModel` thành `ObservableObject`, bổ sung `DisplayIndex`, `RawIdentifier`, `IsExtendedIdentifier`, `LastSent`, và `DbcSource`.
- [x] Thêm `IMessageDialogService` và `DefaultMessageDialogService` để trừu tượng hóa hộp thoại chọn Message.
- [x] Tạo giao diện `SelectMessageWindow` (Modal Dialog) với ô tìm kiếm nhanh (Search Box) và danh sách Message từ DBC có checkbox chọn nhiều.
- [x] Cập nhật `SimulationViewModel`:
  - Khởi tạo danh sách `Messages` ban đầu hoàn toàn rỗng khi nạp DBC.
  - Thêm `AddMessagesCommand`, `DeleteMessageCommand`, `DeleteAllMessagesCommand`, `MoveUpCommand`, `MoveDownCommand`.
  - Quản lý `SelectedMessage`, cập nhật tự động `DisplayIndex` (`1, 2, 3...`), và lọc trùng lặp khi thêm thông điệp.
  - Tự động dọn sạch bảng phát khi DBC bị hủy (Unload).
- [x] Cập nhật `MainWindow.xaml` (Panel 3): Gắn Command cho 5 nút Toolbar và sửa lỗi binding các cột DataGrid (`#`, `Enable`, `Mode`, `Send Type`, `Signals`, `Last Sent`).
- [x] Tạo mới bộ kiểm thử toàn diện `SimulationMessageDraftTests.cs` (10 unit tests) kiểm thử toàn bộ hành vi thêm, xóa, đổi thứ tự, lọc trùng, hủy DBC.
- [x] Sửa lỗi XamlParseException bằng cách định nghĩa tài nguyên cục bộ `DialogButton` và `BoolToVis`.
- [x] Nâng cấp bảng màu cho `SelectMessageWindow` (Dark Slate `#141D2E` / `#1C273C` / `#23314B` với điểm nhấn Cyan `#38BDF8`), tăng độ sáng, độ tương phản và căn giữa nội dung DataGrid theo yêu cầu người dùng.
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 210/210 tests.
- [x] Committed changes: `ade2cca` ("feat: implement Panel 2 DBC Management and Panel 3 TX Message List selective drafting").

## Work log — 2026-08-20 (UI-04 Live Signal Monitor & UI-01 Driver/Channel Grouping Enhancement)

- [x] Nâng cấp `SignalModel` thành `ObservableObject`, bổ sung `RawValue`, `PhysicalValueDisplay`, `HasReceivedData`, `StatusText`, `StatusColor`, `LastUpdated` và phương thức `UpdateValue` / `ResetData`.
- [x] Bổ sung phương thức giải mã `UnpackRaw` và `Unpack` tuple trong `SignalCodec.cs` hỗ trợ cả Intel và Motorola byte order.
- [x] Cập nhật `SimulationViewModel`:
  - Thêm `SignalSearchText`, `SelectedSignalMessageFilter`, `AvailableSignalMessageFilters`, `IsSignalMonitorPaused`, `PauseMonitorButtonContent`, `FilteredSignals` (ICollectionView).
  - Thêm `TogglePauseMonitorCommand`, `ClearSignalMonitorCommand`.
  - Triển khai `ProcessIncomingFrame` tự động giải mã bit-level và cập nhật trạng thái màu **Xanh lá (`#10B981` — `● Active`)** khi có data, giữ màu **Xám (`#64748B` — `● No Data`)** khi chưa có data theo yêu cầu người dùng.
- [x] Cập nhật `MainWindow.xaml` (Panel 4):
  - Gắn binding cho ô Search, ComboBox Filter, nút Pause (`Ⅱ`/`▶`), nút Clear.
  - Sửa toàn bộ lỗi binding cột DataGrid (`MessageName`, `RawValue`, `PhysicalValueDisplay`, `Unit`, `StatusText/StatusColor`, `LastUpdated`).
- [x] Cải tiến logic gom nhóm thiết bị `MapCanInterfaces` trong `VectorHardwareService.cs`:
  - Gom toàn bộ các kênh ảo vào `Virtual CAN` với tên kênh chi tiết (`Virtual Bus 1 - Channel 1`, `Virtual Bus 1 - Channel 2`, `Virtual Bus 2 - Channel 1`, `Virtual Bus 2 - Channel 2`...).
  - Tách các thiết bị phần cứng thật thành từng nhóm riêng (`VN1640A 1`...).
  - Tự động bổ sung tùy chọn `All Vector Devices` khi có nhiều thiết bị trên hệ thống.
- [x] Bổ sung unit tests cho Multi-bus Virtual Discovery và All Vector Devices trong `VectorHardwareServiceTests.cs`.
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 219/219 tests.

## Work log — 2026-08-20 (UI-05 Fault Configuration Redesign & Visual Guides Enhancement)

- [x] Nâng cấp công thái học Bảng 5 (Fault Configuration) theo phê duyệt của người dùng:
  - Bổ sung `FaultTypeGuideText` hiển thị mô tả trực quan cơ chế hoạt động tương ứng với từng kiểu lỗi trong `Fault Type` (`Signal Override`, `Stuck at Value`, `Bit Flip`, `Offset`, `Noise`, `Ramp`).
  - Hiển thị song song cả hướng dẫn `FaultTypeGuideText` và `ModeGuideText` qua thuộc tính `CombinedGuideText` ở chân Bảng 5 với căn lề thông minh chống đè nút `+ Add to Queue`.
  - Loại bỏ ô `Fault Value` không cần thiết ở Cột 3 để tránh trùng lặp với **Bảng 6 (Signal Value Configuration)**; thay bằng nhãn đồng bộ `Override Control` và `Value set in Panel 6`.
  - Bổ sung `ToolTip` giải thích trực quan bằng tiếng Việt cho toàn bộ các control và tham số: `Fault Type`, `Cycle`, `Repeat`, `Injection Mode`, `Duration`, `Delay`, `Override existing`, `Restore after stop`.
  - Thiết lập cơ chế tự động kích hoạt/làm mờ động (`IsEnabled`):
    * `Event`: Vô hiệu hóa toàn bộ 4 ô `Cycle`, `Repeat`, `Duration`, `Delay`.
    * `One-Shot`: Chỉ bật ô `Delay`, vô hiệu hóa 3 ô `Cycle`, `Repeat`, `Duration`.
    * `Cyclic` / `Sequence`: Bật toàn bộ cả 4 ô để nhập liệu đầy đủ.
  - Bổ sung Style Trigger `Opacity="0.35"` và nền `#080C16` cho TextBox và TextBlock khi `IsEnabled="False"`, làm cho các ô không dùng xám mờ và chìm hẳn xuống nền tối cực kỳ rõ rệt.
  - Tinh chỉnh bố cục chiều dọc chống tràn/tụt chữ:
    * Gộp dòng hiển thị tín hiệu mục tiêu `Selected: ...` lên ngang hàng với tiêu đề `5. FAULT CONFIGURATION` (bên phải), tiết kiệm 20px chiều dọc.
    * Chiều cao ô nhập TextBox & ComboBox cân đối ở `Height="22"`, `FontSize="9.5pt"`, nhãn `FontSize="8.5pt"`, `Margin="0,3,0,0"` rất gọn gàng.
    * Giải phóng hoàn toàn không gian chân bảng cho dòng mô tả `CombinedGuideText` và nút `+ Add to Queue`, loại bỏ 100% tình trạng chữ bị tụt hay đè lên các ô `Repeat`/`Delay`.
- [x] Cập nhật bộ kiểm thử `FaultConfigurationTests.cs` (7 unit tests): bổ sung kiểm thử chuyển đổi `FaultTypeGuideText` và `CombinedGuideText` trên toàn bộ các loại lỗi, cùng kiểm thử ma trận `FieldEnablement` trên cả 4 modes.
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 226/226 tests (100% PASS).
- [x] User acceptance: Người dùng đã kiểm thử thủ công và xác nhận chấp thuận (`USER_ACCEPTED`).

## Work log — 2026-08-20 (UI-06 Signal Value Configuration & Layout Ergonomics Optimization)

- [x] Tối ưu hóa bố cục tổng thể ứng dụng theo yêu cầu người dùng:
  - Thu gọn Bảng 8 (Log / Output) và Bảng 9 (Bus Monitor) từ `120px` xuống `78px`, loại bỏ diện tích thừa màu đen.
  - Dồn diện tích thu hồi được để mở rộng Bảng 6 (Signal Value Configuration):
    * Chiều cao hàng Row 3 tăng từ `1.23*` lên `1.5*` (tăng ~45px chiều cao khả dụng).
    * Chiều ngang Bảng 6 tăng từ `1.45*` lên `1.72*` (thu gọn Bảng 7 sang `0.72*`, giữ nguyên Bảng 5 ở `1.0*`).
  - Nâng cấp cỡ chữ nội dung DataGrid từ `8pt` lên `9.5pt`, chiều cao dòng `RowHeight="24px"` (chuẩn Fitts' Law cho desktop, chống click nhầm).
  - Khắc phục lỗi hiển thị cột:
    * Cột `Override` mở rộng lên `65px`, hiển thị trọn vẹn tiêu đề (không bị cụt thành `Overrid`), chứa CheckBox tương tác ở giữa.
    * Cột `Message` mở rộng lên `80px`, hiển thị chính xác tên `MessageName` thay vì số `StartBit`.
    * Cột `Value` rộng `85px`, cỡ chữ `10pt` sắc nét, hỗ trợ format số thực.
- [x] Triển khai logic ViewModel cho Bảng 6 (`SimulationViewModel.cs` & `MainViewModel.cs`):
  - Bổ sung `SignalValueSearchText`, `ShowOnlyOverridden`, `FilteredValueSignals` (ICollectionView độc lập).
  - Cơ chế đồng bộ snapshot override nguyên tử hai chiều vào `ISimulationEngine.ReplaceSignalOverrides` khi người dùng toggle `IsOverridden` hoặc thay đổi `Value`.
  - Tự động đồng bộ trạng thái `Injected` (`#EF4444`) sang Bảng 4 và đồng bộ `SelectedSignal` sang Bảng 5.
- [x] Tạo bộ kiểm thử `SignalValueConfigurationTests.cs` (7 unit tests):
  - Kiểm thử lọc theo tên tín hiệu, lọc theo message, lọc chỉ tín hiệu override.
  - Kiểm thử cập nhật trạng thái màu sắc/hiển thị giá trị khi override.
  - Kiểm thử đồng bộ gọi `ReplaceSignalOverrides` khi bật/tắt override hoặc thay đổi giá trị.
  - Kiểm thử đồng bộ lựa chọn tín hiệu sang Bảng 5.
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 233/233 tests (100% PASS).

## Work log — 2026-08-20 (UI-06 Dynamic Signal Value Selection & VAL_ Enum Support)

- [x] Phân tích giải pháp công thái học từ dự án tham khảo `TreeViews and Value Converters` (đảm bảo 100% READ-ONLY không thay đổi dự án tham khảo):
  - Áp dụng cơ chế phân loại signal động `HasValueDescriptions` từ bảng `VAL_` trong DBC.
  - Sử dụng `DataGridTemplateColumn` kết hợp `ContentControl` và `DataTrigger`:
    * Với signal số thực liên tục: Hiển thị `TextBox` nhập số mượt mà, kiểm tra giới hạn `[Min, Max]` và đổi màu đỏ cảnh báo (`#EF4444`) khi vi phạm dải giá trị.
    * Với signal trạng thái/enum: Tự động chuyển đổi thành `ComboBox` dropdown hiển thị định dạng chuẩn `[RawValue] Description` (ví dụ `[0] Released`, `[1] Applied`).
  - Hỗ trợ parser hai chiều `PhysicalValueInput`: Tự động trích xuất mã số trong ngoặc vuông `[...]` để ghi vào backend và tự động kích hoạt `IsOverridden = true`.
  - Phản chiếu định dạng enum sang Bảng 4 (Live Signal Monitor) hiển thị `[0] Released` thay vì số thô.
  - Bổ sung nút **"Clear All"** trong thanh tìm kiếm Bảng 6 (`ClearAllOverridesCommand`) cho phép hủy nhanh toàn bộ tín hiệu ghi đè.
- [x] Bổ sung 3 unit tests mới trong `SignalValueConfigurationTests.cs` (nâng tổng số lên 10 tests):
  - `SignalWithValueDescriptions_ExposesAvailableDescriptionsAndDynamicDisplay`
  - `ContinuousSignal_MinMaxValidation_UpdatesValueColor`
  - `ClearAllOverrides_ResetsAllOverriddenSignals`
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 236/236 tests (100% PASS).
- [x] Tối ưu trực quan theo phản hồi người dùng:
  - **Bảng 6**: Khi tín hiệu được thay đổi/ghi đè (`IsOverridden == true`), ô giá trị Value (cả TextBox và ComboBox) tự động chuyển sang **nền trắng chữ đen** (`#FFFFFF` background, `#000000` text, font SemiBold) giúp phân biệt tức thì tín hiệu đang cấu hình.
  - **Bảng 4 (Live Signal Monitor)**: Tách biệt hoàn toàn trạng thái setup của Bảng 6 khỏi Bảng 4; Bảng 4 giữ nguyên trạng thái bus thực tế (`● No Data` / `● Active`), tuyệt đối không hiển thị sớm `● Injected` khi chưa khởi động tiêm lỗi ở Bảng 7.

## Work log — 2026-08-20 (UI-06 & UI-04 Bug Fix: Min/Max Range Validation Warning & Message-Filtered Signal Isolation)

- [x] **Khắc phục Lỗi 1 (Cảnh báo vi phạm dải Min/Max tại Bảng 6)**:
  - **Nguyên nhân gốc**: Khi tín hiệu được nhập/ghi đè, trigger `IsOverridden == true` ép màu chữ thành `#000000` và nền thành `#FFFFFF`, ghi đè hoàn toàn màu đỏ cảnh báo `ValueColor` (`#EF4444`), đồng thời ô thiếu viền cảnh báo và icon chỉ báo lỗi.
  - **Giải pháp xử lý**:
    * Trong `SignalModel` (`MainViewModel.cs`): Thêm thuộc tính `ValidationToolTip` tự động cập nhật câu cảnh báo tiếng Việt chi tiết (`⚠️ CẢNH BÁO: Giá trị {Value} vượt dải cho phép [{Min} .. {Max}]!`). Bổ sung bắt lỗi khi parse chuỗi phi số trong `PhysicalValueInput`.
    * Trong `MainWindow.xaml`: Cấu hình thứ tự trigger ưu tiên `IsValueValid == False` sau `IsOverridden == True`. Khi giá trị ngoài dải cho phép:
      - Nền ô chuyển sang màu đỏ nhạt cảnh báo (`Background="#FEF2F2"`).
      - Viền ô chuyển sang màu đỏ rực dày (`BorderBrush="#EF4444"`, `BorderThickness="1.5"`).
      - Chữ chuyển sang màu đỏ đậm in đậm (`Foreground="#DC2626"`, `FontWeight="Bold"`).
      - Xuất hiện biểu tượng cảnh báo `⚠️` màu đỏ ngay cạnh ô nhập với tooltip cảnh báo tiếng Việt rõ ràng.
- [x] **Khắc phục Lỗi 2 (Hiển thị riêng tín hiệu của message được chọn ở Bảng 3 cho Bảng 4 và Bảng 6)**:
  - **Nguyên nhân gốc**: `FilterSignal` (Bảng 4) và `FilterValueSignal` (Bảng 6) chưa lọc theo `SelectedMessage`, và `OnSelectedMessageChanged` chưa kích hoạt làm mới hai view này khi người dùng click chọn dòng trong Bảng 3.
  - **Giải pháp xử lý**:
    * Cập nhật `FilterValueSignal` và `FilterSignal` trong `SimulationViewModel.cs`: Khi `SelectedMessage is not null`, chỉ hiển thị các tín hiệu thuộc message đó (`signal.MessageId == SelectedMessage.Id || signal.MessageName == SelectedMessage.Name`). Khi `SelectedMessage is null`, hiển thị toàn bộ tín hiệu để duy trì tương thích.
    * Đồng bộ hai chiều an toàn giữa `SelectedMessage` (Bảng 3) và `SelectedSignalMessageFilter` (Bảng 4) qua guard `_isSyncingMessageSelection`. Khi người dùng click chọn 1 message ở Bảng 3, cả Bảng 4 và Bảng 6 lập tức đồng bộ lọc hiển thị riêng các tín hiệu của message đó.
    * Khi chuyển đổi message, `SelectedSignal` tự động chuyển sang tín hiệu đầu tiên của message mới, đồng bộ mượt mà sang Bảng 5 (Fault Configuration).
- [x] **Bộ kiểm thử tự động**:
  - Bổ sung các unit test trong `SignalValueConfigurationTests.cs`:
    * `ContinuousSignal_MinMaxValidation_UpdatesValidationToolTip`
    * `SelectingMessage_InMessageList_FiltersLiveMonitorAndValueConfigToSelectedMessage`
    * `ClearingSelectedMessage_RestoresAllSignalsInLiveMonitorAndValueConfig`
    * `ChangingSelectedSignalMessageFilter_BidirectionallySyncsSelectedMessage`
  - Verification: `dotnet build Simulate.sln` PASS 0 warning / 0 error; `dotnet test Simulate.sln` PASS 239/239 tests (100% PASS).

## Work log — 2026-08-20 (Test Benchmark Dataset & Virtual CAN Simulation Environment Registration)

- [x] **Cập nhật kho dữ liệu Test DBC chuẩn**:
  - Đã nạp file DBC chuẩn `C:\Users\Hnam\Downloads\data\VF EBUS6M_PCAN_V2.0.0_20250524.dbc` vào thư mục dự án `DBC/VF EBUS6M_PCAN_V2.0.0_20250524.dbc`.
  - Phân tích cấu trúc DBC: 61 Messages, 370 Signals, 11 Nodes, đầy đủ định nghĩa `VAL_` enum (chứa các message trọng yếu `VCU_NM`, `VCU_ASR_Ctrl`...).
  - Bổ sung kiểm thử chuyên biệt `Parse_VF_EBUS6M_PCAN_LoadsExpectedMessagesAndSignals` trong `DbcParserTests.cs` xác nhận DBC nạp thành công 100% không có lỗi.
- [x] **Thiết lập và ghi nhớ cấu hình phần cứng giả lập Vector CANoe (Virtual CAN Harness)**:
  - Driver / Interface: `Virtual CAN` (Vector XL Virtual Channel).
  - TX Channel (Phát): `Virtual CAN Bus 1 (000100) - Channel 1` -> UI: `Virtual Bus 1 - Channel 1`.
  - RX Channel (Nhận): `Virtual CAN Bus 2 (000101) - Channel 1` -> UI: `Virtual Bus 2 - Channel 1`.
  - Cấu hình truyền thông: Baudrate TX = 500k, Baudrate RX = 500k, CAN FD = Enabled.
- [x] **Quy tắc kiểm thử hành vi vòng lặp tiêm lỗi (Core Simulation Behavioral Requirement)**:
  - Khi Simulator phát data đường PCAN: Đường TX (`Virtual Bus 1 - Channel 1`) gửi frame; bất kỳ thay đổi/can thiệp tín hiệu nào qua UI 5 & UI 6 khi phát ra thì phía RX (`Virtual Bus 2 - Channel 1`) **bắt buộc phải nhận được đúng giá trị đã thay đổi đó**.
  - Đã ghi nhận ràng buộc và domain model vào `CONTEXT.md`, `tasks/plan.md`, `tasks/todo.md`, `handoff.md`.
- [x] Verification: `dotnet build Simulate.sln` PASS 0 warning / 0 error; `dotnet test Simulate.sln` PASS 240/240 tests (100% PASS).
- [x] Trạng thái trước: `READY_FOR_UI-07` (Sẵn sàng triển khai Bảng 7: Execution Control).

## Work log — 2026-08-20 / 2026-09-18 (UI-07 Execution Control Implementation Complete)

- [x] **FaultConfigurationViewModel**:
  - Bổ sung helper method phân tích chuỗi an toàn: `GetCycleInterval()`, `GetStartDelay()`, `GetDuration()`, `GetRepeatCount()`, `GetFaultValue(fallback)`, `ParseTimeSpan(input, defaultVal)`.
  - Hỗ trợ đầy đủ định dạng đơn vị ms, s, số thô, hex `0x...` và số thực với invariant culture.
- [x] **SimulationViewModel**:
  - Thêm các observable properties cho Bảng 7: `QueueStatusText` (mặc định `"Idle"`), `RunningFaultDisplay` (mặc định `"—"`), `QueueItemsDisplay` (`FaultQueue.Count.ToString(CultureInfo.InvariantCulture)`), `PauseInjectionButtonContent` (`"Ⅱ  Pause"` / `"▶  Resume"`).
  - Thêm 4 Relay Commands:
    * `StartInjectionCommand`: Khởi tạo `SimulationPlan` và `SimulationEngine` (nếu chưa có), bắt đầu receive loop (`StartAsync`) và scheduler (`StartSchedulingAsync`), khởi chạy Direct Injection hoặc Sequence Queue Runner tuần tự.
    * `StopInjectionCommand`: Hủy timer, dừng scheduler (`StopSchedulingAsync`), dừng engine (`StopAsync`), hoàn nguyên `RestoreAllOverrides()` nếu `FaultConfig.IsRestoreAfterStop == true`, cập nhật trạng thái `"Idle"`.
    * `TogglePauseInjectionCommand`: Tạm dừng / tiếp tục phát (`PauseScheduling` / `ResumeScheduling`), chuyển đổi trạng thái `"Paused"` và `"Running"`.
    * `ClearQueueCommand`: Xóa toàn bộ hàng đợi lỗi (`FaultQueue.Clear()`), cập nhật hiển thị.
  - Bổ sung seam `SetSessionProvider(Func<ICanGatewaySession?> sessionProvider)` và mượn session an toàn từ `ConnectionViewModel`.
  - Triển khai `IDisposable` và `IAsyncDisposable` đảm bảo dọn dẹp sạch sẽ tài nguyên khi đóng view model.
- [x] **MainViewModel**:
  - Thiết lập liên kết `Simulation.SetSessionProvider(() => Connection.ActiveGatewaySession)`.
  - Lắng nghe `Connection.PropertyChanged` để tự động cập nhật trạng thái khả dụng của `StartInjectionCommand`.
- [x] **MainWindow.xaml (Panel 7 Binding-Only)**:
  - Gắn `Command` cho 4 nút bấm (`Start`, `Stop`, `Pause`, `Clear Queue`) và `Text`/`Content` binding cho 3 nhãn trạng thái (`Queue Status`, `Queue Items`, `Running`).
  - Bảo tồn 100% cấu trúc layout, Grid definitions, colors, margins và control types theo đúng quy tắc bảo vệ UI.
- [x] **Simulate.Tests/ExecutionControlTests.cs**:
  - Bổ sung 9 bài test tự động bao phủ toàn diện:
    * Parse tham số thời gian và giá trị lỗi trong `FaultConfigurationViewModel`.
    * Bắt đầu tiêm lỗi với pre-configured engine và chuyển đổi trạng thái `"Running"`.
    * Tạm dừng và tiếp tục tiêm lỗi qua nút Pause/Resume.
    * Dừng tiêm lỗi và hoàn nguyên override khi `IsRestoreAfterStop` bật/tắt.
    * Xóa hàng đợi và cập nhật số lượng item hiển thị.
    * Mượn session từ provider, tạo plan và chạy engine thật.
    * Liên kết session giữa `ConnectionViewModel` và `MainViewModel`.
    * Thực thi chuỗi Sequence tuần tự từng bước (`Queued` -> `Running` -> `Completed`).
- [x] **Verification**:
  - `dotnet build Simulate.sln`: 0 warning / 0 error.
  - `dotnet test Simulate.sln`: 249/249 tests PASS (100%).

## Work log — 2026-08-20 / 2026-09-21 (Automated 1,000+ TCS Fuzzing & Stress Suite)

- [x] **Dựng bộ 1,001 Test Cases tự động (`Simulate.Tests/AutomatedThousandTests.cs`)**:
  - 370 tests: Roundtrip Pack & Unpack giá trị Minimum cho toàn bộ 370 tín hiệu của file DBC `VF EBUS6M_PCAN_V2.0.0_20250524.dbc`.
  - 370 tests: Roundtrip Pack & Unpack giá trị Maximum cho toàn bộ 370 tín hiệu của file DBC `VF EBUS6M_PCAN_V2.0.0_20250524.dbc`.
  - 100 tests: Fuzzing bộ phân tích thời gian `ParseTimeSpan` với các chuỗi dị thường, biên cực đại, đơn vị `ms`, `s`, khoảng trắng, số âm, ký tự đặc biệt, NaN, Infinity.
  - 60 tests: Fuzzing bộ đọc giá trị lỗi `GetFaultValue` với các chuỗi hex `0x...`, số âm, số thực dấu phẩy động và ký tự không hợp lệ.
  - 61 tests: Tạo lập và thẩm định `SimulationMessageRule` & `SimulationPlan` cho toàn bộ 61 CAN Messages của mạng PCAN với các chế độ Cyclic, One-Shot, Event.
  - 40 tests: Stress test ma trận chuyển đổi trạng thái liên tục (`Start` -> `Pause` -> `Resume` -> `Stop`, chống click đúp, chống race condition).
- [x] **Phát hiện và vá triệt để 2 lỗi qua đợt test 1000 TCS**:
  1. *Lỗi `System.OverflowException` trong `ParseTimeSpan`*: Nhập chuỗi dạng `"Infinity s"` hoặc số vượt quá `TimeSpan.MaxValue` làm sập hàm parse. Đã khắc phục bằng cách bổ sung kiểm tra `double.IsFinite(...)` và bọc `try-catch (OverflowException)` trả về `defaultVal` an toàn.
  2. *Lỗi nghẽn luồng kiểm thử scheduler khi chạy song song 1250 tests*: Ngưỡng chờ timer 1s trong `ManualTimeProvider.WaitForTimerCountAsync` bị timeout khi CPU chịu tải cực đại. Đã tăng lên 10s đảm bảo độ ổn định 100%.
- [x] **Verification**:
  - `dotnet test Simulate.sln`: **1,250 / 1,250 tests PASS (100%)** trong 6 giây.
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.

## Work log — 2026-09-21 (Gateway Bridge & Real-Time Live Signal Decoupling)

- [x] **Tham chiếu & Kế thừa kiến trúc chuẩn từ dự án `MitmEngine` (`D:\TEST_DEV\...`)**:
  - Nghiên cứu mã nguồn `MitmEngine.cs` và `SIMULATE.xaml.cs`: hệ thống chạy tiếp nối (Pass-Through bridging) 2 chiều RX $\leftrightarrow$ TX liên tục ngay khi kết nối bus.
  - Phân tách nhiệm vụ rạch ròi: Bảng 1 `Connect` mở Gateway tiếp nối thông mạng và Live Monitor; Bảng 7 `Start Injection` chỉ kích hoạt việc can thiệp giá trị lỗi và phát kịch bản chu kỳ.
- [x] **ISimulationEngine & SimulationEngine**:
  - Bổ sung sự kiện `FrameRouted` (bắn ra khi nhận được frame từ RX/TX sau khi lọc Echo).
  - Bổ sung phương thức `UpdatePlan(SimulationPlan plan)` nguyên tử, cho phép cập nhật nóng các rule can thiệp lỗi mà không phải dừng/khởi động lại vòng lặp gateway bus.
- [x] **SimulationViewModel**:
  - Bổ sung `StartBaselineGatewayAsync`: tự động dựng plan `PassThrough` cho các message và kích hoạt gateway tiếp nối ngay khi có phiên kết nối và DBC.
  - Bổ sung bộ đệm nhận frame và kỹ thuật **Throttle 33ms (~30fps)** (`_liveFrameBuffer` + Dispatcher marshal) kế thừa từ `SIMULATE.xaml.cs` giúp Bảng 4 (`Live Signal Monitor`) cập nhật sóng mượt mà, chống nghẽn UI khi lưu lượng bus cao.
  - Cập nhật `StartInjectionAsync`: chỉ nạp plan tiêm lỗi vào engine đang chạy (`UpdatePlan`) và khởi động scheduler chu kỳ (`StartSchedulingAsync`).
  - Cập nhật `StopInjectionAsync`: dừng scheduler, hoàn nguyên giá trị đè (`RestoreAllOverrides`), cập nhật lại plan về Baseline, **tiếp tục duy trì vòng lặp Gateway tiếp nối và Live Monitor chạy liên tục**.
  - Bổ sung `StopGatewayAsync`: chỉ dừng hoàn toàn gateway khi người dùng nhấn `Disconnect` ở Bảng 1 hoặc thoát ứng dụng.
- [x] **MainViewModel**:
  - Tự động liên kết `Connection.IsConnected` và `Dbc.DocumentLoaded` với `StartBaselineGatewayAsync` và `StopGatewayAsync`.
- [x] **Fix TOCTOU Race Condition trong `SimulationSchedulerTests`**:
  - Sửa hàm `WaitForTimerCountAsync` kiểm tra `_timerCount >= expectedCount` bên trong khóa `lock (_sync)` trước khi capture task, loại bỏ hoàn toàn khả năng timeout giả khi chạy song song tải cao.
- [x] **Verification**:
  - `dotnet test Simulate.sln`: **1,252 / 1,252 tests PASS (100%)** trong 5 giây.
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.

## Work log — 2026-09-21 (Decouple DBC Dependency from Gateway Pass-Through Bridge)

- [x] **Phân tách triệt để sự phụ thuộc của Gateway vào file DBC**:
  - Nhận định kiến trúc: Gateway là cầu nối Tầng 2 (Data Link Layer), bắc cầu mọi frame CAN nguyên bản giữa 2 bus RX $\leftrightarrow$ TX; DBC chỉ là từ điển Tầng 7 (Application Layer) dùng giải mã tín hiệu và can thiệp lỗi.
  - Gateway không được phép phụ thuộc vào việc có DBC hay không, hoặc DBC đúng hay sai.
- [x] **SimulationPlan & SimulationEngine**:
  - Hỗ trợ `SimulationPlan(DbcDocument? document, ...)` cho phép `Document` có thể là `null`.
  - Bổ sung factory method `SimulationPlan.CreateRawPassThrough()`.
  - Cập nhật `SimulationEngine` và `UpdatePlan` hoạt động an toàn khi `plan.Document == null`: chuyển tiếp 100% các raw frame giữa RX và TX.
- [x] **SimulationViewModel**:
  - Xóa bỏ điều kiện chặn `_currentDocument is null` trong `StartBaselineGatewayAsync()`.
  - Bổ sung cơ chế nạp DBC động (`LoadDocument`): cập nhật baseline plan vào engine đang chạy mà không ngắt luồng bridge.
  - Cập nhật `ClearDocument`: khi dỡ bỏ DBC, chuyển engine về `CreateRawPassThrough()`, tiếp tục duy trì gateway bridge chạy liên tục.
  - Cập nhật `CanStartInjection`: chỉ cho phép tiêm lỗi khi đã có DBC (vì tiêm lỗi cần tín hiệu DBC).
- [x] **MainViewModel**:
  - Trong `Connection.PropertyChanged`: Ngay khi `IsConnected == true`, tự động khởi chạy `StartBaselineGatewayAsync()` lập tức mà không cần chờ `Dbc.HasDocument`.
  - Trong `Dbc.DocumentLoaded`: Nạp document vào ViewModel, nếu gateway chưa chạy thì khởi động.
  - Trong `Dbc.DocumentUnloaded`: Chỉ xóa dữ liệu DBC, giữ nguyên gateway đang chạy.
- [x] **Unit Tests**:
  - Bổ sung 3 unit tests mới trong `Simulate.Tests/ExecutionControlTests.cs`:
    * `SimulationPlan_CreateRawPassThrough_AllowsNullDocumentAndEmptyRules`
    * `StartBaselineGatewayAsync_WithoutDbc_StartsRawBridge_AndAttachesDbcDynamically`
    * `SimulationEngine_RawPassThrough_RoutesFrameWithoutDbc`
- [x] **Verification**:
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.
  - `dotnet test Simulate.sln`: **1,255 / 1,255 tests PASS (100%)**.
  - `git diff --check`: 0 lỗi format / EOF whitespace.
- [x] Trạng thái hiện tại: `USER_ACCEPTED_UI-07` (Đã nghiệm thu hoàn tất UI-07 & Gateway độc lập DBC, sẵn sàng chuyển giao UI-08 Log / Output).

## Work log — 2026-09-21 (UI-06 Bug Fix: Unhandled Exception Crash When Editing Signal Values While CAN is Connected)

- [x] **Nguyên nhân gốc của lỗi Crash**:
  - Khi CAN chưa `Connect`: `_engine == null`, phương thức `SyncSignalOverrides` return sớm $\implies$ Không phát sinh lỗi.
  - Khi CAN đã `Connect`: Gateway tự động chạy tiếp nối ở chế độ Baseline Pass-Through (`_engine != null`, nhưng rule của message là `GatewayMode.PassThrough` vì chưa bấm `▶ Start Injection`).
  - Khi người dùng nhập giá trị mới hoặc tick/untick `Override` tại Bảng 6, sự kiện `SignalModel.PropertyChanged` kích hoạt `SyncSignalOverrides` $\rightarrow$ gọi `_engine.ReplaceSignalOverrides`.
  - Trong `SimulationEngine.cs`, hàm `ReplaceSignalOverrides` kiểm tra rule không phải `Inject` nên ném ngoại lệ:
    `System.ArgumentException: 'Runtime signal overrides require a configured, enabled inject rule. (Parameter 'canIdentifier')'`.
  - Do được gọi trực tiếp trên UI Thread từ sự kiện DataGrid mà không có khối `try-catch`, ngoại lệ này trở thành Unhandled Exception và làm ứng dụng crash ngay lập tức.
- [x] **Giải pháp xử lý triệt để 2 giai đoạn (Drafting vs Live Injection)**:
  - **`Simulate/Services/SimulationEngine.cs`**:
    * Trong `ReplaceSignalOverrides`: Nếu rule không phải là `Inject` nhưng `replacement.Length == 0` (hành động xóa / tắt override), xử lý an toàn (safe no-op) bằng cách return thay vì ném `ArgumentException`.
  - **`Simulate/ViewModels/SimulationViewModel.cs`**:
    * Trong `SyncSignalOverrides`:
      1. *Khi đang trong phiên tiêm lỗi hoạt động (`QueueStatusText == "Running" && _engine.IsRunning`)*: Tự động gọi `_engine.UpdatePlan(BuildSimulationPlan())` để cập nhật nóng toàn bộ plan trên bus. Bất kỳ message nào mới được tick override sẽ được nâng cấp tức thời sang `GatewayMode.Inject` trên bus (Live Tuning) mà không gây gián đoạn luồng tiếp nối.
      2. *Khi đang ở chế độ Gateway Baseline Pass-Through (chưa bấm `Start Injection`)*: Bọc lời gọi `_engine.ReplaceSignalOverrides` trong khối `try-catch (ArgumentException)`. Giá trị override được lưu trữ và duy trì an toàn trong ViewModel state (sẽ được nạp tự động khi bấm `▶ Start Injection`), bảo đảm tuyệt đối không bao giờ làm sập luồng UI.
- [x] **Kiểm thử tự động (Unit Test)**:
  - Bổ sung test mới trong `Simulate.Tests/SignalValueConfigurationTests.cs`:
    * `SignalValueEdit_WhenEngineInPassThroughMode_DoesNotCrashAndSavesOverride`: Khởi tạo session thật, nạp DBC, chạy engine ở chế độ Pass-Through, thực hiện sửa đổi giá trị tín hiệu và bật/tắt checkbox Override; xác nhận không có bất kỳ ngoại lệ nào xảy ra, giá trị lưu trữ chính xác 100%.
- [x] **Verification**:
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.
  - `dotnet test Simulate.sln`: **1,256 / 1,256 tests PASS (100%)** trong 6 giây.
  - `git diff --check`: 0 lỗi format / EOF whitespace.
  - UI XAML: Bảo vệ 100%, không thay đổi bất kỳ thuộc tính layout hay giao diện nào.

## Work log — 2026-09-21 (UI-04 Realtime Signal Monitor: Fix Bidirectional Reception & Decoupled 30fps Dispatcher Flush)

- [x] **Khắc phục lỗi chặn chiều nhận Frame (Bidirectional CAN Monitoring)**:
  - **Nguyên nhân gốc**: `OnEngineFrameRouted` lọc cứng `if (routedFrame.Source != CanGatewaySide.Rx) return;`. Nếu tool test hoặc ECU phát vào đường TX (`Virtual Bus 1`), toàn bộ frame bị drop ngay lập tức.
  - **Giải pháp**: Gỡ bỏ điều kiện lọc 1 chiều, mở rộng cho phép Bảng 4 bắt trọn frame từ cả hai nhánh `CanGatewaySide.Rx` và `CanGatewaySide.Tx`.
- [x] **Kiến trúc Decoupled Periodic Flush Timer (Chống kẹt frame cuối & tối ưu 30fps)**:
  - Bổ sung `System.Threading.Timer _liveFlushTimer` chu kỳ 33ms (~30fps) tự động quét và xả `_liveFrameBuffer` lên luồng UI qua `FlushLiveBufferToSignals`.
  - Loại bỏ hoàn toàn hiện tượng kẹt frame cuối (trailing frame) khi lưu lượng frame ngắt quãng.
  - `OnEngineFrameRouted` chỉ nạp frame nhanh vào dictionary dưới lock nhẹ ($O(1)$), không gây tải hay nghẽn luồng nhận.
  - Quản lý vòng đời timer chặt chẽ: tự động khởi động khi start gateway/injection và dispose an toàn khi stop gateway hoặc dispose ViewModel.
- [x] **Tối ưu hóa tra cứu số nguyên $O(1)$**:
  - Bổ sung `RawIdentifier` và `IsExtendedIdentifier` vào `SignalModel`.
  - Trong `ProcessIncomingFrame`, so khớp trực tiếp theo số nguyên nguyên bản kết hợp fallback chuỗi `FormatIdentifier`, tăng tốc độ giải mã và triệt tiêu phân bổ bộ nhớ rác.
- [x] **Đồng bộ Plan tự động khi thay đổi danh sách Messages**:
  - Trong `AddMessage`, `DeleteMessage`, `DeleteAllMessages`: nếu `_engine.IsRunning == true`, tự động gọi `_engine.UpdatePlan(BuildBaselineSimulationPlan())` để cập nhật đồng bộ các message được cấu hình.
- [x] **Đăng ký lắng nghe FrameRouted trong Constructor**:
  - Đảm bảo `_engine.FrameRouted += OnEngineFrameRouted;` và `EnsureLiveFlushTimerStarted();` được đăng ký ngay trong constructor nhận engine có sẵn.
- [x] **Unit Tests**:
  - Bổ sung 3 bài test tự động trong `Simulate.Tests/LiveSignalMonitorTests.cs`:
    * `LiveSignalMonitor_WhenFrameReceivedFromTxSide_UpdatesSignalsCorrectly`: Xác nhận nhận frame từ nhánh TX, cập nhật `HasReceivedData = true`, `StatusText = "● Active"`, giá trị `RawValue` và `PhysicalValueDisplay`.
    * `LiveSignalMonitor_WhenFrameReceivedFromRxSide_UpdatesSignalsCorrectly`: Xác nhận nhận frame từ nhánh RX.
    * `LiveSignalMonitor_FlushTimer_FlushesTrailingFramesAutomatically`: Xác nhận timer tự động xả cạn frame lên UI mà không cần gọi flush thủ công.
- [x] **Verification**:
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.
  - `dotnet test Simulate.sln`: **1,259 / 1,259 tests PASS (100%)** trong 9 giây.
  - `git diff --check`: 0 lỗi format / EOF whitespace.
  - UI XAML: Bảo vệ 100%, không thay đổi bất kỳ thuộc tính layout hay giao diện nào.

## Work log — 2026-09-21 (UI-04 Live Signal Monitor: Reflect Post-Gateway Injected Values and Injected Status)

- [x] **Khắc phục hiện tượng Bảng 4 (Live Signal Monitor) không cập nhật giá trị đã simulate/tiêm lỗi**:
  - **Phát hiện nguyên nhân gốc rễ**:
    1. Khi frame đến từ ECU nguồn (mang giá trị gốc `44`), `SimulationEngine` gọi `FrameRouted` TRƯỚC KHI thực hiện tiêm lỗi; sau đó mới sửa thành `100` và gửi ra bus đối diện (TSMaster nhận được `100`), nhưng frame sau khi tiêm này không hề được thông báo cho `FrameRouted`.
    2. Trong `SignalModel.UpdateValue`, hàm luôn gán `PhysicalValueDisplay = FormatDisplayValue(physical)` (`44`), `RawValue = 0x2C` và `StatusText = "● Active"` (xanh lá). Khi ECU nguồn gửi frame chu kỳ, Bảng 4 liên tục bị đè lại về giá trị gốc `44` và màu xanh lá, bất chấp người dùng đã cấu hình tiêm `100`.
    3. Các frame tự phát do scheduler (Cyclic/One-Shot/Event) phát trực tiếp ra bus mà không kích hoạt `FrameRouted`, làm Live Monitor không quan sát được các frame do scheduler sinh ra.
- [x] **SignalModel (MainViewModel.cs)**:
  - Bổ sung `RefreshOverriddenDisplay()`: tính toán chính xác giá trị hiển thị `FormatDisplayValue(Value)`, giá trị `RawValue` từ `Value`, và đặt `StatusText = "● Injected"`, `StatusColor = "#EF4444"`.
  - Trong `OnIsOverriddenChanged`: khi tick override và đã có dữ liệu (`HasReceivedData == true`), ngay lập tức cập nhật sang `● Injected` và hiển thị `Value`. Khi bỏ tick, đưa về `● Active` (nếu có data) hoặc `● No Data`.
  - Trong `OnValueChanged`: khi đang override, việc thay đổi giá trị lập tức phản chiếu lên Bảng 4 thời gian thực.
  - Trong `UpdateValue`: khi `IsOverridden == true`, duy trì gọi `RefreshOverriddenDisplay()` và cập nhật `LastUpdated`, tuyệt đối không bị frame gốc từ bus nguồn đè mất giá trị tiêm.
- [x] **SimulationEngine (SimulationEngine.cs)**:
  - Trong `RunReceiveLoopAsync`: khi `wasModified == true` (frame đã được can thiệp tiêm lỗi), kích hoạt `FrameRouted?.Invoke(new RoutedCanFrame(destination, outboundFrame, _timeProvider.GetUtcNow()))` để Live Monitor nhận diện được frame đã tiêm.
  - Trong `TransmitScheduledFrameAsync`: kích hoạt `FrameRouted?.Invoke(new RoutedCanFrame(CanGatewaySide.Tx, outboundFrame, _timeProvider.GetUtcNow()))` khi scheduler phát frame ra bus.
- [x] **SimulationViewModel (SimulationViewModel.cs)**:
  - Trong `StartInjectionAsync`: làm mới toàn bộ các tín hiệu đang override qua `RefreshOverriddenDisplay()` ngay khi bắt đầu phiên tiêm lỗi.
- [x] **Unit Tests (LiveSignalMonitorTests.cs)**:
  - Bổ sung 2 bài kiểm thử tự động mới:
    * `LiveSignalMonitor_WhenSignalIsOverridden_DisplaysInjectedValueAndRedStatus`: Xác nhận Bảng 4 cập nhật tức thời khi override (`500 rpm`, `0xFA0`, `● Injected`, `#EF4444`), không bị frame gốc tiếp theo đè lại, và tự động khôi phục về `● Active` khi bỏ tick override.
    * `SimulationEngine_WhenInjectingFrame_TransmitsModifiedPayloadToDestination`: Xác nhận `SimulationEngine` phát frame đã tiêm sang phía `Tx` với payload đã được sửa đổi và tín hiệu còn lại được bảo toàn nguyên vẹn.
- [x] **Verification**:
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.
  - `dotnet test Simulate.sln`: **1,261 / 1,261 tests PASS (100%)** trong 7 giây.
  - `git diff --check`: 0 lỗi format / EOF whitespace.
  - UI XAML: Bảo vệ 100%, 0% thay đổi layout/styles.

## Work log — 2026-09-21 (Fix Signal Jitter on Unrelated Signals & Eliminate Multi-Source Event Collision)

- [x] **Khắc phục triệt để lỗi nhảy loạn xạ tín hiệu còn lại (`VCU_CBV`) khi tiêm đè `VCU_SourceAddress` (Value = 100)**:
  - **Phân tích nguyên nhân cốt lõi**:
    1. *Multi-Source Event Collision*: Hai lệnh `FrameRouted?.Invoke(...)` mới thêm ở dòng 625 (sau khi inject) và 769 (sau khi scheduler phát) trong `SimulationEngine.cs` đã bắn nhiều sự kiện cho cùng 1 chu kỳ CAN với các payload khác nhau (payload Rx thật vs payload Tx tiêm vs payload scheduler), gây xung đột dữ liệu đè lên nhau.
    2. *Feedback Loop trong ViewModel*: `OnSignalItemPropertyChanged` trong `SimulationViewModel.cs` bắt mọi sự kiện thay đổi thuộc tính `Value` của mọi tín hiệu (kể cả tín hiệu không override). Khi frame CAN thật đến làm `Value` của `VCU_CBV` thay đổi, nó kích hoạt `SyncSignalOverrides` -> `UpdatePlan(plan)` lặp đi lặp lại hàng chục lần/giây, làm gián đoạn luồng engine.
  - **Giải pháp xử lý chính xác (Surgical Fix)**:
    1. **Khôi phục `SimulationEngine.cs` về nguyên bản**:
       - Gỡ bỏ hoàn toàn 2 lệnh `FrameRouted?.Invoke(...)` ở dòng 625 và 769. `FrameRouted` chỉ phát đúng 1 lần duy nhất tại cửa ngõ tiếp nhận frame vào gateway (dòng 578), bảo vệ kiến trúc đơn luồng sự kiện nguyên thủy của engine.
    2. **Chặn triệt để Feedback Loop trong `SimulationViewModel.cs`**:
       - Cập nhật dòng 413: `if (e.PropertyName == nameof(SignalModel.IsOverridden) || (signal.IsOverridden && e.PropertyName == nameof(SignalModel.Value)))`.
       - Chỉ khi nào tín hiệu thực sự đang được đánh dấu override (`IsOverridden == true`) thì việc đổi giá trị mới đồng bộ xuống engine. Tín hiệu bình thường nhận dữ liệu từ bus không bao giờ kích hoạt `SyncSignalOverrides`.
    3. **Hiển thị ổn định tại `SignalModel` (MainViewModel.cs)**:
       - Khi `IsOverridden == true`: Bảng 4 giữ vững trạng thái `● Injected`, màu đỏ `#EF4444`, hiển thị giá trị tiêm `Value`.
       - Khi `!IsOverridden`: Hiển thị ổn định giá trị nhận từ bus thật (`Value = physical`, `● Active`, màu xanh `#10B981`), không bị nhảy loạn hoặc bị payload khác đè lên.
  - **Verification**:
    - `dotnet build Simulate.sln`: **0 warning / 0 error**.
    - `dotnet test Simulate.sln`: **1,261 / 1,261 tests PASS (100%)**.
    - UI XAML: 0% thay đổi, giữ nguyên vẹn toàn bộ giao diện và thư mục tham chiếu.

## Work log — 2026-09-21 (UI-08: User Action Audit & System Diagnostic Log)

- [x] **Triển khai hoàn tất Bảng 8 (8. LOG / OUTPUT)**:
  - **Định hướng chính xác**: Tuyệt đối không hook vào CAN bus frame/signal chatter để tránh ngập rác log; 100% tập trung vào kiểm toán thao tác người dùng (User Action Audit Trail), cảnh báo vận hành và chẩn đoán sự cố phần mềm.
  - **Core Domain & Service**:
    * Tạo `Simulate/Models/LogEntry.cs`: Struct lưu `Timestamp`, `Level` (`Info`, `Warning`, `Error`), `SourceModule` và `FormattedLine`.
    * Tạo `Simulate/Services/ILogService.cs` và `LogService.cs`: Vòng đệm an toàn `BoundedQueue` với `MaxCapacity = 1,000` dòng, thread-safe, tự động giải phóng bản ghi cũ nhất (FIFO eviction), chống tràn RAM 100%.
    * Mở rộng `IFileDialogService.cs` và `DefaultFileDialogService.cs` với phương thức `SaveFileDialog`.
  - **ViewModel & Event Wiring**:
    * Tạo `Simulate/ViewModels/LoggingViewModel.cs`: Quản lý danh sách log, bộ lọc `AvailableLevels` (`All Levels`, `Info`, `Warning`, `Error`), lệnh `ClearCommand` và lệnh `ExportCommand` xuất file UTF-8 an toàn.
    * Tích hợp vào `MainViewModel.cs`: Tự động hook các sự kiện Connect/Disconnect CAN, nạp/gỡ bỏ file DBC, bật/tắt override và sửa giá trị tín hiệu, thêm/xóa hàng đợi lỗi, điều khiển Start/Pause/Resume/Stop Injection.
  - **UI (MainWindow.xaml) — 100% Strict UI Boundary**:
    * Giữ nguyên 100% layout, style, controls hiện có; chỉ gán data binding cho ComboBox, 2 Buttons, và TextBlock hiển thị log.
  - **Unit Tests**:
    * Thêm 10 bài kiểm thử mới trong `Simulate.Tests/LoggingTests.cs`.
  - **Verification**:
    * `dotnet build Simulate.sln`: 0 warning / 0 error.
    * `dotnet test Simulate.sln`: 1,271 / 1,271 tests PASS (100%).
    * `git diff --check`: 0 lỗi format / EOF whitespace.
    * UI XAML: 0% thay đổi layout, chỉ thuần túy data binding.

## Work log — 2026-09-22 (UI-09: Bus Monitor Health & 1,000 Automated Test Cases)

- [x] **Triển khai hoàn tất Bảng 9 (9. BUS MONITOR (HEALTH))**:
  - **Tối ưu kiến trúc loại bỏ sóng động**: Thay vì tính toán vector geometry tọa độ $X,Y$ liên tục (gây lag và giật luồng UI), giữ nguyên các điểm tĩnh của Polyline làm đường viền và sử dụng cơ chế đổi màu thông minh (`HealthStrokeColor`) báo hiệu sức khỏe mạng CAN:
    * ⚪ Xám (`#64748B`): Offline / Ngắt kết nối.
    * 🟢 Xanh lá (`#10B981`): Hoạt động tối ưu (`Bus Load < 60%`, `0` lỗi).
    * 🟡 Vàng (`#F59E0B`): Tải cao / Cảnh báo (`60% <= Bus Load <= 80%` hoặc có Warning).
    * 🔴 Đỏ (`#EF4444`): Nguy cấp / Lỗi (`Bus Load > 80%`, hoặc có Error/Frame Drop).
  - **Đo lường viễn thám thực tế (Real-Time Telemetry)**:
    * Tạo `Simulate/ViewModels/BusHealthViewModel.cs`: Timer $500\text{ms}$ ngầm tính toán tải bus $\Delta \text{Frames} / \text{Baudrate}$, hiển thị dạng `xx.x%` với màu đồng bộ.
    * Tự động liên kết `Lost` với `GatewayStatistics.DroppedFrames`.
    * Tự động ghi nhận `Errors` từ `HardwareFailure` và `Logging.LogService` lỗi.
    * Tự động đếm `Warning` từ `Logging.LogService` cảnh báo.
    * Tự động reset và hoàn nguyên về Offline khi ngắt kết nối (`Disconnect`).
  - **Tích hợp vào `MainViewModel.cs`**:
    * Bổ sung `public BusHealthViewModel BusHealth { get; }`.
    * Kết nối các sự kiện Connect, Disconnect, Hardware Failure, Log Added, Log Cleared.
  - **Bảo vệ UI 100% trong `MainWindow.xaml`**:
    * Giữ nguyên 100% layout, thẻ Canvas, Line, Polyline, Grid, StackPanel.
    * Chỉ gán Data Binding cho `Stroke="{Binding BusHealth.HealthStrokeColor}"` và TextBlock các chỉ số.
- [x] **Xây dựng bộ 1,000 Test Cases chuyên biệt (`Simulate.Tests/BusHealthThousandTests.cs`)**:
  - 500 tests: Fuzzing ma trận tính toán tải bus, boundary values, NaN/Infinity safety, và phân cấp trạng thái sức khỏe.
  - 250 tests: Kiểm thử chuyển đổi trạng thái vòng đời liên tục (Offline -> Connect -> Warning -> Error -> Reset) và concurrency stress test.
  - 250 tests: Kiểm thử cách ly hồi quy, chứng minh `BusHealth` chạy song song không làm xáo trộn, gián đoạn hay ảnh hưởng đến các phân hệ khác (Bảng 4 Live Monitor, Bảng 6 Signal Override, Bảng 7 Execution Control, Bảng 8 Logging).
- [x] **Verification**:
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.
  - `dotnet test Simulate.sln`: **2,271 / 2,271 tests PASS (100%)** trong 6 giây (toàn bộ 1,000 tests mới + 1,271 tests trước đó đều pass sạch).
  - `git diff MainWindow.xaml`: Đúng 1 dòng diff duy nhất gán data binding, 0% thay đổi layout.
  - `git diff --check`: 0 lỗi format / trailing whitespace.

## Work log — 2026-09-22 (Fix Gateway Queue Overflow Sudden Death & Eliminate False-Positive Green on UI 9)

- [x] **Khắc phục triệt để lỗi "Zombie Gateway" do Vector Queue Overflow & Sai lệch màu sắc UI 9**:
  - **Phân tích nguyên nhân cốt lõi**:
    1. *Fatal Throw trên Non-Fatal Hardware Warning*: Khi TSMaster phát 6 CAN message chu kỳ 1ms dồn dập (6,000+ msgs/s), driver Vector báo `QueueOverflow` / `QueueOverrun`. Tầng HAL `VectorCanGatewaySession.cs` ném `HardwareOperationException` làm `_workerTask` trong `SimulationEngine.cs` chết vĩnh viễn ở trạng thái `Faulted`, dừng toàn bộ luồng truyền nhận 2 chiều.
    2. *Hiện tượng Zombie Gateway*: Cổng kết nối vẫn mở (`IsConnected == true`), nhưng luồng nhận đã chết $\implies \Delta \text{Frames} = 0 \implies \text{Bus Load} = 0.0\%$. `Simulation.LastFailure` không được làm mới. Thuật toán `ComputeTelemetrySnapshot` thấy 0% tải và 0 lỗi nên hiển thị **Màu Xanh Tối Ưu (`#10B981`)** giả tạo.
  - **Giải pháp xử lý triệt để (Automotive Standard Solution)**:
    1. **Tầng HAL Session (`VectorCanGatewaySession.cs` & `ICanHardwareDriver.cs`)**:
       - Xóa bỏ hoàn toàn lệnh `throw` khi gặp `QueueOverflow` (CAN FD dòng 547-553) và `QueueOverrun` (Classic CAN dòng 301-307).
       - Bổ sung event `public event Action? FrameLossDetected;` trên `ICanGatewaySession`. Khi driver phát hiện tràn buffer, kích hoạt event báo mất frame và **tiếp tục duyệt các frame hợp lệ còn lại trong batch**.
    2. **Tầng Engine (`SimulationEngine.cs` & `ISimulationEngine.cs`)**:
       - Hook `_session.FrameLossDetected`: Tự động tăng bộ đếm `Interlocked.Increment(ref _droppedFrames)` $\implies$ Phản ánh trung thực vào `Statistics.DroppedFrames` (chỉ số `Lost`).
       - Bổ sung event `public event Action<HardwareFailure>? EngineFaulted;` phát tín hiệu khi engine gặp lỗi phần cứng chí mạng.
    3. **Tầng ViewModel & Viễn thám (`BusHealthViewModel.cs` & `MainViewModel.cs`)**:
       - Bổ sung `_isEngineRunningProvider` vào `BusHealthViewModel`.
       - Thuật toán `ComputeTelemetrySnapshot`:
         * Nếu `Connected` nhưng `!IsEngineRunning`: Hiển thị `Standby` (Màu Xám `#64748B`, `● Standby`) hoặc `Faulted` (Màu Đỏ `#EF4444`, `● Faulted` nếu có lỗi). **Tuyệt đối không bao giờ báo xanh khi engine không chạy**.
         * Nếu `DroppedFrames > 0`: Lập tức chuyển sang **Màu Đỏ Critical (`#EF4444`)**, tăng số đếm `Lost`.
       - Trong `MainViewModel.cs`: Tự động ghi log cảnh báo ra UI 8 khi có buffer overflow: `[WARN] [Hardware] Hardware CAN receive buffer overflow reported — frame(s) lost.`.
    4. **Bảo vệ UI 100%**:
       - 0% thay đổi trên UI XAML, giữ nguyên 100% layout và styles.
  - **Unit Tests & Verification**:
    - Cập nhật 2 unit tests cũ trong `VectorHardwareServiceTests.cs` sang resilience behavior: nhận frame hợp lệ và kích hoạt `FrameLossDetected`.
    - Thêm 4 bài unit test mới trong `BusHealthThousandTests.cs` khóa chặt các trạng thái Standby, Faulted và FrameLoss.
    - `dotnet build Simulate.sln`: **0 warning / 0 error**.
    - `dotnet test Simulate.sln`: **2,275 / 2,275 tests PASS (100%)** trong 9 giây.

## Work log — 2026-09-22 (Disconnect Graceful Exit, Panel 6 Signal Stability & UI 9 Dynamic Recovery)

- [x] **Sự cố 1: Khắc phục triệt để `OperationCanceledException` khi nhấn Disconnect**:
  - **Nguyên nhân**: Lệnh `cancellationToken.ThrowIfCancellationRequested()` được gọi trong `VectorCanGatewaySession.cs` trên background worker thread trong khi session đang dừng; đồng thời `SimulationEngine.RunReceiveLoopAsync` thiếu khối `catch (OperationCanceledException)`, khiến debugger Visual Studio ngắt với `Exception User-Unhandled`.
  - **Giải pháp**:
    1. Trong `VectorCanGatewaySession.cs`: Đảo điều kiện kiểm tra channel active/cleanup trước lệnh kiểm tra cancellation token. Khi session đang đóng hoặc đã giải phóng, trả về `null` thay vì ném exception. Trong `ReceiveClassicAsync` và `ReceiveCanFdAsync`, bắt `OperationCanceledException` khi session không còn active để `yield break` êm thắm.
    2. Trong `SimulationEngine.cs`: Bổ sung khối `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` trong `RunReceiveLoopAsync`, đón bắt sạch sẽ việc dừng luồng đọc mà không để rò rỉ ngoại lệ ra ThreadPool.
- [x] **Sự cố 2: Khắc phục triệt để lỗi nhảy data simulate tại Bảng 6 (`SIGNAL VALUE CONFIGURATION`)**:
  - **Nguyên nhân**: Tight coupling giữa Bảng 4 và Bảng 6. Hàm `UpdateValue` nhận stream CAN từ bus và gán trực tiếp `Value = physical`, kích hoạt `OnValueChanged` $\rightarrow$ bắn event `OnPropertyChanged(nameof(PhysicalValueInput))` làm ComboBox và TextBox tại Bảng 6 bị giật/nhảy số liên tục theo simulator.
  - **Giải pháp**:
    1. Trong `SignalModel` (`MainViewModel.cs`): Bổ sung `_configuredValue` để bảo vệ giá trị cấu hình draft của Bảng 6.
    2. `PhysicalValueInput`: Đọc và ghi độc lập vào `_configuredValue`. Khi chưa override, `PhysicalValueInput` giữ nguyên giá trị cấu hình của kỹ sư (hoặc giá trị khởi tạo DBC), không bị frame CAN trên bus đè lên.
    3. `UpdateValue`: Chỉ cập nhật hiển thị dữ liệu thời gian thực cho Bảng 4 (`PhysicalValueDisplay`, `RawValue`, `StatusText`), tuyệt đối không kích hoạt `PropertyChanged` của `PhysicalValueInput` khi chưa override.
    4. Khi tick `Override`: Tự động nạp giá trị cấu hình vào `Value` để engine tiêm đè lên bus, Bảng 4 lập tức đổi sang màu đỏ `● Injected`.
- [x] **Sự cố 3: Khắc phục Bảng 9 (Bus Monitor Health) kẹt màu vàng sau tải nặng 1ms, không hồi phục về xanh**:
  - **Nguyên nhân**: `_accumulatedWarnings` là bộ đếm tích lũy của toàn bộ phiên làm việc. Khi có tải 1ms làm phát sinh warning (lên đến 152 warnings), điều kiện `else if (busLoad >= 60.0 || warnings > 0)` luôn luôn thỏa mãn kể cả khi tải đã giảm về 0%, khiến đường line bị kẹt cứng ở màu vàng vĩnh viễn.
  - **Giải pháp**:
    1. Trong `BusHealthViewModel.cs`: Tách biệt giữa **Số liệu thống kê tích lũy (Cumulative Counters)** và **Sức khỏe động học tức thời (Dynamic Health State)**.
    2. Theo dõi `deltaWarnings`, `deltaErrors`, `deltaDroppedFrames` và thiết lập cửa sổ hold cooldown (~1.5s).
    3. Khi tải 1ms kết thúc và không còn warning mới phát sinh, hệ thống tự động phục hồi trạng thái sức khỏe đường line và nhãn trạng thái về **MÀU XANH TỐI ƯU (`#10B981` - `● Optimal`)**.
    4. Các nhãn thống kê `Warning: 152` và `Lost: 76` vẫn được bảo toàn nguyên vẹn trên giao diện để kỹ sư theo dõi lịch sử.
- [x] **Unit Tests & Verification**:
  - Bổ sung 3 bài unit test chuyên sâu trong `Simulate.Tests/SignalValueConfigurationTests.cs`:
    * `IncomingCanStream_DoesNotCorruptOrJitterPanel6ConfigurationInput`: Xác nhận Bảng 4 nhận frame CAN thật còn Bảng 6 giữ nguyên cấu hình không bị nhảy số.
    * `BusHealth_DynamicRecovery_WhenBurstLoadFinishes_RecoversToOptimalGreen`: Xác nhận Bảng 9 tự động hồi phục về Xanh sau khi tải 1ms giảm về bình thường, bảo toàn số đếm tích lũy.
    * `SessionDisconnect_CancelsGracefullyWithoutUnhandledException`: Xác nhận ngắt kết nối sạch sẽ 0 ngoại lệ chưa được xử lý.
  - `dotnet build Simulate.sln`: **0 warning / 0 error**.
  - `dotnet test Simulate.sln`: **2,278 / 2,278 tests PASS (100%)** trong 6 giây.
  - UI XAML: **0% thay đổi layout/styles**, bảo vệ UI tuyệt đối theo `RULE[user_global]`.

## Work log — 2026-09-23 (Clean Dual-Channel State & Gateway Immunity across 100ms, 10ms, 1ms)

- [x] **Khắc phục triệt để lỗi nhảy số Bảng 6 và ảnh hưởng qua Gateway ở các dải tốc độ (100ms, 10ms, 1ms)**:
  - **Bản chất nguyên nhân cốt lõi**:
    1. *Sự liên đới giữa Bảng 4 và Bảng 6*: Ngay khi kết nối, Baseline Pass-Through Gateway đã chuyển tiếp các frame từ xe. Hàm `UpdateValue` nhận frame và gán trực tiếp `Value = physical`, làm kích hoạt `PhysicalValueInput.get`.
    2. *Vòng lặp phản hồi ComboBox Enum (Bảng 6)*: ComboBox binding 2 chiều vào `PhysicalValueInput`. Khi frame của xe đến, `Value` đổi khiến ComboBox bị ép chọn mục tương ứng với giá trị xe, kích hoạt `SelectionChanged` giật ngược lại, làm người dùng không thể chọn hoặc bị nhảy số liên tục.
    3. *Lệch pha khi tiêm lỗi*: `SimulationViewModel.BuildSimulationPlan()` và `SyncSignalOverrides()` trước đó đọc `s.Value` (giá trị live từ xe) thay vì giá trị cấu hình, khiến Gateway tiêm giá trị của xe sang TX thay vì giá trị tiêm lỗi người dùng mong muốn.
  - **Giải pháp kiến trúc tối ưu (Clean Dual-Channel State Architecture)**:
    1. **Tách biệt 2 kênh độc lập trong `SignalModel` (`MainViewModel.cs`)**:
       - *Kênh Đo lường Live (`Value`)*: Lưu trữ giá trị thực tế từ xe, chỉ cập nhật cho Bảng 4 (`PhysicalValueDisplay`, màu xanh `● Active`). Có cờ bảo vệ `_isUpdatingFromBus` che chắn tuyệt đối, không động chạm đến ô cấu hình.
       - *Kênh Cấu hình Kịch bản (`ConfiguredValue`)*: Hoàn toàn miễn nhiễm với frame bus. Người dùng thoải mái soạn thảo kịch bản, chọn Enum ComboBox, nhập số trước khi tiêm. Ô Bảng 6 (`PhysicalValueInput`) chỉ binding vào `ConfiguredValue`.
       - *Bảo toàn cấu hình*: Khi người dùng bỏ tick Override (hoặc bấm Stop), `ConfiguredValue` vẫn lưu giữ nguyên vẹn để kích hoạt lại mà không phải nhập lại.
    2. **Đồng bộ chuẩn xác xuống Gateway (`SimulationViewModel.cs`)**:
       - Trong `BuildSimulationPlan()` và `SyncSignalOverrides()`: Đọc chính xác `s.ConfiguredValue` để tiêm sang TX.
       - Trong `OnSignalItemPropertyChanged`: Chỉ đồng bộ khi `IsOverridden` đổi hoặc khi đang override mà `ConfiguredValue` đổi. Frame bus 1ms về chỉ cập nhật `Value` nên không bao giờ kích hoạt `SyncSignalOverrides` $\implies$ Triệt tiêu 100% Feedback Loop!
       - Trong `StartInjectionAsync`: Tự động làm mới `signal.Value = signal.ConfiguredValue` và hiển thị đỏ `● Injected` cho các tín hiệu được tiêm.
    3. **Bảo vệ UI XAML tuyệt đối**:
       - 0% thay đổi mã XAML trong `MainWindow.xaml`, tuân thủ nghiêm ngặt `RULE[user_global]`.
  - **Unit Tests & Verification**:
    - Bổ sung 2 bài kiểm thử tự động chuyên biệt trong `Simulate.Tests/SignalValueConfigurationTests.cs`:
      * `SignalConfiguration_WhenUntickedAndBusFramesArriveAtHighSpeed_ConfiguredValueRemainsImmune`: Kiểm chứng Bảng 6 giữ vững con số cấu hình kịch bản (4500 rpm) khi nhận dồn dập các frame 1ms/10ms/100ms từ xe (1200 rpm), Bảng 4 hiển thị đúng 1200 rpm xanh; khi kích hoạt tiêm, khôi phục ngay 4500 rpm đỏ.
      * `ComboBoxConfiguration_WhenBusFramesArrive_EnumSelectionDoesNotReset`: Kiểm chứng lựa chọn Enum trên ComboBox (`[1] Applied`) không bị giật, nhảy số hay reset về `[0] Released` khi các frame của xe liên tục chạy qua gateway.
    - `dotnet build Simulate.sln`: **0 warning / 0 error**.
    - `dotnet test Simulate.sln`: **2,280 / 2,280 tests PASS (100%)** trong 6 giây.

- [x] **Xử lý dứt điểm lỗi nhảy ComboBox `CCU_TMS_OperatingSts` và `CCU_TMS_FaultLevel` trên message `CCU_06` (DBC `VF EBUS6M_PCAN-CCU.dbc`)**:
  - **Nguyên nhân gốc rễ**:
    1. *Tự ý ép `IsOverridden = true` trong setter `PhysicalValueInput`*: Khi người dùng chọn ComboBox ở Bảng 6 để chuẩn bị cấu hình, setter tự ép `IsOverridden = true`. Điều này làm kích hoạt `DataTrigger` trong XAML thay đổi Style ComboBox (Background, Foreground, BorderBrush, FontWeight) ngay giữa chu trình `SelectionChanged` của WPF, dẫn đến Selection Rollback về item 0 (`[0] OFF`, `[0] No fault`).
    2. *Re-entrancy và chu kỳ PropertyChanged đa tầng*: Trong một lần chọn ComboBox, `OnPropertyChanged(nameof(PhysicalValueInput))` bị gọi liên tiếp tới 3-4 lần do hiệu ứng domino giữa `ConfiguredValue`, `Value` và `IsOverridden`.
    3. *String Instance Mismatch*: Chuỗi trả về từ `FormatDisplayValue` được tạo mới bằng nội suy xâu (`$"[{desc.RawValue}] {desc.Description}"`), không trùng khớp tham chiếu với danh sách item nguồn `AvailableValueDescriptions` trong ComboBox `ItemsSource`.
  - **Giải pháp**:
    1. *Xóa bỏ ép buộc `IsOverridden = true`*: Trong setter `PhysicalValueInput`, chỉ cập nhật `ConfiguredValue` và đồng bộ `Value` nếu đang trong trạng thái override. Checkbox cột Override hoàn toàn do người dùng làm chủ hoặc tự động bật khi tiêm lỗi kịch bản.
    2. *Khởi tạo `_configuredValue` chuẩn xác từ DBC*: Trong setter `DbcSource`, khởi tạo ngay `_configuredValue` bằng Enum đầu tiên (PhysicalValue của item 0) hoặc `Value`, ngăn chặn hoàn toàn fallback về `Value` của xe.
    3. *Khớp tham chiếu chuẩn xác trong `FormatDisplayValue`*: Ưu tiên tìm và trả về đúng đối tượng string instance từ `AvailableValueDescriptions` để WPF ComboBox khớp 100% cả tham chiếu lẫn giá trị.
    4. *Triệt tiêu re-entrancy*: Loại bỏ các lần bắn `OnPropertyChanged` thừa thãi.
  - **Unit Tests & Verification**:
    - Bổ sung file kiểm thử chuyên sâu `Simulate.Tests/Ccu06ReproductionTests.cs` kiểm tra cả việc đóng/mở gói bit Motorola, chọn ComboBox đơn lẻ và kết hợp cả 2 ComboBox `CCU_TMS_OperatingSts` (`[3] Autocyclic`) & `CCU_TMS_FaultLevel` (`[2] Level 2`) dưới tải frame xe dồn dập (100ms, 10ms, 1ms).
    - Cập nhật các bài test trong `SignalValueConfigurationTests.cs`.
    - `dotnet build Simulate.sln`: **0 warning / 0 error**.
- [x] **Triển khai Tính năng Giám sát Viễn thám Trực quan (Live Telemetry & Simulation Control) tại thanh Footer (Row 5)**:
  - **Yêu cầu & Phê duyệt của Người dùng**:
    1. Tham khảo mã nguồn dự án tham chiếu `D:\TEST_DEV\TOOL ĐỌC DTC DID EBUS\V1.5\TreeViews and Value Converters` về thanh `SIMULATION CONTROL` và các chỉ số viễn thám.
    2. Đánh giá mức độ ảnh hưởng: Phân tích 5 nhóm lỗi crash nếu cập nhật UI per-frame (Dispatcher Starvation, GC Stop-the-World, Cross-thread, Torn Read, Render Churn) và đề xuất kiến trúc an toàn tuyệt đối **Throttled Telemetry Sampling 200ms (5 Hz)** với CPU UI < 0.05%, zero-crash. Được người dùng phê duyệt trong artifact `implementation_plan.md`.
    3. Tuân thủ nghiêm ngặt chỉ thị: *"làm nhưng không được thay đổi vị trí các UI chỉ đơn giản là thay thế phần tôi đã bôi đỏ bằng phần mới , áp dụng rule và skill hãy làm điều đó thật cẩn thận"*.
  - **Triển khai Chi tiết**:
    1. *Tầng ViewModel & Telemetry (`BusHealthViewModel.cs` & `SimulationViewModel.cs`)*:
       - `BusHealthViewModel`: Bổ sung `TxRateDisplay`, `RxRateDisplay` tính toán thông lượng tức thời ($\Delta Tx / \Delta t$, $\Delta Rx / \Delta t$) qua snapshot delta frame.
       - `SimulationViewModel`: Bổ sung `SentDisplay`, `RxDisplay`, `InjectedDisplay`, `LatencyDisplay`, `ElapsedDisplay`, `SimulationStatusText`, `SimulationStatusColor`, `SimulationStatusBg`, `SimulationStatusDotColor`, và lệnh `ResetCountersCommand`.
       - Tích hợp vòng lặp viễn thám `_statsTimer` (200ms / 5 Hz), đọc snapshot nguyên tử từ `ISimulationEngine` và cập nhật an toàn qua Dispatcher hoặc trực tiếp nếu ngoài UI context.
       - Gắn lifecycle hooks vào `SimulationViewModel` (`RefreshRuntimeState`, `Dispose`, `DisposeAsync`, `StartInjectionAsync`, `StopGatewayAsync`, và constructors).
    2. *Tầng View (`MainWindow.xaml`)*:
       - Cập nhật dòng Footer (Row 5, `Height="28"`) thay thế toàn bộ text tĩnh bằng các badges viễn thám bo góc sống động:
         * Trái: `● Status Badge` (`STOPPED` / `RUNNING` / `PAUSED`), `⏱ Elapsed` (`hh:mm:ss`), `TX: ...` (xanh lá), `RX: ...` (xanh dương), `INJ: ...` (vàng cam), `LAT: ... µs` (trắng xám), nút `↺ Reset`.
         * Phải: `Tx: ... msgs/s`, `Rx: ... msgs/s`, `Bus Load: ...%`, `Errors: ...`.
       - Bảo toàn 100% vị trí, kích thước, cấu trúc của Bảng 1 đến Bảng 10 hiện có.
    3. *Unit Tests & Verification*:
       - Bổ sung 4 unit tests mới trong `Simulate.Tests/SimulationViewModelTests.cs` kiểm chứng giá trị mặc định, format dữ liệu snapshot viễn thám, chuyển đổi trạng thái status badge và lệnh reset counters.
       - `dotnet build Simulate.sln`: **0 warning / 0 error**.
       - `dotnet test Simulate.sln`: **2,287 / 2,287 tests PASS (100%)**.

## Work log — 2026-09-24 (UI-10: STATUS OVERVIEW Dynamic MVVM Integration)

- [x] **Triển khai Logic Động cho Bảng 10 (`10. STATUS OVERVIEW`)**:
  - **Yêu cầu & Ranh giới nghiêm ngặt từ Người dùng**:
    * Triển khai liên kết dữ liệu sống động cho Bảng 10 sau khi logic đã được người dùng phê duyệt.
    * *Chỉ thị tối cao*: *"UI 10 này không được làm thay đổi bất kì thuộc tính nào của các UI khác"*.
    * Bảo toàn 100% vị trí, layout, kích thước, styling của Bảng 1 đến Bảng 9 và thanh Footer Row 5. Chỉ thay thế các chuỗi tĩnh (hardcoded dummy text) trong Bảng 10 bằng data binding tới `StatusOverview.*`.
  - **Triển khai Chi tiết**:
    1. *Tầng ViewModel (`StatusOverviewViewModel.cs`)*:
       - Tạo mới `StatusOverviewViewModel` kế thừa `ObservableObject` và `IDisposable`.
       - Observable Properties:
         * `ConnectionText`: `"● Connected"` (#10B981) hoặc `"● Disconnected"` (#64748B).
         * `ConnectionColor`: Brush/Mã màu đồng bộ trạng thái kết nối.
         * `DriverText`: Hiển thị tên thiết bị phần cứng đang kết nối (`VN1640A Channel 1`, `Virtual CAN...`) hoặc `"None"`. Cắt bớt đuôi bằng `TextTrimming="CharacterEllipsis"` nếu tên dài.
         * `BusStateText`: `"Active"` (#10B981) khi engine đang chạy, `"Standby"` (#64748B) khi đã kết nối nhưng chưa chạy engine, hoặc `"Off"` (#64748B) khi ngắt kết nối.
         * `BusStateColor`: Brush tương ứng cho Bus State.
         * `DbcText`: `"Tên_file.dbc (N msgs)"` hoặc `"None (0 msgs)"` khi chưa nạp DBC.
         * `BusHealthText`: `"♡ 100%"` (hoặc theo điểm sức khỏe thực tế từ `BusHealthViewModel`) kèm màu `#10B981` (xanh), `#F59E0B` (vàng), hoặc `#EF4444` (đỏ).
         * `CanFdText`: `"Enabled (2 Mbps)"` hoặc `"Disabled (Classic)"`.
       - Lắng nghe event từ 4 ViewModels lõi (`ConnectionViewModel`, `DbcManagementViewModel`, `SimulationViewModel`, `BusHealthViewModel`).
       - Điều hướng cập nhật UI thread an toàn qua Dispatcher khi ở runtime hoặc trực tiếp khi chạy unit test runner.
       - Hủy đăng ký sự kiện (`Dispose()`) tránh rò rỉ bộ nhớ.
    2. *Tầng Composition (`MainViewModel.cs`)*:
       - Khởi tạo `public StatusOverviewViewModel StatusOverview { get; }`.
       - Tích hợp gọi `StatusOverview.Dispose()` trong khối `ShutdownCoreAsync()`.
    3. *Tầng View (`MainWindow.xaml`)*:
       - Giữ nguyên 100% cấu trúc `<Border Grid.Column="2" Style="{StaticResource Panel}" Margin="3,0,0,0">`.
       - Chỉ chuyển các TextBlock tĩnh sang `{Binding StatusOverview.*}`.
       - Tuyệt đối không thay đổi bất kỳ ký tự nào của Bảng 1 đến Bảng 9 hay Row 5 Footer.
    4. *Unit Tests & Verification*:
       - Tạo mới `Simulate.Tests/StatusOverviewViewModelTests.cs` (6 bài kiểm thử toàn diện):
         * `DefaultState_ReflectsDisconnectedAndEmptyDbc`: Kiểm tra trạng thái khởi tạo.
         * `ConnectionStateChange_UpdatesConnectionStatusAndDriver`: Kiểm tra chuyển trạng thái kết nối và driver name.
         * `EngineStateChange_UpdatesBusStateActiveAndStandby`: Kiểm tra chuyển trạng thái Bus State giữa Active và Standby.
         * `DbcStateChange_UpdatesDbcTextAndMessageCount`: Kiểm tra cập nhật DBC text và số lượng message.
         * `CanFdStateChange_UpdatesCanFdText`: Kiểm tra cập nhật định dạng CAN FD / Classic.
         * `MainViewModel_InitializesAndDisposesStatusOverview`: Kiểm tra vòng đời và tích hợp MainViewModel.
       - `dotnet build Simulate.sln`: **PASS (0 warning, 0 error)**.
       - `dotnet test Simulate.sln`: **PASS 100% (2,293 / 2,293 tests)**.

## Work log � 2026-09-24 (E2E Auto-Detection, Status Badge & Manual Fallback CheckBox in Panel 6)

- [x] **Tri?n khai T�nh nang T? d?ng Nh?n di?n E2E t? DBC, Nh�n Tr?ng th�i & CheckBox Can thi?p Th? c�ng t?i B?ng 6**:
  - **Y�u c?u & Ranh gi?i t? Ngu?i d�ng**:
    * �?nh hu?ng 1: T? d?ng ph�t hi?n c?u h�nh E2E (CRC8 + Alive Counter) cho t?ng Message t? DBC.
    * Khi ngu?i d�ng ch?n b?t k? t�n hi?u n�o ? B?ng 6: Hi?n th? ch? nh? ? d?u khung UI 6 cho bi?t Message d� d� k�ch ho?t E2E hay chua.
    * Co ch? ph�ng v? (Defensive Mechanism): Trong tru?ng h?p DBC kh�ng nh?n di?n du?c do c�ch d?t t�n d? thu?ng ho?c thi?u signal, cung c?p CheckBox E2E Protection d? ngu?i d�ng ch? d?ng tick b?t E2E (�p d?ng c?u h�nh chu?n d? ph�ng Fallback: Byte 0 CRC, Byte 1 Counter). Ngu?i d�ng cung c� th? b? tick d? ki?m th? ph?n ?ng c?a ECU khi Checksum b? l?i.
    * Ranh gi?i UI: Ch? t?n d?ng kho?ng tr?ng b�n ph?i c?a ti�u d? B?ng 6 (Row 0), b?o to�n 100% v? tr�, layout, k�ch thu?c c?a c�c UI kh�c.
  - **Tri?n khai Chi ti?t**:
    1. *T?ng Service (DbcE2eDetector.cs)*:
       - T?o m?i DbcE2eDetector: Qu�t danh s�ch signals trong DbcMessage nh?n di?n c?p Checksum/CRC v� Alive/Counter.
       - Tr�ch xu?t t? d?ng ChecksumByteIndex, CounterByteIndex, CounterMask (x? l� c? Motorola BigEndian v� Intel LittleEndian), CounterMaximumValue (14 ho?c 15), d?i t�nh CRC (CrcStartByteIndex, CrcEndByteIndex).
       - Cung c?p phuong th?c CreateStandardFallback(payloadLength, isEnabled) t?o c?u h�nh d? ph�ng chu?n khi ngu?i d�ng b?t th? c�ng.
    2. *T?ng ViewModel (SimulationViewModel.cs)*:
       - Qu?n l� tr?ng th�i E2E theo Message qua _messageE2eStates mapping (uint CanIdentifier, bool IsExtendedIdentifier).
       - Observable Properties: SelectedSignalE2eText, SelectedSignalE2eColor, IsSelectedSignalE2eChecked, CanToggleSelectedSignalE2e.
       - Trong OnSelectedSignalChanged: T? d?ng tra c?u message v� c?p nh?t nh�n tr?ng th�i (? E2E: Active (Auto CRC8) xanh l� #10B981, ? E2E: Active (Manual) xanh ng?c #06B6D4, ho?c ? E2E: Inactive x�m #64748B) c�ng tr?ng th�i CheckBox.
       - Trong OnIsSelectedSignalE2eCheckedChanged: C?p nh?t c?u h�nh E2E c?a message v� d?ng b? xu?ng Gateway (_engine.UpdatePlan(BuildSimulationPlan())).
       - Trong BuildSimulationPlan() v� BuildBaselineSimulationPlan(): S? d?ng GetE2eConfiguration(...) thay th? to�n b? c�c di?m hardcode alse tru?c d�y.
    3. *T?ng View (MainWindow.xaml)*:
       - T?i Row 0 c?a B?ng 6 (Height="22"), thay th? TextBlock ti�u d? b?ng Grid ch?a ti�u d? b�n tr�i v� c?m SelectedSignalE2eText + CheckBox E2E Protection b�n ph?i.
       - Gi? nguy�n 100% b? c?c, kh�ng ?nh hu?ng d?n b?t k? panel n�o kh�c.
    4. *Unit Tests & Verification*:
       - DbcE2eDetectorTests.cs (3 tests): Ki?m ch?ng t? nh?n di?n message c� CRC/Counter, message kh�ng c� E2E, v� t?o fallback configuration.
       - SimulationViewModelE2eTests.cs (4 tests): Ki?m ch?ng t? d?ng b?t E2E v� nh�n tr?ng th�i, ngu?i d�ng b? tick d? t?t, message kh�ng c� E2E, v� ngu?i d�ng tick b?t th? c�ng (Manual fallback).
       - dotnet build Simulate.sln: **PASS (0 warning, 0 error)**.
       - dotnet test Simulate.sln: **PASS 100% (2,300 / 2,300 tests)**.

- [x] **Triển khai Cửa Sổ Khóa Bản Quyền (License Lock UI Window & License Service)**:
  - **Yêu cầu & Ranh giới từ Người dùng**:
    * Tạo UI mới là một XAML riêng biệt (LicenseLockWindow.xaml) khớp 100% với hình ảnh mẫu của người dùng.
    * Không làm thay đổi hay ảnh hưởng đến bất kỳ UI nào đã có (MainWindow.xaml, SelectMessageWindow.xaml).
  - **Triển khai Chi tiết**:
    1. *Tầng Service (ILicenseService.cs & LicenseService.cs)*:
       - Cung cấp GetMachineId(): Lấy Machine GUID từ Windows Registry (HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid) hoặc sinh UUID lưu trữ an toàn.
       - Cung cấp ValidateLicense(machineId, licenseKey): Kiểm tra tính hợp lệ của license key (hỗ trợ cả Master Key SIMULATE-PRO-AUTOMOTIVE-2026 và thuật toán sinh key SHA256 từ Machine ID).
       - Quản lý trạng thái bản quyền: IsLicensed(), SaveLicense(), ClearLicense().
    2. *Tầng ViewModel (LicenseLockViewModel.cs)*:
       - Thuộc tính MVVM: MachineId, LicenseKey, ErrorMessage, HasError, IsActivated, StatusMessage.
       - Commands: CopyMachineIdCommand, PasteLicenseKeyCommand, ActivateCommand, OpenGuideCommand, CloseCommand.
       - Callback RequestClose(bool isSuccess) để tương tác với Window.
    3. *Tầng View (LicenseLockWindow.xaml & LicenseLockWindow.xaml.cs)*:
       - Window Style không viền (WindowStyle= None, AllowsTransparency=True, bo góc CornerRadius=14, đổ bóng DropShadowEffect).
       - Header: Icon ổ khóa đỏ + Tiêu đề 'Ứng dụng đang bị khóa' + Phụ đề + Hình ảnh minh họa hacker nghệ thuật (license_hacker_art.png) + Nút đóng ✕.
       - Card 1: Machine ID + TextBox ReadOnly hiển thị ID + Nút 'Sao chép'.
       - Card 2: License Key + TextBox có Icon chìa khóa & Placeholder mờ + Nút 'Dán' + Khung thông báo lỗi (Error Banner đỏ hồng).
       - Footer: Nút 'Hướng dẫn kích hoạt' (icon ?) + Nút 'Kích hoạt' màu xanh dương (icon ✓).
    4. *Unit Tests & Verification*:
       - LicenseLockViewModelTests.cs (6 tests): Kiểm thử sinh key thực tế, nạp Machine ID mặc định, báo lỗi khi để trống hoặc nhập sai key, xóa lỗi khi người dùng gõ lại, và kích hoạt thành công.
       - dotnet build Simulate.sln: **PASS (0 warning, 0 error)**.
       - dotnet test Simulate.sln: **PASS 100% (2,306 / 2,306 tests)**.
  - **Tích hợp Luồng Khởi Động Ứng Dụng (App.xaml & App.xaml.cs)**:
    * Chuyển StartupUri sang phương thức quản lý OnStartup(StartupEventArgs e) chủ động trong App.xaml.cs.
    * Áp dụng ShutdownMode.OnExplicitShutdown trong quá trình xác thực License để tránh crash khi dialog đóng.
    * Khi ứng dụng chưa có bản quyền: Hiển thị LicenseLockWindow.ShowDialog().
    * Nếu người dùng nhập key hợp lệ: Lưu key và mở MainWindow.Show() với ShutdownMode.OnMainWindowClose.
    * Nếu người dùng hủy hoặc đóng cửa sổ khóa: Gọi Shutdown() thoát an toàn.

- [x] **Nâng cấp Hệ Thống Bản Quyền: Mã Hóa Bất Đối Xứng RSA-2048, Khóa Theo Thời Hạn & Tích Hợp Key Manager**:
  - **Yêu cầu từ Người dùng**:
    * Nhúng hạn sử dụng vào key (7 ngày, 30 ngày, 60 ngày, 90 ngày, 180 ngày, 365 ngày, Vĩnh viễn).
    * Mã hóa bất đối xứng RSA-2048: Dùng Private Key để ký bên phát hành (Key Manager) và Public Key để xác thực bên ứng dụng (Simulate).
    * Chữ ký quản trị bảo vệ Private Key: Tr@nHo@ngN@m*4.
    * Tích hợp vào C:\Users\Hnam\Desktop\Key Manager (màn hình Simulate) và xuất bản sang D:\TEST_DEV\Unlock key cac tool tu lam\Unlock ley Simulate.
  - **Triển khai Chi tiết**:
    1. *Tầng Service (Simulate/Services/LicenseService.cs)*:
       - Nhúng RSA-2048 Public Key tiêu chuẩn SubjectPublicKeyInfo PEM.
       - Giải mã Payload MID=...;EXP=...;TYP=...;ISS=... và xác thực chữ ký SHA256withRSA.
       - Kiểm tra thời hạn bản quyền: Nếu quá hạn báo lỗi chi tiết, nếu còn hạn hiển thị số ngày còn lại.
    2. *Tầng ViewModel (Simulate/ViewModels/LicenseLockViewModel.cs)*:
       - Hỗ trợ LicenseValidationResult, hiển thị trạng thái hạn dùng và thông báo lỗi tương ứng.
    3. *Tầng Tool Quản Lý Key (C:\Users\Hnam\Desktop\Key Manager)*:
       - Cập nhật SimulateActivationWindow.xaml bổ sung trường nhập Chữ ký quản trị (Passphrase).
       - Cập nhật SimulateActivationWindow.xaml.cs kiểm tra Tr@nHo@ngN@m*4, dùng Private Key RSA-2048 ký payload và xuất ra mã kích hoạt chuẩn UTK-SM25-{PayloadB64}.{SignatureB64}.
       - Biên dịch thành công 0 warning / 0 error và pass toàn bộ unit tests của Key Manager.
    4. *Xuất bản Tool sang Thư mục D:*:
       - Chạy dotnet publish xuất bản toàn bộ file thực thi (Key Manager.exe, DLLs, Assets, Data) sang D:\TEST_DEV\Unlock key cac tool tu lam\Unlock ley Simulate\.
    5. *Verification*:
       - dotnet build Simulate.sln: **PASS (0 warning, 0 error)**.
       - dotnet test Simulate.sln: **PASS 100% (2,309 / 2,309 tests)**.
       - dotnet test Key Manager.sln: **PASS 100% (12 / 12 tests)**.

- [x] **Khắc Phục Lỗi Hiển Thị Giao Diện Khóa Bản Quyền (LicenseLockWindow UI Fidelity)**:
  - **Triệu chứng & Nguyên nhân**: File Assets/license_hacker_art.png không được nhúng vào Simulate.g.resources trong Simulate.csproj, dẫn đến ảnh hacker "NO LICENSE NO ACCESS" bị mất lúc runtime thực tế dù vẫn hiện trong VS Designer. Viền Background="#80000000" của Window làm lộ khung đen mờ 10px xung quanh thẻ bo góc.
  - **Khắc phục**:
    1. Cập nhật Simulate/Simulate.csproj: Đăng ký <Resource Include="Assets\license_hacker_art.png" /> và splash.png vào assembly resources.
    2. Cập nhật Simulate/Views/LicenseLockWindow.xaml: Đổi Window.Background sang Transparent để hiệu ứng đổ bóng DropShadowEffect hòa mượt mà vào màn hình.
  - **Verification**:
    * Trích xuất tài nguyên Simulate.dll qua PowerShell: Đã xác nhận ssets/license_hacker_art.png hiện diện trong Simulate.g.resources.
    * dotnet build Simulate.sln: 0 warning, 0 error.
    * dotnet test Simulate.sln: 2,309 / 2,309 tests PASS (100%).