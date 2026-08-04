# Definition of Done

Every task must clear these criteria before it counts as done. These are the standing bar — individual tasks add their own acceptance criteria on top.

## Code Quality
- [ ] Code compiles without errors (`dotnet build`)
- [ ] No new compiler warnings introduced
- [ ] All existing tests pass (`dotnet test`)
- [ ] New code has tests where a correct seam exists
- [ ] Code follows project conventions (naming, structure, patterns)

## Review
- [ ] Changes reviewed (self-review at minimum, peer review for significant changes)
- [ ] No `TODO` or `HACK` comments left unresolved without a tracking issue
- [ ] No commented-out code (use version control instead)
- [ ] No debug/console output left in production code

## Documentation
- [ ] Public API members have XML doc comments
- [ ] Non-obvious decisions have inline comments explaining *why*
- [ ] ADR written if the change involves a significant architectural decision
- [ ] README or relevant docs updated if setup/usage changed

## Git
- [ ] Commits are atomic (one logical change per commit)
- [ ] Commit messages follow conventional format (`feat:`, `fix:`, etc.)
- [ ] No secrets in the diff
- [ ] No unrelated formatting or refactoring mixed with behavior changes

## Security
- [ ] No hardcoded secrets or credentials
- [ ] User input validated at system boundaries
- [ ] No new security warnings from static analysis
