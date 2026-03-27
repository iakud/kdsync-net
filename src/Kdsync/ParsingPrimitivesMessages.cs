using System.Security;

namespace Kdsync;

[SecuritySafeCritical]
internal static class ParsingPrimitivesMessages
{
    public static readonly byte[] ZeroLengthMessageStreamData = new byte[1];

    public static void SkipLastField(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
    {
        if (state.lastTag == 0)
        {
            throw new InvalidOperationException("SkipLastField cannot be called at the end of a stream");
        }

        switch (WireFormat.GetTagWireType(state.lastTag))
        {
            case WireFormat.WireType.StartGroup:
                SkipGroup(ref buffer, ref state, state.lastTag);
                break;
            case WireFormat.WireType.EndGroup:
                throw new InvalidException("SkipLastField called on an end-group tag, indicating that the corresponding start-group was missing");
            case WireFormat.WireType.Fixed32:
                ParsingPrimitives.ParseRawLittleEndian32(ref buffer, ref state);
                break;
            case WireFormat.WireType.Fixed64:
                ParsingPrimitives.ParseRawLittleEndian64(ref buffer, ref state);
                break;
            case WireFormat.WireType.LengthDelimited:
                {
                    int size = ParsingPrimitives.ParseLength(ref buffer, ref state);
                    ParsingPrimitives.SkipRawBytes(ref buffer, ref state, size);
                    break;
                }
            case WireFormat.WireType.Varint:
                ParsingPrimitives.ParseRawVarint32(ref buffer, ref state);
                break;
        }
    }

    public static void SkipGroup(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state, uint startGroupTag)
    {
        state.recursionDepth++;
        if (state.recursionDepth >= state.recursionLimit)
        {
            throw InvalidException.RecursionLimitExceeded();
        }

        uint num;
        while (true)
        {
            num = ParsingPrimitives.ParseTag(ref buffer, ref state);
            if (num == 0)
            {
                throw InvalidException.TruncatedMessage();
            }

            if (WireFormat.GetTagWireType(num) == WireFormat.WireType.EndGroup)
            {
                break;
            }

            SkipLastField(ref buffer, ref state);
        }

        int tagFieldNumber = WireFormat.GetTagFieldNumber(startGroupTag);
        int tagFieldNumber2 = WireFormat.GetTagFieldNumber(num);
        if (tagFieldNumber != tagFieldNumber2)
        {
            throw new InvalidException($"Mismatched end-group tag. Started with field {tagFieldNumber}; ended with field {tagFieldNumber2}");
        }

        state.recursionDepth--;
    }

    public static void ReadMessage(ref ParseContext ctx, IMessage message)
    {
        int byteLimit = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
        if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
        {
            throw InvalidException.RecursionLimitExceeded();
        }

        int oldLimit = SegmentedBufferHelper.PushLimit(ref ctx.state, byteLimit);
        ctx.state.recursionDepth++;
        ReadRawMessage(ref ctx, message);
        CheckReadEndOfStreamTag(ref ctx.state);
        if (!SegmentedBufferHelper.IsReachedLimit(ref ctx.state))
        {
            throw InvalidException.TruncatedMessage();
        }

        ctx.state.recursionDepth--;
        SegmentedBufferHelper.PopLimit(ref ctx.state, oldLimit);
    }
    /*
        public static KeyValuePair<TKey, TValue> ReadMapEntry<TKey, TValue>(ref ParseContext ctx, MapField<TKey, TValue>.Codec codec)
        {
            int byteLimit = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
            if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
            {
                throw InvalidProtocolBufferException.RecursionLimitExceeded();
            }

            int oldLimit = SegmentedBufferHelper.PushLimit(ref ctx.state, byteLimit);
            ctx.state.recursionDepth++;
            TKey key = codec.KeyCodec.DefaultValue;
            TValue val = codec.ValueCodec.DefaultValue;
            uint num;
            while ((num = ctx.ReadTag()) != 0)
            {
                if (num == codec.KeyCodec.Tag)
                {
                    key = codec.KeyCodec.Read(ref ctx);
                }
                else if (num == codec.ValueCodec.Tag)
                {
                    val = codec.ValueCodec.Read(ref ctx);
                }
                else
                {
                    SkipLastField(ref ctx.buffer, ref ctx.state);
                }
            }

            if (val == null)
            {
                if (ctx.state.CodedInputStream != null)
                {
                    val = codec.ValueCodec.Read(new CodedInputStream(ZeroLengthMessageStreamData));
                }
                else
                {
                    ParseContext.Initialize(new ReadOnlySequence<byte>(ZeroLengthMessageStreamData), out var ctx2);
                    val = codec.ValueCodec.Read(ref ctx2);
                }
            }

            CheckReadEndOfStreamTag(ref ctx.state);
            if (!SegmentedBufferHelper.IsReachedLimit(ref ctx.state))
            {
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            ctx.state.recursionDepth--;
            SegmentedBufferHelper.PopLimit(ref ctx.state, oldLimit);
            return new KeyValuePair<TKey, TValue>(key, val);
        }
    */

    public static void ReadRawMessage(ref ParseContext ctx, IMessage message)
    {
        message.MergeFrom(ref ctx);
    }

    public static void CheckReadEndOfStreamTag(ref ParserInternalState state)
    {
        if (state.lastTag != 0)
        {
            throw InvalidException.MoreDataAvailable();
        }
    }

    private static void CheckLastTagWas(ref ParserInternalState state, uint expectedTag)
    {
        if (state.lastTag != expectedTag)
        {
            throw InvalidException.InvalidEndTag();
        }
    }

    public static Timestamp ReadTimestamp(ref ParseContext ctx)
    {
        int byteLimit = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
        if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
        {
            throw InvalidException.RecursionLimitExceeded();
        }

        int oldLimit = SegmentedBufferHelper.PushLimit(ref ctx.state, byteLimit);
        ctx.state.recursionDepth++;
        Timestamp val = ReadRawTimestamp(ref ctx);
        CheckReadEndOfStreamTag(ref ctx.state);
        if (!SegmentedBufferHelper.IsReachedLimit(ref ctx.state))
        {
            throw InvalidException.TruncatedMessage();
        }

        ctx.state.recursionDepth--;
        SegmentedBufferHelper.PopLimit(ref ctx.state, oldLimit);
        return val;
    }

    public static Timestamp ReadRawTimestamp(ref ParseContext ctx)
    {
        Timestamp val = new Timestamp();
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                case Timestamp.SecondsFieldNumber:
                    val.Seconds = ctx.ReadInt64();
                    break;
                case Timestamp.NanosFieldNumber:
                    val.Nanos = ctx.ReadInt32();
                    break;
                default:
                    ctx.SkipLastField();
                    break;
            }
        }
        return val;
    }


    public static Duration ReadDuration(ref ParseContext ctx)
    {
        int byteLimit = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
        if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
        {
            throw InvalidException.RecursionLimitExceeded();
        }

        int oldLimit = SegmentedBufferHelper.PushLimit(ref ctx.state, byteLimit);
        ctx.state.recursionDepth++;
        Duration val = ReadRawDuration(ref ctx);
        CheckReadEndOfStreamTag(ref ctx.state);
        if (!SegmentedBufferHelper.IsReachedLimit(ref ctx.state))
        {
            throw InvalidException.TruncatedMessage();
        }

        ctx.state.recursionDepth--;
        SegmentedBufferHelper.PopLimit(ref ctx.state, oldLimit);
        return val;
    }

    public static Duration ReadRawDuration(ref ParseContext ctx)
    {
        Duration val = new Duration();
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                case Duration.SecondsFieldNumber:
                    val.Seconds = ctx.ReadInt64();
                    break;
                case Duration.NanosFieldNumber:
                    val.Nanos = ctx.ReadInt32();
                    break;
                default:
                    ctx.SkipLastField();
                    break;
            }
        }
        return val;
    }

    public static Empty ReadEmpty(ref ParseContext ctx)
    {
        int byteLimit = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
        if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
        {
            throw InvalidException.RecursionLimitExceeded();
        }

        int oldLimit = SegmentedBufferHelper.PushLimit(ref ctx.state, byteLimit);
        ctx.state.recursionDepth++;
        Empty val = ReadRawEmpty(ref ctx);
        CheckReadEndOfStreamTag(ref ctx.state);
        if (!SegmentedBufferHelper.IsReachedLimit(ref ctx.state))
        {
            throw InvalidException.TruncatedMessage();
        }

        ctx.state.recursionDepth--;
        SegmentedBufferHelper.PopLimit(ref ctx.state, oldLimit);
        return val;
    }

    public static Empty ReadRawEmpty(ref ParseContext ctx)
    {
        Empty val = new Empty();
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                default:
                    ctx.SkipLastField();
                    break;
            }
        }
        return val;
    }
}