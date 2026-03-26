
using System;
using System.IO;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Encodes and writes protocol message fields.
//
// 言论：
//     This class is generally used by generated code to write appropriate primitives
//     to the stream. It effectively encapsulates the lowest levels of protocol buffer
//     format. Unlike some other implementations, this does not include combined "write
//     tag and value" methods. Generated code knows the exact byte representations of
//     the tags they're going to write, so there's no need to re-encode them each time.
//     Manually-written code calling this class should just call one of the WriteTag
//     overloads before each value.
//
//     Repeated fields and map fields are not handled by this class; use RepeatedField<T>
//     and MapField<TKey, TValue> to serialize such fields.
[SecuritySafeCritical]
public sealed class CodedOutputStream : IDisposable
{
    //
    // 摘要:
    //     Indicates that a CodedOutputStream wrapping a flat byte array ran out of space.
    public sealed class OutOfSpaceException : IOException
    {
        internal OutOfSpaceException()
            : base("CodedOutputStream was writing to a flat byte array and ran out of space.")
        {
        }
    }

    private const int LittleEndian64Size = 8;

    private const int LittleEndian32Size = 4;

    internal const int DoubleSize = 8;

    internal const int FloatSize = 4;

    internal const int BoolSize = 1;

    //
    // 摘要:
    //     The buffer size used by CreateInstance(Stream).
    public static readonly int DefaultBufferSize = 4096;

    private readonly bool leaveOpen;

    private readonly byte[] buffer;

    private WriterInternalState state;

    private readonly Stream output;

    //
    // 摘要:
    //     Returns the current position in the stream, or the position in the output buffer
    public long Position
    {
        get
        {
            if (output != null)
            {
                return output.Position + state.position;
            }

            return state.position;
        }
    }

    //
    // 摘要:
    //     Configures whether or not serialization is deterministic.
    //
    // 言论：
    //     Deterministic serialization guarantees that for a given binary, equal messages
    //     (defined by the equals methods in protos) will always be serialized to the same
    //     bytes. This implies:
    //
    //     • Repeated serialization of a message will return the same bytes.
    //     • Different processes of the same binary (which may be executing on different
    //     machines) will serialize equal messages to the same bytes.
    //
    //     Note the deterministic serialization is NOT canonical across languages; it is
    //     also unstable across different builds with schema changes due to unknown fields.
    //     Users who need canonical serialization, e.g. persistent storage in a canonical
    //     form, fingerprinting, etc, should define their own canonicalization specification
    //     and implement the serializer using reflection APIs rather than relying on this
    //     API. Once set, the serializer will: (Note this is an implementation detail and
    //     may subject to change in the future)
    //
    //     • Sort map entries by keys in lexicographical order or numerical order. Note:
    //     For string keys, the order is based on comparing the UTF-16 code unit value of
    //     each character in the strings. The order may be different from the deterministic
    //     serialization in other languages where maps are sorted on the lexicographical
    //     order of the UTF8 encoded keys.
    public bool Deterministic { get; set; }

    //
    // 摘要:
    //     If writing to a flat array, returns the space left in the array. Otherwise, throws
    //     an InvalidOperationException.
    public int SpaceLeft => WriteBufferHelper.GetSpaceLeft(ref state);

    internal byte[] InternalBuffer => buffer;

    internal Stream InternalOutputStream => output;

    internal ref WriterInternalState InternalState => ref state;

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a double field, including
    //     the tag.
    public static int ComputeDoubleSize(double value)
    {
        return 8;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a float field, including
    //     the tag.
    public static int ComputeFloatSize(float value)
    {
        return 4;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a uint64 field, including
    //     the tag.
    public static int ComputeUInt64Size(ulong value)
    {
        return ComputeRawVarint64Size(value);
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an int64 field, including
    //     the tag.
    public static int ComputeInt64Size(long value)
    {
        return ComputeRawVarint64Size((ulong)value);
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an int32 field, including
    //     the tag.
    public static int ComputeInt32Size(int value)
    {
        if (value >= 0)
        {
            return ComputeRawVarint32Size((uint)value);
        }

        return 10;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a fixed64 field,
    //     including the tag.
    public static int ComputeFixed64Size(ulong value)
    {
        return 8;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a fixed32 field,
    //     including the tag.
    public static int ComputeFixed32Size(uint value)
    {
        return 4;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a bool field, including
    //     the tag.
    public static int ComputeBoolSize(bool value)
    {
        return 1;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a string field, including
    //     the tag.
    public static int ComputeStringSize(string value)
    {
        int byteCount = WritingPrimitives.Utf8Encoding.GetByteCount(value);
        return ComputeLengthSize(byteCount) + byteCount;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a group field, including
    //     the tag.
    public static int ComputeGroupSize(IMessage value)
    {
        return value.CalculateSize();
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an embedded message
    //     field, including the tag.
    public static int ComputeMessageSize(IMessage value)
    {
        int num = value.CalculateSize();
        return ComputeLengthSize(num) + num;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a bytes field, including
    //     the tag.
    public static int ComputeBytesSize(byte[] value)
    {
        return ComputeLengthSize(value.Length) + value.Length;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a uint32 field, including
    //     the tag.
    public static int ComputeUInt32Size(uint value)
    {
        return ComputeRawVarint32Size(value);
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a enum field, including
    //     the tag. The caller is responsible for converting the enum value to its numeric
    //     value.
    public static int ComputeEnumSize(int value)
    {
        return ComputeInt32Size(value);
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an sfixed32 field,
    //     including the tag.
    public static int ComputeSFixed32Size(int value)
    {
        return 4;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an sfixed64 field,
    //     including the tag.
    public static int ComputeSFixed64Size(long value)
    {
        return 8;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an sint32 field,
    //     including the tag.
    public static int ComputeSInt32Size(int value)
    {
        return ComputeRawVarint32Size(WritingPrimitives.EncodeZigZag32(value));
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode an sint64 field,
    //     including the tag.
    public static int ComputeSInt64Size(long value)
    {
        return ComputeRawVarint64Size(WritingPrimitives.EncodeZigZag64(value));
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a length, as written
    //     by Google.Protobuf.CodedOutputStream.WriteLength(System.Int32).
    public static int ComputeLengthSize(int length)
    {
        return ComputeRawVarint32Size((uint)length);
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a varint.
    public static int ComputeRawVarint32Size(uint value)
    {
        if ((value & 0xFFFFFF80u) == 0)
        {
            return 1;
        }

        if ((value & 0xFFFFC000u) == 0)
        {
            return 2;
        }

        if ((value & 0xFFE00000u) == 0)
        {
            return 3;
        }

        if ((value & 0xF0000000u) == 0)
        {
            return 4;
        }

        return 5;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a varint.
    public static int ComputeRawVarint64Size(ulong value)
    {
        if ((value & 0xFFFFFFFFFFFFFF80uL) == 0L)
        {
            return 1;
        }

        if ((value & 0xFFFFFFFFFFFFC000uL) == 0L)
        {
            return 2;
        }

        if ((value & 0xFFFFFFFFFFE00000uL) == 0L)
        {
            return 3;
        }

        if ((value & 0xFFFFFFFFF0000000uL) == 0L)
        {
            return 4;
        }

        if ((value & 0xFFFFFFF800000000uL) == 0L)
        {
            return 5;
        }

        if ((value & 0xFFFFFC0000000000uL) == 0L)
        {
            return 6;
        }

        if ((value & 0xFFFE000000000000uL) == 0L)
        {
            return 7;
        }

        if ((value & 0xFF00000000000000uL) == 0L)
        {
            return 8;
        }

        if ((value & 0x8000000000000000uL) == 0L)
        {
            return 9;
        }

        return 10;
    }

    //
    // 摘要:
    //     Computes the number of bytes that would be needed to encode a tag.
    public static int ComputeTagSize(int fieldNumber)
    {
        return ComputeRawVarint32Size(WireFormat.MakeTag(fieldNumber, WireFormat.WireType.Varint));
    }

    //
    // 摘要:
    //     Creates a new CodedOutputStream that writes directly to the given byte array.
    //     If more bytes are written than fit in the array, OutOfSpaceException will be
    //     thrown.
    public CodedOutputStream(byte[] flatArray)
        : this(flatArray, 0, flatArray.Length)
    {
    }

    //
    // 摘要:
    //     Creates a new CodedOutputStream that writes directly to the given byte array
    //     slice. If more bytes are written than fit in the array, OutOfSpaceException will
    //     be thrown.
    private CodedOutputStream(byte[] buffer, int offset, int length)
    {
        output = null;
        this.buffer = Preconditions.CheckNotNull(buffer, "buffer");
        state.position = offset;
        state.limit = offset + length;
        WriteBufferHelper.Initialize(this, out state.writeBufferHelper);
        leaveOpen = true;
    }

    private CodedOutputStream(Stream output, byte[] buffer, bool leaveOpen)
    {
        this.output = Preconditions.CheckNotNull(output, "output");
        this.buffer = buffer;
        state.position = 0;
        state.limit = buffer.Length;
        WriteBufferHelper.Initialize(this, out state.writeBufferHelper);
        this.leaveOpen = leaveOpen;
    }

    //
    // 摘要:
    //     Creates a new Google.Protobuf.CodedOutputStream which write to the given stream,
    //     and disposes of that stream when the returned CodedOutputStream is disposed.
    //
    //
    // 参数:
    //   output:
    //     The stream to write to. It will be disposed when the returned CodedOutputStream
    //     is disposed.
    public CodedOutputStream(Stream output)
        : this(output, DefaultBufferSize, leaveOpen: false)
    {
    }

    //
    // 摘要:
    //     Creates a new CodedOutputStream which write to the given stream and uses the
    //     specified buffer size.
    //
    // 参数:
    //   output:
    //     The stream to write to. It will be disposed when the returned CodedOutputStream
    //     is disposed.
    //
    //   bufferSize:
    //     The size of buffer to use internally.
    public CodedOutputStream(Stream output, int bufferSize)
        : this(output, new byte[bufferSize], leaveOpen: false)
    {
    }

    //
    // 摘要:
    //     Creates a new CodedOutputStream which write to the given stream.
    //
    // 参数:
    //   output:
    //     The stream to write to.
    //
    //   leaveOpen:
    //     If true, output is left open when the returned CodedOutputStream is disposed;
    //     if false, the provided stream is disposed as well.
    public CodedOutputStream(Stream output, bool leaveOpen)
        : this(output, DefaultBufferSize, leaveOpen)
    {
    }

    //
    // 摘要:
    //     Creates a new CodedOutputStream which write to the given stream and uses the
    //     specified buffer size.
    //
    // 参数:
    //   output:
    //     The stream to write to.
    //
    //   bufferSize:
    //     The size of buffer to use internally.
    //
    //   leaveOpen:
    //     If true, output is left open when the returned CodedOutputStream is disposed;
    //     if false, the provided stream is disposed as well.
    public CodedOutputStream(Stream output, int bufferSize, bool leaveOpen)
        : this(output, new byte[bufferSize], leaveOpen)
    {
    }

    //
    // 摘要:
    //     Writes a double field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteDouble(double value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteDouble(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a float field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteFloat(float value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteFloat(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a uint64 field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteUInt64(ulong value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteUInt64(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an int64 field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteInt64(long value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteInt64(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an int32 field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteInt32(int value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteInt32(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a fixed64 field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteFixed64(ulong value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteFixed64(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a fixed32 field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteFixed32(uint value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteFixed32(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a bool field value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteBool(bool value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteBool(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a string field value, without a tag, to the stream. The data is length-prefixed.
    //
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteString(string value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteString(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a message, without a tag, to the stream. The data is length-prefixed.
    //
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteMessage(IMessage value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WriteContext.Initialize(ref span, ref state, out var ctx);
        try
        {
            WritingPrimitivesMessages.WriteMessage(ref ctx, value);
        }
        finally
        {
            ctx.CopyStateTo(this);
        }
    }

    //
    // 摘要:
    //     Writes a message, without a tag, to the stream. Only the message data is written,
    //     without a length-delimiter.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteRawMessage(IMessage value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WriteContext.Initialize(ref span, ref state, out var ctx);
        try
        {
            WritingPrimitivesMessages.WriteRawMessage(ref ctx, value);
        }
        finally
        {
            ctx.CopyStateTo(this);
        }
    }

    //
    // 摘要:
    //     Writes a group, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteGroup(IMessage value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WriteContext.Initialize(ref span, ref state, out var ctx);
        try
        {
            WritingPrimitivesMessages.WriteGroup(ref ctx, value);
        }
        finally
        {
            ctx.CopyStateTo(this);
        }
    }

    //
    // 摘要:
    //     Write a byte string, without a tag, to the stream. The data is length-prefixed.
    //
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteBytes(byte[] value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteBytes(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a uint32 value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteUInt32(uint value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteUInt32(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an enum value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteEnum(int value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteEnum(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sfixed32 value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write.
    public void WriteSFixed32(int value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteSFixed32(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sfixed64 value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteSFixed64(long value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteSFixed64(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sint32 value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteSInt32(int value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteSInt32(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sint64 value, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteSInt64(long value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteSInt64(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes a length (in bytes) for length-delimited data.
    //
    // 参数:
    //   length:
    //     Length value, in bytes.
    //
    // 言论：
    //     This method simply writes a rawint, but exists for clarity in calling code.
    public void WriteLength(int length)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteLength(ref span, ref state, length);
    }

    //
    // 摘要:
    //     Encodes and writes a tag.
    //
    // 参数:
    //   fieldNumber:
    //     The number of the field to write the tag for
    //
    //   type:
    //     The wire format type of the tag to write
    public void WriteTag(int fieldNumber, WireFormat.WireType type)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteTag(ref span, ref state, fieldNumber, type);
    }

    //
    // 摘要:
    //     Writes an already-encoded tag.
    //
    // 参数:
    //   tag:
    //     The encoded tag
    public void WriteTag(uint tag)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteTag(ref span, ref state, tag);
    }

    //
    // 摘要:
    //     Writes the given single-byte tag directly to the stream.
    //
    // 参数:
    //   b1:
    //     The encoded tag
    public void WriteRawTag(byte b1)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawTag(ref span, ref state, b1);
    }

    //
    // 摘要:
    //     Writes the given two-byte tag directly to the stream.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawTag(ref span, ref state, b1, b2);
    }

    //
    // 摘要:
    //     Writes the given three-byte tag directly to the stream.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    //
    //   b3:
    //     The third byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2, byte b3)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawTag(ref span, ref state, b1, b2, b3);
    }

    //
    // 摘要:
    //     Writes the given four-byte tag directly to the stream.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    //
    //   b3:
    //     The third byte of the encoded tag
    //
    //   b4:
    //     The fourth byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2, byte b3, byte b4)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawTag(ref span, ref state, b1, b2, b3, b4);
    }

    //
    // 摘要:
    //     Writes the given five-byte tag directly to the stream.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    //
    //   b3:
    //     The third byte of the encoded tag
    //
    //   b4:
    //     The fourth byte of the encoded tag
    //
    //   b5:
    //     The fifth byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2, byte b3, byte b4, byte b5)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawTag(ref span, ref state, b1, b2, b3, b4, b5);
    }

    //
    // 摘要:
    //     Writes a 32 bit value as a varint. The fast route is taken when there's enough
    //     buffer space left to whizz through without checking for each byte; otherwise,
    //     we resort to calling WriteRawByte each time.
    internal void WriteRawVarint32(uint value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawVarint32(ref span, ref state, value);
    }

    internal void WriteRawVarint64(ulong value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawVarint64(ref span, ref state, value);
    }

    internal void WriteRawLittleEndian32(uint value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawLittleEndian32(ref span, ref state, value);
    }

    internal void WriteRawLittleEndian64(ulong value)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawLittleEndian64(ref span, ref state, value);
    }

    //
    // 摘要:
    //     Writes out an array of bytes.
    internal void WriteRawBytes(byte[] value)
    {
        WriteRawBytes(value, 0, value.Length);
    }

    //
    // 摘要:
    //     Writes out part of an array of bytes.
    internal void WriteRawBytes(byte[] value, int offset, int length)
    {
        Span<byte> span = new Span<byte>(buffer);
        WritingPrimitives.WriteRawBytes(ref span, ref state, value, offset, length);
    }

    //
    // 摘要:
    //     Flushes any buffered data and optionally closes the underlying stream, if any.
    //
    //
    // 言论：
    //     By default, any underlying stream is closed by this method. To configure this
    //     behaviour, use a constructor overload with a leaveOpen parameter. If this instance
    //     does not have an underlying stream, this method does nothing.
    //
    //     For the sake of efficiency, calling this method does not prevent future write
    //     calls - but if a later write ends up writing to a stream which has been disposed,
    //     that is likely to fail. It is recommend that you not call any other methods after
    //     this.
    public void Dispose()
    {
        Flush();
        if (!leaveOpen)
        {
            output.Dispose();
        }
    }

    //
    // 摘要:
    //     Flushes any buffered data to the underlying stream (if there is one).
    public void Flush()
    {
        Span<byte> span = new Span<byte>(buffer);
        WriteBufferHelper.Flush(ref span, ref state);
    }

    //
    // 摘要:
    //     Verifies that SpaceLeft returns zero. It's common to create a byte array that
    //     is exactly big enough to hold a message, then write to it with a CodedOutputStream.
    //     Calling CheckNoSpaceLeft after writing verifies that the message was actually
    //     as big as expected, which can help finding bugs.
    public void CheckNoSpaceLeft()
    {
        WriteBufferHelper.CheckNoSpaceLeft(ref state);
    }
}