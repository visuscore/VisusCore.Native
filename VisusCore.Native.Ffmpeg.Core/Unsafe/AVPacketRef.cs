using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVPacketInstancePointerAction(AVPacket** packet);

public unsafe class AVPacketRef : AVInstanceRef<AVPacket, AVPacketInstancePointerAction>
{
    public AVPacketRef()
        : this((AVPacket*)IntPtr.Zero)
    {
    }

    public AVPacketRef(AVPacket* packet)
        : base(packet)
    {
    }

    public override void InvokeOnInstancePointer(AVPacketInstancePointerAction action)
    {
        fixed (AVPacket** packet = &_nativePointer)
        {
            action?.Invoke(packet);
        }
    }

    public static implicit operator AVPacket*(AVPacketRef packet)
    {
        if (packet is null)
        {
            return null;
        }

        return packet._nativePointer;
    }

    public static implicit operator ReadOnlySpan<byte>(AVPacketRef packet)
    {
        if (packet is null)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return new(packet._nativePointer->buf->data, Convert.ToInt32(packet._nativePointer->buf->size));
    }

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            InvokeOnInstancePointer(ffmpeg.av_packet_free);
        }
    }
}
