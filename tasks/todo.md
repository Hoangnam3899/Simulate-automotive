# Todo: Vector Hardware Gateway & CAN Simulation Backend

> Trạng thái ban đầu: PLAN ONLY — chưa triển khai code.
>
> UI LOCK: Không sửa `App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs` hoặc file UI/XAML nào nếu chưa có yêu cầu và cho phép rõ ràng từ người dùng.

## Task 0: Chốt approval gates và baseline

**Description:** Ghi nhận build/test baseline, dirty worktree và xin duyệt mọi thay đổi package/project structure trước implementation.

**Acceptance criteria:**
- [x] Ghi lại kết quả `dotnet build Simulate.sln` hiện tại: PASS ngày 2026-08-10, 0 warning, 0 error.
- [ ] Xác nhận có/không tạo `Simulate.Tests` và package test tương ứng.
- [ ] Xác nhận có/không ép target x64 trong project configuration.

**Verification:**
- [ ] `git status --short` được ghi nhận, không ghi đè thay đổi của người dùng.
- [ ] UI diff bằng không.

**Dependencies:** None
**Files likely touched:** chỉ tài liệu PLAN cho đến khi có approval
**Estimated scope:** XS
**Model allocation:** Lead `Luna high`; review `Terra high`
**Skills khi triển khai:** `context-engineering`, `git-workflow-and-versioning`

## Task 1: Định nghĩa domain contracts và hardware session seam

**Description:** Thiết kế typed models cho frame, endpoint, bus mode, bitrate, connection options/result và seam tạo gateway session mà không lộ type của Vector XL.

**Acceptance criteria:**
- [ ] Contract biểu diễn được CAN Classic/CAN FD, standard/extended ID và hai phía RX/TX.
- [ ] `ICanHardwareDriver` có surface nhỏ cho discovery và mở session.
- [ ] Session định nghĩa receive/transmit/flush/stop và cleanup idempotent.

**Verification:**
- [ ] Contract review bằng deletion/depth test.
- [ ] `dotnet build Simulate.sln` thành công.
- [ ] UI diff bằng không.

**Dependencies:** Task 0
**Files likely touched:** `Models/*.cs`, `Services/ICanHardwareDriver.cs`
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Terra xhigh`
**Skills khi triển khai:** `api-and-interface-design`, `codebase-design`

## Task 2: Nâng Mock adapter thành in-memory gateway session

**Description:** Cung cấp adapter test có hai chiều frame, fault injection cho lifecycle và state quan sát được qua cùng hardware seam.

**Acceptance criteria:**
- [ ] Có thể enqueue frame từ RX/TX và quan sát frame phát tới phía đối diện.
- [ ] Có thể mô phỏng lỗi discovery/open/configure/activate/transmit.
- [ ] Stop/dispose gọi nhiều lần không lỗi và không phát frame sau stop.

**Verification:**
- [ ] Contract tests chạy trên Mock adapter.
- [ ] Build/test thành công.
- [ ] UI diff bằng không.

**Dependencies:** Task 1
**Files likely touched:** `Services/MockHardwareService.cs`, model/session test files
**Estimated scope:** M
**Model allocation:** Lead `Terra high`; review `Luna high`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Task 3: Hardening Vector discovery và native lifecycle

**Description:** Sửa ownership của driver/port, kiểm tra mọi `XL_Status`, typed error và cleanup đầy đủ khi lỗi giữa chuỗi open/configure/activate.

**Acceptance criteria:**
- [ ] Không còn nhánh activation/configuration failure làm rò port/driver.
- [ ] Discovery dùng `try/finally` và lọc channel CAN hợp lệ.
- [ ] Error result giữ operation, `XL_Status` và message chẩn đoán.

**Verification:**
- [ ] Fault-path tests bằng seam/wrapper phù hợp.
- [ ] Đối chiếu API với tài liệu local `Doc/`.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 1-2
**Files likely touched:** `Services/VectorHardwareService.cs`, supporting models/tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh`
**Skills khi triển khai:** `source-driven-development`, `test-driven-development`

## Task 4: Classic CAN frame I/O hai chiều

**Description:** Mở RX/TX physical channels, nhận và phát Classic CAN frame, bảo toàn standard/extended ID và đóng session đúng thứ tự.

**Acceptance criteria:**
- [ ] Dùng interface V3 và Classic CAN receive/transmit APIs đúng wrapper hiện có.
- [ ] RX/TX channel không được trùng; permission/configuration failure trả typed error.
- [ ] Receive/transmit/flush/cancel hoạt động qua session seam.

**Verification:**
- [ ] Contract/integration tests bằng Mock.
- [ ] Manual Vector checklist được tạo, mục chưa cắm hardware ghi `NEEDS_VERIFY`.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 3
**Files likely touched:** Vector adapter/session files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; test review `Luna xhigh`; code review `Terra xhigh`
**Skills khi triển khai:** `source-driven-development`, `test-driven-development`

## Task 5: CAN FD frame I/O hai chiều

**Description:** Thêm mode CAN FD dùng interface V4, nominal/data bitrate, DLC-length mapping và transmit/receive event phù hợp.

**Acceptance criteria:**
- [ ] Cấu hình arbitration/data bitrate được validate và kiểm tra status.
- [ ] Payload length/DLC hợp lệ cho CAN FD; classic path không bị thay đổi hành vi.
- [ ] Session cleanup/cancellation đạt cùng contract với Classic CAN.

**Verification:**
- [ ] Golden tests cho DLC-length mapping.
- [ ] Contract tests chạy cho cả Classic và FD modes.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 4
**Files likely touched:** Vector adapter/session, CAN FD mapping và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh`
**Skills khi triển khai:** `source-driven-development`, `test-driven-development`

## Task 6: Async connection orchestration

**Description:** Chuyển discovery/connect/disconnect sang async command, busy state và cancellation trong ViewModel, giữ nguyên binding paths hiện tại.

**Acceptance criteria:**
- [ ] Constructor không gọi hardware blocking trên Dispatcher.
- [ ] Connect/refresh/disconnect không chạy đồng thời và có state/error rõ ràng.
- [ ] `Connection.*` bindings và public command names hiện tại được giữ nguyên.

**Verification:**
- [ ] ViewModel tests với Mock adapter.
- [ ] Build/test sạch.
- [ ] Không sửa XAML hoặc code-behind UI.

**Dependencies:** Tasks 2, 4, 5
**Files likely touched:** `ViewModels/ConnectionViewModel.cs`, `ViewModels/MainViewModel.cs`, tests
**Estimated scope:** M
**Model allocation:** Lead `Terra xhigh`; review `Luna high`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Task 7: DBC domain và parser tối thiểu

**Description:** Parse các phần DBC cần cho simulation: node/message/signal, CAN ID, DLC, start bit, length, endian, signedness, factor, offset, min/max và unit.

**Acceptance criteria:**
- [ ] Parser trả typed result kèm lỗi line/context; không phụ thuộc WPF.
- [ ] Standard và extended CAN ID được chuẩn hóa nhất quán với `CanFrame`.
- [ ] Unsupported construct được báo rõ, không silently đoán.

**Verification:**
- [ ] Fixture tests gồm file hợp lệ, malformed và boundary values.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 1
**Files likely touched:** DBC model/parser files và tests
**Estimated scope:** M
**Model allocation:** Lead `Terra xhigh`; review `Luna xhigh`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Task 8: Signal codec và E2E protection

**Description:** Tạo pure module pack/unpack signal và E2E counter/CRC dùng payload hiện hữu làm baseline.

**Acceptance criteria:**
- [ ] Hỗ trợ endian/signedness/scale đã chốt ở Task 7.
- [ ] Không sửa bit ngoài signal target.
- [ ] E2E chỉ áp dụng khi enabled và có bounds validation.

**Verification:**
- [ ] Golden vectors độc lập cho pack/unpack và CRC/counter.
- [ ] Boundary tests cho min/max, overflow và payload ngắn.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 7
**Files likely touched:** signal codec/E2E files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Luna xhigh`
**Skills khi triển khai:** `test-driven-development`, `source-driven-development`

## Task 9: Simulation configuration và validation

**Description:** Thay model placeholder bằng typed simulation plan gồm message rule, gateway mode, overrides, send type, timing và E2E config.

**Acceptance criteria:**
- [ ] Mode/send type dùng enum hoặc discriminated model, không dùng string làm logic.
- [ ] Validate duplicate CAN ID, invalid timing, missing DBC reference và out-of-range override.
- [ ] Model không phụ thuộc control/WPF type.

**Verification:**
- [ ] Validation tests cho happy path và invalid combinations.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 7-8
**Files likely touched:** simulation model/validation files và tests
**Estimated scope:** M
**Model allocation:** Lead `Terra high`; review `Luna high`
**Skills khi triển khai:** `domain-modeling`, `test-driven-development`

## Task 10: Gateway hai chiều với PassThrough và Block

**Description:** Chạy receive loop cancellable cho RX→TX và TX→RX; áp rule pass-through/block và thống kê observable.

**Acceptance criteria:**
- [ ] Frame không có enabled rule được pass-through theo policy đã chốt.
- [ ] Block không phát frame và tăng dropped counter đúng một lần.
- [ ] Stop hoàn tất hữu hạn thời gian và không để background task sống sót.

**Verification:**
- [ ] Bidirectional integration tests bằng in-memory session.
- [ ] Cancellation/race tests và idle-loop test không busy-spin quá mức.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 2, 4, 5, 9
**Files likely touched:** simulation engine files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh`
**Skills khi triển khai:** `test-driven-development`, `incremental-implementation`

## Task 11: Inject/override, live baseline và echo filtering

**Description:** Với Inject mode, clone payload live, pack signal override, áp E2E rồi phát; ngăn loopback/echo hai chiều.

**Acceptance criteria:**
- [ ] Signal không override giữ nguyên dữ liệu live baseline.
- [ ] Extended ID và payload length được bảo toàn.
- [ ] Echo filtering có timeout/bounds và không drop frame thật sau cửa sổ echo.

**Verification:**
- [ ] Golden injection tests và echo-loop integration tests.
- [ ] Concurrency tests khi update override lúc engine đang chạy.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Tasks 8-10
**Files likely touched:** simulation engine/override store và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol ultra`; review `Terra xhigh` + `Luna xhigh`
**Skills khi triển khai:** `test-driven-development`, `diagnosing-bugs`

## Task 12: Scheduler, pause/resume và emergency stop

**Description:** Thêm One-shot/Cyclic/Event với cancellation, debounce, start delay, repeat và emergency stop an toàn.

**Acceptance criteria:**
- [ ] Không dùng blocking sleep trên UI thread.
- [ ] Pause chỉ dừng schedule theo contract; gateway policy được xác định rõ.
- [ ] Emergency stop hủy task, dừng phát và cleanup session theo thứ tự an toàn.

**Verification:**
- [ ] Deterministic timing tests với clock abstraction hoặc tolerance hữu hạn.
- [ ] Stop/pause/resume race tests.
- [ ] Build/test sạch, UI diff bằng không.

**Dependencies:** Task 11
**Files likely touched:** scheduler/engine files và tests
**Estimated scope:** M
**Model allocation:** Lead `Sol xhigh`; review `Terra xhigh`
**Skills khi triển khai:** `test-driven-development`, `diagnosing-bugs`

## Task 13: SimulationViewModel và projection sang bindings hiện hữu

**Description:** Tạo ViewModel orchestration cho engine, project state vào `Messages`, `Signals`, `FaultQueue` mà không sửa UI hoặc tự kích hoạt thao tác chưa có binding.

**Acceptance criteria:**
- [ ] ViewModel không gọi Vector API trực tiếp.
- [ ] Existing public binding paths tiếp tục compile và cung cấp dữ liệu typed thay placeholder.
- [ ] Commands chưa thể nối vào UI được ghi `UI_GATED`, không dùng code-behind workaround.

**Verification:**
- [ ] ViewModel tests với fake engine/session.
- [ ] Build/test sạch.
- [ ] UI/code-behind diff bằng không.

**Dependencies:** Tasks 6, 9-12
**Files likely touched:** `ViewModels/MainViewModel.cs`, new simulation ViewModel/model adapters, tests
**Estimated scope:** M
**Model allocation:** Lead `Terra xhigh`; review `Sol xhigh`
**Skills khi triển khai:** `incremental-implementation`, `test-driven-development`

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
