using System;

namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public class DataFrame : Frame
{
    private readonly byte[] _data;

    public ReadOnlySpan<byte> Data => _data;

    public DataFrame(ReadOnlySpan<byte> data, int streamIndex, long pts, long dts)
        : base(streamIndex, pts, dts) =>
        _data = data.ToArray();
}
