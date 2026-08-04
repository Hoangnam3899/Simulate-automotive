# Simulate — GitHub Copilot Instructions

## Project

C# WPF desktop application. Solution: `Simulate.sln`. Architecture: MVVM.

## Code Style

- C# with .NET, XAML for UI
- MVVM pattern (Model-View-ViewModel)
- Named types over anonymous objects
- Explicit access modifiers on all members
- XML doc comments on public API members

## Conventions

- Conventional commits: `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`
- Build verification: `dotnet build`
- Test execution: `dotnet test`

## Boundaries

- Never commit secrets or credentials
- Never modify files outside the solution without permission
- Ask before adding NuGet dependencies
- Ask before changing project/solution structure

## Agent Skills

This project uses agent skills located in `.agents/skills/`. Read `.agents/SKILL-CATALOG.md` for the full catalog and routing guidance.

## Domain

If `CONTEXT.md` exists at the project root, read it to understand domain vocabulary before generating code.
