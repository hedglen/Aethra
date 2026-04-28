using System;
using System.Runtime.InteropServices;

namespace Aethra.Native;

internal sealed class NativeMpvContext : IDisposable
{
    private IntPtr _context;
    private MpvNative.MpvWakeupCallback? _wakeupCallback;
    private bool _disposed;

    internal NativeMpvContext()
    {
        _context = MpvNative.Create();

        if (_context == IntPtr.Zero)
            throw new InvalidOperationException("mpv_create returned a null context.");
    }

    ~NativeMpvContext()
    {
        Dispose(false);
    }

    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _context;
        }
    }

    internal void Initialize()
    {
        ThrowIfDisposed();
        ThrowIfError(MpvNative.Initialize(_context), "mpv_initialize");
    }

    internal void SetOptionString(string name, string value)
    {
        ThrowIfDisposed();
        ThrowIfError(MpvNative.SetOptionString(_context, name, value), $"set option '{name}'");
    }

    internal bool TrySetOptionString(string name, string value)
    {
        ThrowIfDisposed();
        return MpvNative.SetOptionString(_context, name, value) >= 0;
    }

    internal void SetPropertyString(string name, string value)
    {
        ThrowIfDisposed();
        ThrowIfError(MpvNative.SetPropertyString(_context, name, value), $"set property '{name}'");
    }

    internal string? GetPropertyString(string name)
    {
        ThrowIfDisposed();

        var value = MpvNative.GetPropertyString(_context, name);
        if (value == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8(value);
        }
        finally
        {
            MpvNative.Free(value);
        }
    }

    internal void Command(params string[] args)
    {
        ThrowIfDisposed();
        ThrowIfError(CallCommand(args, argv => MpvNative.Command(_context, argv)), $"command '{GetCommandName(args)}'");
    }

    internal void CommandAsync(ulong replyUserData, params string[] args)
    {
        ThrowIfDisposed();
        ThrowIfError(
            CallCommand(args, argv => MpvNative.CommandAsync(_context, replyUserData, argv)),
            $"async command '{GetCommandName(args)}'");
    }

    internal void ObserveProperty(ulong replyUserData, string name, MpvNative.MpvFormat format)
    {
        ThrowIfDisposed();
        ThrowIfError(MpvNative.ObserveProperty(_context, replyUserData, name, format), $"observe property '{name}'");
    }

    internal void SetWakeupCallback(Action wakeup)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(wakeup);

        _wakeupCallback = _ => wakeup();
        MpvNative.SetWakeupCallback(_context, _wakeupCallback, IntPtr.Zero);
    }

    internal MpvNative.MpvEvent? WaitEvent(double timeout)
    {
        ThrowIfDisposed();

        var eventPointer = MpvNative.WaitEvent(_context, timeout);
        if (eventPointer == IntPtr.Zero)
            return null;

        return Marshal.PtrToStructure<MpvNative.MpvEvent>(eventPointer);
    }

    internal void DrainEvents(Action<MpvNative.MpvEvent> eventHandler)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(eventHandler);

        while (true)
        {
            var mpvEvent = WaitEvent(0);
            if (mpvEvent is not { EventId: not MpvNative.MpvEventId.None })
                return;

            eventHandler(mpvEvent.Value);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (_context != IntPtr.Zero)
        {
            MpvNative.TerminateDestroy(_context);
            _context = IntPtr.Zero;
        }

        _wakeupCallback = null;
        _disposed = true;
    }

    private int CallCommand(string[] args, Func<IntPtr, int> command)
    {
        if (args.Length == 0)
            throw new ArgumentException("mpv command arguments cannot be empty.", nameof(args));

        var stringPointers = new IntPtr[args.Length];
        var argv = Marshal.AllocCoTaskMem(IntPtr.Size * (args.Length + 1));

        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                stringPointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
                Marshal.WriteIntPtr(argv, i * IntPtr.Size, stringPointers[i]);
            }

            Marshal.WriteIntPtr(argv, args.Length * IntPtr.Size, IntPtr.Zero);
            return command(argv);
        }
        finally
        {
            foreach (var stringPointer in stringPointers)
            {
                if (stringPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(stringPointer);
            }

            Marshal.FreeCoTaskMem(argv);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static string GetCommandName(string[] args)
    {
        return args.Length == 0 ? "<empty>" : args[0];
    }

    private static void ThrowIfError(int result, string operation)
    {
        if (result < 0)
            throw new MpvNativeException(operation, result);
    }
}
