using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal static class MpvNative
{
    private const string LibMpv = "libmpv-2.dll";

    internal const string RenderApiTypeOpenGl = "opengl";
    internal const string RenderApiTypeSoftware = "sw";
    internal const string RenderApiTypeD3D11 = "direct3d11";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MpvWakeupCallback(IntPtr callbackContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MpvRenderUpdateCallback(IntPtr callbackContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr MpvOpenGlGetProcAddressCallback(IntPtr callbackContext, IntPtr name);

    internal enum MpvFormat
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
        Node = 6,
        NodeArray = 7,
        NodeMap = 8,
        ByteArray = 9
    }

    internal enum MpvEventId
    {
        None = 0,
        Shutdown = 1,
        LogMessage = 2,
        GetPropertyReply = 3,
        SetPropertyReply = 4,
        CommandReply = 5,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        ClientMessage = 16,
        VideoReconfig = 17,
        AudioReconfig = 18,
        Seek = 20,
        PlaybackRestart = 21,
        PropertyChange = 22
    }

    internal enum MpvRenderParamType
    {
        Invalid = 0,
        ApiType = 1,
        OpenGlInitParams = 2,
        OpenGlFbo = 3,
        FlipY = 4,
        Depth = 5,
        IccProfile = 6,
        AmbientLight = 7,
        AdvancedControl = 10,
        NextFrameInfo = 11,
        BlockForTargetTime = 12,
        SkipRendering = 13,
        SoftwareSize = 17,
        SoftwareFormat = 18,
        SoftwareStride = 19,
        SoftwarePointer = 20
    }

    [Flags]
    internal enum MpvRenderUpdateFlag : ulong
    {
        None = 0,
        Frame = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEvent
    {
        internal MpvEventId EventId;
        internal int Error;
        internal ulong ReplyUserData;
        internal IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventProperty
    {
        internal IntPtr Name;
        internal MpvFormat Format;
        internal IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvRenderParam
    {
        internal MpvRenderParamType Type;
        internal IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlInitParams
    {
        internal IntPtr GetProcAddress;
        internal IntPtr GetProcAddressContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlFbo
    {
        internal int Fbo;
        internal int Width;
        internal int Height;
        internal int InternalFormat;
    }

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_create")]
    internal static extern IntPtr Create();

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_initialize")]
    internal static extern int Initialize(IntPtr context);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_destroy")]
    internal static extern void Destroy(IntPtr context);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_terminate_destroy")]
    internal static extern void TerminateDestroy(IntPtr context);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_free")]
    internal static extern void Free(IntPtr data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_error_string")]
    internal static extern IntPtr ErrorString(int error);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_option_string")]
    internal static extern int SetOptionString(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_property_string")]
    internal static extern int SetPropertyString(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_get_property_string")]
    internal static extern IntPtr GetPropertyString(
        IntPtr context,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command")]
    internal static extern int Command(IntPtr context, IntPtr args);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command_async")]
    internal static extern int CommandAsync(IntPtr context, ulong replyUserData, IntPtr args);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_observe_property")]
    internal static extern int ObserveProperty(
        IntPtr context,
        ulong replyUserData,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        MpvFormat format);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_wait_event")]
    internal static extern IntPtr WaitEvent(IntPtr context, double timeout);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_wakeup_callback")]
    internal static extern void SetWakeupCallback(
        IntPtr context,
        MpvWakeupCallback callback,
        IntPtr callbackContext);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_render_context_create")]
    internal static extern int RenderContextCreate(out IntPtr renderContext, IntPtr context, IntPtr parameters);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_render_context_set_update_callback")]
    internal static extern void RenderContextSetUpdateCallback(
        IntPtr renderContext,
        MpvRenderUpdateCallback? callback,
        IntPtr callbackContext);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_render_context_update")]
    internal static extern MpvRenderUpdateFlag RenderContextUpdate(IntPtr renderContext);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_render_context_render")]
    internal static extern int RenderContextRender(IntPtr renderContext, IntPtr parameters);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_render_context_report_swap")]
    internal static extern void RenderContextReportSwap(IntPtr renderContext);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_render_context_free")]
    internal static extern void RenderContextFree(IntPtr renderContext);
}
