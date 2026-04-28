using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal static class NativeRuntimeLoader
{
    private const string LibMpvFileName = "libmpv-2.dll";
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
    private const uint LoadLibrarySearchUserDirs = 0x00000400;

    private static readonly object SyncRoot = new();
    private static bool _installed;
    private static IntPtr _libMpvHandle;
    private static IntPtr _runtimeDirectoryCookie;

    internal static string RuntimeDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "NativeRuntime", "x64");

    internal static void Install(params Assembly[] additionalResolverAssemblies)
    {
        lock (SyncRoot)
        {
            if (_installed)
                return;

            if (!Directory.Exists(RuntimeDirectory))
                throw new DirectoryNotFoundException($"Native runtime directory was not found: {RuntimeDirectory}");

            if (!SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs | LoadLibrarySearchUserDirs))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure native DLL search directories.");

            _runtimeDirectoryCookie = AddDllDirectory(RuntimeDirectory);
            if (_runtimeDirectoryCookie == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to add native runtime directory: {RuntimeDirectory}");

            NativeLibrary.SetDllImportResolver(typeof(NativeRuntimeLoader).Assembly, ResolveNativeLibrary);

            foreach (var assembly in additionalResolverAssemblies)
            {
                NativeLibrary.SetDllImportResolver(assembly, ResolveNativeLibrary);
            }

            _installed = true;
        }
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        return IsLibMpvName(libraryName) ? LoadLibMpv() : IntPtr.Zero;
    }

    private static bool IsLibMpvName(string libraryName)
    {
        return string.Equals(libraryName, LibMpvFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(libraryName, "libmpv.2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(libraryName, "mpv-2", StringComparison.OrdinalIgnoreCase);
    }

    private static IntPtr LoadLibMpv()
    {
        if (_libMpvHandle != IntPtr.Zero)
            return _libMpvHandle;

        var libMpvPath = Path.Combine(RuntimeDirectory, LibMpvFileName);
        if (!File.Exists(libMpvPath))
            throw new FileNotFoundException("Native mpv runtime was not found.", libMpvPath);

        _libMpvHandle = LoadLibraryEx(
            libMpvPath,
            IntPtr.Zero,
            LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirs | LoadLibrarySearchUserDirs);

        if (_libMpvHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to load native mpv runtime: {libMpvPath}");

        return _libMpvHandle;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr reserved, uint flags);
}
