# Domain docs

How engineering skills should consume this repository's domain documentation.

## Before exploring

Read the following when they exist and are relevant to the task:

- `CONTEXT.md` at the repository root
- ADRs in `docs/adr/`
- Relevant material in `docs-guide/` for CAN, CAN FD, AUTOSAR E2E, or Vector XL
  work

If these files do not exist yet, proceed silently. Create or update them only when a domain term or architectural decision needs to be recorded.

## Layout

This is a single-context repository:

```text
/
|-- CONTEXT.md
|-- docs/
|   |-- adr/
|   `-- agents/
`-- Simulate/
```

## Vocabulary and decisions

Use terms defined in `CONTEXT.md` consistently in code, tickets, and tests. If a proposed change conflicts with an ADR, surface the conflict explicitly instead of silently overriding the decision.

## Mandatory reference corpus

`docs-guide/` contains project-provided standards and API references. It is
mandatory source material for plans, designs, logic, code, tests, and reviews in
its subject areas. If the corpus contains conflicting versions or requirements,
identify the conflict and ask for a decision; do not silently select one.

For every automotive task, also follow the applicability assessment in
`docs/agents/automotive-standards.md`.
