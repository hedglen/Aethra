namespace Aethra.Native;

internal sealed record AngleEglInfo(
    int EglMajor,
    int EglMinor,
    int ClientVersion,
    string? EglVendor,
    string? EglVersion,
    string? GlVendor,
    string? GlRenderer,
    string? GlVersion);
