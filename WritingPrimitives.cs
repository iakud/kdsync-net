
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Security;
using System.Text;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Primitives for encoding protobuf wire format.
[SecuritySafeCritical]
internal static class WritingPrimitives
{
    internal static Encoding Utf8Encoding => Encoding.UTF8;

    //
    // 摘要:
    //     Writes a double field value, without a tag, to the stream.
    public static void WriteDouble(ref Span<byte> buffer, ref WriterInternalState state, double value)
    {
        WriteRawLittleEndian64(ref buffer, ref state, (ulong)BitConverter.DoubleToInt64Bits(value));
    }

    //
    // 摘要:
    //     Writes a float field value, without a tag, to the stream.
    public static void WriteFloat(ref Span<byte> buffer, ref WriterInternalState state, float value)
    {
        if (buffer.Length - state.position >= 4)
        {
            Span<byte> span = buffer.Slice(state.position, 4);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(span), value);
            if (!BitConverter.IsLittleEndian)
            {
                span.Reverse();
            }

            state.position += 4;
        }
        else
        {
            WriteFloatSlowPath(ref buffer, ref state, value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteFloatSlowPath(ref Span<byte> buffer, ref WriterInternalState state, float value)
    {
        Span<byte> span = stackalloc byte[4];
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(span), value);
        if (!BitConverter.IsLittleEndian)
        {
            span.Reverse();
        }

        WriteRawByte(ref buffer, ref state, span[0]);
        WriteRawByte(ref buffer, ref state, span[1]);
        WriteRawByte(ref buffer, ref state, span[2]);
        WriteRawByte(ref buffer, ref state, span[3]);
    }

    //
    // 摘要:
    //     Writes a uint64 field value, without a tag, to the stream.
    public static void WriteUInt64(ref Span<byte> buffer, ref WriterInternalState state, ulong value)
    {
        WriteRawVarint64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an int64 field value, without a tag, to the stream.
    public static void WriteInt64(ref Span<byte> buffer, ref WriterInternalState state, long value)
    {
        WriteRawVarint64(ref buffer, ref state, (ulong)value);
    }

    //
    // 摘要:
    //     Writes an int32 field value, without a tag, to the stream.
    public static void WriteInt32(ref Span<byte> buffer, ref WriterInternalState state, int value)
    {
        if (value >= 0)
        {
            WriteRawVarint32(ref buffer, ref state, (uint)value);
        }
        else
        {
            WriteRawVarint64(ref buffer, ref state, (ulong)value);
        }
    }

    //
    // 摘要:
    //     Writes a fixed64 field value, without a tag, to the stream.
    public static void WriteFixed64(ref Span<byte> buffer, ref WriterInternalState state, ulong value)
    {
        WriteRawLittleEndian64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a fixed32 field value, without a tag, to the stream.
    public static void WriteFixed32(ref Span<byte> buffer, ref WriterInternalState state, uint value)
    {
        WriteRawLittleEndian32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a bool field value, without a tag, to the stream.
    public static void WriteBool(ref Span<byte> buffer, ref WriterInternalState state, bool value)
    {
        WriteRawByte(ref buffer, ref state, (byte)(value ? 1 : 0));
    }

    //
    // 摘要:
    //     Writes a string field value, without a tag, to the stream. The data is length-prefixed.
    public static void WriteString(ref Span<byte> buffer, ref WriterInternalState state, string value)
    {
        if (value.Length <= 42 && buffer.Length - state.position - 1 >= value.Length * 3)
        {
            buffer[state.position++] = (byte)WriteStringToBuffer(buffer, ref state, value);
            return;
        }

        int byteCount = Utf8Encoding.GetByteCount(value);
        WriteLength(ref buffer, ref state, byteCount);
        if (buffer.Length - state.position >= byteCount)
        {
            if (byteCount == value.Length)
            {
                WriteAsciiStringToBuffer(buffer, ref state, value, byteCount);
            }
            else
            {
                WriteStringToBuffer(buffer, ref state, value);
            }
        }
        else
        {
            byte[] bytes = Utf8Encoding.GetBytes(value);
            WriteRawBytes(ref buffer, ref state, bytes);
        }
    }

    private static void WriteAsciiStringToBuffer(Span<byte> buffer, ref WriterInternalState state, string value, int length)
    {
        ref char reference = ref MemoryMarshal.GetReference(value.AsSpan());
        ref byte reference2 = ref MemoryMarshal.GetReference(buffer.Slice(state.position));
        int i = 0;
        if (IntPtr.Size == 8 && length >= 4)
        {
            ref byte source = ref Unsafe.As<char, byte>(ref reference);
            int num = value.Length - 4;
            do
            {
                NarrowFourUtf16CharsToAsciiAndWriteToBuffer(ref Unsafe.AddByteOffset(ref reference2, (IntPtr)i), Unsafe.ReadUnaligned<ulong>(in Unsafe.AddByteOffset(ref source, (IntPtr)(i * 2))));
            }
            while ((i += 4) <= num);
        }

        for (; i < length; i++)
        {
            Unsafe.AddByteOffset(ref reference2, (IntPtr)i) = (byte)Unsafe.AddByteOffset(ref reference, (IntPtr)(i * 2));
        }

        state.position += length;
    }

    //
    // 摘要:
    //     Given a QWORD which represents a buffer of 4 ASCII chars in machine-endian order,
    //     narrows each WORD to a BYTE, then writes the 4-byte result to the output buffer
    //     also in machine-endian order.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void NarrowFourUtf16CharsToAsciiAndWriteToBuffer(ref byte outputBuffer, ulong value)
    {
        if (Sse2.X64.IsSupported)
        {
            Vector128<short> vector = Sse2.X64.ConvertScalarToVector128UInt64(value).AsInt16();
            Vector128<uint> value2 = Sse2.PackUnsignedSaturate(vector, vector).AsUInt32();
            Unsafe.WriteUnaligned(ref outputBuffer, Sse2.ConvertToUInt32(value2));
        }
        else if (AdvSimd.IsSupported)
        {
            Vector64<byte> vector2 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(Vector128.CreateScalarUnsafe(value).AsInt16());
            Unsafe.WriteUnaligned(ref outputBuffer, vector2.AsUInt32().ToScalar());
        }
        else if (BitConverter.IsLittleEndian)
        {
            outputBuffer = (byte)value;
            value >>= 16;
            Unsafe.Add(ref outputBuffer, 1) = (byte)value;
            value >>= 16;
            Unsafe.Add(ref outputBuffer, 2) = (byte)value;
            value >>= 16;
            Unsafe.Add(ref outputBuffer, 3) = (byte)value;
        }
        else
        {
            Unsafe.Add(ref outputBuffer, 3) = (byte)value;
            value >>= 16;
            Unsafe.Add(ref outputBuffer, 2) = (byte)value;
            value >>= 16;
            Unsafe.Add(ref outputBuffer, 1) = (byte)value;
            value >>= 16;
            outputBuffer = (byte)value;
        }
    }

    private unsafe static int WriteStringToBuffer(Span<byte> buffer, ref WriterInternalState state, string value)
    {
        ReadOnlySpan<char> span = value.AsSpan();
        int bytes;
        fixed (char* reference = &MemoryMarshal.GetReference(span))
        {
            fixed (byte* reference2 = &MemoryMarshal.GetReference(buffer))
            {
                bytes = Utf8Encoding.GetBytes(reference, span.Length, reference2 + state.position, buffer.Length - state.position);
            }
        }

        state.position += bytes;
        return bytes;
    }

    //
    // 摘要:
    //     Write a byte string, without a tag, to the stream. The data is length-prefixed.
    public static void WriteBytes(ref Span<byte> buffer, ref WriterInternalState state, byte[] value)
    {
        WriteLength(ref buffer, ref state, value.Length);
        WriteRawBytes(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a uint32 value, without a tag, to the stream.
    public static void WriteUInt32(ref Span<byte> buffer, ref WriterInternalState state, uint value)
    {
        WriteRawVarint32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an enum value, without a tag, to the stream.
    public static void WriteEnum(ref Span<byte> buffer, ref WriterInternalState state, int value)
    {
        WriteInt32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sfixed32 value, without a tag, to the stream.
    public static void WriteSFixed32(ref Span<byte> buffer, ref WriterInternalState state, int value)
    {
        WriteRawLittleEndian32(ref buffer, ref state, (uint)value);
    }

    //
    // 摘要:
    //     Writes an sfixed64 value, without a tag, to the stream.
    public static void WriteSFixed64(ref Span<byte> buffer, ref WriterInternalState state, long value)
    {
        WriteRawLittleEndian64(ref buffer, ref state, (ulong)value);
    }

    //
    // 摘要:
    //     Writes an sint32 value, without a tag, to the stream.
    public static void WriteSInt32(ref Span<byte> buffer, ref WriterInternalState state, int value)
    {
        WriteRawVarint32(ref buffer, ref state, EncodeZigZag32(value));
    }

    //
    // 摘要:
    //     Writes an sint64 value, without a tag, to the stream.
    public static void WriteSInt64(ref Span<byte> buffer, ref WriterInternalState state, long value)
    {
        WriteRawVarint64(ref buffer, ref state, EncodeZigZag64(value));
    }

    //
    // 摘要:
    //     Writes a length (in bytes) for length-delimited data.
    //
    // 言论：
    //     This method simply writes a rawint, but exists for clarity in calling code.
    public static void WriteLength(ref Span<byte> buffer, ref WriterInternalState state, int length)
    {
        WriteRawVarint32(ref buffer, ref state, (uint)length);
    }

    //
    // 摘要:
    //     Writes a 32 bit value as a varint. The fast route is taken when there's enough
    //     buffer space left to whizz through without checking for each byte; otherwise,
    //     we resort to calling WriteRawByte each time.
    public static void WriteRawVarint32(ref Span<byte> buffer, ref WriterInternalState state, uint value)
    {
        if (value < 128 && state.position < buffer.Length)
        {
            buffer[state.position++] = (byte)value;
            return;
        }

        while (state.position < buffer.Length)
        {
            if (value > 127)
            {
                buffer[state.position++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
                continue;
            }

            buffer[state.position++] = (byte)value;
            return;
        }

        while (value > 127)
        {
            WriteRawByte(ref buffer, ref state, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        WriteRawByte(ref buffer, ref state, (byte)value);
    }

    public static void WriteRawVarint64(ref Span<byte> buffer, ref WriterInternalState state, ulong value)
    {
        if (value < 128 && state.position < buffer.Length)
        {
            buffer[state.position++] = (byte)value;
            return;
        }

        while (state.position < buffer.Length)
        {
            if (value > 127)
            {
                buffer[state.position++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
                continue;
            }

            buffer[state.position++] = (byte)value;
            return;
        }

        while (value > 127)
        {
            WriteRawByte(ref buffer, ref state, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        WriteRawByte(ref buffer, ref state, (byte)value);
    }

    public static void WriteRawLittleEndian32(ref Span<byte> buffer, ref WriterInternalState state, uint value)
    {
        if (state.position + 4 > buffer.Length)
        {
            WriteRawLittleEndian32SlowPath(ref buffer, ref state, value);
            return;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(state.position), value);
        state.position += 4;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteRawLittleEndian32SlowPath(ref Span<byte> buffer, ref WriterInternalState state, uint value)
    {
        WriteRawByte(ref buffer, ref state, (byte)value);
        WriteRawByte(ref buffer, ref state, (byte)(value >> 8));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 16));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 24));
    }

    public static void WriteRawLittleEndian64(ref Span<byte> buffer, ref WriterInternalState state, ulong value)
    {
        if (state.position + 8 > buffer.Length)
        {
            WriteRawLittleEndian64SlowPath(ref buffer, ref state, value);
            return;
        }

        BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(state.position), value);
        state.position += 8;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WriteRawLittleEndian64SlowPath(ref Span<byte> buffer, ref WriterInternalState state, ulong value)
    {
        WriteRawByte(ref buffer, ref state, (byte)value);
        WriteRawByte(ref buffer, ref state, (byte)(value >> 8));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 16));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 24));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 32));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 40));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 48));
        WriteRawByte(ref buffer, ref state, (byte)(value >> 56));
    }

    private static void WriteRawByte(ref Span<byte> buffer, ref WriterInternalState state, byte value)
    {
        if (state.position == buffer.Length)
        {
            WriteBufferHelper.RefreshBuffer(ref buffer, ref state);
        }

        buffer[state.position++] = value;
    }

    //
    // 摘要:
    //     Writes out an array of bytes.
    public static void WriteRawBytes(ref Span<byte> buffer, ref WriterInternalState state, byte[] value)
    {
        WriteRawBytes(ref buffer, ref state, new ReadOnlySpan<byte>(value));
    }

    //
    // 摘要:
    //     Writes out part of an array of bytes.
    public static void WriteRawBytes(ref Span<byte> buffer, ref WriterInternalState state, byte[] value, int offset, int length)
    {
        WriteRawBytes(ref buffer, ref state, new ReadOnlySpan<byte>(value, offset, length));
    }

    //
    // 摘要:
    //     Writes out part of an array of bytes.
    public static void WriteRawBytes(ref Span<byte> buffer, ref WriterInternalState state, ReadOnlySpan<byte> value)
    {
        if (buffer.Length - state.position >= value.Length)
        {
            value.CopyTo(buffer.Slice(state.position, value.Length));
            state.position += value.Length;
            return;
        }

        int num = 0;
        while (buffer.Length - state.position < value.Length - num)
        {
            int num2 = buffer.Length - state.position;
            value.Slice(num, num2).CopyTo(buffer.Slice(state.position, num2));
            num += num2;
            state.position += num2;
            WriteBufferHelper.RefreshBuffer(ref buffer, ref state);
        }

        int num3 = value.Length - num;
        value.Slice(num, num3).CopyTo(buffer.Slice(state.position, num3));
        state.position += num3;
    }

    //
    // 摘要:
    //     Encodes and writes a tag.
    public static void WriteTag(ref Span<byte> buffer, ref WriterInternalState state, int fieldNumber, WireFormat.WireType type)
    {
        WriteRawVarint32(ref buffer, ref state, WireFormat.MakeTag(fieldNumber, type));
    }

    //
    // 摘要:
    //     Writes an already-encoded tag.
    public static void WriteTag(ref Span<byte> buffer, ref WriterInternalState state, uint tag)
    {
        WriteRawVarint32(ref buffer, ref state, tag);
    }

    //
    // 摘要:
    //     Writes the given single-byte tag directly to the stream.
    public static void WriteRawTag(ref Span<byte> buffer, ref WriterInternalState state, byte b1)
    {
        WriteRawByte(ref buffer, ref state, b1);
    }

    //
    // 摘要:
    //     Writes the given two-byte tag directly to the stream.
    public static void WriteRawTag(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2)
    {
        if (state.position + 2 > buffer.Length)
        {
            WriteRawTagSlowPath(ref buffer, ref state, b1, b2);
            return;
        }

        buffer[state.position++] = b1;
        buffer[state.position++] = b2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteRawTagSlowPath(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2)
    {
        WriteRawByte(ref buffer, ref state, b1);
        WriteRawByte(ref buffer, ref state, b2);
    }

    //
    // 摘要:
    //     Writes the given three-byte tag directly to the stream.
    public static void WriteRawTag(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2, byte b3)
    {
        if (state.position + 3 > buffer.Length)
        {
            WriteRawTagSlowPath(ref buffer, ref state, b1, b2, b3);
            return;
        }

        buffer[state.position++] = b1;
        buffer[state.position++] = b2;
        buffer[state.position++] = b3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteRawTagSlowPath(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2, byte b3)
    {
        WriteRawByte(ref buffer, ref state, b1);
        WriteRawByte(ref buffer, ref state, b2);
        WriteRawByte(ref buffer, ref state, b3);
    }

    //
    // 摘要:
    //     Writes the given four-byte tag directly to the stream.
    public static void WriteRawTag(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2, byte b3, byte b4)
    {
        if (state.position + 4 > buffer.Length)
        {
            WriteRawTagSlowPath(ref buffer, ref state, b1, b2, b3, b4);
            return;
        }

        buffer[state.position++] = b1;
        buffer[state.position++] = b2;
        buffer[state.position++] = b3;
        buffer[state.position++] = b4;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteRawTagSlowPath(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2, byte b3, byte b4)
    {
        WriteRawByte(ref buffer, ref state, b1);
        WriteRawByte(ref buffer, ref state, b2);
        WriteRawByte(ref buffer, ref state, b3);
        WriteRawByte(ref buffer, ref state, b4);
    }

    //
    // 摘要:
    //     Writes the given five-byte tag directly to the stream.
    public static void WriteRawTag(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2, byte b3, byte b4, byte b5)
    {
        if (state.position + 5 > buffer.Length)
        {
            WriteRawTagSlowPath(ref buffer, ref state, b1, b2, b3, b4, b5);
            return;
        }

        buffer[state.position++] = b1;
        buffer[state.position++] = b2;
        buffer[state.position++] = b3;
        buffer[state.position++] = b4;
        buffer[state.position++] = b5;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteRawTagSlowPath(ref Span<byte> buffer, ref WriterInternalState state, byte b1, byte b2, byte b3, byte b4, byte b5)
    {
        WriteRawByte(ref buffer, ref state, b1);
        WriteRawByte(ref buffer, ref state, b2);
        WriteRawByte(ref buffer, ref state, b3);
        WriteRawByte(ref buffer, ref state, b4);
        WriteRawByte(ref buffer, ref state, b5);
    }

    //
    // 摘要:
    //     Encode a 32-bit value with ZigZag encoding.
    //
    // 言论：
    //     ZigZag encodes signed integers into values that can be efficiently encoded with
    //     varint. (Otherwise, negative values must be sign-extended to 64 bits to be varint
    //     encoded, thus always taking 10 bytes on the wire.)
    public static uint EncodeZigZag32(int n)
    {
        return (uint)((n << 1) ^ (n >> 31));
    }

    //
    // 摘要:
    //     Encode a 64-bit value with ZigZag encoding.
    //
    // 言论：
    //     ZigZag encodes signed integers into values that can be efficiently encoded with
    //     varint. (Otherwise, negative values must be sign-extended to 64 bits to be varint
    //     encoded, thus always taking 10 bytes on the wire.)
    public static ulong EncodeZigZag64(long n)
    {
        return (ulong)((n << 1) ^ (n >> 63));
    }
}