using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using Windows.Foundation.Collections;
using WinRT;

namespace Aethra.Native;

internal sealed class AngleSwapChainPanelContext : IDisposable
{
    private readonly IntPtr _display;
    private readonly IntPtr _surface;
    private readonly IntPtr _context;
    private bool _disposed;

    private AngleSwapChainPanelContext(IntPtr display, IntPtr surface, IntPtr context)
    {
        _display = display;
        _surface = surface;
        _context = context;
    }

    ~AngleSwapChainPanelContext()
    {
        Dispose(false);
    }

    internal static AngleSwapChainPanelContext Create(SwapChainPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var display = CreateDisplay();
        try
        {
            if (AngleNative.BindApi(AngleNative.EglOpenGlEsApi) == AngleNative.EglFalse)
                ThrowEglError("eglBindAPI");

            var config = ChooseConfig(display);
            var context = CreateContext(display, config);

            try
            {
                var surface = CreateWindowSurface(display, config, panel);
                return new AngleSwapChainPanelContext(display, surface, context);
            }
            catch
            {
                AngleNative.DestroyContext(display, context);
                throw;
            }
        }
        catch
        {
            AngleNative.Terminate(display);
            throw;
        }
    }

    internal void MakeCurrent()
    {
        ThrowIfDisposed();

        if (AngleNative.MakeCurrent(_display, _surface, _surface, _context) == AngleNative.EglFalse)
            ThrowEglError("eglMakeCurrent");
    }

    internal (int Width, int Height) GetSurfaceSize()
    {
        ThrowIfDisposed();

        if (AngleNative.QuerySurface(_display, _surface, AngleNative.EglWidth, out var width) == AngleNative.EglFalse)
            ThrowEglError("eglQuerySurface width");

        if (AngleNative.QuerySurface(_display, _surface, AngleNative.EglHeight, out var height) == AngleNative.EglFalse)
            ThrowEglError("eglQuerySurface height");

        return (Math.Max(width, 1), Math.Max(height, 1));
    }

    internal void SetViewport(int width, int height)
    {
        ThrowIfDisposed();
        AngleNative.Viewport(0, 0, Math.Max(width, 1), Math.Max(height, 1));
    }

    internal void SwapBuffers()
    {
        ThrowIfDisposed();

        if (AngleNative.SwapBuffers(_display, _surface) == AngleNative.EglFalse)
            ThrowEglError("eglSwapBuffers");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static IntPtr CreateDisplay()
    {
        var display = TryCreateDisplay(
            new[]
            {
                AngleNative.EglPlatformAngleTypeAngle, AngleNative.EglPlatformAngleTypeD3D11Angle,
                AngleNative.EglExperimentalPresentPathAngle, AngleNative.EglExperimentalPresentPathFastAngle,
                AngleNative.EglPlatformAngleEnableAutomaticTrimAngle, AngleNative.EglTrue,
                AngleNative.EglNone
            });

        if (display != IntPtr.Zero)
            return display;

        display = TryCreateDisplay(
            new[]
            {
                AngleNative.EglPlatformAngleTypeAngle, AngleNative.EglPlatformAngleTypeD3D11Angle,
                AngleNative.EglPlatformAngleDeviceTypeAngle, AngleNative.EglPlatformAngleDeviceTypeD3DWarpAngle,
                AngleNative.EglExperimentalPresentPathAngle, AngleNative.EglExperimentalPresentPathFastAngle,
                AngleNative.EglPlatformAngleEnableAutomaticTrimAngle, AngleNative.EglTrue,
                AngleNative.EglNone
            });

        if (display != IntPtr.Zero)
            return display;

        throw new InvalidOperationException("ANGLE did not return an initialized EGL display.");
    }

    private static IntPtr TryCreateDisplay(int[] attributes)
    {
        var display = AngleNative.GetPlatformDisplay(AngleNative.EglPlatformAngleAngle, IntPtr.Zero, attributes);
        if (display == IntPtr.Zero)
            return IntPtr.Zero;

        if (AngleNative.Initialize(display, out _, out _) != AngleNative.EglFalse)
            return display;

        AngleNative.Terminate(display);
        return IntPtr.Zero;
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
            AngleNative.EglSurfaceType, AngleNative.EglWindowBit,
            AngleNative.EglRenderableType, AngleNative.EglOpenGlEs3Bit | AngleNative.EglOpenGlEs2Bit,
            AngleNative.EglNone
        };

        var configs = new IntPtr[1];
        if (AngleNative.ChooseConfig(display, attributes, configs, configs.Length, out var configCount) == AngleNative.EglFalse)
            ThrowEglError("eglChooseConfig");

        if (configCount <= 0 || configs[0] == IntPtr.Zero)
            throw new InvalidOperationException("ANGLE did not return a usable window EGL config.");

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

    private static IntPtr CreateWindowSurface(IntPtr display, IntPtr config, SwapChainPanel panel)
    {
        var properties = new PropertySet
        {
            { AngleNative.EglNativeWindowTypeProperty, panel },
            { AngleNative.EglRenderResolutionScaleProperty, (float)Math.Max(panel.CompositionScaleX, 1.0) }
        };

        var surface = AngleNative.CreateWindowSurface(
            display,
            config,
            properties.As<IInspectable>().ThisPtr,
            new[] { AngleNative.EglNone });

        if (surface == IntPtr.Zero)
            ThrowEglError("eglCreateWindowSurface");

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
