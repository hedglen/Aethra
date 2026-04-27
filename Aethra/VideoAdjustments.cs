using System.Collections.Generic;

namespace Aethra;

internal sealed record VideoAdjustment(
    string Id,
    string MpvProperty,
    string DisplayName,
    double Min,
    double Max,
    double Default,
    double Step,
    string ValueFormat);

internal static class VideoAdjustments
{
    internal static readonly IReadOnlyList<VideoAdjustment> All = new List<VideoAdjustment>
    {
        new("brightness",  "brightness",  "Brightness",     -100, 100, 0,    1,    "0;-0"),
        new("contrast",    "contrast",    "Contrast",       -100, 100, 0,    1,    "0;-0"),
        new("saturation",  "saturation",  "Saturation",     -100, 100, 0,    1,    "0;-0"),
        new("gamma",       "gamma",       "Gamma",          -100, 100, 0,    1,    "0;-0"),
        new("hue",         "hue",         "Hue",            -100, 100, 0,    1,    "0;-0"),
        new("sharpen",     "sharpen",     "Sharpness",      -2,   2,   0,    0.05, "0.00;-0.00"),
        new("sub-delay",   "sub-delay",   "Subtitle delay", -10,  10,  0,    0.1,  "0.0 s;-0.0 s"),
        new("audio-delay", "audio-delay", "Audio delay",    -10,  10,  0,    0.1,  "0.0 s;-0.0 s"),
    };
}
