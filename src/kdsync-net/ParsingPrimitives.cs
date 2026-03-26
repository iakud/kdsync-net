using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Kdsync;

[SecuritySafeCritical]
internal static class ParsingPrimitives
{
    internal static readonly Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const int StackallocThreshold = 256;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseLength(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        return (int)ParseRawVarint32(ref buffer, ref state);
    }

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
            throw InvalidException.InvalidTag();
        }

        return state.lastTag;
    }

    public static bool MaybeConsumeTag(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, uint tag)
    {
        if (PeekTag(ref buffer, ref state) == tag)
        {
            state.hasNextTag = false;
            return true;
        }

        return false;
    }

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
        throw InvalidException.MalformedVarint();
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
        throw InvalidException.MalformedVarint();
    }

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

                        throw InvalidException.MalformedVarint();
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

                        throw InvalidException.MalformedVarint();
                    }
                }
            }
        }

        return (uint)num2;
    }

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

    public static double ParseDouble(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (!BitConverter.IsLittleEndian || state.bufferPos + 8 > state.bufferSize)
        {
            return BitConverter.Int64BitsToDouble((long)ParseRawLittleEndian64(ref buffer, ref state));
        }

        double result = Unsafe.ReadUnaligned<double>(ref MemoryMarshal.GetReference(buffer.Slice(state.bufferPos, 8)));
        state.bufferPos += 8;
        return result;
    }

    public static float ParseFloat(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (!BitConverter.IsLittleEndian || state.bufferPos + 4 > state.bufferSize)
        {
            return ParseFloatSlow(ref buffer, ref state);
        }

        float result = Unsafe.ReadUnaligned<float>(ref MemoryMarshal.GetReference(buffer.Slice(state.bufferPos, 4)));
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

        return Unsafe.ReadUnaligned<float>(ref MemoryMarshal.GetReference(span));
    }

    public static byte[] ReadRawBytes(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        if (size < 0)
        {
            throw InvalidException.NegativeSize();
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

        Repeated<byte[]> list = new Repeated<byte[]>();
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

    public static void SkipRawBytes(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        if (size < 0)
        {
            throw InvalidException.NegativeSize();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        int length = ParseLength(ref buffer, ref state);
        return ReadRawString(ref buffer, ref state, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadBytes(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        int size = ParseLength(ref buffer, ref state);
        return ReadRawBytes(ref buffer, ref state, size);
    }

    [SecuritySafeCritical]
    public unsafe static string ReadRawString(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        if (length < 0)
        {
            throw InvalidException.NegativeSize();
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
                    throw InvalidException.InvalidUtf8(innerException);
                }
            }

            state.bufferPos += length;
            return result;
        }

        return ReadStringSlow(ref buffer, ref state, length);
    }

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
                        throw InvalidException.InvalidUtf8(innerException);
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
            throw InvalidException.InvalidUtf8(innerException2);
        }
    }

    private static void ValidateCurrentLimit(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, int size)
    {
        if (state.totalBytesRetired + state.bufferPos + size > state.currentLimit)
        {
            SkipRawBytes(ref buffer, ref state, state.currentLimit - state.totalBytesRetired - state.bufferPos);
            throw InvalidException.TruncatedMessage();
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

    public static uint ReadRawVarint32(Stream input)
    {
        int num = 0;
        int i;
        for (i = 0; i < 32; i += 7)
        {
            int num2 = input.ReadByte();
            if (num2 == -1)
            {
                throw InvalidException.TruncatedMessage();
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
                throw InvalidException.TruncatedMessage();
            }

            if ((num3 & 0x80) == 0)
            {
                return (uint)num;
            }
        }

        throw InvalidException.MalformedVarint();
    }

    public static int DecodeZigZag32(uint n)
    {
        return (int)((n >> 1) ^ (0 - (n & 1)));
    }

    public static long DecodeZigZag64(ulong n)
    {
        return (long)((n >> 1) ^ (0L - (n & 1)));
    }

    public static bool IsDataAvailable(ref ParserInternalState state, int size)
    {
        if (size <= state.bufferSize - state.bufferPos)
        {
            return true;
        }

        return IsDataAvailableInSource(ref state, size);
    }

    private static bool IsDataAvailableInSource(ref ParserInternalState state, int size)
    {
        return size <= state.segmentedBufferHelper.TotalLength - state.totalBytesRetired - state.bufferPos;
    }

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