using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using VisusCore.Native.Ffmpeg.Core.Models;
using VisusCore.Native.Ffmpeg.Core.Unsafe;
using AbstractFfmpeg = FFmpeg.AutoGen.Abstractions;

namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public class Extractor : IDisposable
{
    private const uint MinimumInputBufferSize = 1024;
    // These are defined in ffmpeg's avcodec.h and it's better to use the original names.
#pragma warning disable SA1310 // Field names should not contain underscore
    private const int AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX = 1;
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly Stream _input;
    private readonly ExtractorConfiguration _configuration;
    private readonly Dictionary<int, ExtractorStreamContext> _streamToContext = new();
    private bool _disposed;
    private AVIOContextRef _inputIOContext;
    private AVFormatContextRef _formatContext;
    // We need this to keep the delegate alive.
#pragma warning disable S1450 // Private fields only used as local variables in methods should become local variables
    private avio_alloc_context_read_packet _readPacketCallback;
    private avio_alloc_context_seek _seekCallback;
#pragma warning restore S1450 // Private fields only used as local variables in methods should become local variables

    public Extractor(Stream input, ExtractorConfiguration configuration = null)
    {
        _input = input;
        _configuration = configuration ?? new();

        Reset();
    }

    public void Reset()
    {
        _input.Seek(0, SeekOrigin.Begin);

        DisposeNative();

        InitializeInput();
    }

    public unsafe Frame ReadNext()
    {
        using var packet = AVException.ThrowIfNull(
            new AVPacketRef(ffmpeg.av_packet_alloc()),
            "Error allocating packet.");

        do
        {
            AVException.ThrowIfError(
                ffmpeg.av_read_frame(_formatContext.NativePointer, packet),
                "Error reading frame.");

            if (!_streamToContext.ContainsKey(packet.NativePointer->stream_index))
            {
                continue;
            }

            return DecodePacket(_streamToContext[packet.NativePointer->stream_index], packet);
        }
        while (true);
    }

    public bool TryReadNext(out Frame frame)
    {
        try
        {
            frame = ReadNext();
        }
        catch
        {
            frame = null;

            return false;
        }

        return true;
    }

    public unsafe IEnumerable<StreamDetails> GetStreams() =>
        _streamToContext
            .Select(entry =>
            {
                var details = new StreamDetails
                {
                    Index = entry.Key,
                };

                var codecDescriptor = ffmpeg.avcodec_descriptor_get(entry.Value.CodecContext.NativePointer->codec_id);
                if (codecDescriptor is not null)
                {
                    if (codecDescriptor->name is not null)
                    {
                        details.CodecName = Marshal.PtrToStringAnsi((IntPtr)codecDescriptor->name);
                    }

                    if (codecDescriptor->long_name is not null)
                    {
                        details.CodecLongName = Marshal.PtrToStringAnsi((IntPtr)codecDescriptor->long_name);
                    }
                }

                details.Profile = ffmpeg.avcodec_profile_name(
                    entry.Value.CodecContext.NativePointer->codec_id,
                    entry.Value.CodecContext.NativePointer->profile);

                details.MediaTypeName = ffmpeg.av_get_media_type_string(entry.Value.CodecContext.NativePointer->codec_type);

                switch (entry.Value.CodecContext.NativePointer->codec_type)
                {
                    case AVMediaType.AVMEDIA_TYPE_VIDEO:
                        details.MediaType = EMediaType.Video;
                        details.Width = entry.Value.CodecContext.NativePointer->width;
                        details.Height = entry.Value.CodecContext.NativePointer->height;
                        details.PixelFormatName = ffmpeg.av_get_pix_fmt_name(entry.Value.CodecContext.NativePointer->pix_fmt);
                        details.FrameRate = entry.Value.CodecContext.NativePointer->framerate;
                        details.AvgFrameRate = entry.Value.CodecContext.NativePointer->framerate;
                        details.TimeBase = entry.Value.CodecContext.NativePointer->time_base;
                        break;
                    case AVMediaType.AVMEDIA_TYPE_AUDIO:
                        details.MediaType = EMediaType.Audio;
                        details.Channels = entry.Value.CodecContext.NativePointer->ch_layout.nb_channels;
                        details.SampleRate = entry.Value.CodecContext.NativePointer->sample_rate;
                        details.SampleFormatName = ffmpeg.av_get_sample_fmt_name(
                            entry.Value.CodecContext.NativePointer->sample_fmt);
                        break;
                    case AVMediaType.AVMEDIA_TYPE_DATA:
                        details.MediaType = EMediaType.Data;
                        break;
                    case AVMediaType.AVMEDIA_TYPE_SUBTITLE:
                        details.MediaType = EMediaType.Subtitle;
                        break;
                    case AVMediaType.AVMEDIA_TYPE_NB:
                    case AVMediaType.AVMEDIA_TYPE_ATTACHMENT:
                    case AVMediaType.AVMEDIA_TYPE_UNKNOWN:
                    default:
                        details.MediaType = EMediaType.Unknown;
                        break;
                }

                return details;
            })
            .ToArray();

    private unsafe Frame DecodePacket(ExtractorStreamContext context, AVPacketRef packet)
    {
        if (context.CodecContext is null
            || (context.CodecContext is not null
                && context.CodecContext.NativePointer->codec_type != AVMediaType.AVMEDIA_TYPE_VIDEO))
        {
            return new DataFrame(
                packet,
                packet.NativePointer->stream_index,
                packet.NativePointer->pts,
                packet.NativePointer->dts);
        }

        var avFrame = AVException.ThrowIfNull(
            new AVFrameRef(ffmpeg.av_frame_alloc()),
            "Error allocating frame");
        var firstPacket = true;
        var again = false;

        try
        {
            do
            {
                if (!firstPacket)
                {
                    AVException.ThrowIfError(
                        ffmpeg.av_read_frame(_formatContext.NativePointer, packet),
                        "Error reading frame.");
                }

                firstPacket = false;
                AVException.ThrowIfError(
                    ffmpeg.avcodec_send_packet(context.CodecContext, packet),
                    "Error sending packet to decoder context.");
                var ret = AVException.ThrowIfError(
                    ffmpeg.avcodec_receive_frame(context.CodecContext, avFrame),
                    "Error receiving frame.",
                    except: errorCode => errorCode == ffmpeg.AVERROR(ffmpeg.EAGAIN));
                again = ret == ffmpeg.AVERROR(ffmpeg.EAGAIN);
            }
            while (again);

            if (context.CodecHwDeviceContext is not null)
            {
                var avTransferredFrame = AVException.ThrowIfNull(
                    new AVFrameRef(ffmpeg.av_frame_alloc()),
                    "Error allocating frame");

                AVException.ThrowIfError(
                    ffmpeg.av_hwframe_transfer_data(avTransferredFrame, avFrame, 0),
                    "Error transfer frame.",
                    avTransferredFrame.Dispose);

                avFrame.Dispose();

                avFrame = avTransferredFrame;
            }

            if (context.CodecContext.NativePointer->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                avFrame = ConvertPixelFormat(
                    avFrame,
                    context.CodecContext.NativePointer->width,
                    context.CodecContext.NativePointer->height);

                return AVFrameToVideoFrame(avFrame, packet.NativePointer->stream_index);
            }

            return null;
        }
        finally
        {
            avFrame.Dispose();
        }
    }

    private unsafe AVFrameRef ConvertPixelFormat(
        AVFrameRef source,
        int width,
        int height,
        AVPixelFormat format = AVPixelFormat.AV_PIX_FMT_RGB24)
    {
        var destination = AVException.ThrowIfNull(
            new AVFrameRef(ffmpeg.av_frame_alloc()),
            "Error allocating frame.");

        destination.NativePointer->width = width;
        destination.NativePointer->height = height;
        destination.NativePointer->format = (int)format;
        destination.NativePointer->quality = 1;
        destination.NativePointer->pict_type = source.NativePointer->pict_type;
        destination.NativePointer->pts = source.NativePointer->pts;
        destination.NativePointer->pkt_dts = source.NativePointer->pkt_dts;
        destination.NativePointer->duration = source.NativePointer->duration;

        AVException.ThrowIfError(
            ffmpeg.av_frame_get_buffer(destination, 0),
            "Error allocating frame data.",
            destination.Dispose);

        using var swsContext = AVException.ThrowIfNull(
            new SwsContextRef(
                ffmpeg.sws_getCachedContext(
                    context: null,
                    source.NativePointer->width,
                    source.NativePointer->height,
                    (AVPixelFormat)source.NativePointer->format,
                    width,
                    height,
                    format,
                    ffmpeg.SWS_BILINEAR,
                    srcFilter: null,
                    dstFilter: null,
                    param: null)),
            "Error creating sws context.",
            destination.Dispose);

        AVException.ThrowIfError(
            ffmpeg.sws_scale(
                swsContext,
                source.NativePointer->data,
                source.NativePointer->linesize,
                0,
                destination.NativePointer->height,
                destination.NativePointer->data,
                destination.NativePointer->linesize),
            "Error scaling frame.",
            destination.Dispose);

        source.Dispose();

        return destination;
    }

    private unsafe VideoFrame AVFrameToVideoFrame(AVFrameRef avFrame, int streamIndex)
    {
        var buffer = default(IntPtr);
        try
        {
            var bufferSize = AVException.ThrowIfError(
                ffmpeg.av_image_get_buffer_size(
                    (AVPixelFormat)avFrame.NativePointer->format,
                    avFrame.NativePointer->width,
                    avFrame.NativePointer->height,
                    1),
                "Error getting image buffer size.");
            buffer = Marshal.AllocHGlobal(bufferSize);
            var frameData = default(byte_ptrArray4);
            frameData.UpdateFrom(avFrame.NativePointer->data);

            var frameLinesize = default(int_array4);
            frameLinesize.UpdateFrom(avFrame.NativePointer->linesize);

            AVException.ThrowIfError(
                ffmpeg.av_image_copy_to_buffer(
                    (byte*)buffer,
                    bufferSize,
                    frameData,
                    frameLinesize,
                    (AVPixelFormat)avFrame.NativePointer->format,
                    avFrame.NativePointer->width,
                    avFrame.NativePointer->height,
                    1),
                "Error copy image to buffer.");

            return new VideoFrame(
                avFrame.NativePointer->width,
                avFrame.NativePointer->height,
                (AVPixelFormat)avFrame.NativePointer->format,
                new ReadOnlySpan<byte>((void*)buffer, bufferSize),
                streamIndex,
                avFrame.NativePointer->key_frame != 0,
                avFrame.NativePointer->pts,
                avFrame.NativePointer->pkt_dts,
                avFrame.NativePointer->duration);
        }
        finally
        {
            if (buffer != default)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private unsafe void InitializeInput()
    {
        var inputBufferSize = Math.Max(_configuration.InputBufferSize, MinimumInputBufferSize);
        try
        {
            var inputBuffer = (byte*)AVException.ThrowIfNull(
                (IntPtr)ffmpeg.av_malloc(inputBufferSize),
                "Error allocating output buffer");
            _formatContext = AVException.ThrowIfNull(
                new AVFormatContextRef(ffmpeg.avformat_alloc_context()),
                "Error allocating format context.");
            _readPacketCallback = CreateReadPacketCallback(_input);
            _seekCallback = CreateSeekCallback(_input);
            _inputIOContext = AVException.ThrowIfNull(
                new AVIOContextRef(
                    ffmpeg.avio_alloc_context(
                        inputBuffer,
                        Convert.ToInt32(inputBufferSize),
                        0,
                        opaque: _formatContext.NativePointer,
                        read_packet: _readPacketCallback,
                        write_packet: null,
                        seek: _seekCallback)),
                "Error allocating output IO context.",
                () => ffmpeg.av_free(inputBuffer));

            _formatContext.NativePointer->pb = _inputIOContext.NativePointer;

            _formatContext.InvokeOnInstancePointer(context =>
                AVException.ThrowIfError(
                    ffmpeg.avformat_open_input(context, url: null, fmt: null, options: null),
                    "Failed to open input"));

            AVException.ThrowIfError(
                ffmpeg.avformat_find_stream_info(_formatContext.NativePointer, options: null),
                $"Unable to find stream info.");

            for (var streamIndex = 0; streamIndex < _formatContext.NativePointer->nb_streams; streamIndex++)
            {
                _streamToContext.Add(
                    streamIndex,
                    CreateStreamContextByAVStream(_formatContext.NativePointer->streams[streamIndex]));
            }
        }
        catch
        {
            DisposeNative();

            throw;
        }
    }

    private unsafe ExtractorStreamContext CreateStreamContextByAVStream(AVStream* stream)
    {
        var codecParameters = stream->codecpar;
        return codecParameters->codec_type switch
        {
            AVMediaType.AVMEDIA_TYPE_VIDEO => CreateVideoStreamContext(stream),
            AVMediaType.AVMEDIA_TYPE_AUDIO => CreateAudioStreamContext(stream),
            _ => CreateCopyStreamContext(stream),
        };
    }

    private unsafe ExtractorStreamContext CreateVideoStreamContext(AVStream* stream) =>
        CreateStreamContext(
            stream,
            context =>
            {
                if (_configuration.DecoderHardwareAcceleration is null)
                {
                    return;
                }

                var deviceType = ffmpeg.av_hwdevice_find_type_by_name(
                    _configuration.DecoderHardwareAcceleration.DeviceTypeName);
                if (deviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    return;
                }

                var hwConfig = FindDeviceHwConfigByType(context.CodecContext.NativePointer->codec, deviceType);
                if (hwConfig is null)
                {
                    return;
                }

                InitializeHwDecoder(
                    context,
                    deviceType,
                    hwConfig,
                    _configuration.DecoderHardwareAcceleration.DeviceName);
            });

    private unsafe ExtractorStreamContext CreateAudioStreamContext(AVStream* stream) =>
        CreateStreamContext(stream);

    private unsafe ExtractorStreamContext CreateStreamContext(
        AVStream* stream,
        Action<ExtractorStreamContext> advancedConfiguration = null)
    {
        var decoder = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
        var decoderContext = AVException.ThrowIfNull(
            new AVCodecContextRef(ffmpeg.avcodec_alloc_context3(decoder)),
            "Error allocating decoder context.");
        var extractorContext = default(ExtractorStreamContext);

        try
        {
            extractorContext = new ExtractorStreamContext(stream, CopyCodecParameters(stream), decoderContext);

            AVException.ThrowIfError(
                ffmpeg.avcodec_parameters_to_context(decoderContext.NativePointer, stream->codecpar),
                "Error copying codec parameters to decoder context.");

            advancedConfiguration?.Invoke(extractorContext);

            AVException.ThrowIfError(
                ffmpeg.avcodec_open2(decoderContext.NativePointer, decoder, options: null),
                "Error opening decoder.");

            return extractorContext;
        }
        catch
        {
            extractorContext?.Dispose();

            throw;
        }
    }

    private unsafe ExtractorStreamContext CreateCopyStreamContext(AVStream* stream) =>
        new(stream, CopyCodecParameters(stream));

    private unsafe AVCodecParametersRef CopyCodecParameters(AVStream* stream)
    {
        var codecParametersCopy = AVException.ThrowIfNull(
                    new AVCodecParametersRef(ffmpeg.avcodec_parameters_alloc()),
                    "Error allocating codec parameters.");
        AVException.ThrowIfError(
            ffmpeg.avcodec_parameters_copy(codecParametersCopy.NativePointer, stream->codecpar),
            "Error copying codec parameters",
            codecParametersCopy.Dispose);

        return codecParametersCopy;
    }

    private static unsafe avio_alloc_context_read_packet CreateReadPacketCallback(Stream input) =>
        (_, buffer, bufferSize) =>
        {
            try
            {
                return input.Read(new Span<byte>(buffer, bufferSize));
            }
            catch
            {
                return ffmpeg.AVERROR_UNKNOWN;
            }
        };

    private static unsafe avio_alloc_context_seek CreateSeekCallback(Stream input) =>
        (_, offset, whence) =>
        {
            try
            {
                if (!input.CanSeek)
                {
                    return -1;
                }

                return whence == ffmpeg.AVSEEK_SIZE
                    ? input.Length
                    : input.Seek(offset, SeekOrigin.Begin);
            }
            catch
            {
                return -1;
            }
        };

    private static unsafe AVCodecHWConfig* FindDeviceHwConfigByType(AVCodec* codec, AVHWDeviceType deviceType)
    {
        for (var currentConfigIndex = 0; ; currentConfigIndex++)
        {
            var hwConfig = ffmpeg.avcodec_get_hw_config(codec, currentConfigIndex);
            if (hwConfig == null)
            {
                return null;
            }

            if (hwConfig->device_type == deviceType
                && (hwConfig->methods & AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX) != 0)
            {
                return hwConfig;
            }
        }
    }

    private static unsafe void InitializeHwDecoder(
        ExtractorStreamContext context,
        AVHWDeviceType deviceType,
        AVCodecHWConfig* hwConfig = null,
        string device = null)
    {
        if (hwConfig is not null && (hwConfig->methods & AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX) == 0)
        {
            throw new InvalidOperationException($"The {nameof(hwConfig)} doesn't supports the required format.");
        }

        AVBufferRef* decoderContext = null;
        AVException.ThrowIfError(
            ffmpeg.av_hwdevice_ctx_create(
                &decoderContext,
                deviceType,
                string.IsNullOrWhiteSpace(device) ? null : device,
                opts: null,
                0),
            "Error creating hw device cntext");

        if (hwConfig is not null)
        {
            context.CodecContext.NativePointer->pix_fmt = hwConfig->pix_fmt;
        }
        else
        {
            context.WithCodecGetFormatFunc(CreateCodecGetFormatCallback());
        }

        context.CodecContext.NativePointer->hw_device_ctx = ffmpeg.av_buffer_ref(decoderContext);
        context.WithCodecHwDeviceContext(decoderContext);
    }

    private static unsafe AVCodecContext_get_format CreateCodecGetFormatCallback() =>
        (_, pixelFormats) =>
        {
            for (; *pixelFormats != AVPixelFormat.AV_PIX_FMT_NONE; pixelFormats++)
            {
                var pixelFormatDescriptor = ffmpeg.av_pix_fmt_desc_get(*pixelFormats);
                if ((pixelFormatDescriptor->flags & AbstractFfmpeg.ffmpeg.AV_PIX_FMT_FLAG_HWACCEL) == 0)
                {
                    return *pixelFormats;
                }
            }

            return AVPixelFormat.AV_PIX_FMT_NONE;
        };

    // We are disposing the native resources from many places, so we need this method.
#pragma warning disable S2952 // Classes should "Dispose" of members from the classes' own "Dispose" methods
    private unsafe void DisposeNative()
    {
        foreach (var context in _streamToContext.Values)
        {
            context.Dispose();
        }

        _streamToContext.Clear();
        if (_inputIOContext is not null)
        {
            ffmpeg.av_free(_inputIOContext.NativePointer->buffer);
            _inputIOContext.Dispose();
            _inputIOContext = null;
        }

        _formatContext?.Dispose();
        _formatContext = null;
    }
#pragma warning restore S2952 // Classes should "Dispose" of members from the classes' own "Dispose" methods

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _input.Dispose();
            }

            DisposeNative();

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

internal sealed unsafe class ExtractorStreamContext : IDisposable
{
    // We need this to keep the delegate alive.
#pragma warning disable S1450 // Private fields only used as local variables in methods should become local variables
    private AVCodecContext_get_format_func _codecGetFormatFunc;
#pragma warning restore S1450 // Private fields only used as local variables in methods should become local variables
    private bool _disposed;

    public AVStream* Stream { get; }
    public AVCodecParametersRef CodecParameters { get; }
    public AVCodecContextRef CodecContext { get; }
    public AVCodecHWConfig* CodecHwConfig { get; private set; }
    public AVBufferRef* CodecHwDeviceContext { get; private set; }

    public ExtractorStreamContext(
        AVStream* stream,
        AVCodecParametersRef codecParameters,
        AVCodecContextRef codecContext = null)
    {
        Stream = stream;
        CodecParameters = codecParameters;
        CodecContext = codecContext;
    }

    public ExtractorStreamContext WithCodecHwConfig(AVCodecHWConfig* value)
    {
        CodecHwConfig = value;

        return this;
    }

    public ExtractorStreamContext WithCodecHwDeviceContext(AVBufferRef* value)
    {
        CodecHwDeviceContext = value;

        return this;
    }

    public ExtractorStreamContext WithCodecGetFormatFunc(AVCodecContext_get_format_func value)
    {
        _codecGetFormatFunc = value;

        CodecContext.NativePointer->get_format = _codecGetFormatFunc;

        return this;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CodecContext?.Dispose();
            CodecParameters?.Dispose();
            if (CodecHwDeviceContext is not null)
            {
                var pointer = CodecHwDeviceContext;
                var codecHwDeviceContext = &pointer;

                ffmpeg.av_buffer_unref(codecHwDeviceContext);
            }

            _disposed = true;
        }
    }
}
