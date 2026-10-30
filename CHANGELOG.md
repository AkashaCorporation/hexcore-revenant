# Changelog

All notable changes to HexCore Revenant are documented here.

## [0.5.0] - 2026-09-26 - "ILSpy 11.1 and truthful version provenance"

### Changed
- Updated the bundled engine from `ICSharpCode.Decompiler` 10.1.0.8386 to
  11.1.0.9782, the engine shipped with ILSpy 11.1.
- Implemented `IAssemblyResolver.BeginSnapshot()` by delegating filesystem
  snapshot ownership to the fallback `UniversalAssemblyResolver`; immutable
  in-memory bundle entries retain their existing bounded cache.
- Replaced the hardcoded decompiler version with the loaded assembly identity.
- Split `backendVersion` from `decompilerVersion`; backward-compatible
  `toolVersion` now means the decompiler engine rather than the Revenant wrapper.
- Added a managed-candidate resolver gate. A native PE whose filename collides
  with a referenced assembly beside the target is rejected without aborting C#
  recovery; resolution continues through explicit reference paths and the
  framework/GAC.

### Validation
- Existing managed DLL/EXE, IL, mixed-mode, native refusal, malformed-input,
  real single-file, and native dependency-collision gates pass 20/20.
- HX-SH-009 passes on the original Grammarly NSIS plugin directory: C# output
  is byte-identical to the hash-identical isolated control despite the adjacent
  native `System.dll`.
- `Bypass.exe` C#/IL and the 264 MB `update.exe` apphost remain byte-identical;
  the NFS apphost changes only two anonymous delegates to lambda syntax.
- Self-contained publish and archive creation pass for all four release RIDs.
- Adds a version-provenance regression for Revenant 0.5.0 and
  ICSharpCode.Decompiler 11.1.0.9782.

## [0.4.0] - 2026-08-02 - ".NET single-file apphost recovery"

### Added
- The bundled engine now uses ILSpy 10's `SingleFileBundle` API to recognize .NET apphosts,
  select the entry assembly from the bundled runtime/deps metadata, and decompile it as C# or IL.
- Bundle loading is lazy, in-memory, and bounded: manifest count, entry ranges, per-assembly and
  total uncompressed sizes are validated, and each dependency is decompressed only if ILSpy asks
  for it. No embedded assembly is executed or materialized as a temporary executable/DLL.
- The TypeScript runner now recognizes the official 32-byte .NET bundle marker with a validated
  manifest offset, so an apphost without a CLR data-directory is routed to Revenant instead of the
  native Remill -> Helix pipeline.
- Tests cover valid/invalid bundle markers and an opt-in real-sample integration gate.

### Validated
- `NFS Most Wanted.exe` SHA-256
  `06E33FEFBDC89873F0C6052AB98E5F3B2107EB4ED086C905CB2540AAD39C85E2`: recovered the embedded
  `GameSetup.dll` and its Defender exclusion, `audioupdate.online/update.zip` download, and hidden
  `update.exe` launch without executing the sample.
- `update.exe` SHA-256
  `9040A5606EEED4C9206F58234B608A1952C3942BD82916B8D4A48CF5A390BD78`: reduced a 264 MB apphost
  and the old 320,088-line runtime-heavy report to 115 lines of entry-assembly C# in 1.88 s,
  recovering the `%APPDATA%\\Sound` resource drop and hidden `uninstall.bat` launch.

## [0.3.0] - Unreleased - "Engine bump: ICSharpCode.Decompiler 8.2 -> 10.1"

Bumped the bundled engine's pinned `ICSharpCode.Decompiler` from 8.2.0.7535 to
**10.1.0.8386** (= ILSpy 10.1) after a data-driven side-by-side on a real game.
The `ilspycmd` dev-fallback pin (8.2.0.7535) is INDEPENDENT and left intact.

### Changed
- `engine/Revenant/Revenant.csproj`: `ICSharpCode.Decompiler` 8.2.0.7535 -> 10.1.0.8386
  (builds clean, zero API breaks). `Program.cs --version` updated; engine version 0.3.0.

### Added (headless pipeline)
- `src/extension.ts` now accepts the automation runner's `output` as either a bare string
  OR a `{ path }` object (`resolveOutputPath`), and the quiet/headless return mirrors
  `RevenantResult.ok` into a `success` field so the pipeline surfaces Revenant's real error
  (the registry entries that wire the commands into `COMMAND_CAPABILITIES` live in the
  consuming monorepo's `automationPipelineRunner.ts`). Without this, validated steps would
  never find the artifact at `output.path`.
- `examples/decompile-dotnet.hexcore_job.json` -- a runnable headless job: decompile a
  managed PE to C# + IL with an `onResult` rule (skip IL when `isDotNet == false`).
  Verified engine-direct: the registered command path with `{ file, output:{path}, quiet }`
  writes the artifact + returns `success:true`; native targets are rejected.

### Why (evidence)
- 8-agent side-by-side on the same Unity game (Zumbi Blocks 2 `Assembly-CSharp.dll`,
  ~89K lines): 6 deep per-class diffs + a 662-hunk programmatic classifier + a
  dropped-logic scanner + a Roslyn compile test. The 19,661-line diff vs 8.2 is **~98%
  cosmetic/idiomatic with ZERO semantic regressions**. 10.1 is net-better for RE:
  recovers real identifier names from metadata in the network deserializers where 8.2
  emitted keyword-escaped synth names (`@int`/`@byte`/`@float` -> `lastSpawnedWave` /
  `skinGender` / `amount`), resolves enum comparisons (`!= 0` -> `!= Enum.None`), and
  uses C#12 primary constructors. The one cosmetic wart (a debug GUI `string.Format`
  vs interpolation) has byte-identical runtime output.

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
