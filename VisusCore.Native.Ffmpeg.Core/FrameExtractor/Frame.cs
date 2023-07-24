using System;

namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public abstract class Frame : IDisposable
{
    public int StreamIndex { get; }
    public long Pts { get; }
    public long Dts { get; }

    protected Frame(int streamIndex, long pts, long dts)
    {
        StreamIndex = streamIndex;
        Pts = pts;
        Dts = dts;
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
