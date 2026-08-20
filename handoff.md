# Handoff — UI-01 awaiting user debug

## Source of truth

- Task state and phase map: [`tasks/plan.md`](tasks/plan.md).
- Acceptance criteria and work log: [`tasks/todo.md`](tasks/todo.md).
- Workspace rules: `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `PROJECT_RULES_COMBINED.md`, and `CONTEXT.md`.

## Current repository state

- Branch: `chore/merge-agent-skills`; local HEAD `b0e753b` is one commit ahead of `origin/chore/merge-agent-skills`.
- Backend Tasks 0–15 and Checkpoint E are DONE. Build baseline at UI planning start: 0 warnings, 0 errors.
- Current uncommitted changes contain the UI binding plan plus the approved UI-01 implementation/tests.
- `MainWindow.xaml` changes are binding-only on six existing panel-1 controls; code-behind changes are
  graceful-close lifecycle delegation only. Project/solution configuration remains untouched.
- Do not commit or push without a new user instruction.

## UI binding contract — source of truth

- Detailed scope/model/dependency matrix: [`tasks/plan.md`](tasks/plan.md), Phase 5.
- Per-panel acceptance/manual gates: [`tasks/todo.md`](tasks/todo.md), Phase UI binding 1–10.
- Strict order is UI-01→UI-10. Automated tests and agent review can only move a panel to
  `WAITING_USER_DEBUG`; only the user's explicit PASS/"cho qua" unlocks the next panel.
- Before every transition, update PLAN/TODO/Handoff and tell the user the exact panel purpose, allowed
  files/seams, lead/reviewer, automated checks, and manual debug steps. A generic “continue UI” message
  is not a valid transition record.
- If the user reports a defect, keep the current panel in `DEBUG_RETURN`, propose the model best suited
  to that defect, fix/review it, and return it to the user. Never continue to another panel meanwhile.
- Binding-only means existing controls and visual design remain unchanged. XAML edits, when separately
  approved for one panel, are limited to binding/command/state expressions. No layout/style/resource/
  control/content redesign is authorized.

## Current UI inventory and constraints

- Panel 1 has real Refresh/Connect/Disconnect state, safe settings editability, borrowed session handoff, and
  idempotent graceful-close cleanup, but is now in `DEBUG_RETURN` for the user-reported baudrate contract defect.
- Panels 2–10 contain hardcoded runtime/demo values or missing commands; the app currently has only
  three command bindings total.
- Live signal monitor and bus health need bounded backend telemetry seams before binding; they are not
  simple XAML-only work.
- TX row action glyphs are `TextBlock`, and panel 7 has no Emergency button. These remain
  `UI_SHAPE_GATED` if they cannot be made functional without a visual/control change; do not silently
  redesign or claim them complete.
- Interactive DBC input makes the previously Optional document-size/regex resource bound Required in
  UI-02.

## Slice 15.1 accomplished and reviewed

- Added additive `ISimulationEngine.LastFailure` and immutable nullable `GatewayStatistics.LastRoutingLatency`.
- First typed root failure is retained across receive, gateway/scheduler transmit, stop, and emergency cleanup paths; secondary cleanup failure cannot overwrite it.
- A valid run resets telemetry after lifecycle/session validation; an invalid restart preserves the prior failure.
- Routing latency uses monotonic `TimeProvider` from dispatch/before transmit-gate wait through successful session acceptance. Block, echo, scheduler, and failed transmit paths do not update it.
- Added public-seam RED→GREEN coverage and compile-only fake interface updates; `SimulationViewModel` behavior was not changed.
- Full implementation detail and the remaining S2/Q1/Q2/Q3 findings are in [`tasks/task15-review-and-hardware-status.md`](tasks/task15-review-and-hardware-status.md).
- Terra xhigh independent two-axis review found no Critical or Required finding.

## Slice 15.2 accomplished and reviewed

- Added observable `SimulationViewModel.LastFailure` as a direct projection of the engine's durable failure state. The existing immutable `Statistics` projection carries the latency metric without a new ViewModel snapshot.
- An unconfigured ViewModel refreshes to `LastFailure == null` and `GatewayStatistics.Empty`.
- `StopAsync` and `StopSchedulingAsync` refresh state in `finally`; worker exceptions are neither caught nor wrapped, so the original exception instance is retained.
- Public ViewModel seams have RED→GREEN coverage for the projection and both fault paths. The pre-existing delayed-stop test continues proving that observable refresh runs on the caller synchronization context.
- No XAML, binding path, code-behind, package, project, or solution file changed.
- Luna xhigh independent Spec/Standards review: PASS, no Critical/Required finding.

## Slice 15.3 accomplished and reviewed

- Q1: invalid connection options now produce `InvalidConfiguration`, not `Unexpected`; zero bitrate, matching channels, overlapping masks, and CAN FD data-bitrate overflow are rejected before the driver open call.
- Q2: cancellation after a successful open always attempts stop and dispose. The first cleanup failure remains observable, with typed `StopFailed` taking precedence over a later dispose exception; the session is never published as connected.
- Public-seam RED→GREEN coverage and deterministic cancellation control were added without changing UI/binding/package/project boundaries.
- Sol xhigh independent Spec/Standards review: PASS, no Critical/Required finding.

## Verification

- Slice 15.1 targeted formatter: PASS. Repository-wide formatter debt remains out of scope; no bulk normalization was done.
- Build: 0 warnings, 0 errors.
- Full suite: 170/170 PASS.
- Engine + scheduler + integration: 39/39 PASS; stress 390/390 across 10 runs.
- Slice 15.2 ViewModel tests: 9/9 PASS; targeted formatter and `git diff --check`: PASS.
- Slice 15.3 ConnectionViewModel tests: 11/11 PASS; cancellation stress: 110/110 across 10 runs.
- Run .NET gates sequentially: a parallel formatter/build/test attempt transiently caused WPF generated-entry-point `CS5001`; App/project/UI were unchanged and immediate sequential build passed 0/0.
- UI/XAML/code-behind and project/solution scope: PASS. The broad secret-name pattern only found `CancellationToken` parameter names, not credential assignments.

## Slice 15.4 implementation

- Q3 is now rejected at the parser trust boundary: duplicate normalized `(identifier, isExtendedIdentifier)` BO_ identities return `DuplicateMessageIdentifier`; duplicate signal names within a message return `DuplicateSignalName` using ordinal equality.
- Both errors retain the exact duplicate line and context and invalidate the document. `VAL_` and signal lookup now use identity/name indexes rather than first-match search.
- Public-seam RED→GREEN tests cover each duplicate case plus the valid standard/extended same-ID pair. The supplied corpus of eight DBC files remains valid.
- Verification: DBC parser 13/13; build 0/0; full suite 173/173; targeted formatter/diff, NuGet vulnerability audit, secret scan, and UI/project scope all PASS.

Luna xhigh independent Spec/Standards/security review: PASS, no Critical/Required finding. The review did not modify production code or UI.

## Open gates outside completed Task 15 backend/review scope

- S2 graceful-close implementation now exists under the user's exact UI-01 approval; Terra xhigh review passed,
  but physical/user runtime verification remains open, so it is not yet `USER_ACCEPTED`.
- Q3 implementation and Luna xhigh independent review are complete.
- Slice 15.5 lead gate: PASS — build 0/0; full 173/173; high-risk stress 720/720; formatter, NuGet audit, diff/secret/UI scope PASS. Luna high Axis Spec and Terra xhigh Axis Standards/security cross-reviews both PASS with no Critical/Required finding.
- Hardware status remains accurately separated as `PASS`/`FAIL`/`NEEDS_VERIFY`; no physical claim was promoted from fake/in-memory evidence.

## UI-01 implementation and Terra xhigh review checkpoint

- `ConnectionViewModel` exposes the open `ICanGatewaySession` as a borrowed composition reference while
  remaining its sole stop/dispose owner. Settings and Refresh are disabled while busy/connected/shutting down.
- Shutdown atomically becomes terminal, cancels and awaits active discovery/open, rejects late session
  publication, then performs best-effort stop/dispose once while retaining the first typed cleanup failure.
- `MainViewModel.ShutdownAsync()` stops configured simulation work before connection cleanup and still runs
  connection cleanup from `finally` when simulation stop faults.
- `MainWindow.xaml.cs` only delegates the `Closing` lifecycle asynchronously; no Vector or simulation business
  logic was placed in code-behind.
- `MainWindow.xaml` only adds `IsEnabled` bindings to the six existing Interface/TX/RX/CAN FD/bitrate controls.
  There is no control/layout/style/resource/content change.
- TDD added public-seam coverage for session handoff, settings state, disconnect failure precedence,
  idempotent shutdown, active refresh/open races, and engine-before-session close ordering.
- Verification: focused Connection/Simulation ViewModel 29/29 PASS; lifecycle stress 10/10 runs PASS;
  build 0 warnings/0 errors; full suite 182/182 PASS; targeted formatter, `git diff --check`, secret assignment,
  protected config and binding-only XAML scope PASS.
- No package/project/solution change, commit, or push. UI-02 remains locked.
- Terra xhigh two-axis review: **PASS**, no Critical/Required finding. Axis Spec verified real connection state,
  ownership/late-open cleanup and simulation-before-session close. Axis Standards verified MVVM separation,
  code-behind lifecycle-only, off-Dispatcher native cleanup, public-seam tests, formatter/diff/config/secret scope.
- Terra independent verification: build 0/0; focused 29/29; full 182/182; lifecycle stress 5 tests × 10 runs
  (50 executions) PASS. Physical Vector behavior remains `NEEDS_VERIFY`.

## UI-01 bitrate flexibility & channel name formatting completed (2026-08-15)

- Tách độc lập `BaudrateTx` và `BaudrateRx` trong `ConnectionViewModel` (Nominal: 125k, 250k, 500k, 1M).
- Tách độc lập `DataBaudrateTx` và `DataBaudrateRx` (Data: 500k, 1M, 2M, 4M, 5M, 8M; default 2M).
- Hỗ trợ CAN FD với Nominal 500k / Data 500k; loại bỏ phép nhân cứng `Baudrate * 4`.
- Chống tự động ghi đè baudrate người dùng khi đổi channel trong `OnSelectedTxChanged` / `OnSelectedRxChanged`.
- XAML binding Panel 1: `Baudrate TX` -> `Connection.BaudrateTx`, `Baudrate RX` -> `Connection.BaudrateRx`.
- Cấu hình per-channel độc lập trong `VectorHardwareService` khi nominal hoặc data bitrate giữa RX và TX khác nhau.
- Định dạng tên hiển thị channel thành `{HardwareTypeName} Channel {Index}` (ví dụ: `VN1640A Channel 1`, `VN1640A Channel 2`), giúp dễ đọc và không tràn ComboBox.
- Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 190/190 tests.

## UI-02 DBC Management implementation & Real DBC corpus testing (2026-08-20)

- Thêm `IFileDialogService` và `DefaultFileDialogService` để trừu tượng hóa hộp thoại chọn file.
- Thêm `DbcManagementViewModel` quản lý an toàn file bounds (50MB), UTF-8 text parsing, đếm chính xác Messages, Nodes, Signals.
- Tích hợp `DbcManagementViewModel` vào `MainViewModel` và tự động cập nhật projections cho `SimulationViewModel`.
- Binding Panel 2 trong `MainWindow.xaml`: `LoadedFileNameDisplay`, `CheckmarkVisibility`, `MessageCount`, `NodeCount`, `SignalCount`, `LoadDbcCommand`, `UnloadDbcCommand`.
- Kiểm thử tự động với toàn bộ các file DBC thực tế trong thư mục `C:\Users\Hnam\Desktop\Simulate\DBC` (cả CAN class và CAN FD).
- Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 199/199 tests.

## UI-03 TX Message List selective drafting implementation (2026-08-20)

- Cập nhật `MessageModel` thành `ObservableObject`, hỗ trợ `DisplayIndex`, `LastSent`, `DbcSource`.
- Tạo `IMessageDialogService` và `SelectMessageWindow` (Modal dialog) với ô tìm kiếm nhanh và danh sách chọn message từ DBC.
- `SimulationViewModel` giữ danh sách `Messages` ban đầu rỗng khi nạp DBC; thêm các lệnh `AddMessagesCommand`, `DeleteMessageCommand`, `DeleteAllMessagesCommand`, `MoveUpCommand`, `MoveDownCommand`.
- Binding Panel 3 `MainWindow.xaml`: gắn Toolbar commands và sửa toàn bộ lỗi binding cột DataGrid (`#`, `Enable`, `Mode`, `Send Type`, `Signals`, `Last Sent`).
- Tối ưu bảng màu cho `SelectMessageWindow` (Dark Slate `#141D2E` / `#1C273C` / `#23314B` với điểm nhấn Cyan `#38BDF8`), tăng độ tương phản và công thái học hiển thị.
- Tạo bộ kiểm thử `SimulationMessageDraftTests.cs` (10 unit tests).
- Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 210/210 tests.
- Committed to `chore/merge-agent-skills` (Commit `ade2cca`).

## UI-04 Live Signal Monitor implementation (2026-08-20)

- Nâng cấp `SignalModel` thành `ObservableObject`, bổ sung `RawValue`, `PhysicalValueDisplay`, `HasReceivedData`, `StatusText`, `StatusColor`, `LastUpdated` và phương thức `UpdateValue` / `ResetData`.
- Bổ sung phương thức giải mã `UnpackRaw` và `Unpack` tuple trong `SignalCodec.cs` hỗ trợ cả Intel và Motorola byte order.
- `SimulationViewModel`:
  - Thêm `SignalSearchText`, `SelectedSignalMessageFilter`, `AvailableSignalMessageFilters`, `IsSignalMonitorPaused`, `PauseMonitorButtonContent`, `FilteredSignals` (ICollectionView).
  - Thêm `TogglePauseMonitorCommand`, `ClearSignalMonitorCommand`.
  - Triển khai `ProcessIncomingFrame` tự động giải mã bit-level và cập nhật trạng thái màu **Xanh lá (`#10B981` — `● Active`)** khi có data, giữ màu **Xám (`#64748B` — `● No Data`)** khi chưa có data theo yêu cầu người dùng (không dùng hiệu ứng blink).
- Binding Panel 4 `MainWindow.xaml`: gắn Toolbar search, dropdown filter, pause/resume, clear, và sửa lỗi binding các cột DataGrid (`MessageName`, `RawValue`, `PhysicalValueDisplay`, `Unit`, `StatusText/StatusColor`, `LastUpdated`).
- Tạo bộ kiểm thử `LiveSignalMonitorTests.cs` (7 unit tests).
- Cải tiến gom nhóm `MapCanInterfaces` trong `VectorHardwareService.cs`:
  - `Virtual CAN` gom tất cả các kênh ảo (`Virtual Bus 1 - Channel 1, 2`, `Virtual Bus 2 - Channel 1, 2`...).
  - Phần cứng thật tách riêng (`VN1640A 1`...).
  - Tùy chọn `All Vector Devices` tập hợp toàn bộ kênh.
  - Bổ sung unit tests trong `VectorHardwareServiceTests.cs`.
- Verification: `dotnet build Simulate.sln` PASS 0 warning/0 error; `dotnet test Simulate.sln` PASS 219/219 tests.

## Next Panel: UI-05 (Fault Configuration)
- Trạng thái: `READY_FOR_SPEC`.
- Nhiệm vụ:
  1. Chọn tín hiệu cần can thiệp lỗi từ Live Monitor (`Selected Signal`).
  2. Cấu hình Fault Type (Stuck at Value, Bit Flip, Offset, Noise, Ramp, Replay...).
  3. Cấu hình Timing / Injection Mode (Cyclic, OneShot, Duration, Stop Time, Override existing, Restore after stop).
  4. Nút `+ Add to Queue` đưa fault vào hàng đợi `FaultQueue`.

## Suggested skills

1. `domain-modeling` for fault injection types and timing contracts.
2. `codebase-design` for queue projection and validation seams.
3. `incremental-implementation` for component-by-component delivery.
4. `test-driven-development` for fault queue rules and validation logic.
5. `code-review` for verification quality gates.
6. `git-workflow-and-versioning` when committing or pushing changes.
