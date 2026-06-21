# Changelog

All notable changes to HexCore Revenant are documented here.

## [0.2.0] - Unreleased - "Portable: the self-contained engine (Phase 2)"

The user now receives a **portable binary** -- no .NET install, no `ilspycmd`, no
library downloads -- exactly the native-prebuilt model. This is the shippable form;
the Phase-1 `ilspycmd` shell-out remains only as a dev fallback.

### Added
- **`engine/Revenant/`** -- the C# engine: a thin wrapper (`Program.cs`) over a pinned
  `ICSharpCode.Decompiler` 8.2.0.7535 (`Revenant.csproj`). Same CLI as ilspycmd
  (`<assembly> [--ilcode] [-t <type>] [-r <dir>] [--version]`), so the TS runner is
  unchanged across the backend swap. Wrap, not fork.
- **`.github/workflows/revenant-prebuilds.yml`** -- `dotnet publish -r <rid>
  --self-contained -p:PublishSingleFile=true` across win-x64 / linux-x64 / osx-x64 /
  osx-arm64, packaged + attached to the release. Light build, no OOM.
- **`src/ilspyRunner.ts`** -- backend resolution is now `override -> bundled engine
  (bin/<plat>/) -> system ilspycmd`. The `RevenantResult.backend` field reports which
  fired. A new test asserts the bundled engine is preferred.

### Validated
- Built `revenant-engine.exe` win-x64 (36 MB, single-file). Run with a stripped PATH
  (no `dotnet`, no `ilspycmd` reachable) it still decompiles `Bypass.exe` and
  `Accessibility.dll` -- proof it carries its own runtime + the decompiler. The runner
  picks it up (`backend: 'bundled'`); 15/15 tests pass.

## [0.1.0] - Unreleased - "First light: managed decompile via ILSpy (MVP)"

First cut of the managed (.NET / CIL) decompiler -- the "Better" tier of HexCore issue #32.
Pre-alpha: Phase 1 shells out to `ilspycmd`; Phase 2 (the bundled self-contained C#
wrapper) is not built yet.

### Added
- **`src/ilspyRunner.ts`** -- the backend. Auto-locates `ilspycmd` (config override ->
  dotnet global-tools dir -> PATH scan, to a concrete path so no shell is spawned),
  gates on a lightweight CLR-header .NET detection (PE optional-header data directory 14,
  identical to the detector in the other HexCore .NET-aware components), C# and IL modes,
  single-type (`-t`) and reference-dir (`-r`) support, and never throws -- every failure
  (not .NET, tool missing, timeout, empty output) returns a structured `{ ok: false, error }`.
- **`src/extension.ts`** -- two commands (`hexcore.revenant.decompile`,
  `hexcore.revenant.decompileIL`) with a UI path (opens the recovered C#/IL in a new
  editor with a provenance banner) and a headless `{ quiet, output }` path for the pipeline.
- **`test/revenant.test.cjs`** -- dependency-free base validation suite (14 cases):
  managed C#/IL, mixed-mode (C++/CLI) detection, native refusal, truncated / garbage /
  empty / missing inputs, `detectDotNet` units.

### Validated
- `ilspycmd` 8.2.0.7535 recovers the issue #32 repro `Bypass.exe` (the native pipeline's
  empty 85% stub) -> 235 lines of C# / 694 lines of IL; `Accessibility.dll` -> 375 lines
  of clean C#; `System.Data.OracleClient.dll` (mixed-mode) detected as managed; native
  `notepad.exe` refused without invoking the tool. `tsc` 0; 14/14 tests pass.

### Notes
- Wrapper, not a fork: a pinned upstream (`ICSharpCode.Decompiler`, MIT) is invoked, not
  modified -- the Remill-style sourcing model.
- Phase 2 will replace the `ilspycmd` shell-out with a bundled self-contained C# wrapper
  (`dotnet publish --self-contained`) emitting structured JSON, removing the user-side
  .NET runtime dependency. The command surface stays identical.
