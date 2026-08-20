# Implementation Plan: Vector Hardware Gateway & CAN Simulation Backend

## 1. Mục tiêu

Nâng dự án `Simulate` từ trạng thái kết nối Vector XL cơ bản và dữ liệu mô phỏng tĩnh thành một backend có thể:

- khám phá và quản lý vòng đời phần cứng Vector an toàn;
- vận hành CAN Classic và CAN FD qua cùng một seam;
- mở hai phía RX/TX cho gateway hai chiều;
- nhận, chuyển tiếp, chặn và inject CAN frame;
- parse DBC, mã hóa/giải mã signal, override giá trị và hỗ trợ E2E;
- cung cấp state/command qua ViewModel mà không khóa UI thread;
- kiểm thử được bằng adapter in-memory khi không có hardware.

Nguồn tham chiếu hành vi là dự án `D:\TEST_DEV\TOOL ĐỌC DTC DID EBUS\V1.5\TreeViews and Value Converters`, nhưng implementation mới phải phù hợp .NET 8, MVVM và rule của dự án hiện tại; không sao chép cấu trúc manager/code-behind nguyên khối của dự án cũ.

## 2. Rào chắn bắt buộc

1. **Không sửa UI khi chưa có yêu cầu và cho phép rõ ràng từ người dùng.** Trong phạm vi PLAN hiện tại, các file sau bị khóa:
   - `Simulate/App.xaml`
   - `Simulate/MainWindow.xaml`
   - `Simulate/MainWindow.xaml.cs`
   - mọi file XAML hoặc code-behind UI được thêm sau này
2. Giữ nguyên mọi binding path hiện hữu: `Connection.*`, `Messages`, `Signals`, `FaultQueue`.
3. Không thêm/xóa NuGet package nếu chưa được duyệt.
4. Không thêm test project hoặc sửa `Simulate.sln`/`Simulate.csproj` nếu chưa được duyệt.
5. Mọi thay đổi code phải kết thúc bằng `dotnet build` với 0 warning, 0 error; khi test project được duyệt thì chạy thêm `dotnet test`.
6. Không commit/push nếu người dùng chưa yêu cầu.
7. Không lấy license key, private key, credential hoặc file nhạy cảm từ dự án tham chiếu.

## 3. So sánh hiện trạng, tham chiếu và thiết kế đích

| Mảng | Dự án hiện tại | Dự án tham chiếu | Thiết kế đích |
|---|---|---|---|
| Hardware seam | `ICanHardwareDriver` chỉ discover/connect/disconnect | `ICANBusManager` có channel I/O, UDS và MITM | Seam nhỏ cho discovery/session; complexity nằm trong adapter/session |
| CAN/CAN FD | Một `VectorHardwareService` với nhánh `isCanFd` | Hai manager Classic/FD lớn và trùng lặp | Một external seam; strategy/session Classic và FD là implementation nội bộ |
| Vòng đời native | Chưa bảo đảm cleanup trên mọi nhánh lỗi | Có cleanup port riêng cho main/RX/TX | Session sở hữu handle; idempotent cleanup bằng `IAsyncDisposable`/`finally` |
| Gateway | Chưa có frame I/O | `MitmEngine` chạy hai chiều RX→TX và TX→RX | `SimulationEngine` độc lập UI, cancellable, test được qua in-memory session |
| Error model | `bool` | `(bool, string)` | Typed result/error, giữ status code Vector để chẩn đoán |
| Concurrency | Gọi hardware đồng bộ từ ViewModel constructor/command | Background gateway nhưng còn chờ frame bằng `Thread.Sleep` trên UI | Async command, cancellation, bounded receive loop; không block Dispatcher |
| Simulation data | Placeholder trong `MainViewModel` | DBC + signal override + E2E + scheduler | Domain model riêng; ViewModel chỉ chiếu state ra binding hiện hữu |
| UI architecture | MVVM một phần | Nhiều logic trong code-behind | Logic hardware/simulation không đi vào XAML/code-behind |
| Tests | Chưa có test project | Không phải mẫu kiểm thử chính | Test tại seam bằng adapter in-memory; hardware thật là manual gate |

## 4. Module và seam mục tiêu

```text
MainViewModel
├── ConnectionViewModel
│   └── ICanHardwareDriver
│       ├── VectorHardwareService        (adapter thật)
│       └── MockHardwareService          (adapter in-memory)
│
└── SimulationViewModel                  (backend-ready; UI binding là approval gate)
    └── ISimulationEngine
        └── SimulationEngine
            ├── ICanGatewaySession       (session do hardware driver tạo)
            ├── DbcParser                (pure module)
            ├── SignalCodec              (pure module)
            └── E2E protection           (pure module)
```

### Quyết định kiến trúc

- `ICanHardwareDriver` là seam thật vì có ít nhất hai adapter: Vector và Mock.
- Handle/port/mask/permission của Vector nằm hoàn toàn trong session; ViewModel và simulation engine không được biết `XLDriver`.
- Session nhận cấu hình typed gồm RX channel, TX channel, CAN mode, nominal bitrate và data bitrate.
- Frame dùng model typed: CAN ID chuẩn hóa, cờ extended, payload, DLC/length, source side và timestamp.
- Simulation engine xử lý ba gateway mode cốt lõi: `PassThrough`, `Block`, `Inject`.
- DBC parser, signal codec và E2E là pure module; test trực tiếp, không tạo interface hình thức khi chưa có adapter thứ hai.
- Không đưa UDS/DID/DTC vào phase này. `ICanHardwareDriver` không được phình ra theo toàn bộ `ICANBusManager` của dự án tham chiếu.
- Không dùng string để biểu diễn mode/state nội bộ; chỉ format string tại ViewModel.

## 5. Dependency graph

```text
Approval gates + baseline
        │
        ▼
Domain contracts + hardware/session seams
        ├──────────────┐
        ▼              ▼
In-memory adapter   Vector lifecycle/discovery
        │              │
        │              ├── Classic CAN frame I/O
        │              └── CAN FD frame I/O
        └──────┬───────┘
               ▼
      Async connection orchestration

DBC parser ──► Signal codec/E2E ──► Simulation configuration
                                         │
Hardware session ────────────────────────┤
                                         ▼
                             Gateway pass/block/inject
                                         │
                                         ▼
                              Scheduler + emergency stop
                                         │
                                         ▼
                     ViewModel projection + integration tests
```

## 6. Chiến lược phân bổ model AI

Giả định các tên Luna/Terra/Sol là ba profile model nội bộ; nếu capability thực tế khác, giữ nguyên mức reasoning nhưng đổi profile trước khi triển khai.

| Profile | Dùng cho | Không giao chính |
|---|---|---|
| **Luna mid/high/xhigh** | inventory, fixture, test case, tài liệu, kiểm tra spec và các thay đổi cơ học | quyết định lifecycle native hoặc concurrency khó |
| **Terra high/xhigh** | C#/.NET 8, MVVM, domain model, parser/codec, integration chuẩn | quyết định cuối cùng về race/handle leak có rủi ro hardware |
| **Sol xhigh/ultra** | seam architecture, Vector XL interop, resource lifetime, gateway concurrency, final risk review | việc lặp lại/boilerplate đơn giản |

Nguyên tắc:

- `ultra` chỉ dùng cho phần có rủi ro native handle, CAN FD, race condition hoặc safety.
- Lead và reviewer không dùng cùng profile khi có thể.
- Mỗi task chỉ có một lead; reviewer chỉ kiểm tra acceptance criteria và diff.
- Model không được tự mở rộng phạm vi sang UI.

## 7. Kế hoạch theo phase

### Phase 0 — Governance và baseline

- [x] Task 0: Chốt approval gates và baseline hiện tại — **Luna high**, review **Terra high**. Baseline pass và approval decisions đã được người dùng xác nhận.
- [x] Task 1: Thiết kế domain contracts và session seam — **Sol xhigh**, review **Terra xhigh**. Terra review PASS; contract đã được freeze.

### Checkpoint A — Contract freeze

- [x] Người dùng duyệt mọi thay đổi project/package cần thiết.
- [x] Contract đủ cho Vector và Mock nhưng không lộ `vxlapi_NET` ra caller.
- [x] Không có diff ở XAML/code-behind UI.
- [x] `dotnet build Simulate.sln` thành công.

### Phase 1 — Hardware foundation

- [x] Task 2: Nâng Mock adapter thành in-memory gateway session — **Terra high**, review **Luna high**. Luna review PASS; task đã hoàn tất.
- [x] Task 3: Hardening discovery và native lifecycle — **Sol ultra**, review **Terra xhigh** PASS. Build 0 warning/0 error, test 21/21 PASS, UI diff bằng không; hardware thật còn `NEEDS_VERIFY`.
- [x] Task 4: Classic CAN receive/transmit hai chiều — **Sol ultra** implementation COMPLETE; test review **Luna xhigh** PASS và code review **Terra xhigh** PASS. Build 0 warning/0 error, 35/35 tests PASS, UI diff bằng không; hardware thật còn `NEEDS_VERIFY` theo [`tasks/vector-classic-can-hardware-checklist.md`](vector-classic-can-hardware-checklist.md).
- [x] Task 5: CAN FD receive/transmit hai chiều — **Sol ultra** implementation và review **Terra xhigh** PASS; 65/65 tests PASS, hardware thật còn `NEEDS_VERIFY`.
- [x] Task 6: Chuyển connection orchestration sang async/cancellation — **Terra xhigh** implementation, Required-finding fix và **Luna high** re-review PASS; test 71/71, build 0 warning/0 error, UI không đổi.

### Checkpoint B — Hardware session

- [x] Mock session chứng minh connect, receive, transmit, flush và disconnect qua contract tests; review độc lập `Luna high` PASS.
- [x] Mọi nhánh lỗi Vector lifecycle đã mô phỏng đều giải phóng port/driver và giữ cleanup status; hardware thật còn `NEEDS_VERIFY`.
- [x] Classic dùng interface V3; CAN FD dùng V4 và API receive/transmit tương ứng. Task 5 đã review PASS.
- [x] Connect/refresh/disconnect không block UI thread.
- [x] Build/test sạch và không đổi UI tại checkpoint Task 5 implementation: 0 warning/0 error, 65/65 tests PASS.

### Phase 2 — Simulation domain

- [x] Task 7: DBC domain và parser tối thiểu — implementation/fix **Terra xhigh** PASS; re-review **Luna xhigh** PASS; DBC tests 9/9, full suite 80/80, build 0 warning/0 error, UI diff bằng không.
- [x] Task 8: Signal codec và E2E protection — implementation **Sol xhigh** PASS; independent review **Luna xhigh** PASS, không có finding Critical/Required; focused tests 18/18, full suite 98/98, build 0 warning/0 error, UI diff bằng không.
- [x] Task 9: Simulation configuration và validation — implementation **Terra high** PASS; independent review **Luna high** PASS, không có finding Critical/Required; focused tests 17/17, full suite 115/115, build 0 warning/0 error, UI diff bằng không.

### Checkpoint C — Pure simulation logic

- [x] Parse được standard/extended CAN ID, DLC, endian, signedness, factor/offset và min/max trong phạm vi đã định.
- [x] Pack/unpack signal có golden vectors độc lập.
- [x] E2E chỉ chạy khi cấu hình bật; counter/CRC có test vector.
- [x] Không phụ thuộc WPF hoặc Vector XL trong các pure module.

### Phase 3 — Gateway engine

- [x] Task 10: Gateway hai chiều và mode PassThrough/Block — **Sol ultra**, review **Terra xhigh**.
- [x] Task 11: Inject/override, live baseline và echo filtering — **Sol ultra implementation PASS**, **Terra xhigh review PASS**, **Luna xhigh final review PASS** (build 0/0, full test 134/134, 10x engine stress pass, UI diff 0); commit `dd60366`. Signal value contract đã hiện thực: numeric physical value; typed `VAL_` label/key map raw → physical trước khi pack; live baseline chỉ là nguồn giữ bit/hiển thị, không tự thành override. Hardware echo/latency thực vẫn `NEEDS_VERIFY`.
- [x] Task 12: One-shot/Cyclic/Event, pause/resume và emergency stop — **Sol xhigh implementation/self-review PASS**, **Sol ultra Required-finding fix PASS** và **Terra xhigh re-review PASS**. Pause/transmit-gate interleaving không vượt pause boundary; emergency luôn cleanup session dù scheduler fault và vẫn giữ typed root failure. Focused 14/14 lặp 20 vòng (280/280), full 148/148, build 0/0, formatter/diff/secret/UI diff 0. Chưa commit/push.

### Checkpoint D — Simulation engine

- [x] RX→TX và TX→RX hoạt động qua in-memory session.
- [x] Frame không có rule được pass-through; Block không phát; Inject chỉ sửa signal được override.
- [x] Cancellation dừng receive/scheduler hữu hạn thời gian, không phát thêm frame sau stop; Emergency Stop await in-flight Event transmit trước session cleanup.
- [x] Counter quan sát được qua immutable `GatewayStatistics`; typed worker failure được giữ và ném lại qua stop/emergency paths.
- [x] Slices 15.1–15.2 engine telemetry và ViewModel projection đã PASS qua implementation/review gates. Physical hardware latency vẫn `NEEDS_VERIFY`.

### Phase 4 — Application integration không đổi UI

- [x] Task 13: SimulationViewModel và projection sang binding hiện hữu — implementation/fix **Terra xhigh** PASS; independent re-review **Sol xhigh** PASS. Build 0/0, focused 6/6, full 154/154, formatter/diff/UI scope sạch; chưa commit/push.

**Task 13 contract (2026-08-14):** `SimulationViewModel` nhận `SimulationPlan` và `ISimulationEngine` đã được tạo ở composition boundary; không gọi Vector API, không sở hữu/mở/đóng `ICanGatewaySession`. Nó project DBC/rule typed thành `Messages`, `Signals`, `FaultQueue`, phản chiếu runtime engine state qua method backend explicit, và không tự start/stop. `MainViewModel` chỉ forward đúng các binding path hiện hữu, thay demo data bằng collection typed rỗng hoặc plan projection. Start/Stop/Pause/Emergency command binding là `UI_GATED`: chưa thêm RelayCommand, XAML hay code-behind. Public test seams: `SimulationViewModel` và constructor injected của `MainViewModel`; fake `ISimulationEngine` được dùng để kiểm tra lifecycle/state mà không dùng Vector/hardware.

**Task 13 implementation checkpoint (2026-08-14):** Terra xhigh đã hoàn tất projection typed, composition boundary và test seam. Build 0 warning/0 error; focused 5/5 và full suite 153/153 PASS; `git diff --check` và UI/XAML/code-behind scope PASS. Chưa đánh dấu DONE hay commit/push trước independent review `Sol xhigh`.

**Task 13 Sol xhigh review (2026-08-14):** Spec/ownership/UI scope PASS; Standards trả 2 Required findings về CRLF formatter và WPF continuation context.

**Task 13 Terra xhigh fix (2026-08-14):** PASS — đã chạy targeted formatter để chuẩn hóa 5 file C# Task 13 sang CRLF; `dotnet format --verify-no-changes` PASS. Bỏ mọi `ConfigureAwait(false)` khỏi `SimulationViewModel` để observable state trở về caller synchronization context. Regression test dùng fake engine hoàn thành bất đồng bộ + queueing synchronization context đã RED trước fix/GREEN sau fix. Build 0/0; focused 6/6 và full suite 154/154 PASS; diff/UI scope sạch. Chờ `Sol xhigh` re-review, chưa DONE/commit/push.

**Task 13 Sol xhigh re-review (2026-08-14):** PASS — hai Required finding đã được giải quyết đúng phạm vi. Re-review độc lập xác nhận targeted formatter/CRLF PASS; continuation sau `ValueTask` chưa hoàn tất quay lại caller synchronization context trước khi phát `PropertyChanged`; không còn `ConfigureAwait(false)` trong ViewModel. Build 0 warning/0 error; focused 6/6 và full suite 154/154 PASS; `git diff --check` cùng UI/XAML/code-behind/project/solution scope PASS. Không có finding Critical/Required; Task 13 DONE, chưa commit/push.
- [x] Task 14: Integration/soak tests bằng in-memory session — implementation/fix **Luna xhigh** PASS; independent re-review **Sol xhigh** PASS. Focused 4/4, soak 10/10 (500 vòng), build 0/0, full 158/158, formatter/diff/UI scope sạch; chưa commit/push.

**Task 14 implementation checkpoint (2026-08-14):** Luna xhigh đã thêm integration path qua `MockHardwareService`/`MockCanGatewaySession`: Classic PassThrough/Block/Inject, CAN FD one-shot Inject với DLC/BRS, soak 50 vòng connect/start/schedule/stop/disconnect và reconnect sau typed transmit failure. Focused 4/4; soak lặp 10/10 (500 vòng); build 0 warning/0 error; full suite 158/158; targeted formatter, `git diff --check` và UI/XAML/code-behind/project/solution scope PASS. Chưa đánh dấu DONE hay commit/push trước independent review `Sol xhigh`.
**Task 14 Sol xhigh review (2026-08-14):** Axis Standards/build/scope PASS nhưng Axis Spec có 2 Required test gaps. (1) Soak chỉ kiểm tra enqueue/output sau khi `session.StopAsync()`, nên session đóng đã che mất bằng chứng rằng `SimulationEngine.StopAsync()` không còn route/schedule frame trong lúc caller-owned session vẫn mở. (2) Reconnect tạo `healthyDriver` mới, nên chưa chứng minh cùng driver instance có thể mở session mới sau typed transmit failure và cleanup. Task 14 chưa DONE; chuyển lại Luna xhigh để sửa test theo RED→GREEN, không mở rộng UI/production ngoài test seam cần thiết.

**Task 14 Luna xhigh fix (2026-08-14):** PASS — soak giữ session mở sau `engine.StopAsync()`, enqueue vẫn thành công nhưng không có transmission/counter tăng trong bounded window rồi mới disconnect; reconnect dùng fail-once `ICanHardwareDriver` cùng instance cho lần mở lỗi và lần mở khỏe. Targeted formatter PASS; focused 4/4; soak lặp 10/10 (500 vòng); build 0/0; full suite 158/158; `git diff --check` và UI/XAML/code-behind/project/solution scope PASS. Chờ Sol xhigh re-review, chưa DONE/commit/push.

**Task 14 Sol xhigh re-review (2026-08-14):** PASS — hai Required findings được giải quyết đúng contract. Post-stop proof quan sát session vẫn mở, input được session nhận nhưng không có TX/counter tăng sau engine stop; reconnect dùng chính fail-once driver instance để mở session khỏe thứ hai. Không có finding Critical/Required mới. Formatter PASS; build 0/0; focused 4/4; full 158/158; `git diff --check` và UI/XAML/code-behind/project/solution scope PASS. Task 14 DONE, chưa commit/push.

- [x] Task 15: Code review đa trục và hardware verification checklist — **backend/review closure DONE**: slices 15.1–15.5, Sol ultra lead, Luna high Axis Spec và Terra xhigh Axis Standards/security đều PASS. S2 graceful-close vẫn `UI_GATED`; Vector bench thật vẫn `NEEDS_VERIFY`, được theo dõi tại [`tasks/task15-review-and-hardware-status.md`](task15-review-and-hardware-status.md).

**Task 15 Sol ultra lead review (2026-08-14):** build 0/0, full 158/158, high-risk stress 350/350, targeted formatter, dependency/secret/log/UI scope gates PASS. Không sửa production/UI và không commit/push. Review phát hiện Required gaps về durable error/latency observability, graceful application-close cleanup (`UI_GATED`), typed connection validation/cancel cleanup failure và duplicate DBC identity validation. Hardware thật chưa chạy; consolidated status phân biệt `PASS`/`FAIL`/`NEEDS_VERIFY` trong tài liệu Task 15. Task chưa DONE.

**Task 15 Luna high Axis Spec review (2026-08-14):** PASS độc lập ở phạm vi đối chiếu spec/reference. Xác nhận S1 là Required vì `ISimulationEngine`/`SimulationViewModel` chưa expose durable error state và latency snapshot; xác nhận S2 là Required nhưng `UI_GATED` vì close path thiếu cleanup như reference và mọi sửa WPF code-behind đều cần user approval. Không sửa production/UI, không commit/push. Next reviewer: **Terra xhigh** cho Axis Standards/security Q1–Q3.

**Task 15 Terra xhigh Axis Standards/security review (2026-08-14):** PASS độc lập ở phạm vi review, không có Critical finding mới. Xác nhận Q1 Required (validation exception bị map thành `Unexpected`), Q2 Required (cancel-after-open nuốt typed `StopFailed`), Q3 Required (duplicate DBC identity/signal được chấp nhận rồi fail/ambiguous downstream); Q4/Q5 giữ Optional. Build 0/0, full 158/158, NuGet vulnerability audit và tracked-source secret scan PASS. Không sửa production/UI/package/project, không commit/push. Next model: **Sol ultra** thiết kế remediation S1 và sequence Q1–Q3 trước RED→GREEN implementation.

**Task 15 Sol ultra remediation design (2026-08-14):** DESIGN COMPLETE, chưa implementation. S1 dùng API additive: `ISimulationEngine.LastFailure` giữ first typed root failure mỗi run; immutable `GatewayStatistics.LastRoutingLatency` dùng `TimeSpan?` và monotonic `TimeProvider`, chỉ đo software gateway route thành công, không tuyên bố physical-bus latency. `SimulationViewModel` chỉ project state backend, không thêm binding/UI. Raw ticks, incoming hardware timestamp và runtime snapshot type trùng lặp đã bị loại. Build baseline 0/0; không sửa production/UI/package/project, không commit/push.

**Task 15 slice 15.1 Sol xhigh implementation (2026-08-15):** IMPLEMENTATION COMPLETE, chờ **Terra xhigh** review. Public engine seam đã thêm first-root `LastFailure` và immutable nullable `LastRoutingLatency`; failure được giữ qua receive/gateway/scheduler/emergency paths, cleanup thứ cấp không ghi đè root, valid restart reset telemetry còn invalid start giữ state cũ. Latency dùng monotonic `TimeProvider`, bao gồm transmit-gate wait đến session acceptance và không cập nhật bởi block/echo/scheduler/failure. RED→GREEN tests PASS; focused 39/39, full 162/162, stress 390/390, build 0/0, targeted formatter/diff/secret/UI/project scope PASS. Không sửa ViewModel behavior/UI/XAML/code-behind, package/project/solution; không commit/push.

**Task 15 slice 15.1 Terra xhigh review (2026-08-15):** PASS — không có finding Critical/Required. Axis Spec xác nhận first-root/reset/latency interval và các exclusion; Axis Standards xác nhận atomic capture/reset, seam/interface boundary, deterministic manual-clock tests và không có security/performance regression. Independent build 0/0, focused 39/39, full 162/162, stress 390/390, formatter/diff/secret/UI/project scope PASS. Slice 15.1 DONE; không sửa file, không commit/push.

**Task 15 remediation sequence:**

1. [x] Slice 15.1 — S1 engine telemetry contract: **Sol xhigh implementation PASS**, **Terra xhigh review PASS**.
2. [x] Slice 15.2 — S1 ViewModel projection/failure refresh: **Terra high implementation PASS**, **Luna xhigh review PASS**; phụ thuộc 15.1.
3. [x] Slice 15.3 — Q1/Q2 connection validation/cancel cleanup: **Terra xhigh implementation PASS**, **Sol xhigh review PASS**; độc lập sau design freeze.
4. [x] Slice 15.4 — Q3 duplicate DBC boundary: **Terra high implementation PASS**, **Luna xhigh review PASS**; độc lập sau design freeze.
5. [x] Slice 15.5 — full re-review/closure gate: **Sol ultra lead PASS**, **Luna high Axis Spec PASS**, **Terra xhigh Axis Standards/security PASS**; phụ thuộc 15.1–15.4.

S2 graceful application-close cleanup tiếp tục `UI_GATED`; không nằm trong các backend slice và chỉ được triển khai sau user approval riêng cho `MainWindow.xaml.cs`/UI lifecycle.

### Checkpoint E — Backend complete

- [x] `dotnet build Simulate.sln` đạt 0 warning, 0 error tại Task 15 lead gate.
- [x] `dotnet test Simulate.sln` đạt 173/173 tại closure gate Task 15.
- [x] Slice 15.1 implementation gate: build 0/0; full 162/162; high-risk stress 390/390; targeted formatter/diff/secret/UI/project scope PASS.
- [x] `git diff -- Simulate/App.xaml Simulate/MainWindow.xaml Simulate/MainWindow.xaml.cs` không có output.
- [x] Không còn dữ liệu placeholder làm nguồn sự thật của simulation backend; default simulation projection là typed empty state/plan data.
- [x] Hardware checklist ghi rõ software evidence `PASS`, known static gap `FAIL` và physical bench `NEEDS_VERIFY`.
- [x] Task 15 closure: không có finding Critical/Required trong lead và hai cross-review; S2 giữ `UI_GATED`, không bị đánh dấu sai là PASS.
- [ ] Sẵn sàng xin phép riêng cho UI integration nếu người dùng muốn kích hoạt toàn bộ thao tác từ giao diện.

## 8. Phase 5 — Binding backend vào 10 khu vực UI, user-gated tuần tự

### 8.1. Phạm vi được phép và rào chắn tuyệt đối

Phase này chỉ liên kết backend/ViewModel đã có hoặc seam tối thiểu còn thiếu vào **control hiện hữu**.
Yêu cầu lập kế hoạch này không tự động cho phép sửa source UI. Trước mỗi khu vực, người dùng phải cho
phép rõ ràng đúng khu vực đó.

`Binding-only` được hiểu là:

- chỉ thêm/sửa `Binding`, `Command`, `ItemsSource`, `SelectedItem`, `IsChecked`, `IsEnabled` hoặc
  state projection trên control đã tồn tại, sau khi có approval của khu vực;
- có thể thêm ViewModel command/property, composition/lifecycle seam và test cần thiết để binding hoạt động;
- không thêm, xóa, đổi loại hoặc đổi thứ tự control; không sửa layout, kích thước, màu, font, style,
  resource, label, icon hoặc nội dung thiết kế;
- không biến placeholder thành dữ liệu giả mới; giá trị chưa có evidence phải hiển thị từ typed state
  hoặc trạng thái không khả dụng, không được hardcode;
- `MainWindow.xaml.cs` chỉ được chạm cho graceful shutdown của UI-01 sau approval exact; không đưa
  business/hardware logic vào code-behind;
- không thêm package, không sửa `.csproj`/`.sln`, không commit/push nếu chưa có lệnh riêng.

### 8.2. State machine bắt buộc cho từng khu vực

```text
LOCKED
  └─ user cho phép đúng khu vực ─► IMPLEMENTING
       └─ build/test/review PASS ─► WAITING_USER_DEBUG
            ├─ user báo lỗi ──────► DEBUG_RETURN ─► WAITING_USER_DEBUG
            └─ user nói PASS/cho qua ─► USER_ACCEPTED ─► mở khóa khu vực kế tiếp
```

- Agent/reviewer không được tự đổi `WAITING_USER_DEBUG` thành `USER_ACCEPTED`.
- Không triển khai song song hai khu vực. Việc chia model là chia lead/reviewer/debug specialist trong
  cùng một gate, không phải quyền bỏ qua thứ tự 1→10.
- Nếu user tìm thấy lỗi, coordinator giữ nguyên task hiện tại, phân loại lỗi và đề xuất model phù hợp
  trước khi sửa; không được chuyển sang khu vực kế tiếp.

Mỗi lần chuyển trạng thái hoặc mở khu vực mới, coordinator phải cập nhật đồng thời `tasks/plan.md`,
`tasks/todo.md`, `handoff.md` và báo rõ cho user 5 điểm: (1) UI nào đang mở, (2) UI đó làm những gì,
(3) files/seam được phép chạm, (4) lead/reviewer model nào, (5) tiêu chí manual debug để user xác nhận.
Không dùng câu chung chung như “tiếp tục UI”; phải ghi tên panel và hành vi cụ thể.

### 8.3. Dependency và phân bổ model

| UI | Khu vực | Backend/seam chính | Lead | Independent review | Trạng thái |
|---|---|---|---|---|---|
| 1 | Connection / Setup | `ConnectionViewModel`, baudrate contract, session ownership, graceful close | **Terra xhigh → Sol ultra** | **Terra xhigh** | `USER_ACCEPTED` |
| 2 | DBC Management | safe file input, `DbcParser`, document/composition state | **Terra xhigh** | **Sol xhigh** | `USER_ACCEPTED` |
| 3 | TX Message List | DBC projection và editable simulation draft | **Terra xhigh** | **Luna xhigh** | `USER_ACCEPTED` |
| 4 | Live Signal Monitor | bounded live-frame/signal telemetry, Dispatcher projection | **Sol ultra** | **Terra xhigh** | `WAITING_USER_DEBUG` |
| 5 | Fault Configuration | selected signal + typed timing/fault draft validation | **Terra xhigh** | **Luna high** | `LOCKED_BY_UI-04` |
| 6 | Signal Value Configuration | physical value/`VAL_` selection và override replacement | **Terra xhigh** | **Sol xhigh** | `LOCKED_BY_UI-05` |
| 7 | Execution Control | engine/session start-stop-pause lifecycle | **Sol ultra** | **Terra xhigh** | `LOCKED_BY_UI-06` |
| 8 | Log / Output | bounded observable application log, filter/clear/export | **Terra high** | **Luna high** | `LOCKED_BY_UI-07` |
| 9 | Bus Monitor / Health | typed runtime/native health telemetry + bounded history | **Sol ultra** | **Terra xhigh** | `LOCKED_BY_UI-08` |
| 10 | Status Overview | aggregate connection/DBC/engine/health + footer projection | **Terra xhigh** | **Luna high** | `LOCKED_BY_UI-09` |

Sau UI-10, **Sol ultra** thực hiện final lifecycle/race review; người dùng vẫn là final runtime gate.

**UI-01 DEBUG_RETURN — baudrate contract (2026-08-15):** User reported three related defects: TX/RX baudrate
controls bind to one `Connection.Baudrate`; selecting TX implicitly overwrites the user's baudrate from
`HardwareChannel.DefaultBaudrate`; and CAN FD data bitrate is forced to `nominal * 4`. This is one cross-layer
contract issue, not a UI redesign request. First route to **Terra xhigh** to freeze independent `BaudrateTx`,
`BaudrateRx` and explicit CAN FD data-bitrate state/selection without changing visual controls. Then route to
**Sol ultra** for Vector/native `CanGatewayOptions` and `VectorHardwareService` per-channel bitrate configuration.
Do not return to `WAITING_USER_DEBUG` or open UI-02 until both slices are reviewed, built/tested, and user retests.

**UI-01 DEBUG_RETURN — CAN/CAN FD flexibility extension (2026-08-15):** User additionally reported that the
current backend is only an On/Off CAN FD switch. The four recorded gaps are: (1) data bitrate remains derived from
nominal bitrate; (2) Vector FD bit timing uses fixed SJW/TSEG values instead of a selectable/calculated timing
profile; (3) one global FD flag prevents heterogeneous Classic-CAN ↔ CAN-FD gateway sides; and (4) protocol mode
is fixed to ISO, with no Bosch Non-ISO option. This expands the same baudrate/configuration contract and does not
authorize a UI redesign. **Terra xhigh** must first define typed per-side CAN/FD mode, nominal/data bitrate, timing
profile and ISO/Non-ISO protocol state. **Sol ultra** then maps that contract to `VectorXlApi`/Vector XL timing and
native configuration. A new Data Bitrate ComboBox or per-channel mode controls are `UI_SHAPE_GATED` and require
explicit user approval; do not add them under the existing binding-only approval.

**Terra xhigh assessment and slice 1 (2026-08-15):** The shared TX/RX binding, current-bitrate overwrite,
combined-mask configuration, global FD mode and `nominal * 4` calculation are confirmed by the source. The
Non-ISO diagnosis is partly refined: `VectorXlApi` already maps `VectorCanFdProtocolMode.NonIso` to Vector's
`XL_CANFD_CONFOPT_NO_ISO`, but `VectorHardwareService` always supplies `Iso`, so the application contract does
not expose the capability. The fixed `SJW/TSEG` values are a real flexibility defect; however a universal 80 MHz
or 80% timing lookup is not justified for every Vector/Virtual channel without confirmed controller clock/device
documentation. Slice 1 fixes the proven user-state defect: selecting TX retains the user-selected bitrate while
still adding the channel's observed bitrate to the selectable list. TDD regression test was RED (500000 became
250000) then GREEN. Build 0/0, Connection tests 19/19, full 183/183, targeted formatter and diff check PASS.
No XAML or UI-shape change, commit, or push.

**UI-01 bitrate flexibility & binding fix (2026-08-15):** Đã hoàn tất sửa toàn diện theo yêu cầu người dùng:
1. `Baudrate TX` và `Baudrate RX` tách binding riêng tới `Connection.BaudrateTx` và `Connection.BaudrateRx` trên XAML hiện hữu.
2. Dải Nominal Bitrates chuẩn độc lập: 125k, 250k, 500k, 1M (default 500k).
3. Dải Data Bitrates chuẩn: 500k, 1M, 2M, 4M, 5M, 8M (default 2M).
4. CAN FD hỗ trợ Nominal 500k / Data 500k hợp lệ; loại bỏ phép nhân cứng `Baudrate * 4`.
5. Chọn kênh không tự động ghi đè giá trị baudrate do người dùng đã chọn.
6. `VectorHardwareService` áp dụng cấu hình độc lập trên `TxChannel.ChannelMask` và `RxChannel.ChannelMask` khi bitrate hai bên khác nhau.
7. Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 189/189 tests.

**UI-01 review checkpoint (2026-08-15):** binding-only implementation và independent review
đều PASS; trạng thái hiện tại là `WAITING_USER_DEBUG`. Sáu control cấu hình hiện hữu chỉ thêm `IsEnabled` binding;
không đổi visual tree/layout/style/content. `ConnectionViewModel` giữ single-owner session, handoff borrowed
session cho composition, khóa double action và shutdown idempotent; `MainViewModel` dừng simulation trước
connection cleanup; `MainWindow.xaml.cs` chỉ có closing delegation tối thiểu. Build 0/0, full 189/189,
lifecycle stress 10/10 vòng, formatter/diff/secret/config scope PASS. User cần manual debug
Refresh → chọn TX/RX → Connect → Disconnect, close khi connected/đang open, mở lại và reconnect; UI-02 vẫn khóa
cho đến khi user nói UI-01 PASS.

### 8.4. Kết quả bắt buộc theo từng khu vực

| UI | Binding outcome | Manual debug gate của người dùng |
|---|---|---|
| 1 | Refresh/select/connect/disconnect/status dùng state thật; pending operation và session cleanup hữu hạn | Cắm/chọn interface, connect/disconnect, đóng app khi đang connect, mở lại và reconnect |
| 2 | Load/unload DBC, filename/valid state/counts thật; external file có size/regex bounds | Chọn DBC hợp lệ/sai/trùng, unload/reload và kiểm tra message/node/signal count |
| 3 | Grid dùng đúng message fields; Add/Delete/Delete All/Move dùng selected row và draft state | Kiểm tra thứ tự, enable, mode/send type/cycle/signal count; xác nhận behavior cell-action bị gate nếu cần đổi control |
| 4 | Raw/physical/unit/status/timestamp cập nhật bounded; search/filter/pause/clear hoạt động | Chạy traffic, pause/resume/clear, lọc theo message và kiểm tra UI không freeze |
| 5 | Selected signal và fault/timing fields tạo queue item typed, validation không làm crash | Nhập valid/invalid timing/value, Add to Queue, kiểm tra item và thông báo lỗi |
| 6 | Value edit/`VAL_` map thành physical override; min/max/step và filter đúng | Đổi numeric/label value, bật/tắt override, kiểm tra payload qua Mock/hardware phù hợp |
| 7 | Start/Stop/Pause/Resume/Clear Queue và status dùng engine thật, command state chống double action | Chạy injection, pause/resume/stop, lặp nhanh và kiểm tra cleanup; `Emergency` không được tuyên bố có UI nếu chưa có control |
| 8 | Log thật, bounded, lọc level, clear và export an toàn | Tạo connect/load/run/error events, lọc/clear/export rồi đối chiếu file |
| 9 | Health counters/history dùng evidence thật; metric không có nguồn phải hiện unavailable, không fake | Chạy traffic/fault/soak, đối chiếu received/dropped/error/latency và kiểm tra graph bounded |
| 10 | Status overview/footer tổng hợp đúng 1–9, không còn chuỗi demo làm runtime truth | Chạy full flow connect→DBC→configure→inject→stop→disconnect và kiểm tra mọi trạng thái |

### 8.5. Routing model khi user báo lỗi

| Loại lỗi debug | Model đề xuất |
|---|---|
| Native Vector, connect/disconnect, close app, race, leak, scheduler/emergency | **Sol ultra** |
| Binding path, command/CanExecute, validation, ViewModel/composition, DBC projection | **Terra xhigh** |
| Reproduce test, fixture/corpus, kiểm tra field mapping và regression cơ học | **Luna xhigh** |
| Parser/file input resource bound, regex/performance hoặc codec/override edge | **Sol xhigh** phối hợp **Terra xhigh** |

### 8.6. Quy trình user báo lỗi và coordinator phân agent

User không cần tự phân biệt lỗi thuộc model nào. User chỉ cần gửi mô tả lỗi cho coordinator; coordinator
sẽ đọc symptom, log/screenshot và bước tái hiện để phân loại, cập nhật khu vực hiện tại thành `DEBUG_RETURN`,
sau đó trả lại đúng model cần chuyển sang. Coordinator phải nói rõ model trước khi user chuyển; không tự mở
khu vực tiếp theo trong lúc lỗi chưa được đóng.

Thông tin user nên gửi (có gì gửi nấy): panel đang test, thao tác vừa làm, expected, actual, thông báo lỗi/log,
và lỗi có tái hiện lại được không. Nếu thiếu dữ liệu để phân loại, coordinator hỏi bổ sung trước, không đoán
model và không sửa code.

| Dấu hiệu user quan sát được | Phân loại coordinator | Model user chuyển sang |
|---|---|---|
| Vector không mở/đóng, connect/disconnect sai, app đóng treo, double-open, race, leak | Native/lifecycle/concurrency | **Sol ultra** |
| Nút/ComboBox không enable đúng, binding không cập nhật, Connected state sai, command không chạy | Binding/CanExecute/ViewModel state | **Terra xhigh** |
| Không tái hiện ổn định, sai mapping field, cần thêm fixture/regression test | Reproduction/test fixture | **Luna xhigh** |
| DBC/file input, giới hạn tài nguyên, regex/performance hoặc codec/override edge | Parser/resource/domain edge | **Sol xhigh**, phối hợp **Terra xhigh** |

Sau khi user chuyển model và model sửa xong, coordinator sẽ yêu cầu review/build/test lại rồi trả UI-01 về
`WAITING_USER_DEBUG`. Chỉ câu `UI-01 PASS` của user mới được đổi thành `USER_ACCEPTED` và mở UI-02.

Coordinator phải nêu model cần chuyển cùng lý do sau mỗi lỗi; model sửa và model review nên khác nhau.

## 9. Verification bắt buộc cho mỗi task

```powershell
dotnet build Simulate.sln
dotnet test Simulate.sln   # sau khi test project được phê duyệt
git diff -- Simulate/App.xaml Simulate/MainWindow.xaml Simulate/MainWindow.xaml.cs
```

Ngoài ra:

- pure module: focused unit tests với golden vectors;
- hardware adapter: contract tests chạy qua Mock và manual test có Vector hardware;
- gateway: bidirectional integration test, cancellation test và soak test;
- mọi lỗi native phải giữ lại `XL_Status` trong result/log chẩn đoán.

## 9.1. Task completion reminder bắt buộc

Sau khi một task đạt acceptance criteria, coordinator phải nhắc và thực hiện đủ chuỗi sau trước khi chuyển model/task:

1. Đánh dấu task và checkpoint tương ứng trong `tasks/todo.md`/`tasks/plan.md`.
2. Chạy `dotnet build`; chạy `dotnet test` nếu test project đã được duyệt.
3. Kiểm tra `git diff --check`, UI diff và danh sách file thay đổi.
4. Ghi work log, `NEEDS_VERIFY` hoặc approval còn thiếu.
5. Tạo/cập nhật `handoff.md` với next action và model lead/reviewer.
6. Báo cáo cho người dùng: task đã xong, verification, file thay đổi và model tiếp theo.

Không được tự chuyển sang task tiếp theo nếu bước nhắc/bàn giao này chưa hoàn tất.

## 10. Rủi ro và giảm thiểu

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Leak/crash native handle khi lỗi giữa chuỗi Open→Configure→Activate | High | Session ownership, `finally`, cleanup idempotent, Sol ultra review |
| RX/TX chọn cùng physical channel | High | Typed validation trước khi mở port |
| Sai API giữa CAN Classic và CAN FD | High | Source-driven đối chiếu `Doc/XLDriver.txt`, `XLClass.txt`, manual; contract test riêng |
| UI freeze | High | Async command, cancellation; cấm polling/sleep trên Dispatcher |
| Echo loop khi gateway hai chiều | High | Source metadata + sent-frame cache có timeout và integration tests |
| DBC encode sai endian/signed/scale | High | Golden vectors độc lập, property/boundary tests |
| Khác biệt timing/hardware chỉ xuất hiện trên thiết bị thật | Medium | Manual hardware checkpoint và `NEEDS_VERIFY`, không giả định từ Mock |
| PLAN backend hoàn tất nhưng thao tác UI chưa bấm được | Expected | UI approval gate rõ ràng; không vi phạm lệnh khóa UI |

## 11. Approval gates trước implementation

Trước Task 0/1 cần người dùng phê duyệt riêng nếu thực hiện:

1. tạo test project và sửa `Simulate.sln`;
2. thêm test NuGet packages;
3. thay đổi `Simulate.csproj` như ép target x64;
4. tạo thêm cấu trúc thư mục/module mới;
5. bất kỳ thay đổi nào ở UI/XAML/code-behind.

## 12. Definition of Done toàn chương trình

- Acceptance criteria của từng task đạt đủ.
- Build 0 warning/0 error; test 100% pass khi test infrastructure được duyệt.
- Resource cleanup idempotent và được kiểm thử trên mọi nhánh lỗi có thể mô phỏng.
- Simulation engine độc lập WPF và Vector implementation.
- Diff UI bằng không, trừ khi có approval mới bằng văn bản từ người dùng.
- PLAN/todo được cập nhật sau mỗi checkpoint.
- Sau mỗi task phải hoàn tất task completion reminder và handoff checkpoint.
- Không commit/push khi chưa được yêu cầu.
