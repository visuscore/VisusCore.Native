using VisusCore.Native.Ffmpeg.Core.Unsafe;

namespace VisusCore.Native.Ffmpeg.Core.StreamSegmenter;

/// <summary>
/// Represents a stream source.
/// </summary>
public interface IStreamSource
{
    /// <summary>
    /// Creates an input context.
    /// </summary>
    AVFormatContextRef CreateInputContext();
}
