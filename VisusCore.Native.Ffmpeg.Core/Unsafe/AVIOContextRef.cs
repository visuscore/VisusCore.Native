using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVIOContextInstancePointerAction(AVIOContext** context);

// Keeping the name of the native type here to avoid confusion with the managed type.
#pragma warning disable S101 // Types should be named in PascalCase
public unsafe class AVIOContextRef : AVInstanceRef<AVIOContext, AVIOContextInstancePointerAction>
#pragma warning restore S101 // Types should be named in PascalCase
{
    public AVIOContextRef()
        : this((AVIOContext*)IntPtr.Zero)
    {
    }

    public AVIOContextRef(AVIOContext* context)
        : base(context)
    {
    }

    public override void InvokeOnInstancePointer(AVIOContextInstancePointerAction action)
    {
        fixed (AVIOContext** context = &_nativePointer)
        {
            action?.Invoke(context);
        }
    }

    public static implicit operator AVIOContext*(AVIOContextRef context) =>
        context._nativePointer;

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            InvokeOnInstancePointer(ffmpeg.avio_context_free);
        }
    }
}
