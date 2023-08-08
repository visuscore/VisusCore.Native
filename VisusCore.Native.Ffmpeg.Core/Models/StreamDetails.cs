namespace VisusCore.Native.Ffmpeg.Core.Models;

public class StreamDetails
{
    public int Index { get; set; }
    public EMediaType MediaType { get; set; }
    public string CodecName { get; set; }
    public string CodecLongName { get; set; }
    public string Profile { get; set; }
    public string MediaTypeName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string PixelFormatName { get; set; }
    public string SampleFormatName { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public Rational FrameRate { get; set; }
    public Rational AvgFrameRate { get; set; }
    public Rational TimeBase { get; set; }
    public long BitRate { get; set; }
}
