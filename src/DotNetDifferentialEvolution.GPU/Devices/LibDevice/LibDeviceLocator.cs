namespace DotNetDifferentialEvolution.GPU.Devices.LibDevice;

/// <summary>The host platform for libdevice discovery. Any value but <see cref="Windows"/> and <see cref="Linux"/> does no discovery.</summary>
internal enum LocatorPlatform
{
    /// <summary>Windows: <c>CUDA_PATH</c>, then the versioned directories under <c>%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA</c>.</summary>
    Windows = 0,

    /// <summary>Linux: <c>CUDA_PATH</c>, <c>CUDA_HOME</c>, <c>/usr/local/cuda</c>, then <c>/usr/local/cuda-*</c>.</summary>
    Linux = 1,

    /// <summary>Any other platform: nothing is tried.</summary>
    Other = 2,
}

/// <summary>Where libnvvm and libdevice were found, and every path examined on the way.</summary>
/// <param name="Dll">The libnvvm library, or <see langword="null"/> when none was found.</param>
/// <param name="Bitcode">The <c>libdevice.10.bc</c> beside it, or <see langword="null"/>.</param>
/// <param name="Tried">Every library and bitcode path examined, in order.</param>
internal sealed record LibDeviceLocation(string? Dll, string? Bitcode, IReadOnlyList<string> Tried)
{
    /// <summary>Gets a value indicating whether both files were found.</summary>
    public bool Found => Dll is not null && Bitcode is not null;
}

/// <summary>
/// Finds libnvvm and libdevice in the order BOOT.md fixes. Adapted from APThermo
/// (<c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>src/Execution/LibDevice/LibDeviceLocator.cs</c>) without its explicit path pair: the package
/// has no options object, and a caller-owned accelerator brings its own context.
/// </summary>
internal static class LibDeviceLocator
{
    private const string WindowsDllName = "nvvm64_40_0.dll";
    private const string LinuxDllName = "libnvvm.so";

    /// <summary>The bitcode's file name, the same on every platform.</summary>
    public const string BitcodeName = "libdevice.10.bc";

    /// <summary>Gets the platform's libnvvm file name, named in a message when it was not found.</summary>
    public static string LibraryFileName => DllName(CurrentPlatform());

    /// <summary>Finds the two files on this machine.</summary>
    /// <returns>The paths, or nulls, with every path examined.</returns>
    public static LibDeviceLocation Locate()
    {
        var platform = CurrentPlatform();
        return Locate(platform, Environment.GetEnvironmentVariable, DefaultGlobRoot(platform, Environment.GetEnvironmentVariable));
    }

    /// <summary>
    /// The seam the tests drive: <paramref name="platform"/> in place of <see cref="OperatingSystem"/>,
    /// <paramref name="environment"/> in place of <see cref="Environment.GetEnvironmentVariable(string)"/>, and
    /// <paramref name="globRoot"/> in place of the platform's own base directory under which the versioned toolkit
    /// directories are found.
    /// </summary>
    /// <param name="platform">The platform whose layout is searched.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="globRoot">The base directory of the versioned toolkit directories.</param>
    /// <returns>The paths, or nulls, with every path examined.</returns>
    internal static LibDeviceLocation Locate(LocatorPlatform platform, Func<string, string?> environment, string globRoot)
    {
        var tried = new List<string>();
        if (platform == LocatorPlatform.Other)
        {
            return new LibDeviceLocation(null, null, tried);
        }

        var dllName = DllName(platform);
        foreach (var root in ToolkitRoots(platform, environment, globRoot))
        {
            var bitcode = Path.Combine(root, "nvvm", "libdevice", BitcodeName);
            foreach (var dll in DllCandidates(platform, root, dllName))
            {
                tried.Add(dll);
                if (!File.Exists(dll))
                {
                    continue;
                }

                tried.Add(bitcode);
                if (File.Exists(bitcode))
                {
                    return new LibDeviceLocation(dll, bitcode, tried);
                }
            }
        }

        return new LibDeviceLocation(null, null, tried);
    }

    private static string DefaultGlobRoot(LocatorPlatform platform, Func<string, string?> environment) => platform switch
    {
        LocatorPlatform.Windows => Path.Combine(environment("ProgramFiles") ?? @"C:\Program Files", "NVIDIA GPU Computing Toolkit", "CUDA"),
        LocatorPlatform.Linux => "/usr/local",
        LocatorPlatform.Other => string.Empty,
        _ => string.Empty,
    };

    private static LocatorPlatform CurrentPlatform() =>
        OperatingSystem.IsWindows() ? LocatorPlatform.Windows :
        OperatingSystem.IsLinux() ? LocatorPlatform.Linux : LocatorPlatform.Other;

    private static string DllName(LocatorPlatform platform) => platform switch
    {
        LocatorPlatform.Windows => WindowsDllName,
        LocatorPlatform.Linux => LinuxDllName,
        LocatorPlatform.Other => WindowsDllName,
        _ => WindowsDllName,
    };

    /// <summary>The library files tried under one root, in order: two layouts on Windows (12.x, 13.x), one on Linux.</summary>
    private static string[] DllCandidates(LocatorPlatform platform, string root, string dllName) =>
        platform == LocatorPlatform.Windows
            ? [Path.Combine(root, "nvvm", "bin", dllName), Path.Combine(root, "nvvm", "bin", "x64", dllName)]
            : [Path.Combine(root, "nvvm", "lib64", dllName)];

    /// <summary>
    /// Windows: <c>CUDA_PATH</c>, then the toolkit directories under <paramref name="globRoot"/> from the newest version
    /// down. Linux: <c>CUDA_PATH</c>, then <c>CUDA_HOME</c>, then <c>&lt;globRoot&gt;/cuda</c>, then the <c>cuda-*</c>
    /// directories under <paramref name="globRoot"/> from the newest version down. A root already yielded is skipped.
    /// </summary>
    private static IEnumerable<string> ToolkitRoots(LocatorPlatform platform, Func<string, string?> environment, string globRoot)
    {
        var comparer = platform == LocatorPlatform.Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);

        var cudaPath = environment("CUDA_PATH");
        if (!string.IsNullOrWhiteSpace(cudaPath) && seen.Add(cudaPath))
        {
            yield return cudaPath;
        }

        if (platform == LocatorPlatform.Linux)
        {
            var cudaHome = environment("CUDA_HOME");
            if (!string.IsNullOrWhiteSpace(cudaHome) && seen.Add(cudaHome))
            {
                yield return cudaHome;
            }

            var fixedRoot = Path.Combine(globRoot, "cuda");
            if (seen.Add(fixedRoot))
            {
                yield return fixedRoot;
            }

            foreach (var directory in VersionedDirectories(globRoot, "cuda-*", "cuda-"))
            {
                if (seen.Add(directory))
                {
                    yield return directory;
                }
            }

            yield break;
        }

        foreach (var directory in VersionedDirectories(globRoot, "v*", "v"))
        {
            if (seen.Add(directory))
            {
                yield return directory;
            }
        }
    }

    /// <summary>The subdirectories of <paramref name="baseDirectory"/> matching <paramref name="pattern"/>, newest version first.</summary>
    private static IEnumerable<string> VersionedDirectories(string baseDirectory, string pattern, string prefix) =>
        Directory.Exists(baseDirectory)
            ? Directory.GetDirectories(baseDirectory, pattern)
                .Select(directory => (Directory: directory, Version: ParseVersion(Path.GetFileName(directory), prefix)))
                .Where(entry => entry.Version is not null)
                .OrderByDescending(entry => entry.Version)
                .Select(entry => entry.Directory)
            : [];

    private static Version? ParseVersion(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Version.TryParse(name[prefix.Length..], out var version) ? version : null;
}
