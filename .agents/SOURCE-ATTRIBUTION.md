# Source Attribution

This unified skill set is derived from two open-source repositories, both MIT-licensed.

## Sources

### Addy Osmani — Agent Skills (Foundation)

- **Repository**: https://github.com/addyosmani/agent-skills
- **License**: MIT
- **Commit**: `bdf76c7` (cloned 2026-08-04)
- **Role**: SDLC lifecycle foundation — provides the core workflow skills

**Skills kept as-is**: `interview-me`, `planning-and-task-breakdown`, `incremental-implementation`, `context-engineering`, `source-driven-development`, `api-and-interface-design`, `debugging-and-error-recovery`, `security-and-hardening`, `git-workflow-and-versioning`, `ci-cd-and-automation`, `documentation-and-adrs`, `observability-and-instrumentation`, `shipping-and-launch`

**Skills merged (Addy base + Matt additions)**: `spec-driven-development`, `test-driven-development`, `code-review`

**Skills excluded**: `browser-testing-with-devtools`, `frontend-ui-engineering`, `code-simplification`, `code-review-and-quality`, `performance-optimization`, `idea-refine`, `doubt-driven-development`, `deprecation-and-migration`

### Matt Pocock — Skills (Value-Add)

- **Repository**: https://github.com/mattpocock/skills
- **License**: MIT
- **Commit**: `2ab9580` (cloned 2026-08-04)
- **Role**: Requirement interrogation, domain modeling, codebase design, two-axis review

**Skills kept (adapted)**: `grill-with-docs`, `domain-modeling`, `codebase-design`, `diagnosing-bugs`, `handoff`, `wayfinder`

**Skills merged into Addy base**: `tdd` → merged into `test-driven-development`, `code-review` → merged into unified `code-review`, `to-spec` → merged into `spec-driven-development`

**Skills excluded**: `grilling` (absorbed into `grill-with-docs`), `grill-me` (covered by `interview-me`), `ask-matt`, `edit-article`, `obsidian-vault`, `scaffold-exercises`, `teach`, `writing-great-skills`, `implement`, `research`, `improve-codebase-architecture`, `prototype`, `to-tickets`, `triage`, `setup-matt-pocock-skills`, `git-guardrails-claude-code`, `setup-pre-commit`, `migrate-to-shoehorn`, `resolving-merge-conflicts`, all deprecated/in-progress skills

## License

Both source repositories use the MIT License. Copies are stored in `licenses/`.

## Merge Methodology

1. Addy Osmani's lifecycle provides the structural foundation (22-step SDLC)
2. Matt Pocock's skills fill gaps: requirement interrogation, domain modeling, deep module design, hard bug diagnosis
3. Where both sources cover the same topic, a single merged skill was created with clear provenance comments
4. No two skills in the unified set give contradictory instructions
5. Web-specific examples are retained as illustrations of universal principles
