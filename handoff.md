# Handoff — Task 13 DONE; next Task 14

## Source of truth

- Task contract and phase state: [`tasks/plan.md`](tasks/plan.md).
- Acceptance criteria and work log: [`tasks/todo.md`](tasks/todo.md).
- Workspace rules: `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `PROJECT_RULES_COMBINED.md`, and `CONTEXT.md`.

## Current repository state

- Branch: `chore/merge-agent-skills`, three commits ahead of `origin/chore/merge-agent-skills` before Task 13 is committed.
- Latest local commit is Task 12: `377475a feat: add simulation scheduler lifecycle`.
- Task 13 is DONE but remains uncommitted and unpushed. Do not commit or push without a new user instruction.

## Accomplished

- Terra xhigh implemented Task 13 and fixed both Required review findings; Sol xhigh independently re-reviewed the result with no remaining Critical/Required finding.
- `SimulationViewModel` projects the immutable plan into the existing `Messages`, `Signals`, and `FaultQueue` binding paths and exposes explicit backend lifecycle/state refresh methods without owning hardware/session resources.
- `MainViewModel` preserves existing binding paths and removes hardcoded simulation demo rows; production Vector construction is isolated in `ApplicationComposition`.
- A delayed-engine synchronization-context regression protects WPF-observable state refresh after incomplete asynchronous operations.
- Verification: targeted formatter and CRLF checks PASS; build 0 warnings/0 errors; focused 6/6; full suite 154/154; `git diff --check` and UI/XAML/code-behind/project/solution scope PASS.

## Next action — Task 14

- Switch to **Luna xhigh** as lead; **Sol xhigh** is the reviewer.
- Add integration and soak tests through the in-memory hardware path for connect → gateway start → pass/block/inject/schedule → stop/disconnect.
- Cover Classic CAN and CAN FD contracts, post-stop transmission exclusion, bounded resource/task behavior, and stable reconnect state after failures.
- Keep UI/XAML/code-behind locked. Ask before any package, project, or solution configuration change.

## Boundaries and edge cases

- The default `MainViewModel` intentionally has an unconfigured, empty `SimulationViewModel`; no UI path composes a DBC plan/engine or invokes simulation controls yet.
- Runtime state refresh remains explicit; live polling/binding is outside Task 13.
- Real hardware echo and latency behavior remains `NEEDS_VERIFY`; Task 14 uses the in-memory path.
- Checkpoint D latency/error observability and Checkpoint E remain open for later tasks.

## Suggested skills

1. `test-driven-development` for Task 14 RED→GREEN integration cases.
2. `observability-and-instrumentation` for measurable soak/resource assertions.
3. `incremental-implementation` to land the integration scenarios in bounded slices.
4. `code-review` for the Sol xhigh independent review.
5. `git-workflow-and-versioning` only after the user requests a commit or push.
