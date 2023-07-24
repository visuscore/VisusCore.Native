namespace VisusCore.Native.Ffmpeg.Core.FrameExtractor;

public abstract class HardwareAccelerationConfiguration
{
    public abstract string DeviceTypeName { get; }
    public string DeviceName { get; }

    protected HardwareAccelerationConfiguration(string deviceName) =>
        DeviceName = deviceName;
}
