
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     An opaque struct that represents the current parsing state and is passed along
//     as the parsing proceeds. All the public methods are intended to be invoked only
//     by the generated code, users should never invoke them directly.
[SecuritySafeCritical]
public ref struct ParseContext
{
    internal const int DefaultRecursionLimit = 100;

    internal const int DefaultSizeLimit = int.MaxValue;

    internal ReadOnlySpan<byte> buffer;

    internal ParserInternalState state;

    //
    // 摘要:
    //     Returns the last tag read, or 0 if no tags have been read or we've read beyond
    //     the end of the input.
    internal uint LastTag => state.lastTag;

    //
    // 摘要:
    //     Initialize a Google.Protobuf.ParseContext, building all Google.Protobuf.ParserInternalState
    //     from defaults and the given buffer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ReadOnlySpan<byte> buffer, out ParseContext ctx)
    {
        ParserInternalState parserInternalState = new ParserInternalState
        {
            sizeLimit = int.MaxValue,
            recursionLimit = 100,
            currentLimit = int.MaxValue,
            bufferSize = buffer.Length
        };
        ctx.buffer = buffer;
        ctx.state = parserInternalState;
    }

    //
    // 摘要:
    //     Initialize a Google.Protobuf.ParseContext using existing Google.Protobuf.ParserInternalState,
    //     e.g. from Google.Protobuf.CodedInputStream.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ReadOnlySpan<byte> buffer, ref ParserInternalState state, out ParseContext ctx)
    {
        ctx.buffer = buffer;
        ctx.state = state;
    }

    //
    // 摘要:
    //     Creates a ParseContext instance from CodedInputStream. WARNING: internally this
    //     copies the CodedInputStream's state, so after done with the ParseContext, the
    //     CodedInputStream's state needs to be updated.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(CodedInputStream input, out ParseContext ctx)
    {
        ctx.buffer = new ReadOnlySpan<byte>(input.InternalBuffer);
        ctx.state = input.InternalState;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ReadOnlySequence<byte> input, out ParseContext ctx)
    {
        Initialize(input, 100, out ctx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ReadOnlySequence<byte> input, int recursionLimit, out ParseContext ctx)
    {
        ctx.buffer = default(ReadOnlySpan<byte>);
        ctx.state = default(ParserInternalState);
        ctx.state.lastTag = 0u;
        ctx.state.recursionDepth = 0;
        ctx.state.sizeLimit = int.MaxValue;
        ctx.state.recursionLimit = recursionLimit;
        ctx.state.currentLimit = int.MaxValue;
        SegmentedBufferHelper.Initialize(input, out ctx.state.segmentedBufferHelper, out ctx.buffer);
        ctx.state.bufferPos = 0;
        ctx.state.bufferSize = ctx.buffer.Length;
    }

    //
    // 摘要:
    //     Reads a field tag, returning the tag of 0 for "end of input".
    //
    // 返回结果:
    //     The next field tag, or 0 for end of input. (0 is never a valid tag.)
    //
    // 言论：
    //     If this method returns 0, it doesn't necessarily mean the end of all the data
    //     in this CodedInputReader; it may be the end of the logical input for an embedded
    //     message, for example.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadTag()
    {
        return ParsingPrimitives.ParseTag(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a double field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        return ParsingPrimitives.ParseDouble(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a float field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        return ParsingPrimitives.ParseFloat(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a uint64 field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        return ParsingPrimitives.ParseRawVarint64(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an int64 field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        return (long)ParsingPrimitives.ParseRawVarint64(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an int32 field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        return (int)ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a fixed64 field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadFixed64()
    {
        return ParsingPrimitives.ParseRawLittleEndian64(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a fixed32 field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadFixed32()
    {
        return ParsingPrimitives.ParseRawLittleEndian32(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a bool field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        return ParsingPrimitives.ParseRawVarint64(ref buffer, ref state) != 0;
    }

    //
    // 摘要:
    //     Reads a string field from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString()
    {
        return ParsingPrimitives.ReadString(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an embedded message field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadMessage(IMessage message)
    {
        ParsingPrimitivesMessages.ReadMessage(ref this, message);
    }

    //
    // 摘要:
    //     Reads a bytes field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadBytes()
    {
        return ParsingPrimitives.ReadBytes(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads a uint32 field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        return ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an enum field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadEnum()
    {
        return (int)ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an sfixed32 field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadSFixed32()
    {
        return (int)ParsingPrimitives.ParseRawLittleEndian32(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an sfixed64 field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadSFixed64()
    {
        return (long)ParsingPrimitives.ParseRawLittleEndian64(ref buffer, ref state);
    }

    //
    // 摘要:
    //     Reads an sint32 field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadSInt32()
    {
        return ParsingPrimitives.DecodeZigZag32(ParsingPrimitives.ParseRawVarint32(ref buffer, ref state));
    }

    //
    // 摘要:
    //     Reads an sint64 field value from the input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadSInt64()
    {
        return ParsingPrimitives.DecodeZigZag64(ParsingPrimitives.ParseRawVarint64(ref buffer, ref state));
    }

    //
    // 摘要:
    //     Reads a length for length-delimited data.
    //
    // 言论：
    //     This is internally just reading a varint, but this method exists to make the
    //     calling code clearer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadLength()
    {
        return (int)ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    internal void CopyStateTo(CodedInputStream input)
    {
        input.InternalState = state;
    }

    internal void LoadStateFrom(CodedInputStream input)
    {
        state = input.InternalState;
    }
}