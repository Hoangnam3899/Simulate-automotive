# Domain docs

How engineering skills should consume this repository's domain documentation.

## Before exploring

Read the following when they exist and are relevant to the task:

- `CONTEXT.md` at the repository root
- ADRs in `docs/adr/`

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
