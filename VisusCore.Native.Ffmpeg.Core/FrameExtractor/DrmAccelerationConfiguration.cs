namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public class DrmAccelerationConfiguration : HardwareAccelerationConfiguration
{
    public override string DeviceTypeName => "drm";

    public DrmAccelerationConfiguration(string deviceName)
        : base(deviceName)
    {
    }
}
