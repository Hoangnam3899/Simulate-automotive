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
- Task 1 đã commit tại `5a14071 feat: define CAN gateway session contracts`.
- Task 2 đã commit tại `8eae6fa feat: add in-memory mock gateway session`.
- Task 3 đã hoàn tất: `Sol ultra` implementation, `Terra xhigh` review PASS; chưa commit/push.

## Trạng thái repository

- PLAN, Task 1 và Task 2 đã commit; Task 3 cùng cập nhật coordinator hiện chưa commit/push.
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

- Lỗi cleanup cũ của `VectorHardwareService.Connect()` khi activate fail đã được sửa trong Task 3 và có regression test qua legacy seam.
- Discovery và command hiện đồng bộ; không gọi hardware blocking trong ViewModel constructor hoặc Dispatcher.
- `ICanHardwareDriver` mới chỉ làm discovery/open session; `ICanGatewaySession` giữ receive/transmit/flush/stop/dispose và không lộ type Vector.
- `ICanConnectionDriver` là seam chuyển tiếp cho luồng ViewModel đồng bộ hiện tại; Task 6 sẽ xóa seam này sau khi Vector/Mock adapters triển khai contract mới.
- Dự án tham chiếu có kiến trúc MITM hai channel, receive/transmit hai chiều, override signal, DBC, echo filtering và E2E. Chỉ học hành vi; không bê nguyên monolith hoặc UI code-behind.
- CAN Classic/FD cần tách implementation nội bộ: V3/Classic API so với V4/CAN FD API.

## Next action

**Task 3 — DONE: Lead Sol ultra; independent review Terra xhigh PASS.**

Terra xác nhận không còn finding Critical/Required:

1. `VectorHardwareService` triển khai typed discovery/open session và giữ legacy connection seam mà không đổi UI/binding.
2. `IVectorXlApi` cô lập `vxlapi_NET`; discovery lọc `XL_BUS_ACTIVE_CAP_CAN`, valid channel index/mask và luôn đóng driver trong `finally`.
3. Session sở hữu driver/port/channel activation; setup failure và stop cleanup theo thứ tự deactivate → close port → close driver.
4. Tất cả native failure giữ operation, error code, numeric `XL_Status`, enum name và API name; cleanup failure không bị nuốt.
5. Manual local 20.30 xác nhận active CAN capability ở p.61, interface V3/V4 và `permissionMask` ở pp.43-44, cleanup flow ở p.103. DLL đang dùng là 25.20.14.0 và trùng hash với bản trong `Doc/`.
6. Verification độc lập: build 0 warning/0 error, full suite 21/21 PASS, `git diff --check` PASS, UI diff bằng không.

**Next action: Task 4 — Classic CAN receive/transmit hai chiều.** Lead `Sol ultra`; test review `Luna xhigh`; code review `Terra xhigh`. Không commit/push nếu người dùng chưa yêu cầu.

`NEEDS_VERIFY`: chưa cắm hardware Vector thật. `VectorCanGatewaySession` trong Task 3 chỉ hoàn thiện native lifecycle; receive/transmit/flush thực tế được cố ý hoãn sang Task 4 (Classic) và Task 5 (FD), không nối vào UI hiện tại.

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
