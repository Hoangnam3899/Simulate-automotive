---
name: grill-with-docs
description: A relentless interview to sharpen a plan or design, which also creates docs (ADRs and glossary) as we go.
---

# Grill with Docs

This skill combines requirement interrogation with domain documentation. It is used when the user has an idea, but it needs to be heavily stress-tested, and the vocabulary needs to be formalized.

## The Process

1. **Invoke the Interview**: Start by asking the user deep, challenging questions about their proposed plan. Ask ONE question at a time.
2. **Focus on Trade-offs**: Push the user on edge cases, failure modes, scale limits, and "why not the obvious alternative?".
3. **Formalize Vocabulary**: As domain terms emerge, document them in the root `CONTEXT.md` (refer to the `domain-modeling` skill for format).
4. **Record Decisions**: When a hard trade-off is resolved, create an ADR in `docs/adr/` capturing the context and the decision.
5. **Stop**: Once the plan is airtight and no more ambiguities exist, summarize the final design.
