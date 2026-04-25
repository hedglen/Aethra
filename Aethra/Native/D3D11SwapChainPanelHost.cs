using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed class D3D11SwapChainPanelHost : IDisposable
{
    private ID3D11Device? _device;
    private IntPtr _deviceContext;
    private IDXGIFactory2? _factory;
    private IntPtr _swapChain;
    private ISwapChainPanelNative? _panelNative;
    private bool _disposed;

    ~D3D11SwapChainPanelHost()
    {
        Dispose(false);
    }

    internal void Attach(SwapChainPanel panel, int width, int height)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        EnsureDevice();

        _panelNative = GetPanelNative(panel);
        _swapChain = CreateSwapChain(width, height);
        ThrowIfFailed(_panelNative.SetSwapChain(_swapChain), "set SwapChainPanel swap chain");
    }

    internal IntPtr DevicePointer
    {
        get
        {
            ThrowIfDisposed();

            if (_device is null)
                throw new InvalidOperationException("The D3D11 device has not been created.");

            return Marshal.GetIUnknownForObject(_device);
        }
    }

    internal IntPtr GetBackBuffer()
    {
        ThrowIfDisposed();

        if (_swapChain == IntPtr.Zero)
            throw new InvalidOperationException("The composition swap chain has not been created.");

        var textureId = typeof(ID3D11Texture2D).GUID;
        ThrowIfFailed(SwapChainGetBuffer(_swapChain, 0, ref textureId, out var backBuffer), "get composition swap chain back buffer");
        return backBuffer;
    }

    internal void Present()
    {
        ThrowIfDisposed();

        if (_swapChain == IntPtr.Zero)
            throw new InvalidOperationException("The composition swap chain has not been created.");

        ThrowIfFailed(SwapChainPresent(_swapChain, syncInterval: 1, flags: 0), "present composition swap chain");
    }

    internal void ResizeBuffers(int width, int height)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (_swapChain == IntPtr.Zero)
            throw new InvalidOperationException("The composition swap chain has not been created.");

        ThrowIfFailed(
            SwapChainResizeBuffers(_swapChain, bufferCount: 0, (uint)width, (uint)height, format: 0, flags: 0),
            "resize composition swap chain buffers");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void EnsureDevice()
    {
        if (_device is not null)
            return;

        var featureLevels = new[]
        {
            D3D11Native.D3DFeatureLevel110,
            D3D11Native.D3DFeatureLevel101,
            D3D11Native.D3DFeatureLevel100
        };

        ThrowIfFailed(
            D3D11Native.D3D11CreateDevice(
                IntPtr.Zero,
                D3D11Native.D3DDriverTypeHardware,
                IntPtr.Zero,
                D3D11Native.D3D11CreateDeviceBgraSupport,
                featureLevels,
                (uint)featureLevels.Length,
                D3D11Native.D3D11SdkVersion,
                out _device,
                out _,
                out _deviceContext),
            "create D3D11 device");

        var unknown = Marshal.GetIUnknownForObject(_device);
        try
        {
            var dxgiDeviceId = typeof(IDXGIDevice).GUID;
            ThrowIfFailed(Marshal.QueryInterface(unknown, in dxgiDeviceId, out var dxgiDevicePointer), "query IDXGIDevice");
            try
            {
                var dxgiDevice = (IDXGIDevice)Marshal.GetObjectForIUnknown(dxgiDevicePointer);
                ThrowIfFailed(dxgiDevice.GetAdapter(out var adapter), "get DXGI adapter");

                var factoryId = typeof(IDXGIFactory2).GUID;
                ThrowIfFailed(adapter.GetParent(ref factoryId, out var factoryPointer), "get DXGI factory");
                try
                {
                    _factory = (IDXGIFactory2)Marshal.GetObjectForIUnknown(factoryPointer);
                }
                finally
                {
                    Marshal.Release(factoryPointer);
                }

                Marshal.FinalReleaseComObject(adapter);
                Marshal.ReleaseComObject(dxgiDevice);
            }
            finally
            {
                Marshal.Release(dxgiDevicePointer);
            }
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    private IntPtr CreateSwapChain(int width, int height)
    {
        if (_device is null || _factory is null)
            throw new InvalidOperationException("The D3D11 host has not been initialized.");

        var desc = new D3D11Native.DxgiSwapChainDesc1
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = D3D11Native.DxgiFormatB8G8R8A8Unorm,
            Stereo = 0,
            SampleDesc = new D3D11Native.DxgiSampleDesc
            {
                Count = 1,
                Quality = 0
            },
            BufferUsage = D3D11Native.DxgiUsageRenderTargetOutput,
            BufferCount = 2,
            Scaling = D3D11Native.DxgiScalingStretch,
            SwapEffect = D3D11Native.DxgiSwapEffectFlipSequential,
            AlphaMode = D3D11Native.DxgiAlphaModeIgnore,
            Flags = 0
        };

        var devicePointer = Marshal.GetIUnknownForObject(_device);
        var descPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<D3D11Native.DxgiSwapChainDesc1>());
        try
        {
            Marshal.StructureToPtr(desc, descPointer, false);
            ThrowIfFailed(_factory.CreateSwapChainForComposition(devicePointer, descPointer, IntPtr.Zero, out var swapChain), "create composition swap chain");
            return swapChain;
        }
        finally
        {
            Marshal.FreeCoTaskMem(descPointer);
            Marshal.Release(devicePointer);
        }
    }

    private static ISwapChainPanelNative GetPanelNative(SwapChainPanel panel)
    {
        var unknown = Marshal.GetIUnknownForObject(panel);
        try
        {
            var panelNativeId = typeof(ISwapChainPanelNative).GUID;
            ThrowIfFailed(Marshal.QueryInterface(unknown, in panelNativeId, out var panelNativePointer), "query ISwapChainPanelNative");
            try
            {
                return (ISwapChainPanelNative)Marshal.GetObjectForIUnknown(panelNativePointer);
            }
            finally
            {
                Marshal.Release(panelNativePointer);
            }
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (_panelNative is not null)
        {
            _panelNative.SetSwapChain(IntPtr.Zero);
            Marshal.FinalReleaseComObject(_panelNative);
            _panelNative = null;
        }

        if (_swapChain != IntPtr.Zero)
        {
            Marshal.Release(_swapChain);
            _swapChain = IntPtr.Zero;
        }

        if (_factory is not null)
        {
            Marshal.FinalReleaseComObject(_factory);
            _factory = null;
        }

        if (_deviceContext != IntPtr.Zero)
        {
            Marshal.Release(_deviceContext);
            _deviceContext = IntPtr.Zero;
        }

        if (_device is not null)
        {
            Marshal.FinalReleaseComObject(_device);
            _device = null;
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void ThrowIfFailed(int result, string operation)
    {
        if (result >= 0)
            return;

        throw new InvalidOperationException($"{operation} failed.", Marshal.GetExceptionForHR(result));
    }

    private static int SwapChainPresent(IntPtr swapChain, uint syncInterval, uint flags)
    {
        var present = GetSwapChainMethod<PresentDelegate>(swapChain, 8);
        return present(swapChain, syncInterval, flags);
    }

    private static int SwapChainGetBuffer(IntPtr swapChain, uint buffer, ref Guid riid, out IntPtr surface)
    {
        var getBuffer = GetSwapChainMethod<GetBufferDelegate>(swapChain, 9);
        return getBuffer(swapChain, buffer, ref riid, out surface);
    }

    private static int SwapChainResizeBuffers(IntPtr swapChain, uint bufferCount, uint width, uint height, uint format, uint flags)
    {
        var resize = GetSwapChainMethod<ResizeBuffersDelegate>(swapChain, 13);
        return resize(swapChain, bufferCount, width, height, format, flags);
    }

    private static TDelegate GetSwapChainMethod<TDelegate>(IntPtr swapChain, int slot)
        where TDelegate : Delegate
    {
        var vtable = Marshal.ReadIntPtr(swapChain);
        var method = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(method);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PresentDelegate(IntPtr self, uint syncInterval, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetBufferDelegate(IntPtr self, uint buffer, ref Guid riid, out IntPtr surface);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ResizeBuffersDelegate(IntPtr self, uint bufferCount, uint width, uint height, uint format, uint flags);
}
