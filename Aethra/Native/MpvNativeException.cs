using System;

namespace Aethra.Native;

internal sealed class MpvNativeException : InvalidOperationException
{
    internal MpvNativeException(string operation, int errorCode)
        : base($"{operation} failed with mpv error {errorCode}.")
    {
        ErrorCode = errorCode;
    }

    internal int ErrorCode { get; }
}
