using FFmpeg.AutoGen;
using System;
using System.Globalization;
using VisusCore.Native.Ffmpeg.Core.Unsafe;

namespace VisusCore.Native.Ffmpeg.Core.StreamSegmenter;

public class RtspStreamSource : IStreamSource
{
    private readonly string _url;
    private readonly ERtspTransport _rtspTransport;
    private readonly bool _preferTcp;
    private readonly TimeSpan? _timeout;

    public RtspStreamSource(
        string url,
        ERtspTransport rtspTransport = ERtspTransport.Unknown,
        bool preferTcp = false,
        TimeSpan? timeout = null)
    {
        _url = url;
        _rtspTransport = rtspTransport;
        _preferTcp = preferTcp;
        _timeout = timeout;
    }

    public unsafe AVFormatContextRef CreateInputContext()
    {
        using var options = new AVDictionaryRef();
        if (_rtspTransport != ERtspTransport.Unknown)
        {
            options.Set(
                "rtsp_transport",
                _rtspTransport switch
                {
                    ERtspTransport.Tcp => "tcp",
                    ERtspTransport.Udp => "udp",
                    _ => string.Empty,
                });
        }

        if (_preferTcp)
        {
            options.Set("rtsp_flags", "prefer_tcp", 1);
        }

        if (_timeout is not null && _timeout?.TotalMilliseconds > 0)
        {
            options.Set("timeout", (_timeout?.TotalMilliseconds * 1000)?.ToString(CultureInfo.InvariantCulture));
        }

        var context = new AVInputFormatContextRef();
        try
        {
            context.InvokeOnInstancePointer(context =>
                options.InvokeOnInstancePointer(options =>
                {
                    AVException.ThrowIfError(
                        ffmpeg.avformat_open_input(context, _url, fmt: null, options),
                        $"Unable to open input: {_url}.");
                    AVException.ThrowIfError(
                        ffmpeg.avformat_find_stream_info(*context, options: null),
                        $"Unable to find stream info.");
                }));
        }
        catch
        {
            context?.Dispose();

            throw;
        }

        return context;
    }
}
