using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void SwsContextInstancePointerAction(SwsContext** context);

public unsafe class SwsContextRef : AVInstanceRef<SwsContext, SwsContextInstancePointerAction>
{
    public SwsContextRef()
        : this((SwsContext*)IntPtr.Zero)
    {
    }

    public SwsContextRef(SwsContext* context)
        : base(context)
    {
    }

    public override void InvokeOnInstancePointer(SwsContextInstancePointerAction action)
    {
        fixed (SwsContext** context = &_nativePointer)
        {
            action?.Invoke(context);
        }
    }

    public static implicit operator SwsContext*(SwsContextRef context)
    {
        if (context is null)
        {
            return null;
        }

        return context._nativePointer;
    }

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            ffmpeg.sws_freeContext(_nativePointer);
        }
    }
}
