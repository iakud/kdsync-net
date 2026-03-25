using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Primitives for parsing protobuf wire format.
[SecuritySafeCritical]
internal static class ParsingPrimitives
{
    internal static readonly Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const int StackallocThreshold = 256;

    //
    // 摘要:
    //     Reads a length for length-delimited data.
    //
    // 言论：
    //     This is internally just reading a varint, but this method exists to make the
    //     calling code clearer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseLength(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        return (int)ParseRawVarint32(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Parses the next tag. If the end of logical stream was reached, an invalid tag
    //     of 0 is returned.
    public static uint ParseTag(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.hasNextTag)
        {
            state.lastTag = state.nextTag;
            state.hasNextTag = false;
            return state.lastTag;
        }

        if (state.bufferPos + 2 <= state.bufferSize)
        {
            int num = buffer[state.bufferPos++];
            if (num < 128)
            {
                state.lastTag = (uint)num;
            }
            else
            {
                int num2 = num & 0x7F;
                if ((num = buffer[state.bufferPos++]) < 128)
                {
                    num2 |= num << 7;
                    state.lastTag = (uint)num2;
                }
                else
                {
                    state.bufferPos -= 2;
                    state.lastTag = ParseRawVarint32(ref buffer, ref state);
                }
            }
        }
        else
        {
            if (SegmentedBufferHelper.IsAtEnd(ref buffer, ref state))
            {
                state.lastTag = 0u;
                return 0u;
            }

            state.lastTag = ParseRawVarint32(ref buffer, ref state);
        }

        if (WireFormat.GetTagFieldNumber(state.lastTag) == 0)
        {
            throw InvalidProtocolBufferException.InvalidTag();
        }

        return state.lastTag;
    }

    //
    // 摘要:
    //     Peeks at the next tag in the stream. If it matches tag, the tag is consumed and
    //     the method returns true; otherwise, the stream is left in the original position
    //     and the method returns false.
    public static bool MaybeConsumeTag(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, uint tag)
    {
        if (PeekTag(ref buffer, ref state) == tag)
        {
            state.hasNextTag = false;
            return true;
        }

        return false;
    }

    //
    // 摘要:
    //     Peeks at the next field tag. This is like calling Google.Protobuf.ParsingPrimitives.ParseTag(System.ReadOnlySpan{System.Byte}@,Google.Protobuf.ParserInternalState@),
    //     but the tag is not consumed. (So a subsequent call to Google.Protobuf.ParsingPrimitives.ParseTag(System.ReadOnlySpan{System.Byte}@,Google.Protobuf.ParserInternalState@)
    //     will return the same value.)
    public static uint PeekTag(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.hasNextTag)
        {
            return state.nextTag;
        }

        uint lastTag = state.lastTag;
        state.nextTag = ParseTag(ref buffer, ref state);
        state.hasNextTag = true;
        state.lastTag = lastTag;
        return state.nextTag;
    }

    //
    // 摘要:
    //     Parses a raw varint.
    public static ulong ParseRawVarint64(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.bufferPos + 10 > state.bufferSize)
        {
            return ParseRawVarint64SlowPath(ref buffer, ref state);
        }

        ulong num = buffer[state.bufferPos++];
        if (num < 128)
        {
            return num;
        }

        num &= 0x7F;
        int num2 = 7;
        do
        {
            byte b = buffer[state.bufferPos++];
            num |= (ulong)((long)(b & 0x7F) << num2);
            if (b < 128)
            {
                return num;
            }

            num2 += 7;
        }
        while (num2 < 64);
        throw InvalidProtocolBufferException.MalformedVarint();
    }

    private static ulong ParseRawVarint64SlowPath(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        int num = 0;
        ulong num2 = 0uL;
        do
        {
            byte b = ReadRawByte(ref buffer, ref state);
            num2 |= (ulong)((long)(b & 0x7F) << num);
            if (b < 128)
            {
                return num2;
            }

            num += 7;
        }
        while (num < 64);
        throw InvalidProtocolBufferException.MalformedVarint();
    }

    //
    // 摘要:
    //     Parses a raw Varint. If larger than 32 bits, discard the upper bits. This method
    //     is optimised for the case where we've got lots of data in the buffer. That means
    //     we can check the size just once, then just read directly from the buffer without
    //     constant rechecking of the buffer length.
    public static uint ParseRawVarint32(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.bufferPos + 5 > state.bufferSize)
        {
            return ParseRawVarint32SlowPath(ref buffer, ref state);
        }

        int num = buffer[state.bufferPos++];
        if (num < 128)
        {
            return (uint)num;
        }

        int num2 = num & 0x7F;
        if ((num = buffer[state.bufferPos++]) < 128)
        {
            num2 |= num << 7;
        }
        else
        {
            num2 |= (num & 0x7F) << 7;
            if ((num = buffer[state.bufferPos++]) < 128)
            {
                num2 |= num << 14;
            }
            else
            {
                num2 |= (num & 0x7F) << 14;
                if ((num = buffer[state.bufferPos++]) < 128)
                {
                    num2 |= num << 21;
                }
                else
                {
                    num2 |= (num & 0x7F) << 21;
                    num2 |= (num = buffer[state.bufferPos++]) << 28;
                    if (num >= 128)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (ReadRawByte(ref buffer, ref state) < 128)
                            {
                                return (uint)num2;
                            }
                        }

                        throw InvalidProtocolBufferException.MalformedVarint();
                    }
                }
            }
        }

        return (uint)num2;
    }

    private static uint ParseRawVarint32SlowPath(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        int num = ReadRawByte(ref buffer, ref state);
        if (num < 128)
        {
            return (uint)num;
        }

        int num2 = num & 0x7F;
        if ((num = ReadRawByte(ref buffer, ref state)) < 128)
        {
            num2 |= num << 7;
        }
        else
        {
            num2 |= (num & 0x7F) << 7;
            if ((num = ReadRawByte(ref buffer, ref state)) < 128)
            {
                num2 |= num << 14;
            }
            else
            {
                num2 |= (num & 0x7F) << 14;
                if ((num = ReadRawByte(ref buffer, ref state)) < 128)
                {
                    num2 |= num << 21;
                }
                else
                {
                    num2 |= (num & 0x7F) << 21;
                    num2 |= (num = ReadRawByte(ref buffer, ref state)) << 28;
                    if (num >= 128)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (ReadRawByte(ref buffer, ref state) < 128)
                            {
                                return (uint)num2;
                            }
                        }

                        throw InvalidProtocolBufferException.MalformedVarint();
                    }
                }
            }
        }

        return (uint)num2;
    }

    //
    // 摘要:
    //     Parses a 32-bit little-endian integer.
    public static uint ParseRawLittleEndian32(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.bufferPos + 8 > state.bufferSize)
        {
            return ParseRawLittleEndian32SlowPath(ref buffer, ref state);
        }

        int result = (int)BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(state.bufferPos, 8));
        state.bufferPos += 4;
        return (uint)result;
    }

    private static uint ParseRawLittleEndian32SlowPath(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        byte num = ReadRawByte(ref buffer, ref state);
        uint num2 = ReadRawByte(ref buffer, ref state);
        uint num3 = ReadRawByte(ref buffer, ref state);
        uint num4 = ReadRawByte(ref buffer, ref state);
        return num | (num2 << 8) | (num3 << 16) | (num4 << 24);
    }

    //
    // 摘要:
    //     Parses a 64-bit little-endian integer.
    public static ulong ParseRawLittleEndian64(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.bufferPos + 8 > state.bufferSize)
        {
            return ParseRawLittleEndian64SlowPath(ref buffer, ref state);
        }

        ulong result = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(state.bufferPos, 8));
        state.bufferPos += 8;
        return result;
    }

    private static ulong ParseRawLittleEndian64SlowPath(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        long num = ReadRawByte(ref buffer, ref state);
        ulong num2 = ReadRawByte(ref buffer, ref state);
        ulong num3 = ReadRawByte(ref buffer, ref state);
        ulong num4 = ReadRawByte(ref buffer, ref state);
        ulong num5 = ReadRawByte(ref buffer, ref state);
        ulong num6 = ReadRawByte(ref buffer, ref state);
        ulong num7 = ReadRawByte(ref buffer, ref state);
        ulong num8 = ReadRawByte(ref buffer, ref state);
        return (ulong)num | (num2 << 8) | (num3 << 16) | (num4 << 24) | (num5 << 32) | (num6 << 40) | (num7 << 48) | (num8 << 56);
    }

    //
    // 摘要:
    //     Parses a double value.
    public static double ParseDouble(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (!BitConverter.IsLittleEndian || state.bufferPos + 8 > state.bufferSize)
        {
            return BitConverter.Int64BitsToDouble((long)ParseRawLittleEndian64(ref buffer, ref state));
        }

        double result = Unsafe.ReadUnaligned<double>(in MemoryMarshal.GetReference(buffer.Slice(state.bufferPos, 8)));
        state.bufferPos += 8;
        return result;
    }

    //
    // 摘要:
    //     Parses a float value.
    public static float ParseFloat(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (!BitConverter.IsLittleEndian || state.bufferPos + 4 > state.bufferSize)
        {
            return ParseFloatSlow(ref buffer, ref state);
        }

        float result = Unsafe.ReadUnaligned<float>(in MemoryMarshal.GetReference(buffer.Slice(state.bufferPos, 4)));
        state.bufferPos += 4;
        return result;
    }

    private unsafe static float ParseFloatSlow(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        byte* pointer = stackalloc byte[4];
        Span<byte> span = new Span<byte>(pointer, 4);
        for (int i = 0; i < 4; i++)
        {
            span[i] = ReadRawByte(ref buffer, ref state);
        }

        if (!BitConverter.IsLittleEndian)
        {
            span.Reverse();
        }

        return Unsafe.ReadUnaligned<float>(in MemoryMarshal.GetReference(span));
    }

    //
    // 摘要:
    //     Reads a fixed size of bytes from the input.
    //
    // 异常:
    //   T:Google.Protobuf.InvalidProtocolBufferException:
    //     the end of the stream or the current limit was reached
    public static byte[] ReadRawBytes(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        if (size < 0)
        {
            throw InvalidProtocolBufferException.NegativeSize();
        }

        if (size <= state.bufferSize - state.bufferPos)
        {
            byte[] array = new byte[size];
            buffer.Slice(state.bufferPos, size).CopyTo(array);
            state.bufferPos += size;
            return array;
        }

        return ReadRawBytesSlow(ref buffer, ref state, size);
    }

    private static byte[] ReadRawBytesSlow(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        ValidateCurrentLimit(ref buffer, ref state, size);
        if ((!state.segmentedBufferHelper.TotalLength.HasValue && size < buffer.Length) || IsDataAvailableInSource(ref state, size))
        {
            byte[] array = new byte[size];
            ReadRawBytesIntoSpan(ref buffer, ref state, size, array);
            return array;
        }

        List<byte[]> list = new List<byte[]>();
        int num = state.bufferSize - state.bufferPos;
        byte[] array2 = new byte[num];
        buffer.Slice(state.bufferPos, num).CopyTo(array2);
        list.Add(array2);
        state.bufferPos = state.bufferSize;
        int num2 = size - num;
        while (num2 > 0)
        {
            state.segmentedBufferHelper.RefillBuffer(ref buffer, ref state, mustSucceed: true);
            byte[] array3 = new byte[Math.Min(num2, state.bufferSize)];
            buffer.Slice(0, array3.Length).CopyTo(array3);
            state.bufferPos += array3.Length;
            num2 -= array3.Length;
            list.Add(array3);
        }

        byte[] array4 = new byte[size];
        int num3 = 0;
        foreach (byte[] item in list)
        {
            Buffer.BlockCopy(item, 0, array4, num3, item.Length);
            num3 += item.Length;
        }

        return array4;
    }

    //
    // 摘要:
    //     Reads and discards size bytes.
    //
    // 异常:
    //   T:Google.Protobuf.InvalidProtocolBufferException:
    //     the end of the stream or the current limit was reached
    public static void SkipRawBytes(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        if (size < 0)
        {
            throw InvalidProtocolBufferException.NegativeSize();
        }

        ValidateCurrentLimit(ref buffer, ref state, size);
        if (size <= state.bufferSize - state.bufferPos)
        {
            state.bufferPos += size;
            return;
        }

        int num = state.bufferSize - state.bufferPos;
        state.bufferPos = state.bufferSize;
        state.segmentedBufferHelper.RefillBuffer(ref buffer, ref state, mustSucceed: true);
        while (size - num > state.bufferSize)
        {
            num += state.bufferSize;
            state.bufferPos = state.bufferSize;
            state.segmentedBufferHelper.RefillBuffer(ref buffer, ref state, mustSucceed: true);
        }

        state.bufferPos = size - num;
    }

    //
    // 摘要:
    //     Reads a string field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        int length = ParseLength(ref buffer, ref state);
        return ReadRawString(ref buffer, ref state, length);
    }

    //
    // 摘要:
    //     Reads a bytes field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadBytes(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        int size = ParseLength(ref buffer, ref state);
        return ReadRawBytes(ref buffer, ref state, size);
    }

    //
    // 摘要:
    //     Reads a UTF-8 string from the next "length" bytes.
    //
    // 异常:
    //   T:Google.Protobuf.InvalidProtocolBufferException:
    //     the end of the stream or the current limit was reached
    [SecuritySafeCritical]
    public unsafe static string ReadRawString(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        if (length < 0)
        {
            throw InvalidProtocolBufferException.NegativeSize();
        }

        if (length <= state.bufferSize - state.bufferPos)
        {
            string result;
            fixed (byte* reference = &MemoryMarshal.GetReference(buffer.Slice(state.bufferPos, length)))
            {
                try
                {
                    result = Utf8Encoding.GetString(reference, length);
                }
                catch (DecoderFallbackException innerException)
                {
                    throw InvalidProtocolBufferException.InvalidUtf8(innerException);
                }
            }

            state.bufferPos += length;
            return result;
        }

        return ReadStringSlow(ref buffer, ref state, length);
    }

    //
    // 摘要:
    //     Reads a string assuming that it is spread across multiple spans in a System.Buffers.ReadOnlySequence`1.
    private unsafe static string ReadStringSlow(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int length)
    {
        ValidateCurrentLimit(ref buffer, ref state, length);
        if (IsDataAvailable(ref state, length))
        {
            byte[] array = null;
            Span<byte> span = ((length > 256) ? ((Span<byte>)(array = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
            Span<byte> span2 = span;
            try
            {
                fixed (byte* reference = &MemoryMarshal.GetReference(span2))
                {
                    Span<byte> byteSpan = new Span<byte>(reference, span2.Length);
                    ReadRawBytesIntoSpan(ref buffer, ref state, length, byteSpan);
                    try
                    {
                        return Utf8Encoding.GetString(reference, length);
                    }
                    catch (DecoderFallbackException innerException)
                    {
                        throw InvalidProtocolBufferException.InvalidUtf8(innerException);
                    }
                }
            }
            finally
            {
                if (array != null)
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }

        byte[] bytes = ReadRawBytes(ref buffer, ref state, length);
        try
        {
            return Utf8Encoding.GetString(bytes, 0, length);
        }
        catch (DecoderFallbackException innerException2)
        {
            throw InvalidProtocolBufferException.InvalidUtf8(innerException2);
        }
    }

    //
    // 摘要:
    //     Validates that the specified size doesn't exceed the current limit. If it does
    //     then remaining bytes are skipped and an error is thrown.
    private static void ValidateCurrentLimit(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        if (state.totalBytesRetired + state.bufferPos + size > state.currentLimit)
        {
            SkipRawBytes(ref buffer, ref state, state.currentLimit - state.totalBytesRetired - state.bufferPos);
            throw InvalidProtocolBufferException.TruncatedMessage();
        }
    }

    [SecuritySafeCritical]
    private static byte ReadRawByte(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.bufferPos == state.bufferSize)
        {
            state.segmentedBufferHelper.RefillBuffer(ref buffer, ref state, mustSucceed: true);
        }

        return buffer[state.bufferPos++];
    }

    //
    // 摘要:
    //     Reads a varint from the input one byte at a time, so that it does not read any
    //     bytes after the end of the varint. If you simply wrapped the stream in a CodedInputStream
    //     and used ReadRawVarint32(Stream) then you would probably end up reading past
    //     the end of the varint since CodedInputStream buffers its input.
    //
    // 参数:
    //   input:
    public static uint ReadRawVarint32(Stream input)
    {
        int num = 0;
        int i;
        for (i = 0; i < 32; i += 7)
        {
            int num2 = input.ReadByte();
            if (num2 == -1)
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            num |= (num2 & 0x7F) << i;
            if ((num2 & 0x80) == 0)
            {
                return (uint)num;
            }
        }

        for (; i < 64; i += 7)
        {
            int num3 = input.ReadByte();
            if (num3 == -1)
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            if ((num3 & 0x80) == 0)
            {
                return (uint)num;
            }
        }

        throw InvalidProtocolBufferException.MalformedVarint();
    }

    //
    // 摘要:
    //     Decode a 32-bit value with ZigZag encoding.
    //
    // 言论：
    //     ZigZag encodes signed integers into values that can be efficiently encoded with
    //     varint. (Otherwise, negative values must be sign-extended to 32 bits to be varint
    //     encoded, thus always taking 5 bytes on the wire.)
    public static int DecodeZigZag32(uint n)
    {
        return (int)((n >> 1) ^ (0 - (n & 1)));
    }

    //
    // 摘要:
    //     Decode a 64-bit value with ZigZag encoding.
    //
    // 言论：
    //     ZigZag encodes signed integers into values that can be efficiently encoded with
    //     varint. (Otherwise, negative values must be sign-extended to 64 bits to be varint
    //     encoded, thus always taking 10 bytes on the wire.)
    public static long DecodeZigZag64(ulong n)
    {
        return (long)((n >> 1) ^ (0L - (n & 1)));
    }

    //
    // 摘要:
    //     Checks whether there is known data available of the specified size remaining
    //     to parse. When parsing from a Stream this can return false because we have no
    //     knowledge of the amount of data remaining in the stream until it is read.
    public static bool IsDataAvailable(ref ParserInternalState state, int size)
    {
        if (size <= state.bufferSize - state.bufferPos)
        {
            return true;
        }

        return IsDataAvailableInSource(ref state, size);
    }

    //
    // 摘要:
    //     Checks whether there is known data available of the specified size remaining
    //     to parse in the underlying data source. When parsing from a Stream this will
    //     return false because we have no knowledge of the amount of data remaining in
    //     the stream until it is read.
    private static bool IsDataAvailableInSource(ref ParserInternalState state, int size)
    {
        return size <= state.segmentedBufferHelper.TotalLength - state.totalBytesRetired - state.bufferPos;
    }

    //
    // 摘要:
    //     Read raw bytes of the specified length into a span. The amount of data available
    //     and the current limit should be checked before calling this method.
    private static void ReadRawBytesIntoSpan(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int length, Span<byte> byteSpan)
    {
        int num = length;
        while (num > 0)
        {
            if (state.bufferSize - state.bufferPos == 0)
            {
                state.segmentedBufferHelper.RefillBuffer(ref buffer, ref state, mustSucceed: true);
            }

            ReadOnlySpan<byte> readOnlySpan = buffer.Slice(state.bufferPos, Math.Min(num, state.bufferSize - state.bufferPos));
            readOnlySpan.CopyTo(byteSpan.Slice(length - num));
            num -= readOnlySpan.Length;
            state.bufferPos += readOnlySpan.Length;
        }
    }

    //
    // 摘要:
    //     Read LittleEndian packed field from buffer of specified length into a span. The
    //     amount of data available and the current limit should be checked before calling
    //     this method.
    internal static void ReadPackedFieldLittleEndian(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int length, Span<byte> outBuffer)
    {
        if (length <= state.bufferSize - state.bufferPos)
        {
            buffer.Slice(state.bufferPos, length).CopyTo(outBuffer);
            state.bufferPos += length;
        }
        else
        {
            ReadRawBytesIntoSpan(ref buffer, ref state, length, outBuffer);
        }
    }
}