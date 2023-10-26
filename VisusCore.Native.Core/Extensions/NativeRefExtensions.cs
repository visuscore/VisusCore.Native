using System;
using VisusCore.Native.Core.Unsafe;

namespace VisusCore.Native.Core.Extensions;

public static class NativeRefExtensions
{
    public static unsafe ReadOnlySpan<TPointer> ToReadOnlySpan<TPointer>(this NativeRef<TPointer> nativeRef, int size)
        where TPointer : unmanaged
    {
        if (nativeRef is null)
        {
            throw new ArgumentNullException(nameof(nativeRef));
        }

        return new(nativeRef.NativePointer, size);
    }

    public static TPointer[] ToArray<TPointer>(this NativeRef<TPointer> nativeRef, int size)
        where TPointer : unmanaged =>
        nativeRef.ToReadOnlySpan(size).ToArray();

    public static void CopyTo<TPointer>(this NativeRef<TPointer> nativeRef, int size, Span<TPointer> destination)
        where TPointer : unmanaged =>
        nativeRef.ToReadOnlySpan(size)
            .CopyTo(destination);
}
