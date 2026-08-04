# Skill Routing — Decision Tree

Use this guide to select the right skill for a given task. Read top-to-bottom and take the first match.

## Phase 1: Understanding & Requirements

| Trigger | Skill | Description |
|---------|-------|-------------|
| "I have a vague idea", "what should we build", unclear requirements | `interview-me` | Structured requirement extraction |
| "stress-test this plan", "grill me", "challenge this design" | `grill-with-docs` | Relentless interrogation + domain doc generation |
| "define terms", "ubiquitous language", "what does X mean in this project" | `domain-modeling` | Build/sharpen domain glossary and ADRs |
| "write a spec", "turn this into requirements" | `spec-driven-development` | Formalize requirements into executable spec |

## Phase 2: Planning & Design

| Trigger | Skill | Description |
|---------|-------|-------------|
| "break this down", "plan the work", "estimate scope" | `planning-and-task-breakdown` | Decompose into ordered, verifiable tasks |
| "design the API", "define the interface", "module boundary" | `api-and-interface-design` | Contract-first interface design |
| "design this module", "deep vs shallow", "seam placement" | `codebase-design` | Deep module vocabulary and design principles |
| "this is too big for one session", "multi-session planning" | `wayfinder` | Map decisions across multiple sessions |

## Phase 3: Implementation

| Trigger | Skill | Description |
|---------|-------|-------------|
| "build this feature", "implement", multi-file change | `incremental-implementation` | Vertical slice execution discipline |
| "write tests first", "TDD", "red-green-refactor" | `test-driven-development` | Test-driven development loop |
| "what do the docs say", "is this API current" | `source-driven-development` | Documentation-verified implementation |
| "set up the context", "agent output quality is bad" | `context-engineering` | Optimize agent context for quality |

## Phase 4: Quality & Review

| Trigger | Skill | Description |
|---------|-------|-------------|
| "review this code", "review since X", "PR review" | `code-review` | Two-axis review (spec compliance + engineering standards) |
| "something is broken", "fix this error", build failure | `debugging-and-error-recovery` | Standard error diagnosis and recovery |
| "hard bug", "intermittent failure", "race condition", "diagnose" | `diagnosing-bugs` | Systematic hard bug diagnosis (6 phases) |
| "security review", "harden this", "handle user input" | `security-and-hardening` | Security-first development patterns |

## Phase 5: Ship & Operate

| Trigger | Skill | Description |
|---------|-------|-------------|
| "commit", "branch strategy", "release version" | `git-workflow-and-versioning` | Git practices and semantic versioning |
| "set up CI", "pipeline", "automate checks" | `ci-cd-and-automation` | CI/CD pipeline setup |
| "add logging", "metrics", "tracing", "alerting" | `observability-and-instrumentation` | Production telemetry |
| "deploy", "launch", "go live", "rollout" | `shipping-and-launch` | Pre-launch checklist and staged rollout |

## Phase 6: Documentation & Handoff

| Trigger | Skill | Description |
|---------|-------|-------------|
| "write an ADR", "document this decision", "why did we" | `documentation-and-adrs` | Decision records and documentation |
| "hand off", "continue in next session", "summarize for next agent" | `handoff` | Session continuity document |

## Meta

| Trigger | Skill | Description |
|---------|-------|-------------|
| "what skills are available", "which skill should I use" | `using-agent-skills` | Skill discovery and routing |
