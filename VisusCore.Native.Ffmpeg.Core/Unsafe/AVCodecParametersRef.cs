using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVCodecParametersInstancePointerAction(AVCodecParameters** parameters);

public unsafe class AVCodecParametersRef : AVInstanceRef<AVCodecParameters, AVCodecParametersInstancePointerAction>
{
    public AVCodecParametersRef()
        : this((AVCodecParameters*)IntPtr.Zero)
    {
    }

    public AVCodecParametersRef(AVCodecParameters* parameters)
        : base(parameters)
    {
    }

    public override void InvokeOnInstancePointer(AVCodecParametersInstancePointerAction action)
    {
        fixed (AVCodecParameters** parameters = &_nativePointer)
        {
            action?.Invoke(parameters);
        }
    }

    public static implicit operator AVCodecParameters*(AVCodecParametersRef parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        return parameters._nativePointer;
    }

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            InvokeOnInstancePointer(ffmpeg.avcodec_parameters_free);
        }
    }
}
