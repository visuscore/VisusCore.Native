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

    public static void ThrowIfError(int errorCode, string message)
    {
        if (errorCode < 0)
        {
            throw new AVException(
                string.Join(Environment.NewLine, message, GetErrorMessage(errorCode)));
        }
    }

    public static void ThrowIfNull(IntPtr nativePointer, string message)
    {
        if (nativePointer == IntPtr.Zero)
        {
            throw new AVException(message);
        }
    }

    public static TInstanceRef ThrowIfNull<TInstanceRef>(TInstanceRef instanceReference, string message)
        where TInstanceRef : IAVInstanceRef
    {
        ThrowIfNull(instanceReference.NativePointer, message);

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
