# C#/.NET automotive coding standard

This standard applies to all new and modified C# code in Simulate. It complements
the automotive standards assessment; it is not a claim of MISRA, ISO 26262, or
AUTOSAR compliance.

## Source baseline

- Microsoft C# coding conventions: <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions>
- Nullable reference types: <https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/nullable-reference-types>
- .NET code analysis configuration: <https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-files>
- Applicable automotive requirements and evidence: `automotive-standards.md`

## Functions and types

- Give every method, type, and parameter one clear responsibility and a domain
  name. Do not use ambiguous names such as `Process`, `Handle`, `Data`, or `Value`
  without a meaningful qualifier.
- Keep public members small, explicit, and documented with XML comments when their
  contract is not obvious from the name and type signature.
- Validate all external, file, driver, bus, UI, and configuration input at the
  boundary. Reject invalid data before it reaches domain or hardware logic.
- Preserve nullable intent: use `T?` only when absence is valid, check it before
  dereference, and do not use the null-forgiving operator (`!`) unless its
  invariant is documented locally.
- Prefer explicit result types or domain exceptions for expected failure. Never
  swallow an exception, return a misleading default, or catch `Exception` without
  recording and preserving the failure context.
- Avoid mutable static state. Make ownership, lifetime, cancellation, and disposal
  explicit for resources, timers, streams, and driver handles.

## WPF and automotive boundaries

- Keep UI event handlers thin: they may translate UI input and trigger an
  application operation, but must not contain CAN, E2E, Vector XL, DBC, or
  fault-injection business logic.
- Isolate hardware and driver calls behind a small interface that can be mocked in
  tests. No direct Vector XL call from XAML code-behind.
- Any operation that can transmit, modify, suppress, delay, or corrupt a frame
  must be explicit, validated, cancellable, logged, and disabled by default until
  the user deliberately starts it.
- Use asynchronous APIs for blocking I/O. Do not block the WPF UI thread with
  driver, file, or bus operations.

## Traceability and verification

- Link automotive-facing behavior to the applicable standards assessment,
  project-provided reference, and verification evidence.
- Add or update focused tests for changed logic. Hardware integration requires a
  mock, simulator, or test-bench verification; a unit test alone cannot justify a
  protocol or safety conformance claim.
- Keep analyzer and compiler diagnostics clean. Do not suppress a rule without a
  documented reason, scope, and reviewable alternative control.

## Prohibited shortcuts

- No magic CAN identifiers, timing values, DLCs, fault values, or driver handles
  in business logic; define them in a named domain model or controlled
  configuration.
- No silent fallback from a failed driver or bus operation to a success-looking UI
  state.
- No production connection to a vehicle or bus as an implicit side effect of
  startup, selection, or UI rendering.
