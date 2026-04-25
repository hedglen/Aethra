using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed record NativeMpvRenderApiProbeResult(
    string ApiType,
    bool Accepted,
    int ErrorCode,
    string? ErrorMessage);

internal static class NativeMpvRenderApiProbe
{
    internal static IReadOnlyList<NativeMpvRenderApiProbeResult> ProbeAll()
    {
        return new[]
        {
            Probe(MpvNative.RenderApiTypeD3D11),
            Probe(MpvNative.RenderApiTypeOpenGl),
            Probe(MpvNative.RenderApiTypeSoftware),
        };
    }

    internal static NativeMpvRenderApiProbeResult Probe(string apiType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiType);

        using var context = new NativeMpvContext();
        context.SetOptionString("config", "no");
        context.SetOptionString("idle", "yes");
        context.SetOptionString("terminal", "no");
        context.SetOptionString("vo", "libmpv");
        context.TrySetOptionString("osc", "no");
        context.SetOptionString("osd-level", "0");
        context.TrySetOptionString("input-default-bindings", "no");
        context.TrySetOptionString("input-vo-keyboard", "no");
        context.Initialize();

        IntPtr apiTypeUtf8 = IntPtr.Zero;
        IntPtr openGlInitParams = IntPtr.Zero;
        IntPtr parameters = IntPtr.Zero;
        IntPtr renderContext = IntPtr.Zero;
        MpvNative.MpvOpenGlGetProcAddressCallback? stubGetProcAddress = null;

        try
        {
            apiTypeUtf8 = Marshal.StringToCoTaskMemUTF8(apiType);

            if (apiType == MpvNative.RenderApiTypeOpenGl)
            {
                stubGetProcAddress = (_, _) => IntPtr.Zero;
                openGlInitParams = AllocateOpenGlInitParams(stubGetProcAddress);

                parameters = AllocateRenderParameters(
                    new MpvNative.MpvRenderParam
                    {
                        Type = MpvNative.MpvRenderParamType.ApiType,
                        Data = apiTypeUtf8
                    },
                    new MpvNative.MpvRenderParam
                    {
                        Type = MpvNative.MpvRenderParamType.OpenGlInitParams,
                        Data = openGlInitParams
                    });
            }
            else
            {
                parameters = AllocateRenderParameters(
                    new MpvNative.MpvRenderParam
                    {
                        Type = MpvNative.MpvRenderParamType.ApiType,
                        Data = apiTypeUtf8
                    });
            }

            var result = MpvNative.RenderContextCreate(out renderContext, context.Handle, parameters);

            if (result >= 0)
                return new NativeMpvRenderApiProbeResult(apiType, Accepted: true, ErrorCode: result, ErrorMessage: null);

            return new NativeMpvRenderApiProbeResult(
                apiType,
                Accepted: false,
                ErrorCode: result,
                ErrorMessage: GetErrorMessage(result));
        }
        catch (Exception ex)
        {
            return new NativeMpvRenderApiProbeResult(
                apiType,
                Accepted: false,
                ErrorCode: int.MinValue,
                ErrorMessage: ex.Message);
        }
        finally
        {
            if (renderContext != IntPtr.Zero)
                MpvNative.RenderContextFree(renderContext);

            if (parameters != IntPtr.Zero)
                Marshal.FreeCoTaskMem(parameters);

            if (openGlInitParams != IntPtr.Zero)
                Marshal.FreeCoTaskMem(openGlInitParams);

            if (apiTypeUtf8 != IntPtr.Zero)
                Marshal.FreeCoTaskMem(apiTypeUtf8);

            GC.KeepAlive(stubGetProcAddress);
        }
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

    private static IntPtr AllocateOpenGlInitParams(MpvNative.MpvOpenGlGetProcAddressCallback callback)
    {
        var pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<MpvNative.MpvOpenGlInitParams>());
        Marshal.StructureToPtr(
            new MpvNative.MpvOpenGlInitParams
            {
                GetProcAddress = Marshal.GetFunctionPointerForDelegate(callback),
                GetProcAddressContext = IntPtr.Zero
            },
            pointer,
            false);
        return pointer;
    }

    private static string? GetErrorMessage(int errorCode)
    {
        var pointer = MpvNative.ErrorString(errorCode);
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);
    }
}
