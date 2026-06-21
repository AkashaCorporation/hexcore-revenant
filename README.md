# HexCore Revenant

**Managed .NET (CIL) decompiler for HexCore.** Resurrects the real C# / IL from a
managed assembly that the native Remill -> Helix pipeline cannot read.

When HexCore points its native x86 pipeline at a .NET assembly, the CIL `.text`
mis-decodes as machine code and the decompiler emits a confident but empty
`void entry_point(void) { return; }` stub. Revenant closes that gap: it detects the
managed assembly and recovers the actual program instead.

> Status: **pre-alpha (v0.1.0, MVP).** Closes the *"Better"* tier of HexCore issue #32
> (the *"Minimum"* honesty short-circuit ships in the disassembler).

## How it works

Revenant is a **wrapper, not a fork** -- the same philosophy HexCore uses for Remill
(which links a pinned `libremill` rather than forking it). It wraps a pinned version of
[ILSpy](https://github.com/icsharpcode/ILSpy)'s MIT-licensed `ICSharpCode.Decompiler`
engine and exposes it through the HexCore IDE.

It belongs to HexCore's **"wrapped engines"** class -- engines that drive a third-party
prebuilt tool over a subprocess, as opposed to the native (N-API / `node-gyp` `.node`)
engines compiled from our own C/C++/Rust.

### Two phases

| | Phase 1 (this release) | Phase 2 (planned) |
|---|---|---|
| Backend | shells out to the `ilspycmd` global tool | a bundled self-contained C# wrapper |
| Build | none (uses an installed tool) | `dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true` per platform |
| User dependency | needs `ilspycmd` installed | none (the binary ships in the release) |
| Command surface | identical -- only the backend (`src/ilspyRunner.ts`) swaps | identical |

`dotnet publish` is light (it links a managed library and copies the runtime -- no
heavy C++/LLVM compile), so the Phase-2 prebuild does **not** risk the OOM that the
native engines do. The per-platform binary goes to the release-download model only to
keep the large self-contained artifact out of the main repo.

## Usage (in the HexCore IDE)

- **Revenant: Decompile .NET Assembly to C#** (`hexcore.revenant.decompile`)
- **Revenant: Disassemble .NET Assembly to IL** (`hexcore.revenant.decompileIL`)

Both accept a file argument, the active editor's file, or a file picker. A native PE is
refused up front (the engine never invokes the tool on non-managed input). The issue #32
honesty marker in the disassembler also offers a **"Decompile with Revenant"** action on
a managed target.

Headless / pipeline form:

```jsonc
{ "command": "hexcore.revenant.decompile",
  "options": { "file": "Sample.exe", "output": "out/Sample.cs", "quiet": true } }
```

### Configuration

| Setting | Default | Meaning |
|---|---|---|
| `hexcore.revenant.ilspyPath` | `""` | Override the `ilspycmd` path. Empty = auto-locate (override -> dotnet global-tools dir -> PATH). |
| `hexcore.revenant.timeoutMs` | `120000` | Abort a decompile after this many ms. |

## Phase 1 prerequisite

Install ILSpy's command-line tool (pinned, known-good):

```sh
dotnet tool install -g ilspycmd --version 8.2.0.7535
```

## Build & test

```sh
npm install      # @types/node, @types/vscode, typescript
npm run compile  # tsc -> out/
npm test         # node test/revenant.test.cjs (base validation suite)
```

The test suite (`test/revenant.test.cjs`) is dependency-free and covers managed C#/IL,
mixed-mode (C++/CLI) detection, native refusal, truncated / garbage / empty / missing
inputs, and the `detectDotNet` unit checks.

## License

Apache License 2.0 -- see [`LICENSE`](./LICENSE).

This product wraps ILSpy / `ICSharpCode.Decompiler` (MIT); attribution is in
[`NOTICE`](./NOTICE). The upstream tool is invoked, not modified.
