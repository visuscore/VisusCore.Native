using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVMemoryInstancePointerAction<TInstance>(TInstance** memory)
    where TInstance : unmanaged;

public unsafe class AVMemoryRef<TInstance> : AVInstanceRef<TInstance, AVMemoryInstancePointerAction<TInstance>>
    where TInstance : unmanaged
{
    public AVMemoryRef()
        : this((TInstance*)IntPtr.Zero)
    {
    }

    public AVMemoryRef(TInstance* memory)
        : base(memory)
    {
    }

    public override void InvokeOnInstancePointer(AVMemoryInstancePointerAction<TInstance> action)
    {
        fixed (TInstance** memory = &_nativePointer)
        {
            action?.Invoke(memory);
        }
    }

    public static implicit operator TInstance*(AVMemoryRef<TInstance> memory) =>
        memory._nativePointer;

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            ffmpeg.av_free(_nativePointer);
        }
    }
}
