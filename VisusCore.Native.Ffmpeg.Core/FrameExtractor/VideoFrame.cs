using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public class VideoFrame : DataFrame
{
    public int Width { get; }
    public int Height { get; }
    public AVPixelFormat PixelFormat { get; }
    public bool Keyframe { get; }
    public long Duration { get; }

    public VideoFrame(
        int width,
        int height,
        AVPixelFormat pixelFormat,
        ReadOnlySpan<byte> data,
        int streamIndex,
        bool keyframe,
        long pts,
        long dts,
        long duration)
        : base(data, streamIndex, pts, dts)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        Keyframe = keyframe;
        Duration = duration;
    }
}
