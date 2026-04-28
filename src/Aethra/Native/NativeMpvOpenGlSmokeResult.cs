using System;

namespace Aethra.Native;

internal sealed record NativeMpvOpenGlSmokeResult(
    bool FileLoaded,
    bool FrameRendered,
    bool ShutdownReceived,
    TimeSpan Elapsed,
    int Width,
    int Height,
    uint GlError);
