# Agent Skills — Unified Skill System

This directory contains a unified set of AI agent skills for software development. Skills are composable, conflict-free instruction sets that guide coding agents through structured workflows.

## Quick Start

1. Read `SKILL-CATALOG.md` to find the right skill for your task
2. Read the skill's `SKILL.md` file before starting work
3. Follow the skill's process end-to-end

## Structure

```
.agents/
├── README.md                 ← You are here
├── SKILL-CATALOG.md          ← Full catalog with triggers and descriptions
├── SOURCE-ATTRIBUTION.md     ← Provenance and license information
├── config/
│   ├── skill-routing.md      ← Decision tree for skill selection
│   └── approval-gates.md     ← Human approval requirements
├── licenses/                 ← Source repository licenses
├── references/               ← Shared reference documents
│   ├── definition-of-done.md
│   ├── testing-patterns.md
│   └── review-severity.md
└── skills/                   ← Individual skill directories
    ├── using-agent-skills/SKILL.md
    ├── interview-me/SKILL.md
    ├── ... (22 skills total)
    └── wayfinder/SKILL.md
```

## Compatibility

These skills work with any coding agent that supports markdown instruction files:

| Agent | Entry Point |
|-------|-------------|
| OpenAI Codex | `AGENTS.md` (root) |
| Claude Code | `CLAUDE.md` (root) |
| Google Gemini | `GEMINI.md` (root) |
| GitHub Copilot | `.github/copilot-instructions.md` |
| Cursor / Windsurf | `.windsurfrules` (root) |

All entry points reference this `.agents/` directory as the single source of truth.

## Sources

Skills are selectively merged from two open-source repositories. See `SOURCE-ATTRIBUTION.md` for full provenance.
