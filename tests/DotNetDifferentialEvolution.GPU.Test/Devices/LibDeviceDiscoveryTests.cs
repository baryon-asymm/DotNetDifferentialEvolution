using DotNetDifferentialEvolution.GPU.Devices.LibDevice;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check L1 of the GPU package's Devices/LibDevice/ACCEPTANCE.md: libdevice discovery over fake toolkit trees built in a temporary
/// directory, through the locator's seam (platform, environment and base directory given), so both platforms and both
/// Windows layouts are covered from one host. After APThermo's <c>LibDeviceDiscoveryTests</c> (commit <c>5fdd82c</c>).
/// </summary>
public sealed class LibDeviceDiscoveryTests : IDisposable
{
    private const string WindowsDll = "nvvm64_40_0.dll";
    private const string LinuxDll = "libnvvm.so";

    private readonly string _root = Directory.CreateTempSubdirectory("dotnet-de-gpu-libdevice-").FullName;

    /// <summary>Removes the fake toolkit trees.</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>A platform other than Windows and Linux tries nothing, even with a toolkit present.</summary>
    [Fact]
    public void AnUnsupportedPlatformTriesNothing()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v1.0"), legacyLayout: true);

        var location = LibDeviceLocator.Locate(LocatorPlatform.Other, Env(), toolkits);

        Assert.False(location.Found);
        Assert.Empty(location.Tried);
    }

    /// <summary>Windows with no <c>CUDA_PATH</c> and no toolkit base directory, the hosted runner's case, tries nothing.</summary>
    [Fact]
    public void WindowsWithNoCudaPathAndNoBaseDirectoryTriesNothing()
    {
        var location = LibDeviceLocator.Locate(LocatorPlatform.Windows, Env(), Path.Combine(_root, "never-created"));

        Assert.False(location.Found);
        Assert.Empty(location.Tried);
    }

    /// <summary>Windows tries <c>CUDA_PATH</c> before the versioned directories, and nothing else once it matched.</summary>
    [Fact]
    public void WindowsTriesCudaPathBeforeTheVersionedDirectories()
    {
        var cudaPath = Path.Combine(_root, "cuda-path");
        var (cudaPathDll, _) = WriteWindowsToolkit(cudaPath, legacyLayout: false);
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v99.0"), legacyLayout: false);

        var location = LibDeviceLocator.Locate(LocatorPlatform.Windows, Env(cudaPath: cudaPath), toolkits);

        Assert.Equal(cudaPathDll, location.Dll);
        Assert.All(location.Tried, path => Assert.StartsWith(cudaPath, path, StringComparison.Ordinal));
    }

    /// <summary>Windows takes the newest version first by parsed version: <c>v13.3</c> before <c>v9.0</c>, which a string sort reverses.</summary>
    [Fact]
    public void WindowsOrdersTheVersionedDirectoriesNewestFirst()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteWindowsToolkit(Path.Combine(toolkits, "v9.0"), legacyLayout: false);
        var (newest, _) = WriteWindowsToolkit(Path.Combine(toolkits, "v13.3"), legacyLayout: false);

        var location = LibDeviceLocator.Locate(LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(newest, location.Dll);
    }

    /// <summary>Windows finds the library in either layout: <c>nvvm\bin</c> (12.x) and <c>nvvm\bin\x64</c> (13.x).</summary>
    /// <param name="legacyLayout">Whether the library is in the 12.x layout.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WindowsFindsBothLibraryLayouts(bool legacyLayout)
    {
        var toolkits = Path.Combine(_root, "toolkits");
        var (dll, bitcode) = WriteWindowsToolkit(Path.Combine(toolkits, "v1.0"), legacyLayout);

        var location = LibDeviceLocator.Locate(LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(dll, location.Dll);
        Assert.Equal(bitcode, location.Bitcode);
    }

    /// <summary>A root with the library and no bitcode is passed over for the next root, and its bitcode path is recorded as tried.</summary>
    [Fact]
    public void ALibraryWithoutBitcodeIsPassedOverForTheNextRoot()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        _ = WriteFile(Path.Combine(toolkits, "v13.0", "nvvm", "bin", "x64", WindowsDll));
        var (dll, bitcode) = WriteWindowsToolkit(Path.Combine(toolkits, "v12.0"), legacyLayout: false);

        var location = LibDeviceLocator.Locate(LocatorPlatform.Windows, Env(), toolkits);

        Assert.Equal(dll, location.Dll);
        Assert.Equal(bitcode, location.Bitcode);
        Assert.Contains(Path.Combine(toolkits, "v13.0", "nvvm", "libdevice", LibDeviceLocator.BitcodeName), location.Tried);
    }

    /// <summary>A root named by <c>CUDA_PATH</c> and found again among the versions is tried once.</summary>
    [Fact]
    public void WindowsTriesARootNamedTwiceOnce()
    {
        var toolkits = Path.Combine(_root, "toolkits");
        var v130 = Path.Combine(toolkits, "v13.0");
        var dll = WriteFile(Path.Combine(v130, "nvvm", "bin", "x64", WindowsDll));

        var location = LibDeviceLocator.Locate(LocatorPlatform.Windows, Env(cudaPath: v130), toolkits);

        Assert.False(location.Found);
        Assert.Equal(1, location.Tried.Count(path => path == dll));
    }

    /// <summary>Linux tries <c>CUDA_PATH</c> before <c>CUDA_HOME</c>, the fixed root and the versions.</summary>
    [Fact]
    public void LinuxTriesCudaPathFirst()
    {
        var cudaPath = Path.Combine(_root, "cuda-path");
        var (cudaPathDll, _) = WriteLinuxToolkit(cudaPath);
        var glob = Path.Combine(_root, "usr-local");
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda"));
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda-13.3"));

        var location = LibDeviceLocator.Locate(LocatorPlatform.Linux, Env(cudaPath: cudaPath), glob);

        Assert.Equal(cudaPathDll, location.Dll);
        Assert.All(location.Tried, path => Assert.StartsWith(cudaPath, path, StringComparison.Ordinal));
    }

    /// <summary>Linux tries <c>CUDA_HOME</c> before the fixed root, and the fixed root before the versions.</summary>
    [Fact]
    public void LinuxTriesCudaHomeThenTheFixedRootThenTheVersions()
    {
        var cudaHome = Path.Combine(_root, "cuda-home");
        var (cudaHomeDll, _) = WriteLinuxToolkit(cudaHome);
        var glob = Path.Combine(_root, "usr-local");
        var (fixedDll, _) = WriteLinuxToolkit(Path.Combine(glob, "cuda"));
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda-13.3"));

        Assert.Equal(cudaHomeDll, LibDeviceLocator.Locate(LocatorPlatform.Linux, Env(cudaHome: cudaHome), glob).Dll);
        Assert.Equal(fixedDll, LibDeviceLocator.Locate(LocatorPlatform.Linux, Env(), glob).Dll);
    }

    /// <summary>Linux takes the newest <c>cuda-*</c> first by parsed version.</summary>
    [Fact]
    public void LinuxOrdersTheVersionedDirectoriesNewestFirst()
    {
        var glob = Path.Combine(_root, "usr-local");
        _ = WriteLinuxToolkit(Path.Combine(glob, "cuda-9.0"));
        var (newest, _) = WriteLinuxToolkit(Path.Combine(glob, "cuda-13.3"));

        var location = LibDeviceLocator.Locate(LocatorPlatform.Linux, Env(), glob);

        Assert.Equal(newest, location.Dll);
    }

    /// <summary>Linux tries a root that <c>CUDA_PATH</c> and <c>CUDA_HOME</c> both name once.</summary>
    [Fact]
    public void LinuxTriesARootNamedTwiceOnce()
    {
        var cudaPath = Path.Combine(_root, "cuda-path");
        var dll = WriteFile(Path.Combine(cudaPath, "nvvm", "lib64", LinuxDll));

        var location = LibDeviceLocator.Locate(LocatorPlatform.Linux, Env(cudaPath, cudaHome: cudaPath), Path.Combine(_root, "usr-local"));

        Assert.False(location.Found);
        Assert.Equal(1, location.Tried.Count(path => path == dll));
    }

    private static Func<string, string?> Env(string? cudaPath = null, string? cudaHome = null) => name => name switch
    {
        "CUDA_PATH" => cudaPath,
        "CUDA_HOME" => cudaHome,
        _ => null,
    };

    private static (string Dll, string Bitcode) WriteWindowsToolkit(string root, bool legacyLayout)
    {
        var dllDirectory = legacyLayout ? Path.Combine(root, "nvvm", "bin") : Path.Combine(root, "nvvm", "bin", "x64");
        var dll = WriteFile(Path.Combine(dllDirectory, WindowsDll));
        var bitcode = WriteFile(Path.Combine(root, "nvvm", "libdevice", LibDeviceLocator.BitcodeName));
        return (dll, bitcode);
    }

    private static (string Dll, string Bitcode) WriteLinuxToolkit(string root)
    {
        var dll = WriteFile(Path.Combine(root, "nvvm", "lib64", LinuxDll));
        var bitcode = WriteFile(Path.Combine(root, "nvvm", "libdevice", LibDeviceLocator.BitcodeName));
        return (dll, bitcode);
    }

    private static string WriteFile(string path)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "stub");
        return path;
    }
}
