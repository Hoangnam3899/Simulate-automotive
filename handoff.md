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
- Task 3 đã hoàn tất và commit tại `2cf43de feat: harden Vector gateway lifecycle`.
- Task 4 đã hoàn tất, commit/push tại `b9f804c feat: add Vector Classic CAN frame I/O`; test review `Luna xhigh` và code review `Terra xhigh` đều PASS.
- Rules hợp nhất đã commit/push tại `54168e3 docs: consolidate workspace agent rules`.
- Task 5 đã hoàn tất: implementation `Sol ultra`, independent code review `Terra xhigh` PASS; V4 CAN/CAN FD RX/TX/flush/cancellation, golden DLC và contract tests đạt 65/65. Hardware thật còn `NEEDS_VERIFY`.

## Trạng thái repository

- Branch `chore/merge-agent-skills` đang track và đồng bộ với `origin/chore/merge-agent-skills` tại `54168e3` trước Task 5.
- Worktree chỉ có thay đổi Task 5 ở Vector adapter/session, tests và tài liệu coordinator/checklist.
- Task 5 đã commit `feat: add Vector CAN FD frame I/O`; chưa push. Không commit/push thêm nếu chưa có lệnh người dùng.

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
- Discovery/open/cleanup của connection orchestration đều chạy bất đồng bộ; không gọi hardware blocking trong ViewModel constructor hoặc Dispatcher.
- `ICanHardwareDriver` mới chỉ làm discovery/open session; `ICanGatewaySession` giữ receive/transmit/flush/stop/dispose và không lộ type Vector.
- `ICanConnectionDriver` đã được xóa ở Task 6; `ConnectionViewModel` chỉ dùng `ICanHardwareDriver` và sở hữu gateway session đang mở.
- Dự án tham chiếu có kiến trúc MITM hai channel, receive/transmit hai chiều, override signal, DBC, echo filtering và E2E. Chỉ học hành vi; không bê nguyên monolith hoặc UI code-behind.
- CAN Classic/FD cần tách implementation nội bộ: V3/Classic API so với V4/CAN FD API.
- Task 4 dùng một V3 port với combined RX/TX access mask; `XLevent.chanIndex` ánh xạ source side, còn transmit chọn từng destination mask riêng.
- Task 5 dùng V4 port với `XL_CanTransmitEx`/`XL_CanReceive`; cùng port nhận cả Classic và FD, EDL quyết định format, BRS được bảo toàn và DLC `0..15` ánh xạ đến tối đa 64 byte.
- V4 RX chỉ map exact `XL_CAN_EV_TAG_RX_OK`; queue overflow, invalid DLC/flags và native status đi qua typed `HardwareOperationException`.
- `XLcanFdConf` nay nhận `VectorCanFdProtocolMode.Iso` rõ ràng từ orchestration và map ISO thành `options=0`; legacy `NO_ISO` chỉ còn là nhánh opt-in nội bộ, chưa được public contract mở. Hardware checklist vẫn bắt buộc xác nhận mode/bit timing.
- `ReceiveAsync` báo native/data-loss failure bằng `HardwareOperationException`; caller vẫn lấy được typed `HardwareFailure` và numeric `XL_Status`.
- Checklist hardware Task 4 ở `tasks/vector-classic-can-hardware-checklist.md`; mọi mục chưa cắm thiết bị là `NEEDS_VERIFY`.
- Checklist hardware Task 5 ở `tasks/vector-can-fd-hardware-checklist.md`; timestamp/latency, ISO/non-ISO mode và soak vẫn `NEEDS_VERIFY`.

## Next action

**Task 6 — DONE: Terra xhigh implementation/fix; Luna high re-review PASS.**

Implementation hiện có:

1. `VectorXlApi` dùng đúng V4 wrapper `XL_CanTransmitEx`/`XL_CanReceive`, exact event tags, extended-ID bit, EDL/BRS/RTR/error flags và queue-overflow chip flag.
2. V4 session truyền/nhận cả Classic và FD, giữ source/destination, standard/extended ID, DLC, payload và BRS; Classic V3 regression vẫn đạt.
3. Golden mapping bao phủ đủ 16 DLC; malformed V4 event, native errors và transmit count bất thường trả typed failure.
4. Flush, caller cancellation, stop pending receive và cleanup idempotent dùng cùng contract với Classic.
5. Source local: manual 20.30 pp.103-104; wrapper DLL `25.20.14.0` và type/enum dumps trong `Doc/`.
6. Targeted formatter PASS; build 0 warning/0 error; full suite PASS 65/65; UI/XAML/code-behind, binding, package/project/solution không đổi.

**Review status:** `Terra xhigh` independent two-axis review PASS; không có finding Critical/Required. Build/test, formatter, diff check và UI/project scope gate đều PASS.

**Task 6 implementation/review:**

1. `ConnectionViewModel` dùng `ICanHardwareDriver` bất đồng bộ, không discovery trong constructor và giữ nguyên `RefreshInterfacesCommand`, `ConnectCommand`, `DisconnectCommand` cho binding hiện hữu.
2. `IsBusy`, `LastFailure`, atomic operation gate và `CancelPendingOperation()` biểu diễn trạng thái/cancellation; discovery/connect/disconnect không chạy chồng nhau.
3. ViewModel sở hữu `ICanGatewaySession`: connect thành công mới set `IsConnected`; disconnect và cancel-after-open stop/dispose trên worker task để native cleanup không block Dispatcher.
4. Xóa transitional `ICanConnectionDriver`/sync adapter code; Vector discovery/open chạy trên worker thread để không block Dispatcher, vẫn giữ nguyên public hardware seam.
5. Thêm 6 tests tại `ConnectionViewModelTests`; full suite 71/71 PASS, build 0 warning/0 error, formatter/diff check/UI scope PASS.
6. Đã bổ sung 8 DBC input nguyên trạng vào `DBC/`: 4 CAN Classic và 4 CAN FD; SHA-256 khớp nguồn `C:\Users\Hnam\Downloads\DBC`.

**Required-finding fix:** `ConnectionViewModel` schedule stop/dispose session lên worker task tại disconnect và cancel-after-open. Regression test `Disconnect_returns_control_while_session_cleanup_runs` đưa một `ICanGatewaySession` cleanup chặn đồng bộ qua public seam và xác nhận command trả control trước khi cleanup được giải phóng; test 71/71, build 0 warning/0 error, UI scope sạch.

**Review result:** Luna high re-review PASS, không còn finding Critical/Required. Task 6 DONE; đã commit và push.

**Task 7 implementation:** `Terra xhigh` đã hoàn tất DBC domain/parser pure, 9 parser tests gồm cả 8 DBC thật PASS; full suite 80/80, build 0 warning/0 error, formatter/diff/UI scope PASS. Multiplexing và metadata ngoài phạm vi Task 7 trả warning có line/context, không bị silently diễn giải. Đã commit `2c7df0f`, chưa push.

**Luna xhigh review / Terra xhigh fix:** Luna tìm thấy finding `Required` về signal span vượt payload. Terra đã bổ sung validation cho span little-endian liên tiếp và DBC sawtooth big-endian, cùng hai regression tests cho `63|2@1+` và `56|2@0+` trong payload 8 byte. Luna xhigh re-review PASS, không còn finding Critical/Required. DBC tests 9/9 PASS, full suite 80/80 PASS, build 0 warning/0 error, formatter/diff/UI scope PASS. Task 7 DONE.

**Task 8 implementation:** `Sol xhigh` đã hoàn tất pure `SignalCodec`, CRC-8/SAE-J1850 và stateless `E2eProtector`. Codec hỗ trợ little/big DBC sawtooth, signedness, factor/offset, min/max/raw/layout validation và bảo toàn bit ngoài target. E2E disabled là no-op; enabled validate payload/index, contiguous counter mask, counter range/wrap và CRC range trước mutation. Đây là configurable checksum/counter scheme theo reference, không phải full AUTOSAR Profile vì chưa có Data ID/Profile mode.

**Task 8 verification:** focused tests 18/18 PASS; full suite 98/98 PASS; Debug build dùng isolated output do app Debug đang chạy và Release build đều 0 warning/0 error; targeted formatter, `git diff --check`, UI/XAML/project scope PASS. Luna xhigh independent review hai trục spec/standards PASS, không có finding Critical/Required. Task 8 DONE; chưa commit/push.

**Next action:** Task 9 — lead `Terra high`, review `Luna high`. UI/XAML vẫn khóa.

`NEEDS_VERIFY`: chưa cắm Vector hardware thật; dùng `tasks/vector-can-fd-hardware-checklist.md`. Không nối frame I/O mới vào UI hiện tại.

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
