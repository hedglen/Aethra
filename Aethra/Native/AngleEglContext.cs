using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed class AngleEglContext : IDisposable
{
    private readonly IntPtr _display;
    private readonly IntPtr _surface;
    private readonly IntPtr _context;
    private bool _disposed;

    private AngleEglContext(IntPtr display, IntPtr surface, IntPtr context, AngleEglInfo info)
    {
        _display = display;
        _surface = surface;
        _context = context;
        Info = info;
    }

    ~AngleEglContext()
    {
        Dispose(false);
    }

    internal AngleEglInfo Info { get; }

    internal void MakeCurrent()
    {
        ThrowIfDisposed();

        if (AngleNative.MakeCurrent(_display, _surface, _surface, _context) == AngleNative.EglFalse)
            ThrowEglError("eglMakeCurrent");
    }

    internal void SwapBuffers()
    {
        ThrowIfDisposed();

        if (AngleNative.SwapBuffers(_display, _surface) == AngleNative.EglFalse)
            ThrowEglError("eglSwapBuffers");
    }

    internal static AngleEglContext CreatePbuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var display = AngleNative.GetDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
            ThrowEglError("eglGetDisplay");

        try
        {
            if (AngleNative.Initialize(display, out int eglMajor, out int eglMinor) == AngleNative.EglFalse)
                ThrowEglError("eglInitialize");

            if (AngleNative.BindApi(AngleNative.EglOpenGlEsApi) == AngleNative.EglFalse)
                ThrowEglError("eglBindAPI");

            var config = ChooseConfig(display);
            var surface = AngleNative.CreatePbufferSurface(
                display,
                config,
                new[]
                {
                    AngleNative.EglWidth, width,
                    AngleNative.EglHeight, height,
                    AngleNative.EglNone
                });

            if (surface == IntPtr.Zero)
                ThrowEglError("eglCreatePbufferSurface");

            try
            {
                var context = CreateContext(display, config, surface);
                var clientVersion = GetClientVersion(context);

                var info = new AngleEglInfo(
                    eglMajor,
                    eglMinor,
                    clientVersion,
                    QueryEglString(display, AngleNative.EglVendor),
                    QueryEglString(display, AngleNative.EglVersion),
                    QueryGlString(AngleNative.GlVendor),
                    QueryGlString(AngleNative.GlRenderer),
                    QueryGlString(AngleNative.GlVersion));

                return new AngleEglContext(display, surface, context, info);
            }
            catch
            {
                AngleNative.DestroySurface(display, surface);
                throw;
            }
        }
        catch
        {
            AngleNative.Terminate(display);
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static IntPtr ChooseConfig(IntPtr display)
    {
        var attributes = new[]
        {
            AngleNative.EglRedSize, 8,
            AngleNative.EglGreenSize, 8,
            AngleNative.EglBlueSize, 8,
            AngleNative.EglAlphaSize, 8,
            AngleNative.EglDepthSize, 0,
            AngleNative.EglStencilSize, 0,
            AngleNative.EglSurfaceType, AngleNative.EglPbufferBit,
            AngleNative.EglRenderableType, AngleNative.EglOpenGlEs3Bit | AngleNative.EglOpenGlEs2Bit,
            AngleNative.EglNone
        };

        var configs = new IntPtr[1];
        if (AngleNative.ChooseConfig(display, attributes, configs, configs.Length, out int configCount) == AngleNative.EglFalse)
            ThrowEglError("eglChooseConfig");

        if (configCount <= 0 || configs[0] == IntPtr.Zero)
            throw new InvalidOperationException("ANGLE did not return a usable EGL config.");

        return configs[0];
    }

    private static IntPtr CreateContext(IntPtr display, IntPtr config, IntPtr surface)
    {
        var context = TryCreateContext(display, config, clientVersion: 3);
        if (context == IntPtr.Zero)
            context = TryCreateContext(display, config, clientVersion: 2);

        if (context == IntPtr.Zero)
            ThrowEglError("eglCreateContext");

        if (AngleNative.MakeCurrent(display, surface, surface, context) == AngleNative.EglFalse)
        {
            AngleNative.DestroyContext(display, context);
            ThrowEglError("eglMakeCurrent");
        }

        return context;
    }

    private static IntPtr TryCreateContext(IntPtr display, IntPtr config, int clientVersion)
    {
        return AngleNative.CreateContext(
            display,
            config,
            IntPtr.Zero,
            new[]
            {
                AngleNative.EglContextClientVersion, clientVersion,
                AngleNative.EglNone
            });
    }

    private static int GetClientVersion(IntPtr context)
    {
        _ = context;
        var version = QueryGlString(AngleNative.GlVersion);
        return version?.Contains("OpenGL ES 3", StringComparison.OrdinalIgnoreCase) == true ? 3 : 2;
    }

    private static string? QueryEglString(IntPtr display, int name)
    {
        return PtrToString(AngleNative.QueryString(display, name));
    }

    private static string? QueryGlString(uint name)
    {
        return PtrToString(AngleNative.GetGlString(name));
    }

    private static string? PtrToString(IntPtr pointer)
    {
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(pointer);
    }

    private static void ThrowEglError(string operation)
    {
        var error = AngleNative.GetError();
        throw new InvalidOperationException($"{operation} failed with EGL error 0x{error:X4}.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (_display != IntPtr.Zero)
        {
            AngleNative.MakeCurrent(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_context != IntPtr.Zero)
                AngleNative.DestroyContext(_display, _context);

            if (_surface != IntPtr.Zero)
                AngleNative.DestroySurface(_display, _surface);

            AngleNative.Terminate(_display);
        }

        _disposed = true;
    }
}
