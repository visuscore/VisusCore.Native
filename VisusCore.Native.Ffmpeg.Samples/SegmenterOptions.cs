using CommandLine;
using System;
using VisusCore.Native.Ffmpeg.Core.StreamSegmenter;

namespace VisusCore.Native.Ffmpeg.Samples;

[Verb("segmenter", true, HelpText = "Create mp4 segments from a video source.")]
public class SegmenterOptions
{
    [Option(
        "ffmpeg-path",
        Required = true,
        HelpText = "Sets the path to the ffmpeg executable.")]
    public string FfmpegPath { get; set; }

    [Option(
        'i',
        "input",
        Required = true,
        HelpText = "Sets the input video source. Example: rtsp://user:password@my-camera/primary")]
    public string Input { get; set; }

    [Option(
        't',
        "transport",
        Default = ERtspTransport.Tcp,
        HelpText = "Sets the transport protocol.")]
    public ERtspTransport Transport { get; set; }

    [Option(
        't',
        "timeout",
        HelpText = "Sets the timeout for the video source.")]
    public TimeSpan Timeout { get; set; } = System.Threading.Timeout.InfiniteTimeSpan;

    [Option(
        'a',
        "allow-audio",
        HelpText = "Sets whether to allow audio.")]
    public bool AllowAudio { get; set; }
}
