// HexCore Revenant engine -- the Phase-2 self-contained backend.
//
// A thin wrapper around ILSpy's MIT-licensed ICSharpCode.Decompiler. Published
// self-contained single-file (`dotnet publish -r <rid> --self-contained
// -p:PublishSingleFile=true`) so the user receives a portable binary with no
// .NET install and no library downloads -- exactly the native-prebuilt model.
//
// CLI is a superset-compatible subset of ilspycmd's flags, so the TS runner's
// argument shape is unchanged when it swaps from system-ilspycmd to this binary:
//   revenant-engine <assembly> [--ilcode] [-t <fullTypeName>] [-r <refDir>]
//   revenant-engine --version
// C# (or IL with --ilcode) is written to stdout; errors to stderr with a
// non-zero exit code. Never prints a partial result on failure.

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

const string Version = "0.2.0";

string? assembly = null;
bool il = false;
string? type = null;
var refDirs = new List<string>();

for (int i = 0; i < args.Length; i++)
{
	string a = args[i];
	switch (a)
	{
		case "--ilcode":
		case "-il":
			il = true;
			break;
		case "-t":
		case "--type":
			if (i + 1 < args.Length) { type = args[++i]; }
			break;
		case "-r":
		case "--referencepath":
			if (i + 1 < args.Length) { refDirs.Add(args[++i]); }
			break;
		case "--version":
			Console.WriteLine($"revenant-engine: {Version}");
			Console.WriteLine("ICSharpCode.Decompiler: 8.2.0.7535");
			return 0;
		default:
			if (!a.StartsWith('-') && assembly is null) { assembly = a; }
			break;
	}
}

if (assembly is null)
{
	Console.Error.WriteLine("usage: revenant-engine <assembly> [--ilcode] [-t <fullTypeName>] [-r <refDir>]");
	return 2;
}
if (!File.Exists(assembly))
{
	Console.Error.WriteLine($"file not found: {assembly}");
	return 2;
}

try
{
	if (il)
	{
		using var pe = new PEFile(assembly);
		var output = new PlainTextOutput();
		var disassembler = new ReflectionDisassembler(output, CancellationToken.None);
		disassembler.WriteModuleContents(pe);
		Console.Out.Write(output.ToString());
	}
	else
	{
		var resolver = new UniversalAssemblyResolver(assembly, throwOnError: false, targetFramework: null);
		foreach (string dir in refDirs)
		{
			if (Directory.Exists(dir)) { resolver.AddSearchDirectory(dir); }
		}
		var decompiler = new CSharpDecompiler(assembly, resolver, new DecompilerSettings());
		string code = type is null
			? decompiler.DecompileWholeModuleAsString()
			: decompiler.DecompileTypeAsString(new FullTypeName(type));
		Console.Out.Write(code);
	}
	return 0;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"revenant-engine error: {ex.Message}");
	return 1;
}
