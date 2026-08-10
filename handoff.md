# Handoff — Simulate Automotive Backend Plan

## Mục đích

Tài liệu này bàn giao trạng thái để model/agent tiếp theo thực hiện đúng task đang đến, không tự mở rộng sang UI.

## Nguồn sự thật

- Kế hoạch đầy đủ: [`tasks/plan.md`](tasks/plan.md).
- Checklist theo task và phân bổ Luna/Terra/Sol: [`tasks/todo.md`](tasks/todo.md).
- Rule dự án: `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `PROJECT_RULES_COMBINED.md`.
- Tài liệu Vector local: `Doc/XL_Driver_Library_Manual_EN.pdf`, `Doc/XLDriver.txt`, `Doc/XLClass.txt`, `Doc/XLDefine.txt`.
- Dự án tham chiếu chỉ đọc: `D:\TEST_DEV\TOOL ĐỌC DTC DID EBUS\V1.5\TreeViews and Value Converters`.

## Đã hoàn thành

- Đọc rules, catalog và các skill điều phối cần thiết.
- Đọc code hiện tại của `Simulate` và dự án tham chiếu Vector/MITM.
- So sánh kiến trúc, chốt hướng backend .NET 8/MVVM thay vì sao chép manager/code-behind nguyên khối từ dự án tham chiếu.
- Tạo PLAN gồm 16 task, checkpoint, verification, risk register, approval gates và phân bổ model.
- Commit/push PLAN:
  - Commit: `e6a1935 docs: add vector simulation implementation plan`
  - Branch: `chore/merge-agent-skills`
  - Remote tracking branch: `origin/chore/merge-agent-skills`
- Baseline build đã PASS: `dotnet build Simulate.sln`, 0 warning, 0 error.
- Task 0 baseline đã kiểm tra; người dùng đã xác nhận approval cho test project/NuGet, target x64 có điều kiện và module backend.
- Task completion reminder đã được thêm vào `tasks/plan.md`, `tasks/todo.md` và handoff này.
- Phần implementation Task 1 bằng `Sol xhigh` đã hoàn tất: domain frame/options/result và hardware/session seam đã compile sạch.

## Trạng thái repository

- Các file PLAN/todo đã commit.
- Task 1 và các cập nhật coordinator hiện chưa commit/push.
- Worktree còn thay đổi không thuộc phạm vi coordinator và không được stage/commit:
  - `AGENTS.md`
  - `CLAUDE.md`
  - `GEMINI.md`
  - `PROJECT_RULES_COMBINED.md`
  - `.agents/AGENTS.md` (untracked)
- Không reset, checkout, stage hoặc commit các file trên nếu chưa có chỉ thị riêng.

## Rào chắn không được vi phạm

- Không sửa `Simulate/App.xaml`, `Simulate/MainWindow.xaml`, `Simulate/MainWindow.xaml.cs` hoặc file UI/XAML khác khi chưa có yêu cầu và approval rõ ràng.
- Giữ các binding path đang tồn tại: `Connection.*`, `Messages`, `Signals`, `FaultQueue`.
- Không thêm package, test project, thay đổi `.csproj`/`.sln`, target x64 hoặc cấu trúc project khi chưa được duyệt.
- Không commit/push ngoài yêu cầu người dùng.
- Không mang bất kỳ license key, private key hay credential nào từ dự án tham chiếu vào dự án này.

## Kiến trúc cần thực hiện

Tạo một deep hardware module có seam thật:

```text
ConnectionViewModel → ICanHardwareDriver → Vector adapter / Mock adapter
SimulationViewModel → SimulationEngine → ICanGatewaySession
SimulationEngine → DBC parser + signal codec + E2E (pure modules)
```

Vector-specific handles, masks, permissions và `XLDriver` phải nằm trong session/adapter. ViewModel và simulation engine không gọi `vxlapi_NET` trực tiếp.

## Điểm kỹ thuật cần chú ý

- `VectorHardwareService.Connect()` hiện có nguy cơ cleanup thiếu nếu activate fail: `Disconnect()` thoát sớm khi `IsConnected == false`, trong khi port/driver đã được mở.
- Discovery và command hiện đồng bộ; không gọi hardware blocking trong ViewModel constructor hoặc Dispatcher.
- `ICanHardwareDriver` mới chỉ làm discovery/open session; `ICanGatewaySession` giữ receive/transmit/flush/stop/dispose và không lộ type Vector.
- `ICanConnectionDriver` là seam chuyển tiếp cho luồng ViewModel đồng bộ hiện tại; Task 6 sẽ xóa seam này sau khi Vector/Mock adapters triển khai contract mới.
- Dự án tham chiếu có kiến trúc MITM hai channel, receive/transmit hai chiều, override signal, DBC, echo filtering và E2E. Chỉ học hành vi; không bê nguyên monolith hoặc UI code-behind.
- CAN Classic/FD cần tách implementation nội bộ: V3/Classic API so với V4/CAN FD API.

## Next action

**Task 1 — DONE: Lead Sol xhigh; review Terra xhigh PASS.**

Contract freeze đã chốt các điểm sau:

1. `CanFrame` giữ standard/extended ID, frame format Classic/FD, DLC, BRS, payload, source và timestamp mà không phụ thuộc Vector XL.
2. `CanGatewayOptions` phân biệt Classic/FD bitrate và chặn RX/TX có `ChannelIndex` giống nhau hoặc channel mask chồng lấp.
3. `ICanHardwareDriver` chỉ discovery/open session; lifecycle I/O nằm trong `ICanGatewaySession`.
4. `StopAsync` không cancellable giữa cleanup; `DisposeAsync` tiếp tục có contract idempotent.
5. UI/XAML/code-behind và binding không thay đổi; `ICanConnectionDriver` chỉ là seam chuyển tiếp đến Task 6.

**Task 2 — DONE: Lead Terra high; review Luna high PASS.**

Luna review đã xác nhận:

1. `MockHardwareService` triển khai song song seam legacy và `ICanHardwareDriver` mới, không làm đổi UI/binding.
2. `MockCanGatewaySession` có queue receive/transmit tách biệt, RX→TX và TX→RX không tạo echo.
3. Fault plan map đúng discovery/open driver/open session/configure/activate/transmit sang `HardwareFailure` typed.
4. `StopAsync`/`DisposeAsync` idempotent, kết thúc queue và không chấp nhận transmit sau stop; race với `TryWrite` không báo thành công giả.
5. Test project MSTest không thêm dependency ngoài approval; test qua public seam thay vì private state.
6. Không có finding Critical/Required; build/test và UI gate đều PASS.

Task tiếp theo là Task 3 — **Sol ultra**, review **Terra xhigh**. Task 2 hiện chưa commit; chỉ tạo commit khi người dùng yêu cầu. Không bắt đầu Vector native lifecycle trước khi chuyển model sang Sol ultra.

## Task completion reminder

Sau mỗi task, coordinator phải cập nhật PLAN/todo, chạy build/test phù hợp, kiểm tra UI diff, ghi work log/`NEEDS_VERIFY`, cập nhật handoff và báo người dùng model của task kế tiếp. Không chuyển task nếu chuỗi này chưa hoàn tất.

## Suggested skills

1. `api-and-interface-design` cho Task 1 trước khi sửa `ICanHardwareDriver`.
2. `test-driven-development` cho Mock adapter và mọi contract/gateway test.
3. `source-driven-development` trước Vector XL lifecycle, Classic CAN hoặc CAN FD interop; dùng tài liệu local trong `Doc/`.
4. `incremental-implementation` cho từng vertical slice.
5. `debugging-and-error-recovery` khi build/test/hardware failure xảy ra.
6. `code-review` và `security-and-hardening` ở Task 15.
7. `git-workflow-and-versioning` cho mọi commit/push mới.

## Verification required on every implementation task

```powershell
dotnet build Simulate.sln
dotnet test Simulate.sln   # chỉ khi test project đã được duyệt
git diff -- Simulate/App.xaml Simulate/MainWindow.xaml Simulate/MainWindow.xaml.cs
```

Nếu build/test có lỗi, không đánh dấu task hoàn thành; ghi lỗi và `NEEDS_VERIFY` vào `tasks/todo.md`.
