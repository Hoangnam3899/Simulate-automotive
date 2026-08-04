---
name: spec-driven-development
description: Creates specs before coding. Use when starting a new project, feature, or significant change. Merges Addy Osmani's 4-phase spec process with Matt Pocock's domain modeling and seam-identification principles.
---

# Spec-Driven Development

Write a structured specification before writing any code. The spec is the shared source of truth between you and the human engineer — it defines what we're building, why, and how we'll know it's done.

## The Pipeline

If the requirement is unclear or vague, do NOT start here. The pipeline is:
`Unclear requirement → interview-me / grill-with-docs → domain-modeling → spec-driven-development → planning-and-task-breakdown`

## Phase 1: Specify

Start with a high-level vision. 

**1. Surface assumptions immediately.** Before writing any spec content, list what you're assuming. Do not turn assumptions into requirements silently.

**2. Domain Vocabulary Check.** Read the root `CONTEXT.md` (if it exists). Ensure the spec uses the ubiquitous language exactly as defined. Do not introduce synonyms.

**3. ADR Relevance Check.** Before specifying implementation details, check if a previous ADR in `docs/adr/` dictates the architectural approach.

**4. Write the Spec Document:**
- **Objective (Problem Statement)**: Separate the problem statement from the implementation decision. What is the business/user problem?
- **Commands**: Build, test, run.
- **Project Structure**: Where files go.
- **Code Style**: Key conventions.
- **Testing Strategy & Seams**: (CRITICAL) Explicitly identify the **public test seams**. Where will this feature be observed from the outside? Do not test private implementation details.
- **Boundaries**: Always do / Ask first / Never do.

## Phase 2: Plan

Generate a technical implementation plan (saved as an artifact or to `tasks/plan.md`). 
- Identify major components.
- Determine implementation order.
- Note risks and mitigations.

## Phase 3: Tasks

Break the plan into discrete, implementable tasks. Follow the `planning-and-task-breakdown` skill for exact mechanics. Each task must have verifiable acceptance criteria.

## Phase 4: Implement

Execute tasks one at a time following `incremental-implementation` and `test-driven-development`.
