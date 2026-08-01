# Automotive standards policy

This policy applies to every automotive design, plan, code change, test, and
review. It does not certify the product; it requires a traceable decision about
which standards apply before work begins.

## Required assessment

Before work, record in the plan or task brief:

1. The feature boundary and whether it can affect a vehicle, ECU, bus, diagnostic
   interface, test bench, or only an offline simulation.
2. Each applicable standard or an explicit `not applicable` decision with a reason.
3. The controlled source and edition used: a project-provided document, licensed
   standard, or official standards catalogue. Model memory is not a source.
4. The requirement, hazard, threat, or protocol constraint that drives the design.
5. Verification evidence: review, unit/integration test, test bench result, or
   conformance evidence appropriate to the claim.

## Standards to assess

Assess only the standards relevant to the task; this is a baseline, not a claim
that every one applies to every feature.

| Area | Standard family to assess | Apply when |
| --- | --- | --- |
| Functional safety | ISO 26262 | The work contributes to or can affect the functional safety of a road-vehicle E/E item. |
| Cybersecurity | ISO/SAE 21434 | The work exposes, changes, or relies on a vehicle E/E interface, diagnostics, network, credentials, or update path. |
| Intended functionality safety | ISO 21448 (SOTIF) | The work involves safety-relevant intended functionality, situational awareness, sensors, or automated-driving behavior. |
| CAN protocol and physical communication | ISO 11898 family | The work implements, configures, validates, or claims behavior of CAN/CAN FD communication. |
| CAN conformance testing | ISO 16845 family | The work makes a CAN or CAN FD protocol conformance claim. |
| Diagnostic communication over CAN | ISO 14229 and ISO 15765 families | The work implements or validates UDS or DoCAN diagnostics. |
| AUTOSAR E2E protection | AUTOSAR E2E specification and the supplied E2E reference | The work creates, changes, validates, or injects faults into E2E-protected communication. |
| Vector hardware access | Vector XL API reference corpus | The work uses Vector XL hardware, drivers, or API calls. |

## Decision rules

- The supplied `docs-guide/` corpus is mandatory where it covers the task.
- Use the edition approved by the project or customer. If no controlled edition is
  available, identify the gap and ask; do not reconstruct requirements from memory
  or an internet summary.
- When requirements conflict, surface the exact clauses/versions and wait for a
  decision. Never silently choose the newer-looking source.
- Safety, security, protocol-conformance, and regulatory claims require traceable
  requirements and verification evidence. A passing unit test alone is not a
  compliance claim.
- An offline UI, simulation, or test tool may still need a safety/security review
  when it can connect to, send to, or influence a vehicle or test bus.

## Required output for automotive work

Every plan and final handoff must include a short **Standards assessment**:

```md
## Standards assessment

- Scope: [offline simulation / test bench / vehicle-facing interface]
- Applicable: [standard + edition + controlled source]
- Not applicable: [standard + reason]
- Constraints applied: [traceable requirement or protocol rule]
- Verification evidence: [test, review, or bench evidence]
- Compliance claim: [none / bounded claim with evidence]
```

Use `Compliance claim: none` unless the project has supplied sufficient controlled
requirements and evidence for a bounded, reviewable claim.
