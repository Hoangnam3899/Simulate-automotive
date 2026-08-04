# Review Severity Levels

Used by the `code-review` skill to classify findings.

| Severity | Meaning | Action Required |
|----------|---------|-----------------|
| **Critical** | Bug, security vulnerability, data loss risk, or spec violation that would break users | Must fix before merge |
| **Required** | Standards violation, missing test, architectural concern | Must fix before merge |
| **Optional** | Improvement suggestion, better pattern available, minor readability issue | Author's discretion |
| **Nit** | Style preference, naming suggestion, formatting | Author's discretion |
| **FYI** | Information, observation, or context for the author | No action needed |

## Guidelines

- **Critical and Required** findings block merge — they must be addressed
- **Optional and below** are suggestions — the author decides whether to act
- Every finding must cite its source: a documented standard, the spec, or the smell baseline
- Code smell findings (from the baseline) are always **Optional** severity — they are judgment calls, not hard violations
- If a documented project standard endorses something the smell baseline would flag, the standard wins — suppress the smell
