# Testing Patterns Reference

Shared testing principles referenced by `test-driven-development` and `code-review` skills.

## What Makes a Good Test

A good test:
- **Verifies behavior** through public interfaces, not implementation details
- **Reads like a specification** — the test name tells you exactly what capability exists
- **Survives refactors** — code can change entirely; the test shouldn't break unless behavior changes
- **Has one logical assertion** — tests one thing clearly
- **Uses independent expected values** — not recomputed the way the code computes them

## What Makes a Bad Test

### Implementation-Coupled Tests
Tests that mock internal collaborators, test private methods, or verify through side channels. The tell: the test breaks when you refactor but behavior hasn't changed.

### Tautological Tests
The assertion recomputes the expected value the way the code does. Expected values must come from an independent source of truth — a known-good literal, a worked example, the spec.

### Horizontal Slicing
Writing all tests first, then all implementation. Work in **vertical slices** instead — one test → one implementation → repeat.

## Seams — Where Tests Go

A **seam** is the public boundary you test at: the interface where you observe behavior without reaching inside. Tests live at seams, never against internals.

**Test only at pre-agreed seams.** Before writing any test, identify the seams under test and confirm them with the user.

## When to Mock

Mock at **system boundaries** only:
- External APIs and services
- Databases (prefer test DB when possible)
- Time and randomness
- File system (sometimes)

**Don't mock** your own classes, internal collaborators, or anything you control.

### Designing for Mockability

1. **Accept dependencies, don't create them** — use dependency injection
2. **Return results, don't produce side effects** — prefer pure functions
3. **Small surface area** — fewer methods = fewer tests needed
4. **Prefer SDK-style interfaces** — each function independently mockable

## Test Naming

Test names describe WHAT the system does, not HOW:

```
GOOD: "user can checkout with valid cart"
BAD:  "checkout calls paymentService.process"

GOOD: "createUser makes user retrievable"
BAD:  "createUser saves to database"
```
