using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal static class AngleNative
{
    internal const int EglFalse = 0;
    internal const int EglTrue = 1;
    internal const int EglNone = 0x3038;
    internal const int EglPbufferBit = 0x0001;
    internal const int EglWindowBit = 0x0004;
    internal const int EglOpenGlEs2Bit = 0x0004;
    internal const int EglOpenGlEs3Bit = 0x0040;
    internal const int EglOpenGlEsApi = 0x30A0;
    internal const int EglTextureFormat = 0x3080;
    internal const int EglTextureTarget = 0x3081;
    internal const int EglTextureRgba = 0x305E;
    internal const int EglTexture2D = 0x305F;

    internal const uint EglPlatformDeviceExt = 0x313F;
    internal const uint EglPlatformAngleAngle = 0x3202;
    internal const uint EglD3D11DeviceAngle = 0x33A1;
    internal const uint EglD3DTextureAngle = 0x33A3;
    internal const int EglPlatformAngleTypeAngle = 0x3203;
    internal const int EglPlatformAngleTypeD3D11Angle = 0x3208;
    internal const int EglPlatformAngleDeviceTypeAngle = 0x3209;
    internal const int EglPlatformAngleDeviceTypeD3DWarpAngle = 0x320B;
    internal const int EglPlatformAngleEnableAutomaticTrimAngle = 0x320F;
    internal const int EglExperimentalPresentPathAngle = 0x33A4;
    internal const int EglExperimentalPresentPathFastAngle = 0x33AA;

    internal const string EglNativeWindowTypeProperty = "EGLNativeWindowTypeProperty";
    internal const string EglRenderResolutionScaleProperty = "EGLRenderResolutionScaleProperty";

    internal const int EglRedSize = 0x3024;
    internal const int EglGreenSize = 0x3023;
    internal const int EglBlueSize = 0x3022;
    internal const int EglAlphaSize = 0x3021;
    internal const int EglDepthSize = 0x3025;
    internal const int EglStencilSize = 0x3026;
    internal const int EglSurfaceType = 0x3033;
    internal const int EglRenderableType = 0x3040;
    internal const int EglWidth = 0x3057;
    internal const int EglHeight = 0x3056;
    internal const int EglContextClientVersion = 0x3098;

    internal const int EglVendor = 0x3053;
    internal const int EglVersion = 0x3054;
    internal const int EglExtensions = 0x3055;

    internal const uint GlVendor = 0x1F00;
    internal const uint GlRenderer = 0x1F01;
    internal const uint GlVersion = 0x1F02;
    internal const int GlRgba8 = 0x8058;

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglGetPlatformDisplayEXT")]
    internal static extern IntPtr GetPlatformDisplay(uint platform, IntPtr nativeDisplay, int[] attributes);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglGetDisplay")]
    internal static extern IntPtr GetDisplay(IntPtr nativeDisplay);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglInitialize")]
    internal static extern int Initialize(IntPtr display, out int major, out int minor);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglBindAPI")]
    internal static extern int BindApi(int api);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglChooseConfig")]
    internal static extern int ChooseConfig(
        IntPtr display,
        int[] attributes,
        IntPtr[] configs,
        int configSize,
        out int configCount);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglCreatePbufferSurface")]
    internal static extern IntPtr CreatePbufferSurface(IntPtr display, IntPtr config, int[] attributes);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglCreatePbufferFromClientBuffer")]
    internal static extern IntPtr CreatePbufferFromClientBuffer(
        IntPtr display,
        uint bufferType,
        IntPtr buffer,
        IntPtr config,
        int[] attributes);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglCreateWindowSurface")]
    internal static extern IntPtr CreateWindowSurface(IntPtr display, IntPtr config, IntPtr nativeWindow, int[] attributes);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglQuerySurface")]
    internal static extern int QuerySurface(IntPtr display, IntPtr surface, int attribute, out int value);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglCreateContext")]
    internal static extern IntPtr CreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int[] attributes);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglMakeCurrent")]
    internal static extern int MakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglSwapBuffers")]
    internal static extern int SwapBuffers(IntPtr display, IntPtr surface);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglGetProcAddress")]
    internal static extern IntPtr GetProcAddress(IntPtr procname);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglDestroySurface")]
    internal static extern int DestroySurface(IntPtr display, IntPtr surface);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglDestroyContext")]
    internal static extern int DestroyContext(IntPtr display, IntPtr context);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglTerminate")]
    internal static extern int Terminate(IntPtr display);

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglGetError")]
    internal static extern int GetError();

    [DllImport("libEGL.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "eglQueryString")]
    internal static extern IntPtr QueryString(IntPtr display, int name);

    [DllImport("libGLESv2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "glGetString")]
    internal static extern IntPtr GetGlString(uint name);

    [DllImport("libGLESv2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "glViewport")]
    internal static extern void Viewport(int x, int y, int width, int height);

    [DllImport("libGLESv2.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "glGetError")]
    internal static extern uint GetGlError();
}
