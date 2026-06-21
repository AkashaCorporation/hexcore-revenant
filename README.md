# HexCore Revenant

**Managed .NET (CIL) decompiler for HexCore.** It resurrects the real **C# / IL** from a
managed assembly that the native `Remill → Helix` pipeline can't read — and ships as a
**portable, self-contained binary**: no .NET install, no `ilspycmd`, no downloads.

When HexCore points its native x86 pipeline at a .NET assembly, the CIL `.text` mis-decodes
as machine code and the decompiler emits a confident-but-empty
`void entry_point(void) { return; }` stub. Revenant closes that gap: it detects the managed
assembly and recovers the actual program instead. It's the **"Better" tier of HexCore issue #32**
(the "Minimum" honesty short-circuit ships in the disassembler).

> **Status: v0.3.0.** Portable engine shipped. Wraps `ICSharpCode.Decompiler` **10.1.0.8386**.
> Validated on a real Unity game (see below).

---

## What it can do (validated)

Run against the Unity game *Zumbi Blocks 2* (`Assembly-CSharp.dll`):

- **Whole-game recovery:** 89,025 lines of C# / 752 classes / 0 errors in ~20 s.
- **Robustness:** a 24-assembly sweep (4 KB … 4.5 MB, incl. `mscorlib` at 325,999 lines)
  decompiled **24/24, 1,821,359 lines, 0 failures** — run with a *stripped PATH* (proves portability).
- **Quality:** an adversarial audit scored it **9.3/10**, on par with or cleaner than ILSpy/dnSpy —
  zero `goto`, zero stub bodies, **zero non-compilable output** (a Roslyn compile-gate confirmed it),
  with generics, iterator state-machines→`yield`, switch-expressions, tuples and pattern-matching
  all reconstructed correctly.

## How it works

Revenant is a **wrapper, not a fork** — the same philosophy HexCore uses for Remill (which links a
pinned `libremill` rather than forking it). It wraps a **pinned** version of
[ILSpy](https://github.com/icsharpcode/ILSpy)'s MIT-licensed `ICSharpCode.Decompiler` engine and
exposes it through the HexCore IDE. The engine is invoked **unmodified** (raw passthrough), so it's
trivial to follow upstream: bumping the pin is a one-line, low-risk change.

It belongs to HexCore's **"wrapped engines"** class — engines that drive a third-party prebuilt tool
over a **subprocess**, as opposed to the native (N-API / `node-gyp` `.node`) engines compiled from
our own C/C++/Rust.

### Two layers

```
extensions/hexcore-revenant/   (TS) the VS Code surface: commands + the ilspyRunner backend
engine/Revenant/               (C#) the self-contained engine: a thin wrapper over ICSharpCode.Decompiler
.github/workflows/             (CI) dotnet publish -> per-platform portable binaries (release assets)
```

### Backend resolution (Phase 1 → Phase 2)

`src/ilspyRunner.ts` resolves the decompiler backend in priority order:

| order | backend | notes |
|------:|---------|-------|
| 1 | config override (`hexcore.revenant.ilspyPath`) | explicit path |
| 2 | **bundled self-contained engine** (`bin/<plat>/revenant-engine`) | **Phase 2 — the shipped default; no user dependency** |
| 3 | system `ilspycmd` | **Phase 1 — dev fallback only** (pinned 8.2.0.7535, *independent* of the engine version) |

`RevenantResult.backend` reports which one fired. `dotnet publish` is light (no C++/LLVM compile), so
the per-platform prebuild never OOMs — the binary goes to the release-download model only to keep the
~36 MB self-contained artifact out of the main repo.

## Usage (in the HexCore IDE)

- **Revenant: Decompile .NET Assembly to C#** (`hexcore.revenant.decompile`)
- **Revenant: Disassemble .NET Assembly to IL** (`hexcore.revenant.decompileIL`)

Both accept a file argument, the active editor's file, or a file picker. A native PE is refused up
front (the engine never invokes the tool on non-managed input). The issue #32 honesty marker in the
disassembler also offers a **"Decompile with Revenant"** action on a managed target.

Headless / pipeline form:

```jsonc
{ "command": "hexcore.revenant.decompile",
  "options": { "file": "Sample.exe", "output": "out/Sample.cs", "quiet": true } }
```

### Configuration

| setting | default | meaning |
|---------|---------|---------|
| `hexcore.revenant.ilspyPath` | `""` | Override the backend path. Empty = auto-locate (override → bundled → `ilspycmd`). |
| `hexcore.revenant.timeoutMs` | `120000` | Abort a decompile after this many ms. |

## Build & test

The engine (C#) — produces the portable binary:

```sh
dotnet publish engine/Revenant/Revenant.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -o publish/win-x64
```

The extension (TS) — backend resolution + tests:

```sh
npm install        # @types/node, @types/vscode, typescript
npm run compile    # tsc -> out/
npm test           # node test/revenant.test.cjs  (15-case base suite)
```

The suite covers managed C#/IL, mixed-mode (C++/CLI) detection, native refusal, truncated / garbage /
empty / missing inputs, the bundled-vs-`ilspycmd` backend choice, and the `detectDotNet` units.

**Phase 1 dev fallback** (optional — only if you run without the bundled binary):
```sh
dotnet tool install -g ilspycmd --version 8.2.0.7535
```

## License

Apache License 2.0 — see [`LICENSE`](./LICENSE). This product wraps ILSpy / `ICSharpCode.Decompiler`
(MIT); attribution is in [`NOTICE`](./NOTICE). The upstream engine is invoked, not modified.
