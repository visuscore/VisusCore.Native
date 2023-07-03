using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VisusCore.Native.Core.Extensions;
using VisusCore.Native.Core.Unsafe;
using VisusCore.Native.Ffmpeg.Core.Extensions;
using VisusCore.Native.Ffmpeg.Core.Unsafe;

namespace VisusCore.Native.Ffmpeg.Core.StreamSegmenter;

internal enum ESegmenterState
{
    Unknown,
    WaitingForFtyp,
    WaitingForMoov,
    WaitingForMoof,
}

internal delegate int BufferAppenderDelegate(WorkerContext context, ReadOnlySpan<byte> buffer);

internal sealed class StreamInfo
{
    public int TargetStreamIndex { get; set; }
    public AVMediaType MediaType { get; set; }
    public long? LastPts { get; set; }
    public long? LastDts { get; set; }
    public long? SegmentFirstFrameTimestampUtc { get; set; }
    public long? SegmentLastFrameTimeStampUtc { get; set; }
    public long? SegmentFirstFrameTimestampProvided { get; set; }
    public long? SegmentLastFrameTimestampProvided { get; set; }
    public long? SegmentFrameCount { get; set; }

    public void ResetCounters()
    {
        LastPts = null;
        LastDts = null;
        SegmentFirstFrameTimestampUtc = null;
        SegmentLastFrameTimeStampUtc = null;
        SegmentFirstFrameTimestampProvided = null;
        SegmentLastFrameTimestampProvided = null;
        SegmentFrameCount = null;
    }
}

internal class WorkerContext : IDisposable
{
    private readonly Segmenter _segmenter;
    private bool _disposed;

    public WorkerContext(Segmenter segmenter) =>
        _segmenter = segmenter;

    public ESegmenterState State { get; set; }
    public AVFormatContextRef InputFormatContext { get; set; }
    public AVFormatContextRef OutputFormatContext { get; set; }
    public IDictionary<int, StreamInfo> StreamMap { get; set; }
    public uint OutputBufferSize { get; set; }
    public AVMemoryRef<byte> OutputBuffer { get; set; }
    public AVIOContextRef OutputIOContext { get; set; }
    public NativeRef<byte> FtypBuffer { get; set; }
    public uint FtypBufferSize { get; set; }
    public uint ExpectingFtypSize { get; set; }
    public NativeRef<byte> MoovBuffer { get; set; }
    public uint MoovBufferSize { get; set; }
    public uint ExpectingMoovSize { get; set; }
    public NativeRef<byte> SegmentBuffer { get; set; }
    public uint SegmentBufferSize { get; set; }
    public uint ExpectingSegmentSize { get; set; }

    internal void SetSegmentResult(Segment segment) =>
        _segmenter.SetSegmentResult(segment);

    internal void SetSegmentError(Exception exception) =>
        _segmenter.SetSegmentError(exception);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                InputFormatContext?.Dispose();
                OutputFormatContext?.Dispose();
                OutputIOContext?.Dispose();
                OutputBuffer?.Dispose();
                FtypBuffer?.Dispose();
                MoovBuffer?.Dispose();
                SegmentBuffer?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class Segmenter : IDisposable
{
    public const uint MinOutputBufferSize = 8;
    private readonly IStreamSource _streamSource;
    private readonly bool _allowAudio;
    private readonly uint _outputBufferSize;
    private readonly ManualResetEventSlim _stopWorker = new();
    private bool _disposed;
    private Thread _workerThread;
    private TaskCompletionSource _startCompletionSource;
    private TaskCompletionSource _stopCompletionSource;
    private TaskCompletionSource<Segment> _segmentCompletionSource;
    // We need this to keep the delegate alive.
#pragma warning disable S1450 // Private fields only used as local variables in methods should become local variables
    private avio_alloc_context_write_packet _writePacketCallback;
#pragma warning restore S1450 // Private fields only used as local variables in methods should become local variables

    public Segmenter(IStreamSource streamSource, bool allowAudio = true, uint outputBufferSize = 4096)
    {
        if (outputBufferSize < MinOutputBufferSize)
            throw new ArgumentOutOfRangeException(
                nameof(outputBufferSize),
                $"Output buffer size must be at least {MinOutputBufferSize}.");

        _streamSource = streamSource;
        _allowAudio = allowAudio;
        _outputBufferSize = outputBufferSize;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_workerThread is not null)
            throw new InvalidOperationException("Segmenter is already started.");

        _stopWorker.Reset();
        _startCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _stopCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _segmentCompletionSource = new TaskCompletionSource<Segment>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => _startCompletionSource.TrySetCanceled(cancellationToken)))
        {
            _workerThread = new Thread(Worker);
            _workerThread.Start();

            try
            {
                await _startCompletionSource.Task;
            }
            catch
            {
                _workerThread.Join();
                _workerThread = null;
                _startCompletionSource = null;
                _stopCompletionSource = null;
                _segmentCompletionSource = null;

                throw;
            }
        }
    }

    public async Task StopAsync()
    {
        if (_workerThread is null)
            throw new InvalidOperationException("Segmenter is not started.");

        _stopWorker.Set();

        await _stopCompletionSource.Task;

        _workerThread.Join();
        _workerThread = null;
        _startCompletionSource = null;
        _stopCompletionSource = null;
        _segmentCompletionSource = null;
    }

    public async Task<Segment> GetNextSegmentAsync(CancellationToken cancellationToken = default)
    {
        using (cancellationToken.Register(() => _segmentCompletionSource.TrySetCanceled(cancellationToken)))
        {
            return await _segmentCompletionSource.Task;
        }
    }

    internal void SetSegmentResult(Segment segment)
    {
        _segmentCompletionSource.TrySetResult(segment);
        Interlocked.Exchange(
            ref _segmentCompletionSource,
            new TaskCompletionSource<Segment>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    internal void SetSegmentError(Exception exception) =>
        _segmentCompletionSource.TrySetException(exception);

    private unsafe void Worker()
    {
        using var workerContext = new WorkerContext(this)
        {
            OutputFormatContext = new AVFormatContextRef(),
            OutputBufferSize = _outputBufferSize,
            FtypBuffer = new NativeRef<byte>(),
            MoovBuffer = new NativeRef<byte>(),
            SegmentBuffer = new NativeRef<byte>(),
        };

        try
        {
            workerContext.InputFormatContext = _streamSource.CreateInputContext();
            workerContext.OutputFormatContext.InvokeOnInstancePointer(outputFormatContext =>
                AVException.ThrowIfError(
                    ffmpeg.avformat_alloc_output_context2(outputFormatContext, oformat: null, "mp4", filename: null),
                    "Error allocating output context"));
            workerContext.StreamMap = SetupStreams(
                workerContext.InputFormatContext,
                workerContext.OutputFormatContext,
                _allowAudio);
            workerContext.OutputBuffer = AVException.ThrowIfNull(
                new AVMemoryRef<byte>((byte*)ffmpeg.av_malloc(workerContext.OutputBufferSize)),
                "Error allocating output buffer");
            workerContext.OutputIOContext = CreateOutputIOContext(workerContext);

            workerContext.OutputFormatContext.NativePointer->pb = workerContext.OutputIOContext;

            using var muxerOptions = new AVDictionaryRef();
            muxerOptions.Set("movflags", "frag_keyframe+empty_moov+default_base_moof");
            muxerOptions.InvokeOnInstancePointer(muxerOptions =>
                AVException.ThrowIfError(
                    ffmpeg.avformat_write_header(workerContext.OutputFormatContext, muxerOptions),
                    "Error writing header."));

            _startCompletionSource.TrySetResult();
        }
        catch (Exception exception)
        {
            _startCompletionSource.TrySetException(exception);

            return;
        }

        var avPacket = default(AVPacket);
        try
        {
            while (!_stopWorker.IsSet)
            {
                AVException.ThrowIfError(
                    ffmpeg.av_read_frame(workerContext.InputFormatContext, &avPacket),
                    "Error reading frame.");
                if (!workerContext.StreamMap.TryGetValue(avPacket.stream_index, out var streamInfo))
                {
                    ffmpeg.av_packet_unref(&avPacket);
                    continue;
                }

                avPacket.stream_index = streamInfo.TargetStreamIndex;
                avPacket.AdjustDts(streamInfo.MediaType, streamInfo.LastDts);
                streamInfo.LastDts = avPacket.dts;
                streamInfo.LastPts = avPacket.pts;
                streamInfo.SegmentFirstFrameTimestampUtc ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
                streamInfo.SegmentLastFrameTimeStampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
                streamInfo.SegmentFirstFrameTimestampProvided ??= avPacket.GetProducerReferenceTime();
                streamInfo.SegmentLastFrameTimestampProvided = avPacket.GetProducerReferenceTime();
                if (streamInfo.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    streamInfo.SegmentFrameCount = (streamInfo.SegmentFrameCount ?? 0) + 1;
                }

                AVException.ThrowIfError(
                    ffmpeg.av_write_frame(workerContext.OutputFormatContext, &avPacket),
                    "Error writing frame.");

                ffmpeg.av_packet_unref(&avPacket);
            }
        }
        catch (Exception exception)
        {
            _segmentCompletionSource.TrySetException(exception);
        }
        finally
        {
            ffmpeg.av_packet_unref(&avPacket);
        }

        _segmentCompletionSource.TrySetCanceled();
        _stopCompletionSource.TrySetResult();
    }

    private unsafe AVIOContextRef CreateOutputIOContext(WorkerContext workerContext)
    {
        _writePacketCallback = CreateOutputIOWritePacket(workerContext);

        return AVException.ThrowIfNull(
            new AVIOContextRef(
                ffmpeg.avio_alloc_context(
                    workerContext.OutputBuffer,
                    Convert.ToInt32(workerContext.OutputBufferSize),
                    1,
                    opaque: null,
                    read_packet: null,
                    write_packet: _writePacketCallback,
                    seek: null)),
            "Error allocating output IO context.");
    }

    private static unsafe IDictionary<int, StreamInfo> SetupStreams(
        AVFormatContextRef inputFormatContext,
        AVFormatContextRef outputFormatContext,
        bool allowAudio)
    {
        var streamMap = new Dictionary<int, StreamInfo>();
        for (var streamIndex = 0; streamIndex < inputFormatContext.NativePointer->nb_streams; streamIndex++)
        {
            var inputStream = inputFormatContext.NativePointer->streams[streamIndex];
            if (!IsStreamSupported(inputStream, outputFormatContext.NativePointer->oformat, allowAudio))
            {
                continue;
            }

            var outputStream = ffmpeg.avformat_new_stream(outputFormatContext, c: null);
            if (outputStream == null)
            {
                throw new InvalidOperationException("Error allocating output stream.");
            }

            AVException.ThrowIfError(
                ffmpeg.avcodec_parameters_copy(outputStream->codecpar, inputStream->codecpar),
                "Error copying codec parameters.");

            outputStream->codecpar->codec_tag = 0;
            streamMap.Add(
                streamIndex,
                new StreamInfo
                {
                    TargetStreamIndex = outputStream->index,
                    MediaType = inputStream->codecpar->codec_type,
                });
        }

        if (!streamMap.Values.Any(streamInfo => streamInfo.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO))
        {
            throw new InvalidOperationException("No video stream found.");
        }

        return streamMap;
    }

    private static unsafe bool IsStreamSupported(AVStream* stream, AVOutputFormat* outputFormat, bool allowAudio) =>
        stream->codecpar->codec_type switch
        {
            AVMediaType.AVMEDIA_TYPE_AUDIO => allowAudio
                && ffmpeg.avformat_query_codec(outputFormat, stream->codecpar->codec_id, ffmpeg.FF_COMPLIANCE_NORMAL) == 1,
            AVMediaType.AVMEDIA_TYPE_VIDEO => true,
            _ => false,
        };

    private static unsafe avio_alloc_context_write_packet CreateOutputIOWritePacket(WorkerContext workerContext) =>
        (_, buffer, bufferSize) =>
        {
            var bufferSpan = new ReadOnlySpan<byte>(buffer, bufferSize);
            if (IsoBoxHeader.TryParse(bufferSpan, out var boxHeader))
            {
                if (boxHeader.Type is "ftyp")
                {
                    workerContext.FtypBufferSize = 0;
                    workerContext.ExpectingFtypSize = Convert.ToUInt32(boxHeader.Size);
                    workerContext.State = ESegmenterState.WaitingForFtyp;
                }
                else if (boxHeader.Type is "moov" && workerContext.State is ESegmenterState.WaitingForMoov)
                {
                    workerContext.MoovBufferSize = 0;
                    workerContext.ExpectingMoovSize = Convert.ToUInt32(boxHeader.Size);
                    workerContext.State = ESegmenterState.WaitingForMoov;
                }
                else if (boxHeader.Type is "moof"
                    && workerContext.State is ESegmenterState.Unknown
                    && IsoBoxHeader.TryParse(bufferSpan[boxHeader.Size..], out var maybeMdat)
                    && maybeMdat.Type is "mdat")
                {
                    workerContext.SegmentBufferSize = 0;
                    workerContext.ExpectingSegmentSize = Convert.ToUInt32(boxHeader.Size + maybeMdat.Size);
                    workerContext.State = ESegmenterState.WaitingForMoof;
                }
            }

            var result = workerContext.State switch
            {
                ESegmenterState.WaitingForFtyp => AppendFtypBuffer(workerContext, bufferSpan),
                ESegmenterState.WaitingForMoov => AppendMoovBuffer(workerContext, bufferSpan),
                ESegmenterState.WaitingForMoof => AppendMoofBuffer(workerContext, bufferSpan),
                _ => bufferSize,
            };

            if (result != bufferSize)
            {
                return ffmpeg.AVERROR_UNKNOWN;
            }

            return result;
        };

    private static int AppendFtypBuffer(WorkerContext workerContext, ReadOnlySpan<byte> buffer) =>
        AppendBuffer(
            buffer,
            workerContext.FtypBuffer,
            Convert.ToInt32(workerContext.FtypBufferSize),
            Convert.ToInt32(workerContext.ExpectingFtypSize),
            (bytesCopied, newBufferSize) =>
            {
                workerContext.FtypBufferSize = newBufferSize;
                if (workerContext.FtypBufferSize == workerContext.ExpectingFtypSize)
                {
                    workerContext.State = ESegmenterState.WaitingForMoov;
                }

                return bytesCopied;
            });

    private static int AppendMoovBuffer(WorkerContext workerContext, ReadOnlySpan<byte> buffer) =>
        AppendBuffer(
            buffer,
            workerContext.MoovBuffer,
            Convert.ToInt32(workerContext.MoovBufferSize),
            Convert.ToInt32(workerContext.ExpectingMoovSize),
            (bytesCopied, newBufferSize) =>
            {
                workerContext.MoovBufferSize = newBufferSize;
                if (workerContext.MoovBufferSize == workerContext.ExpectingMoovSize)
                {
                    workerContext.State = ESegmenterState.Unknown;
                }

                return bytesCopied;
            });

    private static int AppendMoofBuffer(WorkerContext workerContext, ReadOnlySpan<byte> buffer) =>
        AppendBuffer(
            buffer,
            workerContext.SegmentBuffer,
            Convert.ToInt32(workerContext.SegmentBufferSize),
            Convert.ToInt32(workerContext.ExpectingSegmentSize),
            (bytesCopied, newBufferSize) =>
            {
                workerContext.SegmentBufferSize = newBufferSize;
                if (workerContext.SegmentBufferSize == workerContext.ExpectingSegmentSize)
                {
                    var videoStreamInfo = workerContext.StreamMap.Values.First(streamInfo =>
                        streamInfo.MediaType == AVMediaType.AVMEDIA_TYPE_VIDEO
                        && streamInfo.SegmentFrameCount is not null
                        && streamInfo.SegmentFirstFrameTimestampUtc is not null
                        && streamInfo.SegmentLastFrameTimeStampUtc is not null);
                    var initBuffer = new byte[workerContext.FtypBufferSize + workerContext.MoovBufferSize];
                    workerContext.FtypBuffer.CopyTo(Convert.ToInt32(workerContext.FtypBufferSize), initBuffer);
                    workerContext.MoovBuffer.CopyTo(Convert.ToInt32(workerContext.MoovBufferSize), initBuffer);
                    workerContext.SetSegmentResult(new Segment
                    {
                        Data = workerContext.SegmentBuffer.ToArray(Convert.ToInt32(workerContext.SegmentBufferSize)),
                        Init = initBuffer,
                        Duration = videoStreamInfo?.SegmentLastFrameTimeStampUtc - videoStreamInfo?.SegmentFirstFrameTimestampUtc,
                        TimestampUtc = videoStreamInfo?.SegmentFirstFrameTimestampUtc,
                        TimestampProvided = videoStreamInfo?.SegmentFirstFrameTimestampProvided,
                        FrameCount = videoStreamInfo?.SegmentFrameCount,
                    });

                    foreach (var streamInfo in workerContext.StreamMap.Values)
                    {
                        streamInfo.ResetCounters();
                    }

                    workerContext.State = ESegmenterState.Unknown;
                }

                return bytesCopied;
            });

    private static int AppendBuffer(
        ReadOnlySpan<byte> buffer,
        NativeRef<byte> currentBuffer,
        int currentBufferSize,
        int expectedSize,
        Func<int, uint, int> after)
    {
        var remaining = Convert.ToInt32(currentBufferSize + buffer.Length - expectedSize);
        var bytesToCopy = remaining <= 0 ? buffer.Length : remaining;
        var bufferToCopy = buffer[..bytesToCopy];

        try
        {
            return after(
                bytesToCopy,
                AppendBuffer(bufferToCopy, currentBuffer, Convert.ToUInt32(currentBufferSize)));
        }
        catch (OutOfMemoryException)
        {
            return ffmpeg.AVERROR(ffmpeg.ENOMEM);
        }
        catch (ArgumentException)
        {
            return ffmpeg.AVERROR(ffmpeg.EINVAL);
        }
        catch
        {
            return ffmpeg.AVERROR_UNKNOWN;
        }
    }

    private static unsafe uint AppendBuffer(ReadOnlySpan<byte> source, NativeRef<byte> destination, uint currentSize)
    {
        destination.Realloc(Convert.ToUInt32(source.Length) + currentSize);
        var destinationSpan = new Span<byte>(
            destination.NativePointer,
            Convert.ToInt32(currentSize) + source.Length)[Convert.ToInt32(currentSize)..];

        source.CopyTo(destinationSpan);

        return currentSize + Convert.ToUInt32(source.Length);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_workerThread is not null)
            {
                _stopWorker.Set();
                _workerThread.Join();
                _workerThread = null;
            }

            _stopWorker.Dispose();

            _disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
