---
name: code-review
description: Conducts multi-axis code review. Merges Matt Pocock's Two-Axis framework (Spec vs Standards) with Addy Osmani's 5-criteria quality gate.
---

# Code Review

Every change gets reviewed before merge. Review the changes between `HEAD` and a fixed point (commit, branch) along two distinct axes. 

## Axis 1: Spec (Does it match requirements?)

Look for the originating spec or PRD.
Report on:
- Requirements that are missing or partial.
- Behavior in the diff that wasn't asked for (scope creep).
- Requirements that look implemented but the implementation looks wrong.

## Axis 2: Standards & Quality (Is it good code?)

Evaluate the code against these five criteria:

1. **Correctness**: Edge cases handled? Error paths handled?
2. **Readability & Simplicity**: Are names descriptive? Could this be done in fewer lines?
3. **Architecture**: Clean boundaries? Does the refactor actually reduce complexity?
4. **Security**: Input validated? Secrets out of code?
5. **Performance**: No unbounded loops or N+1 queries?

**The Smell Baseline (Matt Pocock):**
Watch for: Mysterious Name, Duplicated Code, Feature Envy, Data Clumps, Primitive Obsession, Repeated Switches, Shotgun Surgery, Divergent Change, Speculative Generality, Message Chains, Middle Man, Refused Bequest.

## The Output

Present the findings categorized by Axis. Do NOT merge the two axes — a change can pass the Standards axis but fail the Spec axis, or vice versa.

Label every finding with a Severity from `.agents/references/review-severity.md`:
- **Critical**: Blocks merge (Security/Data loss)
- **Required**: Must fix (Architecture/Test gap)
- **Optional**: Suggestion (Code smells)
- **Nit**: Style preference
- **FYI**: Context only

End with a summary of findings.
