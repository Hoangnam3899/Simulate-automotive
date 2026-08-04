---
name: using-agent-skills
description: Discover and select the right skill for a task. Use when you're unsure which skill to apply, when starting a new task, or when the user asks "what skills are available."
---

# Using Agent Skills

This project uses a skill-driven execution model. Skills are structured workflows in `.agents/skills/` that guide you through specific types of work.

## How Skills Work

1. Each skill is a directory containing a `SKILL.md` file with YAML frontmatter (`name`, `description`) and detailed instructions
2. Some skills include reference files (additional `.md` files in the same directory)
3. Skills are composable — one skill can reference another

## Selecting a Skill

Read `.agents/config/skill-routing.md` for the full decision tree. Quick summary:

### By SDLC Phase

| Phase | Skills |
|-------|--------|
| **Understand** | `interview-me`, `grill-with-docs`, `domain-modeling` |
| **Specify** | `spec-driven-development` |
| **Plan** | `planning-and-task-breakdown`, `wayfinder` |
| **Design** | `api-and-interface-design`, `codebase-design` |
| **Implement** | `incremental-implementation`, `test-driven-development`, `source-driven-development`, `context-engineering` |
| **Review** | `code-review`, `security-and-hardening` |
| **Debug** | `debugging-and-error-recovery`, `diagnosing-bugs` |
| **Ship** | `git-workflow-and-versioning`, `ci-cd-and-automation`, `observability-and-instrumentation`, `shipping-and-launch` |
| **Document** | `documentation-and-adrs`, `handoff` |

### By Trigger

If the user says something like:
- "I have an idea" → `interview-me`
- "challenge this plan" → `grill-with-docs`
- "write a spec" → `spec-driven-development`
- "break this down" → `planning-and-task-breakdown`
- "build this" → `incremental-implementation`
- "write tests first" → `test-driven-development`
- "review this code" → `code-review`
- "something is broken" → `debugging-and-error-recovery`
- "hard bug" / "diagnose" → `diagnosing-bugs`
- "deploy" / "ship" → `shipping-and-launch`

## Rules

1. **Read the skill before using it.** Don't skim — read the full `SKILL.md`.
2. **One skill at a time.** Don't mix workflows from different skills in the same task.
3. **Skills compose, not overlap.** When a skill references another skill, switch to that skill's workflow for that specific sub-task.
4. **Check approval gates.** Read `.agents/config/approval-gates.md` for actions that require human permission.
5. **Read CONTEXT.md.** If the project root has a `CONTEXT.md`, read it before starting any skill to understand domain vocabulary.
