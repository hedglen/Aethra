using Aethra.Native;

var mediaPath = args.Length > 0 ? args[0] : @"C:\Users\rjh\Videos\test.mp4";

try
{
    NativeRuntimeLoader.Install();

    Console.WriteLine("ANGLE/EGL smoke");
    Console.WriteLine("===============");

    using (var angle = AngleEglContext.CreatePbuffer(width: 16, height: 16))
    {
        Console.WriteLine($"EGL={angle.Info.EglMajor}.{angle.Info.EglMinor}");
        Console.WriteLine($"EGL vendor={angle.Info.EglVendor ?? "unknown"}");
        Console.WriteLine($"EGL version={angle.Info.EglVersion ?? "unknown"}");
        Console.WriteLine($"GLES client={angle.Info.ClientVersion}");
        Console.WriteLine($"GL vendor={angle.Info.GlVendor ?? "unknown"}");
        Console.WriteLine($"GL renderer={angle.Info.GlRenderer ?? "unknown"}");
        Console.WriteLine($"GL version={angle.Info.GlVersion ?? "unknown"}");
    }

    Console.WriteLine();

    Console.WriteLine("mpv render API probe");
    Console.WriteLine("====================");

    // mpv returns MPV_ERROR_NOT_IMPLEMENTED (-19) when the api-type string does
    // not match any render backend compiled into libmpv. Any other negative
    // error means the backend IS compiled in but init failed for this probe -
    // usually because the probe didn't provide a real graphics context.
    const int MpvErrorNotImplemented = -19;

    var probeResults = NativeMpvRenderApiProbe.ProbeAll();
    foreach (var probe in probeResults)
    {
        string status;
        if (probe.Accepted)
            status = "ACCEPTED";
        else if (probe.ErrorCode == MpvErrorNotImplemented)
            status = "NOT COMPILED IN";
        else
            status = "RECOGNIZED (init failed - needs real context)";

        var errorText = probe.Accepted
            ? string.Empty
            : $" (error {probe.ErrorCode}: {probe.ErrorMessage ?? "unknown"})";
        Console.WriteLine($"  {probe.ApiType,-12} {status}{errorText}");
    }

    static bool IsUsable(NativeMpvRenderApiProbeResult result)
        => result.Accepted || result.ErrorCode != MpvErrorNotImplemented;

    string recommendation;
    if (TryFind(probeResults, MpvNative.RenderApiTypeD3D11, IsUsable, out _))
        recommendation = "direct3d11 (use the D3D11 path in the plan)";
    else if (TryFind(probeResults, MpvNative.RenderApiTypeOpenGl, IsUsable, out _))
        recommendation = "opengl (fall back to OpenGL via ANGLE per the plan)";
    else if (TryFind(probeResults, MpvNative.RenderApiTypeSoftware, IsUsable, out _))
        recommendation = "sw (software only - investigate libmpv build)";
    else
        recommendation = "none accepted - investigate libmpv build";

    Console.WriteLine();
    Console.WriteLine($"Recommended render API: {recommendation}");
    Console.WriteLine();

    Console.WriteLine("Native mpv software smoke");
    Console.WriteLine("=========================");

    var smoke = await NativeMpvSoftwareSmokeRunner.RunAsync(mediaPath);
    Console.WriteLine($"FileLoaded={smoke.FileLoaded}");
    Console.WriteLine($"FrameRendered={smoke.FrameRendered}");
    Console.WriteLine($"ShutdownReceived={smoke.ShutdownReceived}");
    Console.WriteLine($"Elapsed={smoke.Elapsed}");
    Console.WriteLine($"Width={smoke.Width}");
    Console.WriteLine($"Height={smoke.Height}");
    Console.WriteLine($"Stride={smoke.Stride}");
    Console.WriteLine($"BufferLength={smoke.BufferLength}");

    Console.WriteLine();
    Console.WriteLine("Native mpv OpenGL smoke");
    Console.WriteLine("=======================");

    var openGlSmoke = await NativeMpvOpenGlSmokeRunner.RunAsync(mediaPath);
    Console.WriteLine($"FileLoaded={openGlSmoke.FileLoaded}");
    Console.WriteLine($"FrameRendered={openGlSmoke.FrameRendered}");
    Console.WriteLine($"ShutdownReceived={openGlSmoke.ShutdownReceived}");
    Console.WriteLine($"Elapsed={openGlSmoke.Elapsed}");
    Console.WriteLine($"Width={openGlSmoke.Width}");
    Console.WriteLine($"Height={openGlSmoke.Height}");
    Console.WriteLine($"GlError=0x{openGlSmoke.GlError:X}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("TempMpv harness failed");
    Console.Error.WriteLine(ex);
    return 1;
}

static bool TryFind(
    IReadOnlyList<NativeMpvRenderApiProbeResult> results,
    string apiType,
    Func<NativeMpvRenderApiProbeResult, bool> predicate,
    out NativeMpvRenderApiProbeResult? match)
{
    foreach (var result in results)
    {
        if (result.ApiType == apiType && predicate(result))
        {
            match = result;
            return true;
        }
    }

    match = null;
    return false;
}
