using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;

namespace HexCore.Revenant;

internal sealed class SingleFileInput : IDisposable
{
	private const int MaxBundleEntries = 4096;
	private const long MaxAssemblySize = 512L * 1024 * 1024;
	private const long MaxTotalAssemblySize = 1024L * 1024 * 1024;

	private readonly BundleAssemblyResolver resolver;

	private SingleFileInput(BundleAssemblyResolver resolver, PEFile mainAssembly)
	{
		this.resolver = resolver;
		MainAssembly = mainAssembly;
	}

	public PEFile MainAssembly { get; }
	public IAssemblyResolver Resolver => resolver;

	public static bool TryOpen(string fileName, out SingleFileInput? input)
	{
		input = null;
		using var mappedFile = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
		using var view = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
		if (!SingleFileBundle.IsBundle(view, out long headerOffset)) { return false; }

		long fileLength = new FileInfo(fileName).Length;
		if (headerOffset < 0 || headerOffset > fileLength - 12)
		{
			throw new InvalidDataException("single-file bundle header is outside the host");
		}
		uint majorVersion = view.ReadUInt32(headerOffset);
		int declaredFileCount = view.ReadInt32(headerOffset + 8);
		if (majorVersion is < 1 or > 6 || declaredFileCount <= 0 || declaredFileCount > MaxBundleEntries)
		{
			throw new InvalidDataException($"invalid single-file bundle header: version {majorVersion}, entries {declaredFileCount}");
		}

		SingleFileBundle.Header manifest = SingleFileBundle.ReadManifest(view, headerOffset);
		if (manifest.FileCount != declaredFileCount || manifest.Entries.Length != manifest.FileCount)
		{
			throw new InvalidDataException($"invalid single-file bundle entry count: {manifest.FileCount}");
		}

		var assemblies = manifest.Entries.Where(entry => entry.Type == SingleFileBundle.FileType.Assembly).ToList();
		if (assemblies.Count == 0) { throw new InvalidDataException("single-file bundle contains no managed assemblies"); }

		long totalSize = 0;
		foreach (SingleFileBundle.Entry entry in assemblies)
		{
			ValidateEntry(entry, fileLength);
			totalSize = checked(totalSize + entry.Size);
			if (totalSize > MaxTotalAssemblySize)
			{
				throw new InvalidDataException($"single-file bundle assemblies exceed the {MaxTotalAssemblySize / (1024 * 1024)} MiB limit");
			}
		}

		SingleFileBundle.Entry selected = SelectMainAssembly(manifest, assemblies);
		var resolver = new BundleAssemblyResolver(fileName, assemblies);
		try
		{
			PEFile mainAssembly = resolver.Load(selected);
			input = new SingleFileInput(resolver, mainAssembly);
			return true;
		}
		catch
		{
			resolver.Dispose();
			throw;
		}
	}

	public void Dispose() => resolver.Dispose();

	private static void ValidateEntry(SingleFileBundle.Entry entry, long fileLength)
	{
		long storedSize = entry.CompressedSize > 0 ? entry.CompressedSize : entry.Size;
		if (entry.Offset < 0 || entry.Size <= 0 || storedSize <= 0 || entry.Size > MaxAssemblySize)
		{
			throw new InvalidDataException($"invalid single-file assembly entry: {entry.RelativePath}");
		}
		if (entry.Offset > fileLength - storedSize)
		{
			throw new InvalidDataException($"single-file assembly entry is outside the host: {entry.RelativePath}");
		}
		ValidateRelativePath(entry.RelativePath);
	}

	private static SingleFileBundle.Entry SelectMainAssembly(SingleFileBundle.Header manifest, List<SingleFileBundle.Entry> assemblies)
	{
		var appNames = manifest.Entries
			.Where(entry => entry.Type is SingleFileBundle.FileType.RuntimeConfigJson or SingleFileBundle.FileType.DepsJson)
			.Select(entry => BundleFileName(entry.RelativePath))
			.Select(name => name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase)
				? name[..^".runtimeconfig.json".Length] + ".dll"
				: name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
					? name[..^".deps.json".Length] + ".dll"
					: string.Empty)
			.Where(name => name.Length > 0)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		return assemblies.FirstOrDefault(entry => appNames.Contains(BundleFileName(entry.RelativePath)), assemblies[0]);
	}

	private static void ValidateRelativePath(string relativePath)
	{
		string normalized = relativePath.Replace('\\', '/');
		if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || normalized.Contains(':'))
		{
			throw new InvalidDataException($"unsafe single-file entry path: {relativePath}");
		}
		string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
		{
			throw new InvalidDataException($"unsafe single-file entry path: {relativePath}");
		}
	}

	private static string BundleFileName(string relativePath)
	{
		string normalized = relativePath.Replace('\\', '/');
		int slash = normalized.LastIndexOf('/');
		return slash >= 0 ? normalized[(slash + 1)..] : normalized;
	}

	private sealed class BundleAssemblyResolver : IAssemblyResolver, IDisposable
	{
		private readonly string hostFileName;
		private readonly Dictionary<string, SingleFileBundle.Entry> entries;
		private readonly Dictionary<string, PEFile> loaded = new(StringComparer.OrdinalIgnoreCase);
		private readonly ManagedAssemblyResolver fallback;

		public BundleAssemblyResolver(string hostFileName, IEnumerable<SingleFileBundle.Entry> assemblyEntries)
		{
			this.hostFileName = hostFileName;
			entries = assemblyEntries
				.GroupBy(entry => BundleFileName(entry.RelativePath), StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
			fallback = new ManagedAssemblyResolver(hostFileName, targetFramework: null);
		}

		public PEFile Load(SingleFileBundle.Entry entry)
		{
			string key = BundleFileName(entry.RelativePath);
			if (loaded.TryGetValue(key, out PEFile? cached)) { return cached; }

			byte[] image = ReadEntry(entry);
			var stream = new MemoryStream(image, writable: false);
			try
			{
				var peFile = new PEFile(entry.RelativePath, stream, PEStreamOptions.PrefetchEntireImage);
				loaded.Add(key, peFile);
				return peFile;
			}
			catch
			{
				stream.Dispose();
				throw;
			}
		}

		public MetadataFile? Resolve(IAssemblyReference reference)
		{
			if (entries.TryGetValue(reference.Name + ".dll", out SingleFileBundle.Entry entry) ||
				entries.TryGetValue(reference.Name + ".exe", out entry))
			{
				return Load(entry);
			}
			return fallback.Resolve(reference);
		}

		public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference) => Task.FromResult(Resolve(reference));

		public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
		{
			if (entries.TryGetValue(BundleFileName(moduleName), out SingleFileBundle.Entry entry)) { return Load(entry); }
			return fallback.ResolveModule(mainModule, moduleName);
		}

		public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName) =>
			Task.FromResult(ResolveModule(mainModule, moduleName));

		// ILSpy 11.1 resolves one reference closure against a stable filesystem
		// snapshot. Bundle entries are immutable in-memory images; only the
		// UniversalAssemblyResolver fallback needs to own snapshot state.
		public IDisposable? BeginSnapshot() => fallback.BeginSnapshot();

		public void Dispose()
		{
			foreach (PEFile peFile in loaded.Values)
			{
				try { peFile.Dispose(); } catch { }
			}
			loaded.Clear();
		}

		private byte[] ReadEntry(SingleFileBundle.Entry entry)
		{
			using var source = new FileStream(hostFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			source.Position = entry.Offset;
			long storedSize = entry.CompressedSize > 0 ? entry.CompressedSize : entry.Size;
			using var slice = new BoundedReadStream(source, storedSize);
			using var output = new MemoryStream(checked((int)entry.Size));
			if (entry.CompressedSize > 0)
			{
				using var inflater = new DeflateStream(slice, CompressionMode.Decompress, leaveOpen: true);
				CopyExactly(inflater, output, entry.Size, entry.RelativePath);
			}
			else
			{
				CopyExactly(slice, output, entry.Size, entry.RelativePath);
			}
			return output.ToArray();
		}

		private static void CopyExactly(Stream input, Stream output, long expectedSize, string relativePath)
		{
			byte[] buffer = new byte[81920];
			long total = 0;
			while (total < expectedSize)
			{
				int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, expectedSize - total));
				if (read == 0) { throw new InvalidDataException($"single-file assembly is truncated: {relativePath}"); }
				output.Write(buffer, 0, read);
				total += read;
			}
			if (input.ReadByte() != -1)
			{
				throw new InvalidDataException($"single-file assembly exceeds its declared size: {relativePath}");
			}
		}
	}

	private sealed class BoundedReadStream : Stream
	{
		private readonly Stream source;
		private long remaining;

		public BoundedReadStream(Stream source, long length)
		{
			this.source = source;
			remaining = length;
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (remaining == 0) { return 0; }
			int read = source.Read(buffer, offset, (int)Math.Min(count, remaining));
			remaining -= read;
			return read;
		}
		public override int Read(Span<byte> buffer)
		{
			if (remaining == 0) { return 0; }
			int read = source.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
			remaining -= read;
			return read;
		}
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
