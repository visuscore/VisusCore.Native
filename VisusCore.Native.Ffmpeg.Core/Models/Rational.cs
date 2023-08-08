using FFmpeg.AutoGen;

namespace VisusCore.Native.Ffmpeg.Core.Models;

public class Rational
{
    public int Numerator { get; set; }
    public int Denominator { get; set; }

    public static implicit operator Rational(AVRational value) =>
        new()
        {
            Numerator = value.num,
            Denominator = value.den,
        };
}
