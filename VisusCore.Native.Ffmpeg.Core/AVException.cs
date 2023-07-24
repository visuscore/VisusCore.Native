using FFmpeg.AutoGen;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using VisusCore.Native.Core.Unsafe;
using VisusCore.Native.Ffmpeg.Core.Unsafe;

namespace VisusCore.Native.Ffmpeg.Core;

[Serializable]
public class AVException : Exception
{
    public AVException()
    {
    }

    public AVException(string message)
        : base(message)
    {
    }

    public AVException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected AVException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        : base(serializationInfo, streamingContext)
    {
    }

    public static int ThrowIfError(int errorCode, string message, Action cleanup = null, Func<int, bool> except = null)
    {
        if (errorCode < 0 && except?.Invoke(errorCode) is not true)
        {
            cleanup?.Invoke();
            throw new AVException(
                string.Join(Environment.NewLine, message, GetErrorMessage(errorCode)));
        }

        return errorCode;
    }

    public static IntPtr ThrowIfNull(IntPtr nativePointer, string message, Action cleanup = null)
    {
        if (nativePointer == IntPtr.Zero)
        {
            cleanup?.Invoke();
            throw new AVException(message);
        }

        return nativePointer;
    }

    public static TInstanceRef ThrowIfNull<TInstanceRef>(
        TInstanceRef instanceReference,
        string message,
        Action cleanup = null)
        where TInstanceRef : IAVInstanceRef
    {
        ThrowIfNull(instanceReference.NativePointer, message, cleanup);

        return instanceReference;
    }

    private static unsafe string GetErrorMessage(int errorCode)
    {
        using var errorBuffer = new NativeRef<byte>(
            (byte*)NativeMemory.Alloc(ffmpeg.AV_ERROR_MAX_STRING_SIZE));
        ffmpeg.av_make_error_string(errorBuffer.NativePointer, ffmpeg.AV_ERROR_MAX_STRING_SIZE, errorCode);

        return Marshal.PtrToStringAnsi((IntPtr)errorBuffer.NativePointer);
    }
}
