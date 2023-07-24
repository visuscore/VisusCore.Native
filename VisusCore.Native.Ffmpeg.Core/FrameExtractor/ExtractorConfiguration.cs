namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public class ExtractorConfiguration
{
    public uint InputBufferSize { get; set; } = 4096;
    public HardwareAccelerationConfiguration DecoderHardwareAcceleration { get; set; }
}
