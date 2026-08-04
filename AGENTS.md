 Simulate — Agent Instructions

> This file is the root entry point for AI coding agents (Codex, Claude Code, Cursor, Copilot, Gemini, etc.).
> All agent skills, references, and configuration live in [`.agents/`](.agents/).

## Project Overview

This is a **C# WPF desktop application** built with .NET. The project source lives in `Simulate.sln`.

## Commands

| Command | Description |
|---------|-------------|
| `dotnet build` | Build the solution |
| `dotnet test` | Run unit tests |
| `dotnet run --project Simulate` | Run the application |

## Key Conventions

- **Language**: C# (.NET), XAML for UI
- **Architecture**: MVVM (Model-View-ViewModel)
- **Tests**: colocated in test projects within the solution
- **Git**: trunk-based development, conventional commits (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`)

## Boundaries

- **Never** commit secrets, API keys, or credentials to version control
- **Never** modify files outside the solution without explicit user permission
- **Ask first** before adding NuGet package dependencies
- **Ask first** before changing project structure or solution configuration
- **Always** run `dotnet build` after code changes to verify compilation

## Agent Skills

This project uses a unified skill system. Skills are located in `.agents/skills/`.

To discover which skill to use for a given task, read [`.agents/SKILL-CATALOG.md`](.agents/SKILL-CATALOG.md) or invoke the `using-agent-skills` skill.

## Domain Context

If a `CONTEXT.md` file exists at the root, read it before starting work to understand the project's domain vocabulary and terminology conventions.
