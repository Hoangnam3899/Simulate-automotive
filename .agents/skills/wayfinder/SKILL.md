---
name: wayfinder
description: Plan a huge chunk of work as a shared map of decisions, resolving them one at a time until the route is clear.
---

# Wayfinder

A loose idea has arrived — too big for one agent session, and wrapped in fog. Wayfinding is about charting the route as a **shared map** using local markdown files, then working its **decisions** one at a time until the route is clear.

## The Map

The map lives in `docs/wayfinder-map.md`. It is an index, not a store.

```markdown
# Destination
<what reaching the end of this map looks like>

## Decisions so far
- [Decision 1](wayfinder/001-decision.md) - Gist of the answer

## Open Decisions (The Frontier)
- [Decision 2](wayfinder/002-decision.md) - Blocked by nothing
- [Decision 3](wayfinder/003-decision.md) - Blocked by Decision 2

## Not yet specified (Fog of War)
<vague questions that aren't sharp enough to be tickets yet>
```

## The Decisions (Tickets)

Each decision lives in `docs/wayfinder/NNN-slug.md`. It contains:
1. The core question to resolve.
2. The resolution (once decided).

## Execution Loop

1. **Load Map**: Read `docs/wayfinder-map.md`.
2. **Pick Frontier**: Find an open decision that is not blocked.
3. **Resolve**: Use other skills (`interview-me`, `domain-modeling`) to resolve the question.
4. **Record**: Update the decision file with the answer, move it to "Decisions so far" in the map, and unblock any dependent decisions.
5. **Clear Fog**: If resolving the decision makes the "Fog of War" clearer, convert those fuzzy ideas into concrete new Open Decisions.
