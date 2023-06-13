using System;
using System.Runtime.InteropServices;

namespace VisusCore.Native.Core.Unsafe;

public unsafe class NativeRef<TPointer> : IDisposable
    where TPointer : unmanaged
{
    private bool _disposed;

    public TPointer* NativePointer { get; private set; }

    public NativeRef()
        : this((TPointer*)IntPtr.Zero)
    {
    }

    public NativeRef(TPointer* nativePointer) =>
        NativePointer = nativePointer;

    public void Realloc(uint size)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NativeRef<TPointer>));
        }

        NativePointer = (TPointer*)NativeMemory.Realloc(NativePointer, size);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (NativePointer != (TPointer*)IntPtr.Zero)
            {
                NativeMemory.Free(NativePointer);
            }

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
