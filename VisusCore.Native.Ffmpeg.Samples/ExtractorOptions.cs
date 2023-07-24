using CommandLine;

namespace VisusCore.Native.Ffmpeg.Samples;

[Verb("extractor", HelpText = "Extracts frames from video files.")]
public class ExtractorOptions
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
        HelpText = "Sets the input video source. Example: sample_hevc.mp4")]
    public string Input { get; set; }

    [Option("hwaccel", HelpText = "Enables hardware acceleration.", Default = nameof(HwAccel.None))]
    public HwAccel HwAccel { get; set; }

    [Option("hwdevice", HelpText = "Sets the hardware device to use.")]
    public string HwDevice { get; set; }

    [Option('o', "output-directory", HelpText = "Sets the output directory where the extracted frames get saved.")]
    public string OutputDirectory { get; set; }
}

public enum HwAccel
{
    None,
    Qsv,
    Drm,
}
