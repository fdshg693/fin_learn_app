## Project Overview

Stock trading simulator (株売買シミュレートアプリ) built with DDD + TDD methodology. The project consists of a .NET Core backend and a React frontend located in the `frontend/` directory.

## Development Methodology

**TDD is mandatory.** Always write test code BEFORE implementation code. The workflow is: write a failing test, implement the minimum code to pass, then refactor.

**DDD domain questions:** When uncertain about domain concepts, ask the user first, then update `docs/DDD/MAIN.md` with clarifications.

## Build & Test Commands

```shell
# Build
dotnet build

# Run all tests
dotnet test

# TDD watch mode (recommended during development)
cd tests/FinLearn.Tests && dotnet watch test
```

## Documentation
Whenever you find notable fact or big picture which helps understand the project and is not yet documented, please write it down.
if that information is folder specific, write it in `{folder_name}/CLAUDE.md`. if that information is project wide, write it in `CLAUDE.md` under the root folder.

## Skill
always use `write-docs` SKILL whenever you write documents.