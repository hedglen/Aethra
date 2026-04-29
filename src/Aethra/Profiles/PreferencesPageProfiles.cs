namespace Aethra.Profiles;

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

    public static ProfilesPreferencesProfile CreateDefault() => new();
}

public sealed class PreferencesPageProfiles
{
    public PlaybackPreferencesProfile Playback { get; set; } = PlaybackPreferencesProfile.CreateDefault();
    public AudioPreferencesProfile Audio { get; set; } = AudioPreferencesProfile.CreateDefault();
    public SubtitlePreferencesProfile Subtitles { get; set; } = SubtitlePreferencesProfile.CreateDefault();
    public LibraryPreferencesProfile Library { get; set; } = LibraryPreferencesProfile.CreateDefault();
    public ProfilesPreferencesProfile Profiles { get; set; } = ProfilesPreferencesProfile.CreateDefault();

    public static PreferencesPageProfiles CreateDefault() => new();
}
