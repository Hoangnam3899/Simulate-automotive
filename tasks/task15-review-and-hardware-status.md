# Task 15 — Lead Review and Hardware Status

> Status: **SLICES 15.1–15.3 DONE — SLICE 15.4 NEXT — TASK 15 NOT DONE**
>
> Date: 2026-08-15
>
> Next gate: slice 15.4 Q3 duplicate DBC boundary by **Terra high**, then review by **Luna xhigh**.

## Scope and evidence boundary

This review covers the backend domain, simulation engine, ViewModel projection, in-memory
integration tests, Vector SDK boundary, and hardware verification records. It does not authorize
or modify XAML, UI code-behind, package references, project configuration, commits, or pushes.

No real Vector device, analyzer trace, bus termination, or bench timing evidence was available in
this run. Therefore software/fake-SDK evidence may be `PASS`, known static gaps may be `FAIL`, and
every physical-bench claim remains `NEEDS_VERIFY`.

## Axis 1 — Spec

### Required S1 — runtime observability contract is incomplete

Checkpoint D requires counter, latency, and error state to be observable through the engine
interface. Counters are available through `GatewayStatistics`, and typed worker faults can be
re-thrown later by `StopAsync`/`EmergencyStopAsync`; however, `ISimulationEngine` and
`SimulationViewModel` expose neither a durable last-failure state nor a latency metric/snapshot.
The original checkpoint therefore cannot be marked complete yet.

Evidence:

- [`tasks/plan.md`](plan.md), Checkpoint D.
- [`ISimulationEngine.cs`](../Simulate/Services/ISimulationEngine.cs).
- [`SimulationViewModel.cs`](../Simulate/ViewModels/SimulationViewModel.cs).

### Required S2 — graceful application-close cleanup is missing (`UI_GATED`)

The current close button calls `Close()` without cancelling a pending connection or stopping and
disposing an open gateway session. The supplied reference project explicitly attaches a window
closing handler and stops MITM channels to prevent a port-handle leak that can break the next open.
The backend cleanup primitives exist, but the application shutdown hook does not.

This is a known `FAIL` before real-hardware use. Fixing the WPF closing hook requires the user's
separate UI/code-behind approval and must not be implemented implicitly.

Evidence:

- [`MainWindow.xaml.cs`](../Simulate/MainWindow.xaml.cs), close path.
- Reference project `SIMULATE.xaml.cs`, constructor closing subscription and lines 1558–1563.
- User rule: no UI/XAML/code-behind change without explicit approval.

### Spec areas that passed the lead review

- Classic and CAN FD session contracts preserve typed native failures and bounded receive batches.
- Gateway PassThrough/Block/Inject, live baseline, E2E ordering, echo bound/window, scheduler
  lifecycle, and caller-owned session semantics match the recorded task contracts.
- Task 14 proves Classic/FD in-memory integration, 50-cycle cleanup, post-engine-stop silence while
  the session remains open, and same-driver reconnect after a typed transmit failure.
- `MainViewModel` defaults to typed empty simulation collections; hard-coded XAML demo text is not
  used as backend simulation truth.

## Luna high independent Axis Spec review — 2026-08-14

The independent review checked the Task 15 lead findings against Checkpoint D, the public engine
and ViewModel contracts, and the supplied reference application's shutdown behavior. No production
or UI file was changed.

### Required S1 confirmed — runtime observability contract remains incomplete

The lead finding is confirmed. `GatewayStatistics` proves counters, but the public
`ISimulationEngine`/`SimulationViewModel` seam still has no durable last-failure state and no
latency metric or immutable latency snapshot. A failure re-thrown later by stop/emergency is not a
durable observable error state. Checkpoint D therefore remains incomplete until the contract is
explicitly designed and tested.

### Required S2 confirmed — graceful application-close cleanup is missing (`UI_GATED`)

The current close path still only calls `Close()`. The reference `SIMULATE.xaml.cs` subscribes to
the window closing event and stops both the gateway engine and MITM channels, which is the relevant
behavior for avoiding a native port-handle leak. This is a valid spec gap for real hardware, but the
required WPF closing-hook change is explicitly outside the current permission boundary. It remains
`FAIL (UI_GATED)` and is recorded for a later, separately approved UI phase.

### Luna Axis Spec summary

- S1: **Required — confirmed**.
- S2: **Required — confirmed, UI-gated**.
- Backend simulation contracts, Task 14 integration evidence, and no-UI scope remain accepted from
  the lead evidence; no new Axis Spec finding was introduced.
- Task 15 remains **NOT DONE** pending Terra xhigh Axis Standards review and remediation planning.

## Axis 2 — Standards and quality

### Required Q1 — invalid connection configuration is typed as `Unexpected`

`ConnectionViewModel.ConnectAsync` catches `ArgumentException` from `CanGatewayOptions` and maps it
through `CreateUnexpectedFailure`. A zero bitrate or identical RX/TX channel is an expected
`InvalidConfiguration`, not an unexpected failure. This contradicts the typed boundary and the
Task 6 requirement for clear state/error reporting.

Required remediation: preserve `HardwareErrorCode.InvalidConfiguration` and add regression tests
for same-channel, zero-bitrate, and CAN FD bitrate-overflow inputs.

### Required Q2 — cancel-after-open can hide cleanup failure

When cancellation arrives after a session opens, `StopAndDisposeAfterCancelledOpenAsync` discards
the `HardwareOperationResult` returned by `StopAsync`. `RunExclusiveAsync` then treats the path as
ordinary cancellation, so a failed deactivate/close can leave `LastFailure` empty.

Required remediation: complete best-effort disposal but retain the typed cleanup failure, with a
test where open succeeds, cancellation wins, and stop returns `StopFailed`.

### Required Q3 — duplicate DBC identities are accepted before failing downstream

`DbcParser` does not reject duplicate normalized message identifiers or duplicate signal names
within a message. Later consumers use `ToDictionary`, `Single`, or first-match lookup, turning the
malformed external document into a generic runtime exception or ambiguous override selection.

Required remediation: emit line/context parse errors at the DBC boundary and add duplicate-message
and duplicate-signal regression tests.

### Optional Q4 — bound future file-input resource consumption

Before the UI-gated DBC file picker is enabled, add a documented maximum input size/count and a
finite timeout or non-backtracking strategy for regex parsing. The supplied DBC set (largest file
about 2.2 MB) parses successfully, but the public parser currently has no explicit consumption cap.

### Optional Q5 — repository-wide formatter baseline

`dotnet format Simulate.sln --no-restore --verify-no-changes` reports pre-existing EOL/charset debt,
including locked UI/code-behind files. The Task 14 file passes targeted formatting. No bulk
normalization was performed because it would create unrelated diffs and touch UI-owned files.

## Terra xhigh independent Axis Standards/security review — 2026-08-14

This independent review checks only Axis 2 quality against the typed hardware boundary, the parser
trust boundary, existing tests, and the project UI lock. It confirms the lead's Q1–Q3 findings; no
production/UI file, package, project configuration, commit, or push was changed.

### Required Q1 confirmed — expected configuration errors lose their typed code

`CanGatewayOptions` deliberately rejects zero nominal bitrate, invalid channel masks, and matching
RX/TX channels through argument exceptions. `ConnectionViewModel.ConnectAsync` catches those
expected validation exceptions (including the CAN FD checked-overflow path) but converts each to
`HardwareErrorCode.Unexpected`. This breaks the typed failure contract and makes an operator-error
state indistinguishable from an internal fault. Existing connection tests cover open-driver failure,
but not zero bitrate, same/overlapping channel, or CAN FD multiplication overflow.

Required remediation: map all option-construction validation failures to
`HardwareErrorCode.InvalidConfiguration`, retain actionable validation text, and add those four
regression cases.

### Required Q2 confirmed — cancellation hides a typed cleanup failure

After a successful session open, the cancellation branch invokes cleanup but drops the
`HardwareOperationResult` returned from `StopAsync`. The subsequent cancellation exception is
intentionally swallowed by `RunExclusiveAsync`, leaving `LastFailure` empty even when native
deactivation/close returned `StopFailed`. The normal disconnect path already preserves a failed stop
result after best-effort disposal, so the cancellation path is inconsistent with the resource
ownership/error-reporting boundary. Current tests cover cancellation before a connection and
successful blocking disconnect cleanup, but not cancellation after open with a typed stop failure.

Required remediation: always perform best-effort disposal, retain the first typed cleanup failure,
and add a deterministic test where open succeeds, cancellation wins, and stop returns `StopFailed`.

### Required Q3 confirmed — parser permits duplicate identities at the trust boundary

The parser appends every valid `BO_` message and `SG_` signal without duplicate checks. It then uses
first-match lookup for `VAL_` metadata, while later `SimulationEngine` and `SimulationViewModel`
build dictionaries or call `Single`. A duplicate normalized message identifier can therefore throw
from `ToDictionary`; a duplicate signal name can make `VAL_` or override selection ambiguous and
can throw from `Single`. The external DBC document must instead fail with a line/context parse error
at the point of intake. Existing parser tests prove multiple other malformed definitions but contain
no duplicate-message or duplicate-signal regression case.

Required remediation: add typed duplicate parse issue code(s), track normalized message keys and
per-message signal names during parsing, then add line/context tests for both cases.

### Optional Q4 retained — resource bounds before a file-input phase

The supplied DBC files parse successfully and no UI command currently loads arbitrary files. The
public parser still splits the entire input and uses regex patterns without a finite timeout or a
document-size bound. This is not a current interactive ingress, so it remains Optional in Task 15;
it becomes Required before the separately approved DBC file-input/UI phase. A later design should
choose a maximum document size and a finite/non-backtracking regex strategy compatible with the
supplied DBC corpus.

### Terra Axis Standards/security summary

- Q1, Q2, Q3: **Required — confirmed**.
- Q4/Q5: **Optional — retained**; no new Critical finding.
- Security boundary: DBC text is untrusted parser input; no secret, vulnerable NuGet package, UI,
  package, or project-configuration concern was found in this review.
- Independent verification: build 0 warnings/0 errors; full test suite 158/158 PASS; NuGet
  vulnerability audit and tracked-source secret-pattern scan PASS.
- Task 15 remains **NOT DONE** until the confirmed backend remediation is implemented, tested,
  reviewed, and the UI-gated shutdown decision receives separate user approval.

## Sol ultra remediation design — 2026-08-14

Status: **DESIGN COMPLETE — IMPLEMENTATION NOT STARTED**. The design follows additive public API
evolution, keeps typed failure semantics consistent with the existing hardware boundary, and does
not authorize any UI/XAML/code-behind, package, project, commit, or push change.

### S1 contract decision — additive runtime observability

Use the existing immutable `GatewayStatistics` snapshot instead of adding a second overlapping
runtime-snapshot type:

```csharp
public interface ISimulationEngine
{
    HardwareFailure? LastFailure { get; }
    GatewayStatistics Statistics { get; }
}

public sealed class GatewayStatistics
{
    public TimeSpan? LastRoutingLatency { get; }
}
```

`SimulationViewModel` adds an observable `HardwareFailure? LastFailure` and continues projecting
the immutable `Statistics` object. No XAML binding is added.

Contract semantics:

- `LastRoutingLatency` is `null` before the first successful gateway-routed transmission in a run.
- It measures the most recent successful software gateway route with monotonic `TimeProvider`
  timestamps, from routing dispatch/transmit-gate wait through session acceptance.
- It is not ECU response time, analyzer round-trip time, CAN arbitration time, or physical bus
  latency. Those remain hardware `NEEDS_VERIFY` items.
- Echo-consumed, blocked, and scheduler-originated frames do not update this gateway-routing metric.
- `LastFailure` retains the first typed runtime/hardware failure in a run so emergency cleanup cannot
  overwrite the root cause. It remains observable after worker completion/stop and resets only when
  a subsequent `StartAsync` has passed lifecycle/session validation and begins a new run.
- `StopAsync`/`StopSchedulingAsync` keep their current exception behavior. The ViewModel refreshes
  state in `finally`, so the durable failure is projected even when stop rethrows the worker fault.

Rejected alternatives:

- Raw latency ticks: leaks clock units and repeats the reference project's conversion ambiguity.
- Incoming frame timestamps: hardware/mock timestamps do not share the engine's monotonic clock.
- A new all-purpose runtime snapshot: duplicates the existing state/statistics surface and creates
  unnecessary implementer churn for this narrow additive requirement.
- Last-write-wins cleanup failure: can hide the Task 12 typed root failure during emergency cleanup.

### Backend remediation slices and model allocation

| Slice | Scope | Lead / review | Acceptance summary |
|---|---|---|---|
| 15.1 | S1 engine contract, root failure retention, monotonic routing latency | **Sol xhigh** / **Terra xhigh** | Typed failure observable before/after stop; deterministic latency/reset tests; no physical-latency claim |
| 15.2 | S1 ViewModel projection and failure-path refresh | **Terra high** / **Luna xhigh** | `LastFailure` and latency-bearing statistics projected; stop/scheduler fault refresh runs in `finally`; no UI change |
| 15.3 | Q1/Q2 connection validation and cancel-after-open cleanup | **Terra xhigh** / **Sol xhigh** | Invalid config stays typed; first cleanup failure retained; disposal always attempted; deterministic tests |
| 15.4 | Q3 duplicate DBC boundary validation | **Terra high** / **Luna xhigh** | Typed duplicate message/signal parse errors with exact line/context; supplied DBC corpus remains valid |
| 15.5 | Full re-review and closure gate | **Sol ultra** / **Luna high + Terra xhigh** | Full build/test/stress/security/scope PASS; remaining hardware/UI items explicitly gated |

Dependency order:

```text
15.1 engine contract
    └── 15.2 ViewModel projection

15.3 connection boundary ─┐
15.4 DBC boundary ────────┼── 15.5 final re-review
15.2 S1 completion ───────┘
```

15.3 and 15.4 are independent after this design freeze and may be implemented in either order. Each
slice must start with failing public-seam tests, end with targeted tests plus full build/test, and
preserve the UI/project/solution scope gate.

## Sol xhigh slice 15.1 implementation — 2026-08-15

Status: **IMPLEMENTATION COMPLETE — TERRA XHIGH REVIEW PASS**. This slice changed only the
engine/statistics contract, implementation, public-seam tests, and compile-only test fakes. It did
not change `SimulationViewModel` behavior, XAML, code-behind, package references, project/solution
configuration, commits, or pushes.

Implemented contract:

- `ISimulationEngine.LastFailure` durably exposes the first typed hardware/runtime failure in a run.
  Receive-stream, gateway transmit, scheduler transmit, and emergency cleanup failure paths feed the
  same first-wins capture. A secondary cleanup failure cannot replace the earlier root.
- A valid `StartAsync` resets failure and routing telemetry only after disposed/running/session-open
  validation succeeds. An invalid restart preserves the previous root failure.
- `GatewayStatistics.LastRoutingLatency` is immutable `TimeSpan?`: `null` before a successful route,
  reset on a valid new run, and updated with monotonic `TimeProvider` elapsed time from routing
  dispatch/before transmit-gate wait through successful session acceptance.
- Echo-consumed, blocked, scheduler-originated, and failed transmissions do not update routing
  latency. The metric remains software gateway-routing latency; physical bus/analyzer latency stays
  `NEEDS_VERIFY`.

TDD evidence:

- RED was observed for the missing `LastFailure` API, missing scheduler capture, missing cleanup-only
  capture, missing native receive capture, and missing `LastRoutingLatency` API.
- GREEN tests cover durable failure before/after stop, first-root precedence against `StopFailed`,
  cleanup-only capture, receive-stream capture, valid/invalid restart semantics, exact deterministic
  17 ms latency, reset, and block/echo/scheduler/failure exclusions.

| Slice 15.1 gate | Result |
|---|---|
| Targeted engine/scheduler/integration | PASS — 39/39 |
| Build | PASS — 0 warnings, 0 errors |
| Full suite | PASS — 162/162 |
| High-risk stress | PASS — 39 tests × 10 runs = 390/390 |
| Targeted formatter | PASS |
| Diff/secret/UI/ViewModel/project scope | PASS |
| Physical hardware | `NEEDS_VERIFY` — no Vector bench was run |
| Commit/push | None |

## Terra xhigh independent slice 15.1 review — 2026-08-15

Result: **PASS — no Critical or Required finding**. The review followed the two axes in
`code-review` against the frozen S1 contract and the implementation/test diff.

Axis Spec:

- `LastFailure` is additive, preserves the first typed root before and after stop, captures the
  receive-stream contract error, and allows emergency cleanup only to fill an otherwise empty state.
- A valid start clears failure/latency only after lifecycle and open-session validation; an invalid
  start leaves the prior root failure intact.
- `LastRoutingLatency` is nullable and immutable in the statistics snapshot, measures the frozen
  software routing interval, and remains untouched by block, echo, scheduler, and failed routes.

Axis Standards and quality:

- The capture/reset operations use appropriate atomic primitives; no new ownership, UI, package, or
  project boundary was introduced.
- `ICanGatewaySession.TransmitAsync` maps native transmit failures to `HardwareOperationResult`; the
  direct `HardwareOperationException` receive-stream contract is covered by the new outer capture.
- Tests assert public seams and use a manually advanced monotonic clock, rather than timing the host.
- No security, resource-growth, or performance finding was introduced by this slice.

Independent verification: build 0 warning/0 error; focused engine/scheduler/integration 39/39;
full suite 162/162; high-risk stress 39 tests × 10 runs = 390/390; targeted formatter,
`git diff --check`, high-signal secret scan, and UI/XAML/code-behind/ViewModel/project/solution scope
all PASS. No files were changed during the review; no commit or push was performed.

## Terra high slice 15.2 implementation — 2026-08-15

Status: **IMPLEMENTATION COMPLETE — LUNA XHIGH REVIEW PASS**. This ViewModel-only slice adds no
XAML, binding path, code-behind, package, project/solution, commit, or push change.

- `SimulationViewModel.LastFailure` is an observable projection of `ISimulationEngine.LastFailure`.
  An unconfigured ViewModel consistently returns `null` alongside `GatewayStatistics.Empty`.
- `StopAsync` and `StopSchedulingAsync` now refresh in `finally`. The original
  `HardwareOperationException` instance is rethrown, and there is no `ConfigureAwait(false)`, so the
  existing caller synchronization-context behavior remains intact.
- Public-seam RED→GREEN tests cover missing failure projection, safe unconfigured defaults, and both
  stop fault paths; the existing delayed-stop test still proves caller-context property notification.

| Slice 15.2 gate | Result |
|---|---|
| Targeted ViewModel tests | PASS — 9/9 |
| Build | PASS — 0 warnings, 0 errors |
| Full suite | PASS — 165/165 |
| Targeted formatter / diff check | PASS |
| UI/XAML/code-behind/project/solution scope | PASS |
| Commit/push | None |

## Luna xhigh independent slice 15.2 review — 2026-08-15

Result: **PASS — no Critical or Required finding**. The review followed the `code-review` Spec and
Standards axes against the frozen S1 contract and the ViewModel/test diff.

- Axis Spec: projection/default/fault-refresh behavior matches the contract; latency-bearing
  statistics remain the existing immutable engine snapshot; no binding or UI scope was added.
- Axis Standards: the ViewModel remains a thin caller-owned projection, `finally` preserves refresh
  on success and fault paths, and the original `HardwareOperationException` is not wrapped or replaced.
- Independent verification: ViewModel 9/9; full suite 165/165; targeted formatter, `git diff --check`,
  and UI/XAML/code-behind/project/solution scope PASS.
- No production file was changed during review; no commit or push was performed. Slice 15.2 is DONE.

## Terra xhigh slice 15.3 implementation — 2026-08-15

Status: **IMPLEMENTATION COMPLETE — SOL XHIGH REVIEW PASS**. This connection-boundary slice adds no
XAML, binding path, code-behind, package, project/solution, commit, or push change.

- Q1 maps option-construction `ArgumentException` and `OverflowException` to typed
  `HardwareErrorCode.InvalidConfiguration`. Zero bitrate, same channel, overlapping masks, and CAN FD
  data-bitrate overflow all return before `OpenGatewaySessionAsync` is invoked.
- Q2 always attempts stop then dispose after a cancelled open. It preserves the first cleanup failure:
  `StopFailed` wins over a later dispose exception, while a dispose exception remains observable when
  stop itself succeeds. The session is never published as connected after cancellation.
- Public-seam RED→GREEN tests cover all four Q1 inputs and the Q2 stop-failure/dispose-exception race;
  existing normal disconnect and command-availability coverage stays intact.

| Slice 15.3 gate | Result |
|---|---|
| Targeted ConnectionViewModel tests | PASS — 11/11 |
| Cancellation stress | PASS — 110/110 across 10 runs |
| Build | PASS — 0 warnings, 0 errors |
| Full suite | PASS — 170/170 |
| Targeted formatter / diff / secret check | PASS |
| UI/XAML/code-behind/project/solution scope | PASS |
| Commit/push | None |

## Sol xhigh independent slice 15.3 review — 2026-08-15

Result: **PASS — no Critical or Required finding**. The review followed the `code-review` Spec and
Standards axes against the frozen Q1/Q2 contract and the connection ViewModel/test diff.

- Axis Spec: all four invalid configurations retain `InvalidConfiguration` and never reach driver
  open; cancel-after-open always attempts stop/dispose, preserves typed `StopFailed` over a later
  dispose exception, and never publishes the cancelled session as connected.
- Axis Standards: the fix stays at the connection boundary, preserves normal disconnect/session
  ownership, uses deterministic public seams, and adds no security or performance regression.
- Independent sequential verification: build 0/0; focused 11/11; full 170/170; stress 110/110;
  targeted formatter, `git diff --check`, secret and UI/XAML/project/solution scope PASS.
- A parallel formatter/build/test attempt transiently produced WPF generated-entry-point `CS5001`;
  App/project/UI had no diff and immediate sequential build passed. Subsequent .NET gates must run
  sequentially to avoid generated `obj` contention.
- No production file was changed during review; no commit or push was performed. Slice 15.3 is DONE.

## Review/design baseline verification

| Gate | Status | Evidence |
|---|---|---|
| Build | PASS | `dotnet build Simulate.sln --no-restore`: 0 warnings, 0 errors |
| Full tests | PASS | 158/158 |
| High-risk stress | PASS | Engine + scheduler + integration: 35 tests × 10 runs = 350/350 |
| Task 14 targeted formatter | PASS | `SimulationIntegrationTests.cs` |
| Repository-wide formatter | FAIL (baseline) | Existing EOL/charset debt; no files changed |
| NuGet vulnerability audit | PASS | No vulnerable direct/transitive packages from configured sources |
| Secret-pattern scan | PASS | 0 tracked-text candidate files |
| New binary/log scan | PASS | No new `.log`/trace/dump/capture artifact |
| UI/XAML/code-behind diff | PASS | No Task 15 change; no backend-task change to App/MainWindow UI files |
| Commit/push | PASS | None performed during Task 15 lead review |

Supply-chain note: `Doc/vxlapi_NET.dll` and `Simulate/lib/vxlapi_NET.dll` both have version
`25.20.14.0`, strong-name token `9b9ef2c94571ded1`, and SHA-256
`4A2B5CD702B6DC50ADDD2C631C057C5D0FA2AB84C119BAEDBB0EDBDE9B983CEA`. The managed wrapper is not
Authenticode-signed; the two native Vector DLLs in `Doc/` have valid Authenticode signatures.
Matching the wrapper to the official target-machine Vector installation remains `NEEDS_VERIFY`.

## Consolidated hardware status

| Verification item | Status | Reason/evidence |
|---|---|---|
| Fake Vector lifecycle and typed status mapping | PASS | Unit/contract tests |
| In-memory Classic/FD gateway and scheduler soak | PASS | Task 14 integration/stress tests |
| Real Vector device and driver discovery | NEEDS_VERIFY | No physical device evidence captured |
| Classic V3 physical RX/TX/flush/cancel/cleanup | NEEDS_VERIFY | Run the Classic checklist on a safe bench |
| CAN FD V4 ISO mode, BRS/DLC and bit timing | NEEDS_VERIFY | Run the CAN FD checklist with a compatible peer/analyzer |
| Inject/E2E payload observed by analyzer | NEEDS_VERIFY | Mock/fake evidence cannot prove bus output |
| Echo behavior, routing latency, timestamp and CPU | NEEDS_VERIFY | Requires physical loopback and runtime measurement |
| One-shot/cyclic/event timing and pause boundary | NEEDS_VERIFY | Deterministic tests PASS; hardware jitter is unmeasured |
| Emergency stop during real native failure | NEEDS_VERIFY | Requires safe fault-injection bench |
| 50-cycle real-hardware open/RX/TX/stop soak | NEEDS_VERIFY | In-memory 500-cycle evidence is not hardware evidence |
| Graceful cleanup when the application window closes | FAIL (`UI_GATED`) | No shutdown hook currently invokes session cleanup |

Detailed bench procedures:

- [`vector-classic-can-hardware-checklist.md`](vector-classic-can-hardware-checklist.md)
- [`vector-can-fd-hardware-checklist.md`](vector-can-fd-hardware-checklist.md)

## Next review sequence

1. Reviews, the **Sol ultra** design, and slices 15.1–15.3 are complete without changing UI.
2. Switch to **Terra high** for slice 15.4, followed by independent **Luna xhigh** review.
3. Continue slice 15.5 only after slice 15.4 passes its RED→GREEN and review gates.
   The shutdown hook remains paused until the user explicitly permits the exact UI/code-behind change.
4. Re-run build/full tests/stress/scope gates. Only then may Task 15 and Checkpoint E be marked DONE.
