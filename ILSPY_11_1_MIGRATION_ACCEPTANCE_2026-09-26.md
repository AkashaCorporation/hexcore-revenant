# Revenant 0.5.0 / ILSpy 11.1 Migration Acceptance

Date: 2026-09-26

## Scope

- Wrapper: HexCore Revenant 0.4.0 -> 0.5.0
- Decompiler: ICSharpCode.Decompiler 10.1.0.8386 -> 11.1.0.9782
- Runtime: self-contained single-file `win-x64`, .NET 8 host
- No analyzed target was executed. Both engines only read managed assemblies and
  .NET single-file apphosts.

## Compatibility change

ILSpy 11.1 adds `IAssemblyResolver.BeginSnapshot()`. Revenant's immutable
in-memory bundle resolver delegates that lifecycle to its
`UniversalAssemblyResolver` fallback, the only component that consults the
filesystem. Version output is derived from the loaded decompiler assembly
instead of a hardcoded string.

## Artifact

| Field | Value |
|---|---|
| Path | `publish/win-x64/revenant-engine.exe` |
| Size | 36,155,173 bytes |
| SHA-256 | `F04334FEEF9B9FA27689D5B30342FE81A3865821CBA0F480127A660663CD58A2` |
| Wrapper identity | `revenant-engine: 0.5.0` |
| Decompiler identity | `ICSharpCode.Decompiler: 11.1.0.9782` |
| Build | clean, zero warnings |

The retained 0.4.0 engine used for comparison has SHA-256
`B1E659B4901C9D97A275EC212C08FD84A5EBC1E9838C3E59E65898668E91DBC5`.

## Automated gates

- TypeScript compile: pass.
- Revenant suite: 20 passed, 0 failed.
- The real single-file integration gate was enabled and passed against:
  - `NFS Most Wanted -XMG CriptoMiner Virus.exe`
  - SHA-256 `06E33FEFBDC89873F0C6052AB98E5F3B2107EB4ED086C905CB2540AAD39C85E2`
  - Expected Defender-exclusion, download and hidden-launch indicators recovered.
- Self-contained publish and release archive creation succeeded for `win-x64`,
  `linux-x64`, `osx-x64`, and `osx-arm64`.
- HexCore's engine manifest records `runtimeValidation:"version-probe"` and the
  expected decompiler pin. Runtime smoke requires package `0.5.0`, wrapper
  `0.5.0`, and decompiler `11.1.0.9782` to match exactly.

| RID | Engine SHA-256 | Archive SHA-256 |
|---|---|---|
| win-x64 | `F04334FEEF9B9FA27689D5B30342FE81A3865821CBA0F480127A660663CD58A2` | `234453FEF62007338DABFA03EBDBF0E21F99EF55FD7B926D6999E0EE453C0EC7` |
| linux-x64 | `043224B567BD2C16BF276DC4B58D17A9EBD1EEE29EDB0F4268A705804B5488AC` | `3F62AE0DBF1C3F9B2B55FEFDADFB07097A6AA776EEEBB670CE2DACB5B8540743` |
| osx-x64 | `B6E807BEB50196ADA72F6499415D0E9CC47ADE4E07A0AA7D72DC2D7AA40B711B` | `D6DCD03ECB97728FFBEB4288414B1BBD51118F10FEE9A488DC6FDAD480B6FB8E` |
| osx-arm64 | `65F9454B6DC5012FFECC697F1C840404736028F34DA6E17EF83DCA4FDA8C99EC` | `011E4CB8FDC00FF3C79DC604088F98C9C9E1C849EA82FBE100C8C4F21DA6D6E7` |

## HX-SH-009 dependency-collision acceptance

- Target: `Grammarly.Desktop.Dialog.exe`, SHA-256
  `66716DB20DE20092BB3BE904C99244959D7A86FB85D414BA9532641F35309A61`.
- Colliding native NSIS plugin: `System.dll`, SHA-256
  `8B4C47C4CF5E76EC57DD5A050D5ACD832A0D532EE875D7B44F6CDAF68F90D37C`.
- Before: C# returned exit 1 with `PE file does not contain any managed metadata`;
  IL returned exit 0.
- After: C# and IL return exit 0 in the original plugin directory.
- Original-directory C# and isolated-control C# are byte-identical at the raw
  stdout boundary: 8,230 bytes, SHA-256
  `E52D1E34EBEE1E5E8B7084C4615F88A15A70FC7736F5114F462EE9515C0E72A0`.
- A deterministic regression places a native PE at `mscorlib.dll` beside a
  managed fixture and proves that framework resolution continues.

## 10.1 versus 11.1

| Corpus | 10.1 | 11.1 | Result |
|---|---:|---:|---|
| `Bypass.exe` C# | 238 lines | 238 lines | byte-identical |
| `Bypass.exe` IL | 691 lines | 691 lines | byte-identical |
| `Accessibility.dll` | 373 lines | 361 lines | parameterized COM properties recovered as accessor methods |
| `System.Data.OracleClient.dll` mixed-mode | 25,971 lines | 25,911 lines | same type/goto/IL-residue counts; idiomatic/type-inference changes |
| NFS single-file apphost | 114 lines | 114 lines | two anonymous delegates modernized to lambdas |
| `update.exe` single-file apphost | 122 lines | 122 lines | byte-identical |

No case crashed or returned a non-zero exit code. The 11.1 controls were roughly
20-30% slower on this small matrix, while retaining or improving output quality.

## Residual gates

- GitHub Actions still needs to reproduce and publish all four release RIDs;
  non-Windows artifacts were cross-published but not executed locally.
- The historical 24-assembly Unity sweep and Roslyn compile corpus were not
  repeated in this local gate.
- Packaged HexCore acceptance must verify that provenance reports
  `backendVersion:0.5.0`, `decompilerVersion:11.1.0.9782`, and
  `toolVersion:11.1.0.9782`.

## Verdict

Accepted for synchronization into the HexCore 3.8.5 development tree and for a
packaged release-candidate smoke. Publication remains a separate decision.
