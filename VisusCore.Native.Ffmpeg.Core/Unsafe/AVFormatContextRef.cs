using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVFormatContextInstancePointerAction(AVFormatContext** context);

public unsafe class AVFormatContextRef : AVInstanceRef<AVFormatContext, AVFormatContextInstancePointerAction>
{
    public AVFormatContextRef()
        : this((AVFormatContext*)IntPtr.Zero)
    {
    }

    public AVFormatContextRef(AVFormatContext* context)
        : base(context)
    {
    }

    public override void InvokeOnInstancePointer(AVFormatContextInstancePointerAction action)
    {
        fixed (AVFormatContext** context = &_nativePointer)
        {
            action?.Invoke(context);
        }
    }

    public static implicit operator AVFormatContext*(AVFormatContextRef context) =>
        context._nativePointer;

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            ffmpeg.avformat_free_context(_nativePointer);
        }
    }
}
