
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Abstraction for reading from a stream / read only sequence. Parsing from the
//     buffer is a loop of reading from current buffer / refreshing the buffer once
//     done.
[SecuritySafeCritical]
internal struct SegmentedBufferHelper
{
    private int? totalLength;

    private ReadOnlySequence<byte>.Enumerator readOnlySequenceEnumerator;

    private CodedInputStream codedInputStream;

    public int? TotalLength => totalLength;

    public CodedInputStream CodedInputStream => codedInputStream;

    //
    // 摘要:
    //     Initialize an instance with a coded input stream. This approach is faster than
    //     using a constructor because the instance to initialize is passed by reference
    //     and we can write directly into it without copying.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(CodedInputStream codedInputStream, out SegmentedBufferHelper instance)
    {
        instance.totalLength = ((codedInputStream.InternalInputStream == null) ? new int?(codedInputStream.InternalBuffer.Length) : ((int?)null));
        instance.readOnlySequenceEnumerator = default(ReadOnlySequence<byte>.Enumerator);
        instance.codedInputStream = codedInputStream;
    }

    //
    // 摘要:
    //     Initialize an instance with a read only sequence. This approach is faster than
    //     using a constructor because the instance to initialize is passed by reference
    //     and we can write directly into it without copying.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(ReadOnlySequence<byte> sequence, out SegmentedBufferHelper instance, out ReadOnlySpan<byte> firstSpan)
    {
        instance.codedInputStream = null;
        ReadOnlyMemory<byte> readOnlyMemory;
        if (sequence.IsSingleSegment)
        {
            readOnlyMemory = sequence.First;
            firstSpan = readOnlyMemory.Span;
            instance.totalLength = firstSpan.Length;
            instance.readOnlySequenceEnumerator = default(ReadOnlySequence<byte>.Enumerator);
        }
        else
        {
            instance.readOnlySequenceEnumerator = sequence.GetEnumerator();
            instance.totalLength = (int)sequence.Length;
            instance.readOnlySequenceEnumerator.MoveNext();
            readOnlyMemory = instance.readOnlySequenceEnumerator.Current;
            firstSpan = readOnlyMemory.Span;
        }
    }

    public bool RefillBuffer(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, bool mustSucceed)
    {
        if (codedInputStream != null)
        {
            return RefillFromCodedInputStream(ref buffer, ref state, mustSucceed);
        }

        return RefillFromReadOnlySequence(ref buffer, ref state, mustSucceed);
    }

    //
    // 摘要:
    //     Sets currentLimit to (current position) + byteLimit. This is called when descending
    //     into a length-delimited embedded message. The previous limit is returned.
    //
    // 返回结果:
    //     The old limit.
    public static int PushLimit(ref ParserInternalState state, int byteLimit)
    {
        if (byteLimit < 0)
        {
            throw InvalidProtocolBufferException.NegativeSize();
        }

        byteLimit += state.totalBytesRetired + state.bufferPos;
        int currentLimit = state.currentLimit;
        if (byteLimit > currentLimit)
        {
            throw InvalidProtocolBufferException.TruncatedMessage();
        }

        state.currentLimit = byteLimit;
        RecomputeBufferSizeAfterLimit(ref state);
        return currentLimit;
    }

    //
    // 摘要:
    //     Discards the current limit, returning the previous limit.
    public static void PopLimit(ref ParserInternalState state, int oldLimit)
    {
        state.currentLimit = oldLimit;
        RecomputeBufferSizeAfterLimit(ref state);
    }

    //
    // 摘要:
    //     Returns whether or not all the data before the limit has been read.
    public static bool IsReachedLimit(ref ParserInternalState state)
    {
        if (state.currentLimit == int.MaxValue)
        {
            return false;
        }

        return state.totalBytesRetired + state.bufferPos >= state.currentLimit;
    }

    //
    // 摘要:
    //     Returns true if the stream has reached the end of the input. This is the case
    //     if either the end of the underlying input source has been reached or the stream
    //     has reached a limit created using PushLimit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAtEnd(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.bufferPos == state.bufferSize)
        {
            return !state.segmentedBufferHelper.RefillBuffer(ref buffer, ref state, mustSucceed: false);
        }

        return false;
    }

    private bool RefillFromReadOnlySequence(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, bool mustSucceed)
    {
        CheckCurrentBufferIsEmpty(ref state);
        if (state.totalBytesRetired + state.bufferSize == state.currentLimit)
        {
            if (mustSucceed)
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            return false;
        }

        state.totalBytesRetired += state.bufferSize;
        state.bufferPos = 0;
        state.bufferSize = 0;
        while (readOnlySequenceEnumerator.MoveNext())
        {
            buffer = readOnlySequenceEnumerator.Current.Span;
            state.bufferSize = buffer.Length;
            if (buffer.Length != 0)
            {
                break;
            }
        }

        if (state.bufferSize == 0)
        {
            if (mustSucceed)
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            return false;
        }

        RecomputeBufferSizeAfterLimit(ref state);
        int num = state.totalBytesRetired + state.bufferSize + state.bufferSizeAfterLimit;
        if (num < 0 || num > state.sizeLimit)
        {
            throw InvalidProtocolBufferException.SizeLimitExceeded();
        }

        return true;
    }

    private bool RefillFromCodedInputStream(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, bool mustSucceed)
    {
        CheckCurrentBufferIsEmpty(ref state);
        if (state.totalBytesRetired + state.bufferSize == state.currentLimit)
        {
            if (mustSucceed)
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            return false;
        }

        Stream internalInputStream = codedInputStream.InternalInputStream;
        state.totalBytesRetired += state.bufferSize;
        state.bufferPos = 0;
        state.bufferSize = internalInputStream?.Read(codedInputStream.InternalBuffer, 0, buffer.Length) ?? 0;
        if (state.bufferSize < 0)
        {
            throw new InvalidOperationException("Stream.Read returned a negative count");
        }

        if (state.bufferSize == 0)
        {
            if (mustSucceed)
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            return false;
        }

        RecomputeBufferSizeAfterLimit(ref state);
        int num = state.totalBytesRetired + state.bufferSize + state.bufferSizeAfterLimit;
        if (num < 0 || num > state.sizeLimit)
        {
            throw InvalidProtocolBufferException.SizeLimitExceeded();
        }

        return true;
    }

    private static void RecomputeBufferSizeAfterLimit(ref ParserInternalState state)
    {
        state.bufferSize += state.bufferSizeAfterLimit;
        int num = state.totalBytesRetired + state.bufferSize;
        if (num > state.currentLimit)
        {
            state.bufferSizeAfterLimit = num - state.currentLimit;
            state.bufferSize -= state.bufferSizeAfterLimit;
        }
        else
        {
            state.bufferSizeAfterLimit = 0;
        }
    }

    private static void CheckCurrentBufferIsEmpty(ref ParserInternalState state)
    {
        if (state.bufferPos < state.bufferSize)
        {
            throw new InvalidOperationException("RefillBuffer() called when buffer wasn't empty.");
        }
    }
}