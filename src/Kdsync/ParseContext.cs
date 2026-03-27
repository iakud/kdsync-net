using System.Runtime.CompilerServices;
using System.Security;

namespace Kdsync;

[SecuritySafeCritical]
public ref struct ParseContext
{
    internal const int DefaultRecursionLimit = 100;

    internal const int DefaultSizeLimit = int.MaxValue;

    internal ReadOnlySpan<byte> buffer;

    internal ParserInternalState state;

    internal uint LastTag => state.lastTag;

    internal bool ReachedLimit => SegmentedBufferHelper.IsReachedLimit(ref state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(ReadOnlySpan<byte> buffer, out ParseContext ctx)
    {
        ParserInternalState parserInternalState = new ParserInternalState
        {
            recursionLimit = 100,
            currentLimit = buffer.Length,
            bufferSize = buffer.Length
        };
        ctx.buffer = buffer;
        ctx.state = parserInternalState;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ReadOnlySpan<byte> buffer, ref ParserInternalState state, out ParseContext ctx)
    {
        ctx.buffer = buffer;
        ctx.state = state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckReadEndOfStreamTag()
    {
        ParsingPrimitivesMessages.CheckReadEndOfStreamTag(ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadTag()
    {
        return ParsingPrimitives.ParseTag(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipLastField()
    {
        ParsingPrimitivesMessages.SkipLastField(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        return ParsingPrimitives.ParseDouble(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        return ParsingPrimitives.ParseFloat(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        return ParsingPrimitives.ParseRawVarint64(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        return (long)ParsingPrimitives.ParseRawVarint64(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        return (int)ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadFixed64()
    {
        return ParsingPrimitives.ParseRawLittleEndian64(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadFixed32()
    {
        return ParsingPrimitives.ParseRawLittleEndian32(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        return ParsingPrimitives.ParseRawVarint64(ref buffer, ref state) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString()
    {
        return ParsingPrimitives.ReadString(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadMessage(IMessage message)
    {
        ParsingPrimitivesMessages.ReadMessage(ref this, message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadBytes()
    {
        return ParsingPrimitives.ReadBytes(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        return ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadEnum()
    {
        return (int)ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadSFixed32()
    {
        return (int)ParsingPrimitives.ParseRawLittleEndian32(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadSFixed64()
    {
        return (long)ParsingPrimitives.ParseRawLittleEndian64(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadSInt32()
    {
        return ParsingPrimitives.DecodeZigZag32(ParsingPrimitives.ParseRawVarint32(ref buffer, ref state));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadSInt64()
    {
        return ParsingPrimitives.DecodeZigZag64(ParsingPrimitives.ParseRawVarint64(ref buffer, ref state));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Timestamp ReadTimestamp()
    {
        return ParsingPrimitivesMessages.ReadTimestamp(ref this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Duration ReadDuration()
    {
        return ParsingPrimitivesMessages.ReadDuration(ref this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Empty ReadEmpty()
    {
        return ParsingPrimitivesMessages.ReadEmpty(ref this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadLength()
    {
        return (int)ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int PushLimit(int byteLimit)
    {
        return SegmentedBufferHelper.PushLimit(ref state, byteLimit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PopLimit(int oldLimit)
    {
        SegmentedBufferHelper.PopLimit(ref state, oldLimit);
    }
}