using System;

namespace Aethra.Native;

internal sealed record NativeMpvSoftwareSmokeResult(
    bool FileLoaded,
    bool FrameRendered,
    bool ShutdownReceived,
    TimeSpan Elapsed,
    int Width,
    int Height,
    int Stride,
    int BufferLength);
