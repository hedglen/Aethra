using System;
using System.Collections.Generic;
using Aethra.Models;

namespace Aethra.Native;

internal interface INativeMpvPlayerBackend : IDisposable
{
    event EventHandler<NativeMpvPlaybackProgress>? ProgressChanged;
    event EventHandler<bool>? PlaybackPausedChanged;
    event EventHandler<IReadOnlyList<MpvChapter>>? ChaptersChanged;

    void LoadFile(string path);
    void TogglePause();
    void Pause();
    void SetProperty(string name, double value);
    void SetProperty(string name, string value);
    void Seek(double seconds);
    void SeekToTime(double seconds);
    void SeekToPercent(double percent);
    void SetVolume(double value);
}
