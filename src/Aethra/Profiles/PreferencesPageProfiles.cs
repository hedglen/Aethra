using System.Collections.Generic;

namespace Aethra.Profiles;

public enum VideoOutputMode
{
    GpuNext = 0,
    Gpu = 1
}

public enum HardwareDecodeMode
{
    Auto = 0,
    Nvdec = 1,
    Dxva2 = 2,
    Copy = 3
}

public enum AdvancedLogLevel
{
    Off = 0,
    Warnings = 1,
    Verbose = 2,
    Debug = 3
}

public sealed class PlaybackPreferencesProfile
{
    public bool ResumeWhereLeftOff { get; set; } = true;
    public bool AutoplayOnOpen { get; set; } = true;
    public PlaybackEndOfFileAction EndOfFileAction { get; set; } = PlaybackEndOfFileAction.Stop;
    public double DefaultPlaybackSpeedPercent { get; set; } = 100;

    public static PlaybackPreferencesProfile CreateDefault() => new();
}

public sealed class AudioPreferencesProfile
{
    public string OutputDevice { get; set; } = "System default";
    public bool DynamicRangeCompression { get; set; }
    public bool ReplayGainNormalization { get; set; }
    public AudioChannelLayout ChannelLayout { get; set; } = AudioChannelLayout.Auto;

    public static AudioPreferencesProfile CreateDefault() => new();
}

public sealed class VideoPreferencesProfile
{
    public VideoOutputMode OutputMode { get; set; } = VideoOutputMode.GpuNext;
    public HardwareDecodeMode HardwareDecode { get; set; } = HardwareDecodeMode.Auto;
    public bool InterpolationEnabled { get; set; }
    public bool DeinterlaceEnabled { get; set; }

    public static VideoPreferencesProfile CreateDefault() => new();
}

public sealed class SubtitlePreferencesProfile
{
    public bool AutoLoadMatchingSubtitles { get; set; } = true;
    public string PreferredLanguagesCsv { get; set; } = "eng,jpn";
    public double FontSize { get; set; } = 40;
    public bool BorderAndShadow { get; set; } = true;

    public static SubtitlePreferencesProfile CreateDefault() => new();
}

public sealed class LibraryPreferencesProfile
{
    public bool WatchFoldersEnabled { get; set; }
    public bool RememberRecentFiles { get; set; } = true;

    public static LibraryPreferencesProfile CreateDefault() => new();
}

public sealed class ProfilesPreferencesProfile
{
    public string ActiveProfileName { get; set; } = "Default";
    public List<NamedPreferencesProfileBundle> Bundles { get; set; } = new()
    {
        NamedPreferencesProfileBundle.CreateDefault()
    };

    public static ProfilesPreferencesProfile CreateDefault() => new();
}

public sealed class AdvancedPreferencesProfile
{
    public AdvancedLogLevel LogLevel { get; set; } = AdvancedLogLevel.Warnings;
    public string ExtraMpvOptionsText { get; set; } = string.Empty;

    public static AdvancedPreferencesProfile CreateDefault() => new();
}

public sealed class NamedPreferencesProfileBundle
{
    public string Name { get; set; } = "Default";
    public PlaybackPreferencesProfile Playback { get; set; } = PlaybackPreferencesProfile.CreateDefault();
    public VideoPreferencesProfile Video { get; set; } = VideoPreferencesProfile.CreateDefault();
    public AudioPreferencesProfile Audio { get; set; } = AudioPreferencesProfile.CreateDefault();
    public SubtitlePreferencesProfile Subtitles { get; set; } = SubtitlePreferencesProfile.CreateDefault();
    public LibraryPreferencesProfile Library { get; set; } = LibraryPreferencesProfile.CreateDefault();
    public AdvancedPreferencesProfile Advanced { get; set; } = AdvancedPreferencesProfile.CreateDefault();

    public static NamedPreferencesProfileBundle CreateDefault() => new();
}

public sealed class PreferencesPageProfiles
{
    public PlaybackPreferencesProfile Playback { get; set; } = PlaybackPreferencesProfile.CreateDefault();
    public VideoPreferencesProfile Video { get; set; } = VideoPreferencesProfile.CreateDefault();
    public AudioPreferencesProfile Audio { get; set; } = AudioPreferencesProfile.CreateDefault();
    public SubtitlePreferencesProfile Subtitles { get; set; } = SubtitlePreferencesProfile.CreateDefault();
    public LibraryPreferencesProfile Library { get; set; } = LibraryPreferencesProfile.CreateDefault();
    public AdvancedPreferencesProfile Advanced { get; set; } = AdvancedPreferencesProfile.CreateDefault();
    public ProfilesPreferencesProfile Profiles { get; set; } = ProfilesPreferencesProfile.CreateDefault();

    public static PreferencesPageProfiles CreateDefault() => new();
}
