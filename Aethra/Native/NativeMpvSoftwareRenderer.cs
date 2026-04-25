using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed class NativeMpvSoftwareRenderer : IDisposable
{
    private readonly NativeMpvContext _context;
    private IntPtr _renderContext;
    private MpvNative.MpvRenderUpdateCallback? _updateCallback;
    private bool _disposed;

    internal NativeMpvSoftwareRenderer(NativeMpvContext context)
    {
        _context = context;
    }

    ~NativeMpvSoftwareRenderer()
    {
        Dispose(false);
    }

    internal event EventHandler? FrameRequested;

    internal void Create()
    {
        ThrowIfDisposed();

        if (_renderContext != IntPtr.Zero)
            return;

        var apiType = Marshal.StringToCoTaskMemUTF8(MpvNative.RenderApiTypeSoftware);
        var parameters = AllocateRenderParameters(
            new MpvNative.MpvRenderParam
            {
                Type = MpvNative.MpvRenderParamType.ApiType,
                Data = apiType
            });

        try
        {
            ThrowIfError(
                MpvNative.RenderContextCreate(out _renderContext, _context.Handle, parameters),
                "create software render context");

            _updateCallback = _ => FrameRequested?.Invoke(this, EventArgs.Empty);
            MpvNative.RenderContextSetUpdateCallback(_renderContext, _updateCallback, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(apiType);
            Marshal.FreeCoTaskMem(parameters);
        }
    }

    internal MpvNative.MpvRenderUpdateFlag Update()
    {
        ThrowIfDisposed();
        ThrowIfNoRenderContext();

        return MpvNative.RenderContextUpdate(_renderContext);
    }

    internal bool Render(NativeMpvSoftwareFrame frame)
    {
        ThrowIfDisposed();
        ThrowIfNoRenderContext();
        ArgumentNullException.ThrowIfNull(frame);

        var update = Update();
        if (!update.HasFlag(MpvNative.MpvRenderUpdateFlag.Frame))
            return false;

        var size = Marshal.AllocCoTaskMem(sizeof(int) * 2);
        var format = Marshal.StringToCoTaskMemUTF8(NativeMpvSoftwareFrame.PixelFormat);
        var stride = Marshal.AllocCoTaskMem(IntPtr.Size);
        IntPtr parameters = IntPtr.Zero;

        try
        {
            Marshal.WriteInt32(size, 0, frame.Width);
            Marshal.WriteInt32(size, sizeof(int), frame.Height);
            Marshal.WriteIntPtr(stride, (IntPtr)frame.Stride);

            parameters = AllocateRenderParameters(
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.SoftwareSize,
                    Data = size
                },
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.SoftwareFormat,
                    Data = format
                },
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.SoftwareStride,
                    Data = stride
                },
                new MpvNative.MpvRenderParam
                {
                    Type = MpvNative.MpvRenderParamType.SoftwarePointer,
                    Data = frame.Buffer
                });

            ThrowIfError(MpvNative.RenderContextRender(_renderContext, parameters), "render software frame");
            return true;
        }
        finally
        {
            if (parameters != IntPtr.Zero)
                Marshal.FreeCoTaskMem(parameters);

            Marshal.FreeCoTaskMem(stride);
            Marshal.FreeCoTaskMem(format);
            Marshal.FreeCoTaskMem(size);
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
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ThrowIfNoRenderContext()
    {
        if (_renderContext == IntPtr.Zero)
            throw new InvalidOperationException("The software render context has not been created.");
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
