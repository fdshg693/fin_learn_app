## Project Overview

Stock trading simulator (株売買シュミレートアプリ) built with DDD + TDD methodology. Currently focused on .NET Core backend; React frontend is planned but not yet implemented.

## Development Methodology

**TDD is mandatory.** Always write test code BEFORE implementation code. The workflow is: write a failing test, implement the minimum code to pass, then refactor.

**DDD domain questions:** When uncertain about domain concepts, ask the user first, then update `docs/DDD.md` with clarifications.

## Build & Test Commands

```shell
# Build
dotnet build

# Run all tests
dotnet test

# TDD watch mode (recommended during development)
cd tests/MyApp.Tests && dotnet watch test
```

## Skill
always use `write-docs` SKILL whenever you write documents.