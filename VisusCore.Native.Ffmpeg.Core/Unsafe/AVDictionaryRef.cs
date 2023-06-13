using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public unsafe delegate void AVDictionaryInstancePointerAction(AVDictionary** dictionary);

public unsafe class AVDictionaryRef : AVInstanceRef<AVDictionary, AVDictionaryInstancePointerAction>
{
    public AVDictionaryRef()
        : this((AVDictionary*)IntPtr.Zero)
    {
    }

    public AVDictionaryRef(AVDictionary* dictionary)
        : base(dictionary)
    {
    }

    public int Set(string key, string value, int flags = 0)
    {
        fixed (AVDictionary** dictionary = &_nativePointer)
        {
            return ffmpeg.av_dict_set(dictionary, key, value, flags);
        }
    }

    public override void InvokeOnInstancePointer(AVDictionaryInstancePointerAction action)
    {
        fixed (AVDictionary** dictionary = &_nativePointer)
        {
            action?.Invoke(dictionary);
        }
    }

    public static implicit operator AVDictionary*(AVDictionaryRef context) =>
        context._nativePointer;

    public static implicit operator AVDictionaryRef(AVDictionary* context) =>
        new(context);

    protected override void ReleaseInstance()
    {
        if (_nativePointer != null)
        {
            fixed (AVDictionary** dictionary = &_nativePointer)
            {
                ffmpeg.av_dict_free(dictionary);
            }
        }
    }
}
