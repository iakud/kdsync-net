namespace Kdsync;

internal delegate TValue ValueReader<out TValue>(ref ParseContext ctx);

public sealed class FieldCodec<T>
{
    internal delegate void InputMerger(ref ParseContext ctx, ref T value);

    internal delegate bool ValuesMerger(ref T value, T other);

    private static readonly T DefaultDefault;

    internal ValueReader<T> ValueReader { get; }

    internal InputMerger ValueMerger { get; }

    internal uint Tag { get; }

    internal uint EndTag { get; }

    internal T DefaultValue { get; }

    static FieldCodec()
    {
        if (typeof(T) == typeof(string))
        {
            DefaultDefault = (T)(object)"";
        }
        else if (typeof(T) == typeof(byte[]))
        {
            DefaultDefault = (T)(object)(new byte[0]);
        }
    }

    internal FieldCodec(ValueReader<T> reader, uint tag, T defaultValue)
        : this(reader, delegate (ref ParseContext ctx, ref T v)
        {
            v = reader(ref ctx);
        }, tag, 0u, defaultValue)
    {
    }

    internal FieldCodec(ValueReader<T> reader, InputMerger inputMerger, uint tag, uint endTag = 0u)
        : this(reader, inputMerger, tag, endTag, DefaultDefault)
    {
    }

    internal FieldCodec(ValueReader<T> reader, InputMerger inputMerger, uint tag, uint endTag, T defaultValue)
    {
        ValueReader = reader;
        ValueMerger = inputMerger;
        Tag = tag;
        EndTag = endTag;
        DefaultValue = defaultValue;
    }

    public T Read(ref ParseContext ctx)
    {
        return ValueReader(ref ctx);
    }
}

public static class FieldCodec
{
    public static FieldCodec<string> ForString(uint tag)
    {
        return ForString(tag, "");
    }

    public static FieldCodec<bool> ForBool(uint tag)
    {
        return ForBool(tag, defaultValue: false);
    }

    public static FieldCodec<int> ForInt32(uint tag)
    {
        return ForInt32(tag, 0);
    }

    public static FieldCodec<int> ForSInt32(uint tag)
    {
        return ForSInt32(tag, 0);
    }

    public static FieldCodec<uint> ForFixed32(uint tag)
    {
        return ForFixed32(tag, 0u);
    }

    public static FieldCodec<int> ForSFixed32(uint tag)
    {
        return ForSFixed32(tag, 0);
    }

    public static FieldCodec<uint> ForUInt32(uint tag)
    {
        return ForUInt32(tag, 0u);
    }

    public static FieldCodec<long> ForInt64(uint tag)
    {
        return ForInt64(tag, 0L);
    }

    public static FieldCodec<long> ForSInt64(uint tag)
    {
        return ForSInt64(tag, 0L);
    }

    public static FieldCodec<ulong> ForFixed64(uint tag)
    {
        return ForFixed64(tag, 0uL);
    }

    public static FieldCodec<long> ForSFixed64(uint tag)
    {
        return ForSFixed64(tag, 0L);
    }

    public static FieldCodec<ulong> ForUInt64(uint tag)
    {
        return ForUInt64(tag, 0uL);
    }

    public static FieldCodec<float> ForFloat(uint tag)
    {
        return ForFloat(tag, 0f);
    }

    public static FieldCodec<double> ForDouble(uint tag)
    {
        return ForDouble(tag, 0.0);
    }

    public static FieldCodec<Timestamp> ForTimestamp(uint tag)
    {
        return ForTimestamp(tag, default);
    }

    public static FieldCodec<Duration> ForDuration(uint tag)
    {
        return ForDuration(tag, default);
    }

    public static FieldCodec<Empty> ForEmpty(uint tag)
    {
        return ForEmpty(tag, default);
    }

    public static FieldCodec<T> ForEnum<T>(uint tag, Func<T, int> toInt32, Func<int, T> fromInt32)
    {
        return ForEnum(tag, toInt32, fromInt32, default(T));
    }

    public static FieldCodec<string> ForString(uint tag, string defaultValue)
    {
        return new FieldCodec<string>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadString();
        }, tag, defaultValue);
    }

    public static FieldCodec<byte[]> ForBytes(uint tag, byte[] defaultValue)
    {
        return new FieldCodec<byte[]>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadBytes();
        }, tag, defaultValue);
    }

    public static FieldCodec<bool> ForBool(uint tag, bool defaultValue)
    {
        return new FieldCodec<bool>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadBool();
        }, tag, defaultValue);
    }

    public static FieldCodec<int> ForInt32(uint tag, int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadInt32();
        }, tag, defaultValue);
    }

    public static FieldCodec<int> ForSInt32(uint tag, int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSInt32();
        }, tag, defaultValue);
    }

    public static FieldCodec<uint> ForFixed32(uint tag, uint defaultValue)
    {
        return new FieldCodec<uint>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFixed32();
        }, tag, defaultValue);
    }

    public static FieldCodec<int> ForSFixed32(uint tag, int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSFixed32();
        }, tag, defaultValue);
    }

    public static FieldCodec<uint> ForUInt32(uint tag, uint defaultValue)
    {
        return new FieldCodec<uint>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadUInt32();
        }, tag, defaultValue);
    }

    public static FieldCodec<long> ForInt64(uint tag, long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadInt64();
        }, tag, defaultValue);
    }

    public static FieldCodec<long> ForSInt64(uint tag, long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSInt64();
        }, tag, defaultValue);
    }

    public static FieldCodec<ulong> ForFixed64(uint tag, ulong defaultValue)
    {
        return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFixed64();
        }, tag, defaultValue);
    }

    public static FieldCodec<long> ForSFixed64(uint tag, long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSFixed64();
        }, tag, defaultValue);
    }

    public static FieldCodec<ulong> ForUInt64(uint tag, ulong defaultValue)
    {
        return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadUInt64();
        }, tag, defaultValue);
    }

    public static FieldCodec<float> ForFloat(uint tag, float defaultValue)
    {
        return new FieldCodec<float>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFloat();
        }, tag, defaultValue);
    }

    public static FieldCodec<double> ForDouble(uint tag, double defaultValue)
    {
        return new FieldCodec<double>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadDouble();
        }, tag, defaultValue);
    }

    public static FieldCodec<T> ForEnum<T>(uint tag, Func<T, int> toInt32, Func<int, T> fromInt32, T defaultValue)
    {
        return new FieldCodec<T>(delegate (ref ParseContext ctx)
        {
            return fromInt32(ctx.ReadEnum());
        }, tag, defaultValue);
    }

    public static FieldCodec<T> ForMessage<T>(uint tag) where T : class, IMessage, new()
    {
        return new FieldCodec<T>(delegate (ref ParseContext ctx)
        {
            // T val = parser.CreateTemplate();
            T val = new T();
            ctx.ReadMessage(val);
            return val;
        }, delegate (ref ParseContext ctx, ref T v)
        {
            if (v == null)
            {
                // v = parser.CreateTemplate();
                v = new T();
            }

            ctx.ReadMessage(v);
        }, tag);
    }

    public static FieldCodec<Timestamp> ForTimestamp(uint tag, Timestamp defaultValue)
    {
        return new FieldCodec<Timestamp>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadTimestamp();
        }, tag, defaultValue);
    }

    public static FieldCodec<Duration> ForDuration(uint tag, Duration defaultValue)
    {
        return new FieldCodec<Duration>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadDuration();
        }, tag, defaultValue);
    }

    public static FieldCodec<Empty> ForEmpty(uint tag, Empty defaultValue)
    {
        return new FieldCodec<Empty>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadEmpty();
        }, tag, defaultValue);
    }
}