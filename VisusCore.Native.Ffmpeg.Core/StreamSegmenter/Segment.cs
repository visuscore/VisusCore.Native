using System.Collections.Generic;

namespace VisusCore.Native.Ffmpeg.Core.StreamSegmenter;

public class Segment
{
    public IEnumerable<byte> Init { get; init; }
    public IEnumerable<byte> Data { get; init; }
    public long? Duration { get; init; }
    public long? TimestampUtc { get; init; }
    public long? TimestampWallclock { get; init; }
    public long? FrameCount { get; init; }
}
