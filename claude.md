# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Cymbal is a .NET MSBuild task that bundles debug symbols (PDB files) with deployed applications, enabling stack trace line numbers for exceptions in production. It works in two phases: copying PDBs from NuGet references at build time, and downloading missing symbols via `dotnet-symbol` at publish time.

## Build Commands

All commands run from the `src/` directory. The solution file is `src/Cymbal.slnx`.

```bash
dotnet build                              # Debug build
dotnet build -c Release                   # Release build (generates NuGet package)
dotnet build -c IncludeTask               # Build with task included (for testing)
dotnet test                               # Run all tests
dotnet test --filter "Name~HasEmbedded"   # Run a single test by name
```

Restore the local dotnet-symbol tool (needed for integration tests):
```bash
dotnet tool restore --tool-manifest src/.config/dotnet-tools.json
```

## Architecture

**Main task library** (`src/Cymbal/`, targets netstandard2.0):
- `CymbalTask` — MSBuild task entry point implementing `ICancelableTask`. Scans published DLLs for missing symbols and orchestrates downloads.
- `SymbolDownloader` — Builds `dotnet-symbol` arguments, parses output to detect success/failure per assembly.
- `Dotnet` (ProcessRunner) — Executes dotnet processes with a 4.5-minute timeout.
- `SymbolChecker` — Uses `PEReader` to detect embedded portable PDB symbols, skipping assemblies that don't need external PDBs.

**MSBuild integration** (`src/Cymbal/build/Cymbal.targets`):
- `IncludeSymbolFromReferences` target — runs after `ResolveAssemblyReferences` to copy co-located PDB files from NuGet packages.
- `CymbalTarget` — runs after `Publish` to download missing symbols via `dotnet-symbol`.

**Tests** (`src/Tests/`, targets net10.0):
- NUnit with Verify snapshot testing (`.verified.txt` files are baselines).
- Integration tests build and publish `SampleApp` and `SampleWithSymbolServer` using CliWrap, then verify output with `Verify()`.
- Tests are parameterized with `[Values]` for cache configuration variants (environment variable vs MSBuild property).
- Tests shut down the build server on dispose (skipped on CI via `BuildServerDetector`).

**Sample projects** under `src/`: `SampleApp`, `SampleWithSymbolServer`, `AssemblyWithEmbeddedSymbols`, `AssemblyWithNoSymbols`, `AssemblyWithPdb` — used by integration tests.

## Build Configuration

- Three build configurations: `Debug`, `Release`, `IncludeTask`
- Central package version management via `src/Directory.Packages.props`
- `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` (in `src/Directory.Build.props`)
- `LangVersion=preview` with `ImplicitUsings` enabled
- NuGet package is only generated on `Release` builds
- The task DLL targets `netstandard2.0`; tests target `net10.0`

## CI

GitHub Actions workflows in `.github/workflows/`:
- `on-push-do-docs.yml` — runs MarkdownSnippets to sync code snippets in docs
- `merge-dependabot.yml` — auto-merges minor dependabot PRs
- `milestone-release.yml` — syncs milestones with GitHub releases
