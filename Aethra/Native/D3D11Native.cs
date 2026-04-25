using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal static class D3D11Native
{
    internal const uint D3D11CreateDeviceBgraSupport = 0x20;
    internal const uint D3D11SdkVersion = 7;
    internal const int D3DDriverTypeHardware = 1;
    internal const int D3DFeatureLevel110 = 0xB000;
    internal const int D3DFeatureLevel101 = 0xA100;
    internal const int D3DFeatureLevel100 = 0xA000;

    internal const int DxgiFormatB8G8R8A8Unorm = 87;
    internal const uint DxgiUsageShaderInput = 0x10;
    internal const uint DxgiUsageRenderTargetOutput = 0x20;
    internal const int DxgiScalingStretch = 0;
    internal const int DxgiSwapEffectFlipSequential = 3;
    internal const int DxgiAlphaModeIgnore = 3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct DxgiSampleDesc
    {
        internal uint Count;
        internal uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DxgiSwapChainDesc1
    {
        internal uint Width;
        internal uint Height;
        internal int Format;
        internal int Stereo;
        internal DxgiSampleDesc SampleDesc;
        internal uint BufferUsage;
        internal uint BufferCount;
        internal int Scaling;
        internal int SwapEffect;
        internal int AlphaMode;
        internal uint Flags;
    }

    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
    internal static extern int D3D11CreateDevice(
        IntPtr adapter,
        int driverType,
        IntPtr software,
        uint flags,
        int[] featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out ID3D11Device device,
        out int featureLevel,
        out IntPtr immediateContext);
}

[ComImport]
[Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11Device
{
}

[ComImport]
[Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11Texture2D
{
}

[ComImport]
[Guid("ae02eedb-c735-4690-8d52-5a8dc20213aa")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIObject
{
    [PreserveSig]
    int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

    [PreserveSig]
    int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

    [PreserveSig]
    int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

    [PreserveSig]
    int GetParent(ref Guid riid, out IntPtr parent);
}

[ComImport]
[Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIDevice : IDXGIObject
{
    [PreserveSig]
    new int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

    [PreserveSig]
    new int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

    [PreserveSig]
    new int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

    [PreserveSig]
    new int GetParent(ref Guid riid, out IntPtr parent);

    [PreserveSig]
    int GetAdapter(out IDXGIAdapter adapter);

    [PreserveSig]
    int CreateSurface(IntPtr desc, uint numSurfaces, uint usage, IntPtr sharedResource, out IntPtr surface);

    [PreserveSig]
    int QueryResourceResidency(IntPtr resources, IntPtr residencyStatus, uint numResources);

    [PreserveSig]
    int SetGPUThreadPriority(int priority);

    [PreserveSig]
    int GetGPUThreadPriority(out int priority);
}

[ComImport]
[Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIAdapter : IDXGIObject
{
    [PreserveSig]
    new int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

    [PreserveSig]
    new int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

    [PreserveSig]
    new int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

    [PreserveSig]
    new int GetParent(ref Guid riid, out IntPtr parent);

    [PreserveSig]
    int EnumOutputs(uint output, out IntPtr outputPointer);

    [PreserveSig]
    int GetDesc(IntPtr desc);

    [PreserveSig]
    int CheckInterfaceSupport(ref Guid interfaceName, out long umdVersion);
}

[ComImport]
[Guid("50c83a1c-e072-4c48-87b0-3630fa36a6d0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIFactory2 : IDXGIObject
{
    [PreserveSig]
    new int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

    [PreserveSig]
    new int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

    [PreserveSig]
    new int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

    [PreserveSig]
    new int GetParent(ref Guid riid, out IntPtr parent);

    [PreserveSig]
    int EnumAdapters(uint adapter, out IntPtr adapterPointer);

    [PreserveSig]
    int MakeWindowAssociation(IntPtr windowHandle, uint flags);

    [PreserveSig]
    int GetWindowAssociation(out IntPtr windowHandle);

    [PreserveSig]
    int CreateSwapChain(IntPtr device, IntPtr desc, out IntPtr swapChain);

    [PreserveSig]
    int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);

    [PreserveSig]
    int EnumAdapters1(uint adapter, out IntPtr adapterPointer);

    [PreserveSig]
    int IsCurrent();

    [PreserveSig]
    int IsWindowedStereoEnabled();

    [PreserveSig]
    int CreateSwapChainForHwnd(IntPtr device, IntPtr hwnd, IntPtr desc, IntPtr fullscreenDesc, IntPtr restrictToOutput, out IntPtr swapChain);

    [PreserveSig]
    int CreateSwapChainForCoreWindow(IntPtr device, IntPtr window, IntPtr desc, IntPtr restrictToOutput, out IntPtr swapChain);

    [PreserveSig]
    int GetSharedResourceAdapterLuid(IntPtr resource, out long luid);

    [PreserveSig]
    int RegisterStereoStatusWindow(IntPtr windowHandle, uint message, out uint cookie);

    [PreserveSig]
    int RegisterStereoStatusEvent(IntPtr eventHandle, out uint cookie);

    [PreserveSig]
    void UnregisterStereoStatus(uint cookie);

    [PreserveSig]
    int RegisterOcclusionStatusWindow(IntPtr windowHandle, uint message, out uint cookie);

    [PreserveSig]
    int RegisterOcclusionStatusEvent(IntPtr eventHandle, out uint cookie);

    [PreserveSig]
    void UnregisterOcclusionStatus(uint cookie);

    [PreserveSig]
    int CreateSwapChainForComposition(
        IntPtr device,
        IntPtr desc,
        IntPtr restrictToOutput,
        out IntPtr swapChain);
}

[ComImport]
[Guid("790a45f7-0d42-4876-983a-0a55cfe6f4aa")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGISwapChain1 : IDXGIObject
{
    [PreserveSig]
    new int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

    [PreserveSig]
    new int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

    [PreserveSig]
    new int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

    [PreserveSig]
    new int GetParent(ref Guid riid, out IntPtr parent);

    [PreserveSig]
    int GetDevice(ref Guid riid, out IntPtr device);

    [PreserveSig]
    int Present(uint syncInterval, uint flags);

    [PreserveSig]
    int GetBuffer(uint buffer, ref Guid riid, out IntPtr surface);

    [PreserveSig]
    int SetFullscreenState(int fullscreen, IntPtr target);

    [PreserveSig]
    int GetFullscreenState(out int fullscreen, out IntPtr target);

    [PreserveSig]
    int GetDesc(IntPtr desc);

    [PreserveSig]
    int ResizeBuffers(uint bufferCount, uint width, uint height, int newFormat, uint swapChainFlags);

    [PreserveSig]
    int ResizeTarget(IntPtr newTargetParameters);

    [PreserveSig]
    int GetContainingOutput(out IntPtr output);

    [PreserveSig]
    int GetFrameStatistics(IntPtr stats);

    [PreserveSig]
    int GetLastPresentCount(out uint lastPresentCount);
}

[ComImport]
[Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISwapChainPanelNative
{
    [PreserveSig]
    int SetSwapChain(IntPtr swapChain);
}
