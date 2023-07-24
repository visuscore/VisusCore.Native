namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public class QsvAccelerationConfiguration : HardwareAccelerationConfiguration
{
    public override string DeviceTypeName => "qsv";

    public QsvAccelerationConfiguration()
        : base(string.Empty)
    {
    }

    public QsvAccelerationConfiguration(string deviceName)
        : base(deviceName)
    {
    }
}
