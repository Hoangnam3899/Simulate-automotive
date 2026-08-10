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

- [ ] Task 2: Nâng Mock adapter thành in-memory gateway session — **Terra high**, review **Luna high**.
- [ ] Task 3: Hardening discovery và native lifecycle — **Sol ultra**, review **Terra xhigh**.
- [ ] Task 4: Classic CAN receive/transmit hai chiều — **Sol ultra**, review **Luna xhigh** + **Terra xhigh**.
- [ ] Task 5: CAN FD receive/transmit hai chiều — **Sol ultra**, review **Terra xhigh**.
- [ ] Task 6: Chuyển connection orchestration sang async/cancellation — **Terra xhigh**, review **Luna high**.

### Checkpoint B — Hardware session

- [ ] Mock session chứng minh connect, receive, transmit, flush và disconnect.
- [ ] Mọi nhánh lỗi Vector đều đóng port/driver đúng một lần.
- [ ] Classic dùng interface V3; CAN FD dùng V4 và API receive/transmit tương ứng.
- [ ] Connect/refresh/disconnect không block UI thread.
- [ ] Build/test sạch và không đổi UI.

### Phase 2 — Simulation domain

- [ ] Task 7: DBC domain và parser tối thiểu — **Terra xhigh**, review **Luna xhigh**.
- [ ] Task 8: Signal codec và E2E protection — **Sol xhigh**, review **Luna xhigh**.
- [ ] Task 9: Simulation configuration và validation — **Terra high**, review **Luna high**.

### Checkpoint C — Pure simulation logic

- [ ] Parse được standard/extended CAN ID, DLC, endian, signedness, factor/offset và min/max trong phạm vi đã định.
- [ ] Pack/unpack signal có golden vectors độc lập.
- [ ] E2E chỉ chạy khi cấu hình bật; counter/CRC có test vector.
- [ ] Không phụ thuộc WPF hoặc Vector XL trong các pure module.

### Phase 3 — Gateway engine

- [ ] Task 10: Gateway hai chiều và mode PassThrough/Block — **Sol ultra**, review **Terra xhigh**.
- [ ] Task 11: Inject/override, live baseline và echo filtering — **Sol ultra**, review **Terra xhigh** + **Luna xhigh**.
- [ ] Task 12: One-shot/Cyclic/Event, pause/resume và emergency stop — **Sol xhigh**, review **Terra xhigh**.

### Checkpoint D — Simulation engine

- [ ] RX→TX và TX→RX hoạt động qua in-memory session.
- [ ] Frame không có rule được pass-through; Block không phát; Inject chỉ sửa signal được override.
- [ ] Cancellation dừng receive/scheduler hữu hạn thời gian, không phát thêm frame sau stop.
- [ ] Counter/latency/error state quan sát được qua engine interface.

### Phase 4 — Application integration không đổi UI

- [ ] Task 13: SimulationViewModel và projection sang binding hiện hữu — **Terra xhigh**, review **Sol xhigh**.
- [ ] Task 14: Integration/soak tests bằng in-memory session — **Luna xhigh**, review **Sol xhigh**.
- [ ] Task 15: Code review đa trục và hardware verification checklist — **Sol ultra**, review chéo **Luna high** + **Terra xhigh**.

### Checkpoint E — Backend complete

- [ ] `dotnet build Simulate.sln` đạt 0 warning, 0 error.
- [ ] `dotnet test Simulate.sln` đạt 100% khi test project đã được duyệt.
- [ ] `git diff -- Simulate/App.xaml Simulate/MainWindow.xaml Simulate/MainWindow.xaml.cs` không có output.
- [ ] Không còn dữ liệu placeholder làm nguồn sự thật của simulation backend.
- [ ] Hardware checklist ghi rõ mục nào đã test thật và mục nào `NEEDS_VERIFY`.
- [ ] Sẵn sàng xin phép riêng cho UI integration nếu người dùng muốn kích hoạt toàn bộ thao tác từ giao diện.

## 8. Những phần UI bị hoãn có chủ đích

UI hiện tại chỉ có command binding cho Connect/Disconnect/Refresh; các control simulation phần lớn chưa có command hoặc binding thao tác. Vì vậy các hành vi sau chỉ được chuẩn bị ở backend/ViewModel và **không nối vào XAML** trong PLAN này:

- Load DBC từ nút UI;
- Start/Stop/Emergency Stop simulation;
- Add/Remove/Clear fault queue;
- chỉnh signal override trực tiếp từ grid;
- pause/clear live monitor;
- hiển thị error/status/counter mới cần thêm binding.

Khi cần các thao tác này, phải tạo một approval gate riêng, liệt kê chính xác binding/control dự kiến sửa và chờ người dùng cho phép.

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
