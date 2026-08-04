---
name: handoff
description: Compact the current conversation into a handoff document for another agent to pick up.
---

# Handoff

Write a handoff document summarising the current conversation so a fresh agent can continue the work. 

## Instructions

1. Save the document as an Artifact (`handoff.md`).
2. Include a "suggested skills" section in the document, which suggests skills from `.agents/SKILL-CATALOG.md` that the next agent should invoke.
3. Do not duplicate content already captured in other artifacts (specs, plans, ADRs, issues, commits, diffs). Reference them by path or URL instead.
4. Redact any sensitive information, such as API keys, passwords, or personally identifiable information.
5. Detail exactly what was accomplished, what is left to do, and any specific edge cases the next agent should watch out for.
