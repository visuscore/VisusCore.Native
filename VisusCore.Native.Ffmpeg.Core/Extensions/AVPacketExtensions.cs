using FFmpeg.AutoGen;
using System;

namespace VisusCore.Native.Ffmpeg.Core.Extensions;

public static class AVPacketExtensions
{
    public static unsafe long GetProducerReferenceTime(this AVPacket avPacket)
    {
        ulong size;
        var data = ffmpeg.av_packet_get_side_data(&avPacket, AVPacketSideDataType.AV_PKT_DATA_PRFT, &size);
        if (data == null)
        {
            return -1;
        }

        var prft = (AVProducerReferenceTime*)data;

        return prft->wallclock;
    }

    public static long GetProducerReferenceTimeMs(this AVPacket avPacket)
    {
        var wallclock = avPacket.GetProducerReferenceTime();

        return wallclock == -1 ? -1 : wallclock / 1000;
    }

    // Adjusts the DTS of the packet to be at least one greater than the last DTS.
    // See: https://github.com/FFmpeg/FFmpeg/blob/1460acc2ac4c5687742b0fdfc9af89f9f0d28029/fftools/ffmpeg_mux.c#L97-L116
    public static void AdjustDts(this AVPacket avPacket, AVMediaType mediaType, long? lastDts)
    {
        if (mediaType is not AVMediaType.AVMEDIA_TYPE_VIDEO and not AVMediaType.AVMEDIA_TYPE_AUDIO
            || avPacket.dts == ffmpeg.AV_NOPTS_VALUE
            || lastDts is null
            || lastDts == ffmpeg.AV_NOPTS_VALUE)
        {
            avPacket.dts = avPacket.dts == ffmpeg.AV_NOPTS_VALUE ? 0 : avPacket.dts;
            avPacket.pts = avPacket.pts == ffmpeg.AV_NOPTS_VALUE ? 0 : avPacket.pts;

            return;
        }

        var nextDts = lastDts.Value + 1;
        if (avPacket.dts < nextDts)
        {
            if (avPacket.pts >= avPacket.dts)
            {
                avPacket.pts = Math.Max(avPacket.pts, nextDts);
            }

            avPacket.dts = nextDts;
        }
    }
}
