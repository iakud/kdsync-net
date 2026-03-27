namespace Kdsync;

internal delegate TValue ValueReader<out TValue>(ref ParseContext ctx);

public sealed class FieldCodec<T>
{
    internal delegate void InputMerger(ref ParseContext ctx, ref T value);

    internal delegate bool ValuesMerger(ref T value, T other);

    private static readonly T DefaultDefault;

    internal ValueReader<T> ValueReader { get; }

    internal InputMerger ValueMerger { get; }

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

    internal FieldCodec(ValueReader<T> reader, T defaultValue)
        : this(reader, delegate (ref ParseContext ctx, ref T v)
        {
            v = reader(ref ctx);
        }, defaultValue)
    {
    }

    internal FieldCodec(ValueReader<T> reader, InputMerger inputMerger)
        : this(reader, inputMerger, DefaultDefault)
    {
    }

    internal FieldCodec(ValueReader<T> reader, InputMerger inputMerger, T defaultValue)
    {
        ValueReader = reader;
        ValueMerger = inputMerger;
        DefaultValue = defaultValue;
    }

    public T Read(ref ParseContext ctx)
    {
        return ValueReader(ref ctx);
    }
}

public static class FieldCodec
{
    public static FieldCodec<string> ForString()
    {
        return ForString("");
    }

    public static FieldCodec<bool> ForBool()
    {
        return ForBool(defaultValue: false);
    }

    public static FieldCodec<int> ForInt32()
    {
        return ForInt32(0);
    }

    public static FieldCodec<int> ForSInt32()
    {
        return ForSInt32(0);
    }

    public static FieldCodec<uint> ForFixed32()
    {
        return ForFixed32(0u);
    }

    public static FieldCodec<int> ForSFixed32()
    {
        return ForSFixed32(0);
    }

    public static FieldCodec<uint> ForUInt32()
    {
        return ForUInt32(0u);
    }

    public static FieldCodec<long> ForInt64()
    {
        return ForInt64(0L);
    }

    public static FieldCodec<long> ForSInt64()
    {
        return ForSInt64(0L);
    }

    public static FieldCodec<ulong> ForFixed64()
    {
        return ForFixed64(0uL);
    }

    public static FieldCodec<long> ForSFixed64()
    {
        return ForSFixed64(0L);
    }

    public static FieldCodec<ulong> ForUInt64()
    {
        return ForUInt64(0uL);
    }

    public static FieldCodec<float> ForFloat()
    {
        return ForFloat(0f);
    }

    public static FieldCodec<double> ForDouble()
    {
        return ForDouble(0.0);
    }

    public static FieldCodec<Timestamp> ForTimestamp()
    {
        return ForTimestamp(default);
    }

    public static FieldCodec<Duration> ForDuration()
    {
        return ForDuration(default);
    }

    public static FieldCodec<Empty> ForEmpty()
    {
        return ForEmpty(default);
    }

    public static FieldCodec<T> ForEnum<T>(Func<T, int> toInt32, Func<int, T> fromInt32)
    {
        return ForEnum(toInt32, fromInt32, default(T));
    }

    public static FieldCodec<string> ForString(string defaultValue)
    {
        return new FieldCodec<string>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadString();
        }, defaultValue);
    }

    public static FieldCodec<byte[]> ForBytes(byte[] defaultValue)
    {
        return new FieldCodec<byte[]>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadBytes();
        }, defaultValue);
    }

    public static FieldCodec<bool> ForBool(bool defaultValue)
    {
        return new FieldCodec<bool>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadBool();
        }, defaultValue);
    }

    public static FieldCodec<int> ForInt32(int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadInt32();
        }, defaultValue);
    }

    public static FieldCodec<int> ForSInt32(int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSInt32();
        }, defaultValue);
    }

    public static FieldCodec<uint> ForFixed32(uint defaultValue)
    {
        return new FieldCodec<uint>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFixed32();
        }, defaultValue);
    }

    public static FieldCodec<int> ForSFixed32(int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSFixed32();
        }, defaultValue);
    }

    public static FieldCodec<uint> ForUInt32(uint defaultValue)
    {
        return new FieldCodec<uint>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadUInt32();
        }, defaultValue);
    }

    public static FieldCodec<long> ForInt64(long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadInt64();
        }, defaultValue);
    }

    public static FieldCodec<long> ForSInt64(long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSInt64();
        }, defaultValue);
    }

    public static FieldCodec<ulong> ForFixed64(ulong defaultValue)
    {
        return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFixed64();
        }, defaultValue);
    }

    public static FieldCodec<long> ForSFixed64(long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSFixed64();
        }, defaultValue);
    }

    public static FieldCodec<ulong> ForUInt64(ulong defaultValue)
    {
        return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadUInt64();
        }, defaultValue);
    }

    public static FieldCodec<float> ForFloat(float defaultValue)
    {
        return new FieldCodec<float>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFloat();
        }, defaultValue);
    }

    public static FieldCodec<double> ForDouble(double defaultValue)
    {
        return new FieldCodec<double>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadDouble();
        }, defaultValue);
    }

    public static FieldCodec<T> ForEnum<T>(Func<T, int> toInt32, Func<int, T> fromInt32, T defaultValue)
    {
        return new FieldCodec<T>(delegate (ref ParseContext ctx)
        {
            return fromInt32(ctx.ReadEnum());
        }, defaultValue);
    }

    public static FieldCodec<T> ForMessage<T>() where T : class, IMessage, new()
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
        });
    }

    public static FieldCodec<Timestamp> ForTimestamp(Timestamp defaultValue)
    {
        return new FieldCodec<Timestamp>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadTimestamp();
        }, defaultValue);
    }

    public static FieldCodec<Duration> ForDuration(Duration defaultValue)
    {
        return new FieldCodec<Duration>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadDuration();
        }, defaultValue);
    }

    public static FieldCodec<Empty> ForEmpty(Empty defaultValue)
    {
        return new FieldCodec<Empty>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadEmpty();
        }, defaultValue);
    }
}