using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVFrameInstancePointerAction(AVFrame** packet);

public unsafe class AVFrameRef : AVInstanceRef<AVFrame, AVFrameInstancePointerAction>
{
    public AVFrameRef()
        : this((AVFrame*)IntPtr.Zero)
    {
    }

    public AVFrameRef(AVFrame* packet)
        : base(packet)
    {
    }

    public override void InvokeOnInstancePointer(AVFrameInstancePointerAction action)
    {
        fixed (AVFrame** packet = &_nativePointer)
        {
            action?.Invoke(packet);
        }
    }

    public static implicit operator AVFrame*(AVFrameRef packet) =>
        packet._nativePointer;

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            InvokeOnInstancePointer(ffmpeg.av_frame_free);
        }
    }
}
