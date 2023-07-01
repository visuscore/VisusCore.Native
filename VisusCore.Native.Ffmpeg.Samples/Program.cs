using CommandLine;
using FFmpeg.AutoGen;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VisusCore.Native.Ffmpeg.Core.StreamSegmenter;
using VisusCore.Native.Ffmpeg.Samples;

using var stoppingToken = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArg) =>
{
    stoppingToken.Cancel();

    eventArg.Cancel = true;
};

return await Parser.Default.ParseArguments<SegmenterOptions>(args)
    .MapResult(
        async options =>
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

            await segmenter.StartAsync(stoppingToken.Token);

            var segment = default(Segment);

            do
            {
                segment = await segmenter.GetNextSegmentAsync(stoppingToken.Token);
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Timestamp: {segment.TimestampUtc}"));
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wallclock: {segment.TimestampWallclock}"));
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Frames received: {segment.FrameCount}"));
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Segment duration: {segment.Duration}"));
                Console.WriteLine($"Init size: {segment.Init.Length}");
                Console.WriteLine($"Data size: {segment.Data.Length}");
            }
            while (!stoppingToken.IsCancellationRequested);

            return 0;
        },
        error => Task.FromResult(0));
