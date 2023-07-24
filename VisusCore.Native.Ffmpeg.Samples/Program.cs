using CommandLine;
using FFmpeg.AutoGen;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VisusCore.Native.Ffmpeg.Core.FrameExtractor;
using VisusCore.Native.Ffmpeg.Core.StreamSegmenter;
using VisusCore.Native.Ffmpeg.Samples;

using var stoppingToken = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArg) =>
{
    stoppingToken.Cancel();

    eventArg.Cancel = true;
};

static async Task<int> SegmenterAsync(SegmenterOptions options, CancellationToken cancellationToken)
{
    ffmpeg.RootPath = options.FfmpegPath;

    using var segmenter = new Segmenter(
        new RtspStreamSource(
            options.Input,
            options.Transport,
            options.Transport == ERtspTransport.Tcp,
            options.Timeout),
        options.AllowAudio,
        4096);

    await segmenter.StartAsync(cancellationToken);

    var segment = default(Segment);

    do
    {
        segment = await segmenter.GetNextSegmentAsync(cancellationToken);
        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"Timestamp: {segment.TimestampUtc}"));
        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"Provided timestamp: {segment.TimestampProvided}"));
        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"Frames received: {segment.FrameCount}"));
        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"Segment duration: {segment.Duration}"));
        Console.WriteLine($"Init size: {segment.Init.Length}");
        Console.WriteLine($"Data size: {segment.Data.Length}");
    }
    while (!cancellationToken.IsCancellationRequested);

    return 0;
}

static Task<int> ExtractorAsync(ExtractorOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.OutputDirectory)
        && !Directory.Exists(options.OutputDirectory))
    {
        Directory.CreateDirectory(options.OutputDirectory);
    }

    ffmpeg.RootPath = options.FfmpegPath;
    using var input = File.OpenRead(options.Input);
    var configuration = new ExtractorConfiguration
    {
        DecoderHardwareAcceleration = options.HwAccel switch
        {
            HwAccel.Qsv => new QsvAccelerationConfiguration(options.HwDevice),
            HwAccel.Drm => new DrmAccelerationConfiguration(options.HwDevice),
            _ => default,
        },
    };
    using var extractor = new Extractor(input, configuration);
    var videoFrameIndex = 0;
    while (extractor.TryReadNext(out var frame))
    {
        if (frame is VideoFrame videoFrame)
        {
            if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                using var image = Image.LoadPixelData<Rgb24>(videoFrame.Data, videoFrame.Width, videoFrame.Height);
                image.SaveAsPng(
                    Path.Combine(
                        options.OutputDirectory,
                        $"frame_{videoFrameIndex.ToString(CultureInfo.InvariantCulture)}.png"));
            }

            videoFrameIndex++;
        }
    }

    return Task.FromResult(0);
}

return await Parser.Default.ParseArguments<SegmenterOptions, ExtractorOptions>(args)
    .MapResult(
        options => options switch
        {
            SegmenterOptions segmenterOptions => SegmenterAsync(segmenterOptions, stoppingToken.Token),
            ExtractorOptions extractorOptions => ExtractorAsync(extractorOptions),
            _ => throw new InvalidOperationException(),
        },
        error => Task.FromResult(0));
