using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed class NativeMpvSoftwareFrame : IDisposable
{
    internal const string PixelFormat = "bgr0";
    private const int BytesPerPixel = 4;

    private bool _disposed;

    internal NativeMpvSoftwareFrame(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Stride = Align(width * BytesPerPixel, 64);
        BufferLength = checked(Stride * Height);
        Buffer = Marshal.AllocHGlobal(BufferLength);
    }

    ~NativeMpvSoftwareFrame()
    {
        Dispose(false);
    }

    internal int Width { get; }

    internal int Height { get; }

    internal int Stride { get; }

    internal int BufferLength { get; }

    internal IntPtr Buffer { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (Buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Buffer);
            Buffer = IntPtr.Zero;
        }

        _disposed = true;
    }

    private static int Align(int value, int alignment)
    {
        return checked((value + alignment - 1) / alignment * alignment);
    }
}
