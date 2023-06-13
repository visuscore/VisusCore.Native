using System;
using System.Buffers.Binary;
using System.Text;

namespace VisusCore.Native.Ffmpeg.Core.StreamSegmenter;

public class IsoBoxHeader
{
    public int Size { get; set; }
    public string Type { get; set; }

    public static IsoBoxHeader Parse(ReadOnlySpan<byte> data) =>
        new()
        {
            Size = BinaryPrimitives.ReadInt32BigEndian(data),
            Type = Encoding.GetEncoding("ISO-8859-1").GetString(data.Slice(4, 4)),
        };

    public static bool TryParse(ReadOnlySpan<byte> data, out IsoBoxHeader header)
    {
        header = null;

        try
        {
            header = Parse(data);
        }
        catch
        {
            return false;
        }

        return true;
    }
}
