---
name: test-driven-development
description: Drives development with tests. Merges Addy Osmani's TDD loop with Matt Pocock's principles on seams, mocks, and anti-tautological tests.
---

# Test-Driven Development

Write a failing test before writing the code that makes it pass. For bug fixes, reproduce the bug with a test before attempting a fix. Tests are proof — "seems right" is not done.

## The TDD Cycle

```
    RED                GREEN              REFACTOR
 Write a test    Write minimal code    Clean up the
 that fails  ──→  to make it pass  ──→  implementation  ──→  (repeat)
      │                  │                    │
      ▼                  ▼                    ▼
   Test FAILS        Test PASSES         Tests still PASS
```

Work in **vertical slices** — one test, one implementation, repeat. Do not write all tests upfront (horizontal slicing).

## The Prove-It Pattern (Bug Fixes)

When a bug is reported, **do not start by trying to fix it.** Start by writing a test that reproduces it.

## Testing Principles

### 1. Test at Boundaries (Seams)
Test behavior through the public interface, never by mocking internal parts or asserting on private state. A **seam** is the public boundary you test at. Before writing any test, identify the seams under test.

### 2. Anti-Tautological Assertions
Expected values must come from an independent source of truth (a literal, a known-good calculation). Do not recompute the expected value the same way the code computes it, otherwise the test passes by construction.

### 3. State, Not Interactions
Assert on the *outcome* of an operation, not on which methods were called internally. Tests that verify method call sequences break when you refactor, even if the behavior is unchanged.

### 4. DAMP Over DRY
In tests, **DAMP (Descriptive And Meaningful Phrases)** is better than DRY. A test should read like a specification. Duplication in test setup is acceptable if it makes the test independently understandable.

## Mocking Rules

Mock at **system boundaries** only:
- External APIs and services
- Databases (prefer test DB when possible)
- Time and randomness
- File system (sometimes)

**Don't mock**:
- Your own classes/modules
- Internal collaborators
- Anything you control

Design for mockability by using dependency injection and SDK-style interfaces rather than generic fetchers.

## Reference

See `.agents/references/testing-patterns.md` for more details on these principles.
