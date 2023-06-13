using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

/// <summary>
/// Represents a reference to a memory region allocated by FFmpeg.
/// </summary>
public interface IAVInstanceRef : IDisposable
{
    /// <summary>
    /// Gets the native pointer.
    /// </summary>
    IntPtr NativePointer { get; }
}
