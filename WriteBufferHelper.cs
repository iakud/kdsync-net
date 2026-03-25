
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Abstraction for writing to a steam / IBufferWriter
[SecuritySafeCritical]
internal struct WriteBufferHelper
{
    private IBufferWriter<byte> bufferWriter;

    private CodedOutputStream codedOutputStream;

    public CodedOutputStream CodedOutputStream => codedOutputStream;

    //
    // 摘要:
    //     Initialize an instance with a coded output stream. This approach is faster than
    //     using a constructor because the instance to initialize is passed by reference
    //     and we can write directly into it without copying.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(CodedOutputStream codedOutputStream, out WriteBufferHelper instance)
    {
        instance.bufferWriter = null;
        instance.codedOutputStream = codedOutputStream;
    }

    //
    // 摘要:
    //     Initialize an instance with a buffer writer. This approach is faster than using
    //     a constructor because the instance to initialize is passed by reference and we
    //     can write directly into it without copying.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(IBufferWriter<byte> bufferWriter, out WriteBufferHelper instance, out Span<byte> buffer)
    {
        instance.bufferWriter = bufferWriter;
        instance.codedOutputStream = null;
        buffer = default(Span<byte>);
    }

    //
    // 摘要:
    //     Initialize an instance with a buffer represented by a single span (i.e. buffer
    //     cannot be refreshed) This approach is faster than using a constructor because
    //     the instance to initialize is passed by reference and we can write directly into
    //     it without copying.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitializeNonRefreshable(out WriteBufferHelper instance)
    {
        instance.bufferWriter = null;
        instance.codedOutputStream = null;
    }

    //
    // 摘要:
    //     Verifies that SpaceLeft returns zero.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckNoSpaceLeft(ref WriterInternalState state)
    {
        if (GetSpaceLeft(ref state) != 0)
        {
            throw new InvalidOperationException("Did not write as much data as expected.");
        }
    }

    //
    // 摘要:
    //     If writing to a flat array, returns the space left in the array. Otherwise, throws
    //     an InvalidOperationException.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSpaceLeft(ref WriterInternalState state)
    {
        if (state.writeBufferHelper.codedOutputStream?.InternalOutputStream == null && state.writeBufferHelper.bufferWriter == null)
        {
            return state.limit - state.position;
        }

        throw new InvalidOperationException("SpaceLeft can only be called on CodedOutputStreams that are writing to a flat array or when writing to a single span.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RefreshBuffer(ref Span<byte> buffer, ref WriterInternalState state)
    {
        if (state.writeBufferHelper.codedOutputStream?.InternalOutputStream != null)
        {
            state.writeBufferHelper.codedOutputStream.InternalOutputStream.Write(state.writeBufferHelper.codedOutputStream.InternalBuffer, 0, state.position);
            state.position = 0;
            return;
        }

        if (state.writeBufferHelper.bufferWriter != null)
        {
            state.writeBufferHelper.bufferWriter.Advance(state.position);
            state.position = 0;
            buffer = state.writeBufferHelper.bufferWriter.GetSpan();
            state.limit = buffer.Length;
            return;
        }

        throw new CodedOutputStream.OutOfSpaceException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Flush(ref Span<byte> buffer, ref WriterInternalState state)
    {
        if (state.writeBufferHelper.codedOutputStream?.InternalOutputStream != null)
        {
            state.writeBufferHelper.codedOutputStream.InternalOutputStream.Write(state.writeBufferHelper.codedOutputStream.InternalBuffer, 0, state.position);
            state.position = 0;
        }
        else if (state.writeBufferHelper.bufferWriter != null)
        {
            state.writeBufferHelper.bufferWriter.Advance(state.position);
            state.position = 0;
            state.limit = 0;
            buffer = default(Span<byte>);
        }
    }
}