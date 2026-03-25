
using System;
using System.IO;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Reads and decodes protocol message fields.
//
// 言论：
//     This class is generally used by generated code to read appropriate primitives
//     from the stream. It effectively encapsulates the lowest levels of protocol buffer
//     format.
//
//     Repeated fields and map fields are not handled by this class; use Google.Protobuf.Collections.RepeatedField`1
//     and Google.Protobuf.Collections.MapField`2 to serialize such fields.
[SecuritySafeCritical]
public sealed class CodedInputStream : IDisposable
{
    //
    // 摘要:
    //     Whether to leave the underlying stream open when disposing of this stream. This
    //     is always true when there's no stream.
    private readonly bool leaveOpen;

    //
    // 摘要:
    //     Buffer of data read from the stream or provided at construction time.
    private readonly byte[] buffer;

    //
    // 摘要:
    //     The stream to read further input from, or null if the byte array buffer was provided
    //     directly on construction, with no further data available.
    private readonly Stream input;

    //
    // 摘要:
    //     The parser state is kept separately so that other parse implementations can reuse
    //     the same parsing primitives.
    private ParserInternalState state;

    internal const int DefaultRecursionLimit = 100;

    internal const int DefaultSizeLimit = int.MaxValue;

    internal const int BufferSize = 4096;

    //
    // 摘要:
    //     Returns the current position in the input stream, or the position in the input
    //     buffer
    public long Position
    {
        get
        {
            if (input != null)
            {
                return input.Position - (state.bufferSize + state.bufferSizeAfterLimit - state.bufferPos);
            }

            return state.bufferPos;
        }
    }

    //
    // 摘要:
    //     Returns the last tag read, or 0 if no tags have been read or we've read beyond
    //     the end of the stream.
    internal uint LastTag => state.lastTag;

    //
    // 摘要:
    //     Returns the size limit for this stream.
    //
    // 值:
    //     The size limit.
    //
    // 言论：
    //     This limit is applied when reading from the underlying stream, as a sanity check.
    //     It is not applied when reading from a byte array data source without an underlying
    //     stream. The default value is Int32.MaxValue.
    public int SizeLimit => state.sizeLimit;

    //
    // 摘要:
    //     Returns the recursion limit for this stream. This limit is applied whilst reading
    //     messages, to avoid maliciously-recursive data.
    //
    // 值:
    //     The recursion limit for this stream.
    //
    // 言论：
    //     The default limit is 100.
    public int RecursionLimit => state.recursionLimit;

    internal byte[] InternalBuffer => buffer;

    internal Stream InternalInputStream => input;

    internal ref ParserInternalState InternalState => ref state;

    //
    // 摘要:
    //     Returns whether or not all the data before the limit has been read.
    internal bool ReachedLimit => SegmentedBufferHelper.IsReachedLimit(ref state);

    //
    // 摘要:
    //     Returns true if the stream has reached the end of the input. This is the case
    //     if either the end of the underlying input source has been reached or the stream
    //     has reached a limit created using PushLimit.
    public bool IsAtEnd
    {
        get
        {
            ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
            return SegmentedBufferHelper.IsAtEnd(ref readOnlySpan, ref state);
        }
    }

    //
    // 摘要:
    //     Creates a new CodedInputStream reading data from the given byte array.
    public CodedInputStream(byte[] buffer)
        : this(null, ProtoPreconditions.CheckNotNull(buffer, "buffer"), 0, buffer.Length, leaveOpen: true)
    {
    }

    //
    // 摘要:
    //     Creates a new Google.Protobuf.CodedInputStream that reads from the given byte
    //     array slice.
    public CodedInputStream(byte[] buffer, int offset, int length)
        : this(null, ProtoPreconditions.CheckNotNull(buffer, "buffer"), offset, offset + length, leaveOpen: true)
    {
        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset", "Offset must be within the buffer");
        }

        if (length < 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("length", "Length must be non-negative and within the buffer");
        }
    }

    //
    // 摘要:
    //     Creates a new Google.Protobuf.CodedInputStream reading data from the given stream,
    //     which will be disposed when the returned object is disposed.
    //
    // 参数:
    //   input:
    //     The stream to read from.
    public CodedInputStream(Stream input)
        : this(input, leaveOpen: false)
    {
    }

    //
    // 摘要:
    //     Creates a new Google.Protobuf.CodedInputStream reading data from the given stream.
    //
    //
    // 参数:
    //   input:
    //     The stream to read from.
    //
    //   leaveOpen:
    //     true to leave input open when the returned is disposed; false to dispose of the
    //     given stream when the returned object is disposed.
    public CodedInputStream(Stream input, bool leaveOpen)
        : this(ProtoPreconditions.CheckNotNull(input, "input"), new byte[4096], 0, 0, leaveOpen)
    {
    }

    //
    // 摘要:
    //     Creates a new CodedInputStream reading data from the given stream and buffer,
    //     using the default limits.
    internal CodedInputStream(Stream input, byte[] buffer, int bufferPos, int bufferSize, bool leaveOpen)
    {
        this.input = input;
        this.buffer = buffer;
        state.bufferPos = bufferPos;
        state.bufferSize = bufferSize;
        state.sizeLimit = int.MaxValue;
        state.recursionLimit = 100;
        SegmentedBufferHelper.Initialize(this, out state.segmentedBufferHelper);
        this.leaveOpen = leaveOpen;
        state.currentLimit = int.MaxValue;
    }

    //
    // 摘要:
    //     Creates a new CodedInputStream reading data from the given stream and buffer,
    //     using the specified limits.
    //
    // 言论：
    //     This chains to the version with the default limits instead of vice versa to avoid
    //     having to check that the default values are valid every time.
    internal CodedInputStream(Stream input, byte[] buffer, int bufferPos, int bufferSize, int sizeLimit, int recursionLimit, bool leaveOpen)
        : this(input, buffer, bufferPos, bufferSize, leaveOpen)
    {
        if (sizeLimit <= 0)
        {
            throw new ArgumentOutOfRangeException("sizeLimit", "Size limit must be positive");
        }

        if (recursionLimit <= 0)
        {
            throw new ArgumentOutOfRangeException("recursionLimit!", "Recursion limit must be positive");
        }

        state.sizeLimit = sizeLimit;
        state.recursionLimit = recursionLimit;
    }

    //
    // 摘要:
    //     Creates a Google.Protobuf.CodedInputStream with the specified size and recursion
    //     limits, reading from an input stream.
    //
    // 参数:
    //   input:
    //     The input stream to read from
    //
    //   sizeLimit:
    //     The total limit of data to read from the stream.
    //
    //   recursionLimit:
    //     The maximum recursion depth to allow while reading.
    //
    // 返回结果:
    //     A CodedInputStream reading from input with the specified size and recursion limits.
    //
    //
    // 言论：
    //     This method exists separately from the constructor to reduce the number of constructor
    //     overloads. It is likely to be used considerably less frequently than the constructors,
    //     as the default limits are suitable for most use cases.
    public static CodedInputStream CreateWithLimits(Stream input, int sizeLimit, int recursionLimit)
    {
        return new CodedInputStream(input, new byte[4096], 0, 0, sizeLimit, recursionLimit, leaveOpen: false);
    }

    //
    // 摘要:
    //     Disposes of this instance, potentially closing any underlying stream.
    //
    // 言论：
    //     As there is no flushing to perform here, disposing of a Google.Protobuf.CodedInputStream
    //     which was constructed with the leaveOpen option parameter set to true (or one
    //     which was constructed to read from a byte array) has no effect.
    public void Dispose()
    {
        if (!leaveOpen)
        {
            input.Dispose();
        }
    }

    //
    // 摘要:
    //     Verifies that the last call to ReadTag() returned tag 0 - in other words, we've
    //     reached the end of the stream when we expected to.
    //
    // 异常:
    //   T:Google.Protobuf.InvalidProtocolBufferException:
    //     The tag read was not the one specified
    internal void CheckReadEndOfStreamTag()
    {
        ParsingPrimitivesMessages.CheckReadEndOfStreamTag(ref state);
    }

    //
    // 摘要:
    //     Peeks at the next field tag. This is like calling Google.Protobuf.CodedInputStream.ReadTag,
    //     but the tag is not consumed. (So a subsequent call to Google.Protobuf.CodedInputStream.ReadTag
    //     will return the same value.)
    public uint PeekTag()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.PeekTag(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads a field tag, returning the tag of 0 for "end of stream".
    //
    // 返回结果:
    //     The next field tag, or 0 for end of stream. (0 is never a valid tag.)
    //
    // 言论：
    //     If this method returns 0, it doesn't necessarily mean the end of all the data
    //     in this CodedInputStream; it may be the end of the logical stream for an embedded
    //     message, for example.
    public uint ReadTag()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseTag(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Skips the data for the field with the tag we've just read. This should be called
    //     directly after Google.Protobuf.CodedInputStream.ReadTag, when the caller wishes
    //     to skip an unknown field.
    //
    // 异常:
    //   T:Google.Protobuf.InvalidProtocolBufferException:
    //     The last tag was an end-group tag
    //
    //   T:System.InvalidOperationException:
    //     The last read operation read to the end of the logical stream
    //
    // 言论：
    //     This method throws Google.Protobuf.InvalidProtocolBufferException if the last-read
    //     tag was an end-group tag. If a caller wishes to skip a group, they should skip
    //     the whole group, by calling this method after reading the start-group tag. This
    //     behavior allows callers to call this method on any field they don't understand,
    //     correctly resulting in an error if an end-group tag has not been paired with
    //     an earlier start-group tag.
    public void SkipLastField()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        ParsingPrimitivesMessages.SkipLastField(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Skip a group.
    internal void SkipGroup(uint startGroupTag)
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        ParsingPrimitivesMessages.SkipGroup(ref readOnlySpan, ref state, startGroupTag);
    }

    //
    // 摘要:
    //     Reads a double field from the stream.
    public double ReadDouble()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseDouble(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads a float field from the stream.
    public float ReadFloat()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseFloat(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads a uint64 field from the stream.
    public ulong ReadUInt64()
    {
        return ReadRawVarint64();
    }

    //
    // 摘要:
    //     Reads an int64 field from the stream.
    public long ReadInt64()
    {
        return (long)ReadRawVarint64();
    }

    //
    // 摘要:
    //     Reads an int32 field from the stream.
    public int ReadInt32()
    {
        return (int)ReadRawVarint32();
    }

    //
    // 摘要:
    //     Reads a fixed64 field from the stream.
    public ulong ReadFixed64()
    {
        return ReadRawLittleEndian64();
    }

    //
    // 摘要:
    //     Reads a fixed32 field from the stream.
    public uint ReadFixed32()
    {
        return ReadRawLittleEndian32();
    }

    //
    // 摘要:
    //     Reads a bool field from the stream.
    public bool ReadBool()
    {
        return ReadRawVarint64() != 0;
    }

    //
    // 摘要:
    //     Reads a string field from the stream.
    public string ReadString()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ReadString(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads an embedded message field value from the stream.
    public void ReadMessage(IMessage builder)
    {
        ParseContext.Initialize(buffer.AsSpan(), ref state, out var ctx);
        try
        {
            ParsingPrimitivesMessages.ReadMessage(ref ctx, builder);
        }
        finally
        {
            ctx.CopyStateTo(this);
        }
    }

    //
    // 摘要:
    //     Reads a bytes field value from the stream.
    public byte[] ReadBytes()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ReadBytes(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads a uint32 field value from the stream.
    public uint ReadUInt32()
    {
        return ReadRawVarint32();
    }

    //
    // 摘要:
    //     Reads an enum field value from the stream.
    public int ReadEnum()
    {
        return (int)ReadRawVarint32();
    }

    //
    // 摘要:
    //     Reads an sfixed32 field value from the stream.
    public int ReadSFixed32()
    {
        return (int)ReadRawLittleEndian32();
    }

    //
    // 摘要:
    //     Reads an sfixed64 field value from the stream.
    public long ReadSFixed64()
    {
        return (long)ReadRawLittleEndian64();
    }

    //
    // 摘要:
    //     Reads an sint32 field value from the stream.
    public int ReadSInt32()
    {
        return ParsingPrimitives.DecodeZigZag32(ReadRawVarint32());
    }

    //
    // 摘要:
    //     Reads an sint64 field value from the stream.
    public long ReadSInt64()
    {
        return ParsingPrimitives.DecodeZigZag64(ReadRawVarint64());
    }

    //
    // 摘要:
    //     Reads a length for length-delimited data.
    //
    // 言论：
    //     This is internally just reading a varint, but this method exists to make the
    //     calling code clearer.
    public int ReadLength()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseLength(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Peeks at the next tag in the stream. If it matches tag, the tag is consumed and
    //     the method returns true; otherwise, the stream is left in the original position
    //     and the method returns false.
    public bool MaybeConsumeTag(uint tag)
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.MaybeConsumeTag(ref readOnlySpan, ref state, tag);
    }

    //
    // 摘要:
    //     Reads a raw Varint from the stream. If larger than 32 bits, discard the upper
    //     bits. This method is optimised for the case where we've got lots of data in the
    //     buffer. That means we can check the size just once, then just read directly from
    //     the buffer without constant rechecking of the buffer length.
    internal uint ReadRawVarint32()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseRawVarint32(ref readOnlySpan, ref state);
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
    internal static uint ReadRawVarint32(Stream input)
    {
        return ParsingPrimitives.ReadRawVarint32(input);
    }

    //
    // 摘要:
    //     Reads a raw varint from the stream.
    internal ulong ReadRawVarint64()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseRawVarint64(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads a 32-bit little-endian integer from the stream.
    internal uint ReadRawLittleEndian32()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseRawLittleEndian32(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Reads a 64-bit little-endian integer from the stream.
    internal ulong ReadRawLittleEndian64()
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ParseRawLittleEndian64(ref readOnlySpan, ref state);
    }

    //
    // 摘要:
    //     Sets currentLimit to (current position) + byteLimit. This is called when descending
    //     into a length-delimited embedded message. The previous limit is returned.
    //
    // 返回结果:
    //     The old limit.
    internal int PushLimit(int byteLimit)
    {
        return SegmentedBufferHelper.PushLimit(ref state, byteLimit);
    }

    //
    // 摘要:
    //     Discards the current limit, returning the previous limit.
    internal void PopLimit(int oldLimit)
    {
        SegmentedBufferHelper.PopLimit(ref state, oldLimit);
    }

    //
    // 摘要:
    //     Reads a fixed size of bytes from the input.
    //
    // 异常:
    //   T:Google.Protobuf.InvalidProtocolBufferException:
    //     the end of the stream or the current limit was reached
    internal byte[] ReadRawBytes(int size)
    {
        ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer);
        return ParsingPrimitives.ReadRawBytes(ref readOnlySpan, ref state, size);
    }

    //
    // 摘要:
    //     Reads a top-level message or a nested message after the limits for this message
    //     have been pushed. (parser will proceed until the end of the current limit) NOTE:
    //     this method needs to be public because it's invoked by the generated code - e.g.
    //     msg.MergeFrom(CodedInputStream input) method
    public void ReadRawMessage(IMessage message)
    {
        ParseContext.Initialize(this, out var ctx);
        try
        {
            ParsingPrimitivesMessages.ReadRawMessage(ref ctx, message);
        }
        finally
        {
            ctx.CopyStateTo(this);
        }
    }
}