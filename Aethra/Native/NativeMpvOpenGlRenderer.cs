using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed class NativeMpvOpenGlRenderer : IDisposable
{
    private readonly NativeMpvContext _context;
    private IntPtr _renderContext;
    private MpvNative.MpvOpenGlGetProcAddressCallback? _getProcAddressCallback;
    private MpvNative.MpvRenderUpdateCallback? _updateCallback;
    private bool _disposed;

    internal NativeMpvOpenGlRenderer(NativeMpvContext context)
    {
        _context = context;
    }

    ~NativeMpvOpenGlRenderer()
    {
        Dispose(false);
    }

    internal event EventHandler? FrameRequested;

    internal void Create()
    {
        ThrowIfDisposed();

        if (_renderContext != IntPtr.Zero)
            return;

        _getProcAddressCallback = (_, name) => AngleNative.GetProcAddress(name);
        var callbackPointer = Marshal.GetFunctionPointerForDelegate(_getProcAddressCallback);
        var apiType = Marshal.StringToCoTaskMemUTF8(MpvNative.RenderApiTypeOpenGl);
        var openGlInitParams = Marshal.AllocCoTaskMem(Marshal.SizeOf<MpvNative.MpvOpenGlInitParams>());
        IntPtr parameters = IntPtr.Zero;

        try
        {
            Marshal.StructureToPtr(
                new MpvNative.MpvOpenGlInitParams
                {
                    GetProcAddress = callbackPointer,
                    GetProcAddressContext = IntPtr.Zero
                },
                openGlInitParams,
                false);

            parameters = AllocateRenderParameters(
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.ApiType,
                    Data = apiType
                },
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.OpenGlInitParams,
                    Data = openGlInitParams
                });

            ThrowIfError(MpvNative.RenderContextCreate(out _renderContext, _context.Handle, parameters), "create OpenGL render context");

            _updateCallback = _ => FrameRequested?.Invoke(this, EventArgs.Empty);
            MpvNative.RenderContextSetUpdateCallback(_renderContext, _updateCallback, IntPtr.Zero);
        }
        finally
        {
            if (parameters != IntPtr.Zero)
                Marshal.FreeCoTaskMem(parameters);

            Marshal.FreeCoTaskMem(openGlInitParams);
            Marshal.FreeCoTaskMem(apiType);
        }
    }

    internal MpvNative.MpvRenderUpdateFlag Update()
    {
        ThrowIfDisposed();
        ThrowIfNoRenderContext();

        return MpvNative.RenderContextUpdate(_renderContext);
    }

    internal bool Render(int width, int height, bool flipY = false, bool requireFrameUpdate = true)
    {
        ThrowIfDisposed();
        ThrowIfNoRenderContext();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var update = Update();
        if (requireFrameUpdate && !update.HasFlag(MpvNative.MpvRenderUpdateFlag.Frame))
            return false;

        var fboPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<MpvNative.MpvOpenGlFbo>());
        var flipYPointer = Marshal.AllocCoTaskMem(sizeof(int));
        IntPtr parameters = IntPtr.Zero;

        try
        {
            Marshal.StructureToPtr(
                new MpvNative.MpvOpenGlFbo
                {
                    Fbo = 0,
                    Width = width,
                    Height = height,
                    InternalFormat = AngleNative.GlRgba8
                },
                fboPointer,
                false);
            Marshal.WriteInt32(flipYPointer, flipY ? 1 : 0);

            parameters = AllocateRenderParameters(
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.OpenGlFbo,
                    Data = fboPointer
                },
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.FlipY,
                    Data = flipYPointer
                });

            ThrowIfError(MpvNative.RenderContextRender(_renderContext, parameters), "render OpenGL frame");
            return true;
        }
        finally
        {
            if (parameters != IntPtr.Zero)
                Marshal.FreeCoTaskMem(parameters);

            Marshal.FreeCoTaskMem(fboPointer);
            Marshal.FreeCoTaskMem(flipYPointer);
        }
    }

    internal void ReportSwap()
    {
        ThrowIfDisposed();
        ThrowIfNoRenderContext();

        MpvNative.RenderContextReportSwap(_renderContext);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (_renderContext != IntPtr.Zero)
        {
            MpvNative.RenderContextSetUpdateCallback(_renderContext, _ => { }, IntPtr.Zero);
            MpvNative.RenderContextFree(_renderContext);
            _renderContext = IntPtr.Zero;
        }

        _updateCallback = null;
        _getProcAddressCallback = null;
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ThrowIfNoRenderContext()
    {
        if (_renderContext == IntPtr.Zero)
            throw new InvalidOperationException("The OpenGL render context has not been created.");
    }

    private static IntPtr AllocateRenderParameters(params MpvNative.MpvRenderParam[] parameters)
    {
        var itemSize = Marshal.SizeOf<MpvNative.MpvRenderParam>();
        var pointer = Marshal.AllocCoTaskMem(itemSize * (parameters.Length + 1));

        for (var i = 0; i < parameters.Length; i++)
        {
            Marshal.StructureToPtr(parameters[i], pointer + i * itemSize, false);
        }

        Marshal.StructureToPtr(
            new MpvNative.MpvRenderParam
            {
                Type = MpvNative.MpvRenderParamType.Invalid,
                Data = IntPtr.Zero
            },
            pointer + parameters.Length * itemSize,
            false);

        return pointer;
    }

    private static void ThrowIfError(int result, string operation)
    {
        if (result < 0)
            throw new MpvNativeException(operation, result);
    }
}
