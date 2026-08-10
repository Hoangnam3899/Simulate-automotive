# Simulate — Agent Instructions

> **MANDATORY FOR ALL SESSIONS / BẮT BUỘC TRONG MỌI PHIÊN LÀM VIỆC:**
> Agent (Antigravity, Codex, Claude Code, Cursor, Copilot, Gemini, v.v.) **BẮT BUỘC PHẢI ĐỌC TẤT CẢ TỆP LUẬT VÀ SKILL DÙ Ở BẤT CỨ HOÀN CẢNH NÀO** trước khi thực hiện bất kỳ thao tác sửa đổi hay phản hồi nào:
> 1. Read `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`
> 2. Read `PROJECT_RULES_COMBINED.md`
> 3. Read `.agents/skills/using-agent-skills/SKILL.md` hoặc `.agents/SKILL-CATALOG.md` để chọn và áp dụng đúng skill từ `.agents/skills/`

## Project Overview

This is a **C# WPF desktop application** built with .NET 8.0. The project source lives in `Simulate.sln`.

## Commands

| Command | Description |
|---------|-------------|
| `dotnet build` | Build the solution |
| `dotnet test` | Run unit tests |
| `dotnet run --project Simulate` | Run the application |

## Key Conventions

- **Language**: C# (.NET 8.0), XAML for UI
- **Architecture**: MVVM (Model-View-ViewModel) with `CommunityToolkit.Mvvm`
- **Tests**: colocated in test projects within the solution
- **Git**: trunk-based development, conventional commits (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`)

## Boundaries & Strict Rules

- **ALWAYS** read all rules (`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `PROJECT_RULES_COMBINED.md`) and relevant skills (`.agents/skills/`) at the start of every session without exception.
- **NEVER** modify any UI component/XAML file without explicit user permission (`user_global`).
- **NEVER** commit secrets, API keys, or credentials to version control.
- **NEVER** modify files outside the solution without explicit user permission.
- **Ask first** before adding NuGet package dependencies.
- **Ask first** before changing project structure or solution configuration.
- **ALWAYS** run `dotnet build` after code changes to verify compilation.

## Agent Skills

This project uses a unified skill system. Skills are located in `.agents/skills/`.

To discover which skill to use for a given task, read [`.agents/SKILL-CATALOG.md`](.agents/SKILL-CATALOG.md) or invoke the `using-agent-skills` skill at [`.agents/skills/using-agent-skills/SKILL.md`](.agents/skills/using-agent-skills/SKILL.md).

## Domain Context

If a `CONTEXT.md` file exists at the root, read it before starting work to understand the project's domain vocabulary and terminology conventions.
