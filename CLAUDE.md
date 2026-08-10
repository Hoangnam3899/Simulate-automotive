# Simulate — Claude Code Instructions

> **MANDATORY FOR ALL SESSIONS / BẮT BUỘC TRONG MỌI PHIÊN LÀM VIỆC:**
> Read `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `PROJECT_RULES_COMBINED.md`, and relevant skills in `.agents/skills/` at the start of EVERY session regardless of circumstances.

## Project

C# WPF desktop application (.NET 8.0). Solution: `Simulate.sln`. Architecture: MVVM (`CommunityToolkit.Mvvm`).

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project Simulate`

## Code Conventions

- C# with .NET 8.0, XAML for UI
- MVVM pattern (Model-View-ViewModel)
- Conventional commits: `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`
- Named types over anonymous objects
- Explicit access modifiers on all members

## Boundaries

- ALWAYS read rules and skills at the start of every session without exception.
- NEVER modify UI components / XAML files without explicit user permission.
- Never commit secrets or credentials.
- Never modify files outside the solution without permission.
- Ask before adding NuGet dependencies.
- Ask before changing project/solution structure.
- Always verify build after changes: `dotnet build`.

## Skills

Skills are in `.agents/skills/`. Read `.agents/SKILL-CATALOG.md` for the full catalog or read `.agents/skills/using-agent-skills/SKILL.md`.

## Domain

If `CONTEXT.md` exists at the root, read it first — it defines the project's ubiquitous language and domain terms.
