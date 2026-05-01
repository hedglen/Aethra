using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed partial class AngleD3D11SwapChainContext : IDisposable
{
    private readonly D3D11SwapChainPanelHost _d3dHost;
    private readonly IntPtr _eglDevice;
    private readonly IntPtr _display;
    private readonly IntPtr _config;
    private readonly IntPtr _context;
    private IntPtr _surface;
    private IntPtr _backBuffer;
    private bool _disposed;

    private AngleD3D11SwapChainContext(
        D3D11SwapChainPanelHost d3dHost,
        IntPtr eglDevice,
        IntPtr display,
        IntPtr config,
        IntPtr context,
        IntPtr backBuffer,
        IntPtr surface)
    {
        _d3dHost = d3dHost;
        _eglDevice = eglDevice;
        _display = display;
        _config = config;
        _context = context;
        _backBuffer = backBuffer;
        _surface = surface;
    }

    ~AngleD3D11SwapChainContext()
    {
        Dispose(false);
    }

    internal static AngleD3D11SwapChainContext Create(SwapChainPanel panel, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var d3dHost = new D3D11SwapChainPanelHost();
        d3dHost.Attach(panel, width, height);

        var d3dDevice = d3dHost.DevicePointer;
        IntPtr eglDevice = IntPtr.Zero;
        IntPtr display = IntPtr.Zero;
        IntPtr context = IntPtr.Zero;
        IntPtr backBuffer = IntPtr.Zero;
        IntPtr surface = IntPtr.Zero;

        try
        {
            eglDevice = CreateEglDevice(d3dDevice);
            display = CreateDisplay(eglDevice);

            if (AngleNative.BindApi(AngleNative.EglOpenGlEsApi) == AngleNative.EglFalse)
                ThrowEglError("eglBindAPI");

            var config = ChooseConfig(display);
            context = CreateContext(display, config);
            backBuffer = d3dHost.GetBackBuffer();
            surface = CreateBackBufferSurface(display, config, backBuffer);

            if (AngleNative.MakeCurrent(display, surface, surface, context) == AngleNative.EglFalse)
                ThrowEglError("eglMakeCurrent");

            if (AngleNative.MakeCurrent(display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == AngleNative.EglFalse)
                ThrowEglError("eglMakeCurrent clear");

            return new AngleD3D11SwapChainContext(d3dHost, eglDevice, display, config, context, backBuffer, surface);
        }
        catch
        {
            if (surface != IntPtr.Zero)
                AngleNative.DestroySurface(display, surface);

            if (backBuffer != IntPtr.Zero)
                Marshal.Release(backBuffer);

            if (context != IntPtr.Zero)
                AngleNative.DestroyContext(display, context);

            if (display != IntPtr.Zero)
                AngleNative.Terminate(display);

            if (eglDevice != IntPtr.Zero)
                ReleaseEglDevice(eglDevice);

            d3dHost.Dispose();
            throw;
        }
        finally
        {
            Marshal.Release(d3dDevice);
        }
    }

    internal void MakeCurrent()
    {
        ThrowIfDisposed();

        if (AngleNative.MakeCurrent(_display, _surface, _surface, _context) == AngleNative.EglFalse)
            ThrowEglError("eglMakeCurrent");
    }

    internal void SetViewport(int width, int height)
    {
        ThrowIfDisposed();
        AngleNative.Viewport(0, 0, Math.Max(width, 1), Math.Max(height, 1));
    }

    internal void SwapBuffers()
    {
        ThrowIfDisposed();
        _d3dHost.Present();
    }

    internal void Resize(int width, int height)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        AngleNative.MakeCurrent(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_surface != IntPtr.Zero)
        {
            AngleNative.DestroySurface(_display, _surface);
            _surface = IntPtr.Zero;
        }

        if (_backBuffer != IntPtr.Zero)
        {
            Marshal.Release(_backBuffer);
            _backBuffer = IntPtr.Zero;
        }

        _d3dHost.ResizeBuffers(width, height);

        _backBuffer = _d3dHost.GetBackBuffer();
        _surface = CreateBackBufferSurface(_display, _config, _backBuffer);

        if (AngleNative.MakeCurrent(_display, _surface, _surface, _context) == AngleNative.EglFalse)
            ThrowEglError("eglMakeCurrent after resize");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static IntPtr CreateEglDevice(IntPtr d3dDevice)
    {
        var name = Marshal.StringToCoTaskMemAnsi("eglCreateDeviceANGLE");
        try
        {
            var pointer = AngleNative.GetProcAddress(name);
            if (pointer == IntPtr.Zero)
                throw new InvalidOperationException("ANGLE does not expose eglCreateDeviceANGLE.");

            var createDevice = Marshal.GetDelegateForFunctionPointer<EglCreateDeviceAngle>(pointer);
            var eglDevice = createDevice(AngleNative.EglD3D11DeviceAngle, d3dDevice, IntPtr.Zero);
            if (eglDevice == IntPtr.Zero)
                ThrowEglError("eglCreateDeviceANGLE");

            return eglDevice;
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    private static void ReleaseEglDevice(IntPtr eglDevice)
    {
        var name = Marshal.StringToCoTaskMemAnsi("eglReleaseDeviceANGLE");
        try
        {
            var pointer = AngleNative.GetProcAddress(name);
            if (pointer == IntPtr.Zero)
                return;

            var releaseDevice = Marshal.GetDelegateForFunctionPointer<EglReleaseDeviceAngle>(pointer);
            _ = releaseDevice(eglDevice);
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    private static IntPtr CreateDisplay(IntPtr eglDevice)
    {
        var display = AngleNative.GetPlatformDisplay(AngleNative.EglPlatformDeviceExt, eglDevice, new[] { AngleNative.EglNone });
        if (display == IntPtr.Zero)
            ThrowEglError("eglGetPlatformDisplayEXT");

        if (AngleNative.Initialize(display, out _, out _) == AngleNative.EglFalse)
            ThrowEglError("eglInitialize");

        return display;
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
        if (AngleNative.ChooseConfig(display, attributes, configs, configs.Length, out var configCount) == AngleNative.EglFalse)
            ThrowEglError("eglChooseConfig");

        if (configCount <= 0 || configs[0] == IntPtr.Zero)
            throw new InvalidOperationException("ANGLE did not return a usable D3D11 backbuffer EGL config.");

        return configs[0];
    }

    private static IntPtr CreateContext(IntPtr display, IntPtr config)
    {
        var context = TryCreateContext(display, config, clientVersion: 3);
        if (context == IntPtr.Zero)
            context = TryCreateContext(display, config, clientVersion: 2);

        if (context == IntPtr.Zero)
            ThrowEglError("eglCreateContext");

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

    private static IntPtr CreateBackBufferSurface(IntPtr display, IntPtr config, IntPtr backBuffer)
    {
        var surface = AngleNative.CreatePbufferFromClientBuffer(
            display,
            AngleNative.EglD3DTextureAngle,
            backBuffer,
            config,
            new[]
            {
                AngleNative.EglTextureFormat, AngleNative.EglTextureRgba,
                AngleNative.EglTextureTarget, AngleNative.EglTexture2D,
                AngleNative.EglNone
            });

        if (surface == IntPtr.Zero)
            ThrowEglError("eglCreatePbufferFromClientBuffer");

        return surface;
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
            AngleNative.MakeCurrent(_display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_surface != IntPtr.Zero)
        {
            AngleNative.DestroySurface(_display, _surface);
            _surface = IntPtr.Zero;
        }

        if (_backBuffer != IntPtr.Zero)
        {
            Marshal.Release(_backBuffer);
            _backBuffer = IntPtr.Zero;
        }

        if (_context != IntPtr.Zero)
            AngleNative.DestroyContext(_display, _context);

        if (_display != IntPtr.Zero)
            AngleNative.Terminate(_display);

        if (_eglDevice != IntPtr.Zero)
            ReleaseEglDevice(_eglDevice);

        _d3dHost.Dispose();
        _disposed = true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr EglCreateDeviceAngle(uint deviceType, IntPtr nativeDevice, IntPtr attributes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EglReleaseDeviceAngle(IntPtr device);
}
