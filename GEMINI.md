# Simulate — Gemini Instructions

> Read this file at the start of every session. All skills live in `.agents/skills/`.

## Project

C# WPF desktop application. Solution: `Simulate.sln`. Architecture: MVVM.

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project Simulate`

## Code Conventions

- C# with .NET, XAML for UI
- MVVM pattern (Model-View-ViewModel)
- Conventional commits: `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`
- Named types over anonymous objects
- Explicit access modifiers on all members

## Boundaries

- Never commit secrets or credentials
- Never modify files outside the solution without permission
- Ask before adding NuGet dependencies
- Ask before changing project/solution structure
- Always verify build after changes: `dotnet build`

## Skills

Skills are in `.agents/skills/`. Read `.agents/SKILL-CATALOG.md` for the full catalog.

When starting a complex task, consult the catalog to find the right skill before proceeding.

## Domain

If `CONTEXT.md` exists at the root, read it first — it defines the project's ubiquitous language and domain terms.
