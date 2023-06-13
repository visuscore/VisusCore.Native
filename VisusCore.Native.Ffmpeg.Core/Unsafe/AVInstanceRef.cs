using System;

namespace VisusCore.Native.Ffmpeg.Core.Unsafe;

public abstract unsafe class AVInstanceRef<TInstance, TInstanceReferenceAction> : IAVInstanceRef
    where TInstance : unmanaged
    where TInstanceReferenceAction : Delegate
{
    protected readonly TInstance* _nativePointer;
    private bool _disposed;

    public TInstance* NativePointer => _nativePointer;

    IntPtr IAVInstanceRef.NativePointer => (IntPtr)_nativePointer;

    protected AVInstanceRef(TInstance* nativePointer) =>
        _nativePointer = nativePointer;

    public abstract void InvokeOnInstancePointer(TInstanceReferenceAction action);

    protected abstract void ReleaseInstance();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            ReleaseInstance();

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
