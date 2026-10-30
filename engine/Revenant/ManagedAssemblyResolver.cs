using ICSharpCode.Decompiler.Metadata;

namespace HexCore.Revenant;

/// <summary>
/// Resolves managed references without allowing a same-named native PE beside
/// the target to shadow a framework or reference assembly.
/// </summary>
internal sealed class ManagedAssemblyResolver : IAssemblyResolver
{
	private readonly string mainAssemblyFileName;
	private readonly string? targetFramework;
	private readonly string[] searchDirectories;
	private readonly UniversalAssemblyResolver primary;
	private readonly object syncRoot = new();

	public ManagedAssemblyResolver(
		string mainAssemblyFileName,
		string? targetFramework,
		IEnumerable<string>? searchDirectories = null)
	{
		this.mainAssemblyFileName = Path.GetFullPath(mainAssemblyFileName);
		this.targetFramework = targetFramework;
		this.searchDirectories = (searchDirectories ?? [])
			.Where(Directory.Exists)
			.Select(Path.GetFullPath)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		primary = CreateResolver(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
	}

	public MetadataFile? Resolve(IAssemblyReference reference)
	{
		lock (syncRoot)
		{
			var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var rejectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			UniversalAssemblyResolver resolver = primary;

			while (true)
			{
				string? fileName = resolver.FindAssemblyFile(reference);
				if (fileName is null) { return null; }

				string fullPath = Path.GetFullPath(fileName);
				if (!rejectedFiles.Add(fullPath)) { return null; }

				MetadataFile? resolved = TryOpenManagedAssembly(fullPath);
				if (resolved is not null) { return resolved; }

				string? directory = Path.GetDirectoryName(fullPath);
				if (directory is null || !excludedDirectories.Add(directory)) { return null; }
				resolver = CreateResolver(excludedDirectories);
			}
		}
	}

	public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference) =>
		Task.FromResult(Resolve(reference));

	public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
	{
		string? baseDirectory = Path.GetDirectoryName(mainModule.FileName);
		if (baseDirectory is null) { return null; }
		return TryOpenManagedModule(Path.Combine(baseDirectory, moduleName));
	}

	public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName) =>
		Task.FromResult(ResolveModule(mainModule, moduleName));

	public IDisposable? BeginSnapshot() => primary.BeginSnapshot();

	private UniversalAssemblyResolver CreateResolver(IReadOnlySet<string> excludedDirectories)
	{
		var resolver = new UniversalAssemblyResolver(
			mainAssemblyFileName,
			throwOnError: false,
			targetFramework: targetFramework);

		string? mainDirectory = Path.GetDirectoryName(mainAssemblyFileName);
		if (mainDirectory is not null && excludedDirectories.Contains(mainDirectory))
		{
			resolver.RemoveSearchDirectory(mainDirectory);
		}

		foreach (string directory in searchDirectories)
		{
			if (!excludedDirectories.Contains(directory))
			{
				resolver.AddSearchDirectory(directory);
			}
		}
		return resolver;
	}

	private static MetadataFile? TryOpenManagedAssembly(string fileName)
	{
		PEFile? peFile = null;
		try
		{
			peFile = new PEFile(fileName);
			if (!peFile.Metadata.IsAssembly)
			{
				peFile.Dispose();
				return null;
			}
			return peFile;
		}
		catch (BadImageFormatException)
		{
			peFile?.Dispose();
			return null;
		}
		catch (MetadataFileNotSupportedException)
		{
			peFile?.Dispose();
			return null;
		}
		catch (IOException)
		{
			peFile?.Dispose();
			return null;
		}
	}

	private static MetadataFile? TryOpenManagedModule(string fileName)
	{
		if (!File.Exists(fileName)) { return null; }
		PEFile? peFile = null;
		try
		{
			peFile = new PEFile(fileName);
			_ = peFile.Metadata;
			return peFile;
		}
		catch (BadImageFormatException)
		{
			peFile?.Dispose();
			return null;
		}
		catch (MetadataFileNotSupportedException)
		{
			peFile?.Dispose();
			return null;
		}
		catch (IOException)
		{
			peFile?.Dispose();
			return null;
		}
	}
}
