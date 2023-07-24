using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVCodecContextInstancePointerAction(AVCodecContext** context);

public unsafe class AVCodecContextRef : AVInstanceRef<AVCodecContext, AVCodecContextInstancePointerAction>
{
    public AVCodecContextRef()
        : this((AVCodecContext*)IntPtr.Zero)
    {
    }

    public AVCodecContextRef(AVCodecContext* context)
        : base(context)
    {
    }

    public override void InvokeOnInstancePointer(AVCodecContextInstancePointerAction action)
    {
        fixed (AVCodecContext** context = &_nativePointer)
        {
            action?.Invoke(context);
        }
    }

    public static implicit operator AVCodecContext*(AVCodecContextRef context) =>
        context._nativePointer;

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            InvokeOnInstancePointer(ffmpeg.avcodec_free_context);
        }
    }
}
