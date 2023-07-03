using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe class AVInputFormatContextRef : AVFormatContextRef
{
    public AVInputFormatContextRef()
        : this((AVFormatContext*)IntPtr.Zero)
    {
    }

    public AVInputFormatContextRef(AVFormatContext* context)
        : base(context)
    {
    }

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            InvokeOnInstancePointer(ffmpeg.avformat_close_input);
        }
    }
}
