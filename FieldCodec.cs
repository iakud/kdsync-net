using Google.Protobuf;
using Google.Protobuf.Collections;

namespace Kdsync;

public sealed class FieldCodec<T>
{
    //
    // 摘要:
    //     Merges an input stream into a value
    internal delegate void InputMerger(ref ParseContext ctx, ref T value);

    //
    // 摘要:
    //     Merges a value into a reference to another value, returning a boolean if the
    //     value was set
    internal delegate bool ValuesMerger(ref T value, T other);

    private static readonly EqualityComparer<T> EqualityComparer;

    private static readonly T DefaultDefault;

    private static readonly bool TypeSupportsPacking;

    private readonly int tagSize;

    internal bool PackedRepeatedField { get; }

    //
    // 摘要:
    //     Returns a delegate to write a value (unconditionally) to a coded output stream.
    internal ValueWriter<T> ValueWriter { get; }

    //
    // 摘要:
    //     Returns the size calculator for just a value.
    internal Func<T, int> ValueSizeCalculator { get; }

    //
    // 摘要:
    //     Returns a delegate to read a value from a coded input stream. It is assumed that
    //     the stream is already positioned on the appropriate tag.
    internal ValueReader<T> ValueReader { get; }

    //
    // 摘要:
    //     Returns a delegate to merge a value from a coded input stream. It is assumed
    //     that the stream is already positioned on the appropriate tag
    internal InputMerger ValueMerger { get; }

    //
    // 摘要:
    //     Returns a delegate to merge two values together.
    internal ValuesMerger FieldMerger { get; }

    //
    // 摘要:
    //     Returns the fixed size for an entry, or 0 if sizes vary.
    internal int FixedSize { get; }

    //
    // 摘要:
    //     Gets the tag of the codec.
    //
    // 值:
    //     The tag of the codec.
    internal uint Tag { get; }

    //
    // 摘要:
    //     Gets the end tag of the codec or 0 if there is no end tag
    //
    // 值:
    //     The end tag of the codec.
    internal uint EndTag { get; }

    //
    // 摘要:
    //     Default value for this codec. Usually the same for every instance of the same
    //     type, but for string/ByteString wrapper fields the codec's default value is null,
    //     whereas for other string/ByteString fields it's "" or ByteString.Empty.
    //
    // 值:
    //     The default value of the codec's type.
    internal T DefaultValue { get; }

    static FieldCodec()
    {
        EqualityComparer = ProtobufEqualityComparers.GetEqualityComparer<T>();
        TypeSupportsPacking = default(T) != null;
        if (typeof(T) == typeof(string))
        {
            DefaultDefault = (T)(object)"";
        }
        else if (typeof(T) == typeof(ByteString))
        {
            DefaultDefault = (T)(object)ByteString.Empty;
        }
    }

    internal static bool IsPackedRepeatedField(uint tag)
    {
        if (TypeSupportsPacking)
        {
            return WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited;
        }

        return false;
    }

    internal FieldCodec(ValueReader<T> reader, ValueWriter<T> writer, int fixedSize, uint tag, T defaultValue)
        : this(reader, writer, (Func<T, int>)((T _) => fixedSize), tag, defaultValue)
    {
        FixedSize = fixedSize;
    }

    internal FieldCodec(ValueReader<T> reader, ValueWriter<T> writer, Func<T, int> sizeCalculator, uint tag, T defaultValue)
        : this(reader, writer, (InputMerger)delegate (ref ParseContext ctx, ref T v)
        {
            v = reader(ref ctx);
        }, (ValuesMerger)delegate (ref T v, T v2)
        {
            v = v2;
            return true;
        }, sizeCalculator, tag, 0u, defaultValue)
    {
    }

    internal FieldCodec(ValueReader<T> reader, ValueWriter<T> writer, InputMerger inputMerger, ValuesMerger valuesMerger, Func<T, int> sizeCalculator, uint tag, uint endTag = 0u)
        : this(reader, writer, inputMerger, valuesMerger, sizeCalculator, tag, endTag, DefaultDefault)
    {
    }

    internal FieldCodec(ValueReader<T> reader, ValueWriter<T> writer, InputMerger inputMerger, ValuesMerger valuesMerger, Func<T, int> sizeCalculator, uint tag, uint endTag, T defaultValue)
    {
        ValueReader = reader;
        ValueWriter = writer;
        ValueMerger = inputMerger;
        FieldMerger = valuesMerger;
        ValueSizeCalculator = sizeCalculator;
        FixedSize = 0;
        Tag = tag;
        EndTag = endTag;
        DefaultValue = defaultValue;
        tagSize = CodedOutputStream.ComputeRawVarint32Size(tag);
        if (endTag != 0)
        {
            tagSize += CodedOutputStream.ComputeRawVarint32Size(endTag);
        }

        PackedRepeatedField = IsPackedRepeatedField(tag);
    }

    //
    // 摘要:
    //     Write a tag and the given value, *if* the value is not the default.
    public void WriteTagAndValue(CodedOutputStream output, T value)
    {
        WriteContext.Initialize(output, out var ctx);
        try
        {
            WriteTagAndValue(ref ctx, value);
        }
        finally
        {
            ctx.CopyStateTo(output);
        }
    }

    //
    // 摘要:
    //     Write a tag and the given value, *if* the value is not the default.
    public void WriteTagAndValue(ref WriteContext ctx, T value)
    {
        if (!IsDefault(value))
        {
            ctx.WriteTag(Tag);
            ValueWriter(ref ctx, value);
            if (EndTag != 0)
            {
                ctx.WriteTag(EndTag);
            }
        }
    }

    //
    // 摘要:
    //     Reads a value of the codec type from the given Google.Protobuf.CodedInputStream.
    //
    //
    // 参数:
    //   input:
    //     The input stream to read from.
    //
    // 返回结果:
    //     The value read from the stream.
    public T Read(CodedInputStream input)
    {
        ParseContext.Initialize(input, out var ctx);
        try
        {
            return ValueReader(ref ctx);
        }
        finally
        {
            ctx.CopyStateTo(input);
        }
    }

    //
    // 摘要:
    //     Reads a value of the codec type from the given Google.Protobuf.ParseContext.
    //
    //
    // 参数:
    //   ctx:
    //     The parse context to read from.
    //
    // 返回结果:
    //     The value read.
    public T Read(ref ParseContext ctx)
    {
        return ValueReader(ref ctx);
    }

    //
    // 摘要:
    //     Calculates the size required to write the given value, with a tag, if the value
    //     is not the default.
    public int CalculateSizeWithTag(T value)
    {
        if (!IsDefault(value))
        {
            return ValueSizeCalculator(value) + tagSize;
        }

        return 0;
    }

    //
    // 摘要:
    //     Calculates the size required to write the given value, with a tag, even if the
    //     value is the default.
    internal int CalculateUnconditionalSizeWithTag(T value)
    {
        return ValueSizeCalculator(value) + tagSize;
    }

    private bool IsDefault(T value)
    {
        return EqualityComparer.Equals(value, DefaultValue);
    }
}

//
// 摘要:
//     Factory methods for Google.Protobuf.FieldCodec`1.
public static class FieldCodec
{
    //
    // 摘要:
    //     Retrieves a codec suitable for a string field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<string> ForString(uint tag)
    {
        return ForString(tag, "");
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a bool field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<bool> ForBool(uint tag)
    {
        return ForBool(tag, defaultValue: false);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an int32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<int> ForInt32(uint tag)
    {
        return ForInt32(tag, 0);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sint32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<int> ForSInt32(uint tag)
    {
        return ForSInt32(tag, 0);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a fixed32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<uint> ForFixed32(uint tag)
    {
        return ForFixed32(tag, 0u);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sfixed32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<int> ForSFixed32(uint tag)
    {
        return ForSFixed32(tag, 0);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a uint32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<uint> ForUInt32(uint tag)
    {
        return ForUInt32(tag, 0u);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an int64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<long> ForInt64(uint tag)
    {
        return ForInt64(tag, 0L);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sint64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<long> ForSInt64(uint tag)
    {
        return ForSInt64(tag, 0L);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a fixed64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<ulong> ForFixed64(uint tag)
    {
        return ForFixed64(tag, 0uL);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sfixed64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<long> ForSFixed64(uint tag)
    {
        return ForSFixed64(tag, 0L);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a uint64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<ulong> ForUInt64(uint tag)
    {
        return ForUInt64(tag, 0uL);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a float field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<float> ForFloat(uint tag)
    {
        return ForFloat(tag, 0f);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a double field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<double> ForDouble(uint tag)
    {
        return ForDouble(tag, 0.0);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an enum field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   toInt32:
    //     A conversion function from System.Int32 to the enum type.
    //
    //   fromInt32:
    //     A conversion function from the enum type to System.Int32.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<T> ForEnum<T>(uint tag, Func<T, int> toInt32, Func<int, T> fromInt32)
    {
        return ForEnum(tag, toInt32, fromInt32, default(T));
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a string field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<string> ForString(uint tag, string defaultValue)
    {
        return new FieldCodec<string>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadString();
        }, delegate (ref WriteContext ctx, string value)
        {
            ctx.WriteString(value);
        }, CodedOutputStream.ComputeStringSize, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a bytes field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<byte[]> ForBytes(uint tag, byte[] defaultValue)
    {
        return new FieldCodec<byte[]>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadBytes();
        }, delegate (ref WriteContext ctx, byte[] value)
        {
            ctx.WriteBytes(value);
        }, CodedOutputStream.ComputeBytesSize, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a bool field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<bool> ForBool(uint tag, bool defaultValue)
    {
        return new FieldCodec<bool>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadBool();
        }, delegate (ref WriteContext ctx, bool value)
        {
            ctx.WriteBool(value);
        }, 1, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an int32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<int> ForInt32(uint tag, int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadInt32();
        }, delegate (ref WriteContext output, int value)
        {
            output.WriteInt32(value);
        }, CodedOutputStream.ComputeInt32Size, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sint32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<int> ForSInt32(uint tag, int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSInt32();
        }, delegate (ref WriteContext output, int value)
        {
            output.WriteSInt32(value);
        }, CodedOutputStream.ComputeSInt32Size, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a fixed32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<uint> ForFixed32(uint tag, uint defaultValue)
    {
        return new FieldCodec<uint>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFixed32();
        }, delegate (ref WriteContext output, uint value)
        {
            output.WriteFixed32(value);
        }, 4, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sfixed32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<int> ForSFixed32(uint tag, int defaultValue)
    {
        return new FieldCodec<int>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSFixed32();
        }, delegate (ref WriteContext output, int value)
        {
            output.WriteSFixed32(value);
        }, 4, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a uint32 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<uint> ForUInt32(uint tag, uint defaultValue)
    {
        return new FieldCodec<uint>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadUInt32();
        }, delegate (ref WriteContext output, uint value)
        {
            output.WriteUInt32(value);
        }, CodedOutputStream.ComputeUInt32Size, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an int64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<long> ForInt64(uint tag, long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadInt64();
        }, delegate (ref WriteContext output, long value)
        {
            output.WriteInt64(value);
        }, CodedOutputStream.ComputeInt64Size, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sint64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<long> ForSInt64(uint tag, long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSInt64();
        }, delegate (ref WriteContext output, long value)
        {
            output.WriteSInt64(value);
        }, CodedOutputStream.ComputeSInt64Size, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a fixed64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<ulong> ForFixed64(uint tag, ulong defaultValue)
    {
        return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFixed64();
        }, delegate (ref WriteContext output, ulong value)
        {
            output.WriteFixed64(value);
        }, 8, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an sfixed64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<long> ForSFixed64(uint tag, long defaultValue)
    {
        return new FieldCodec<long>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadSFixed64();
        }, delegate (ref WriteContext output, long value)
        {
            output.WriteSFixed64(value);
        }, 8, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a uint64 field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<ulong> ForUInt64(uint tag, ulong defaultValue)
    {
        return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadUInt64();
        }, delegate (ref WriteContext output, ulong value)
        {
            output.WriteUInt64(value);
        }, CodedOutputStream.ComputeUInt64Size, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a float field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<float> ForFloat(uint tag, float defaultValue)
    {
        return new FieldCodec<float>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadFloat();
        }, delegate (ref WriteContext output, float value)
        {
            output.WriteFloat(value);
        }, 4, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a double field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<double> ForDouble(uint tag, double defaultValue)
    {
        return new FieldCodec<double>(delegate (ref ParseContext ctx)
        {
            return ctx.ReadDouble();
        }, delegate (ref WriteContext output, double value)
        {
            output.WriteDouble(value);
        }, 8, tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for an enum field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   toInt32:
    //     A conversion function from System.Int32 to the enum type.
    //
    //   fromInt32:
    //     A conversion function from the enum type to System.Int32.
    //
    //   defaultValue:
    //     The default value.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<T> ForEnum<T>(uint tag, Func<T, int> toInt32, Func<int, T> fromInt32, T defaultValue)
    {
        return new FieldCodec<T>(delegate (ref ParseContext ctx)
        {
            return fromInt32(ctx.ReadEnum());
        }, delegate (ref WriteContext output, T value)
        {
            output.WriteEnum(toInt32(value));
        }, (T value) => CodedOutputStream.ComputeEnumSize(toInt32(value)), tag, defaultValue);
    }

    //
    // 摘要:
    //     Retrieves a codec suitable for a message field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    //   parser:
    //     A parser to use for the message type.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<T> ForMessage<T>(uint tag) where T : class, IMessage, new()
    {
        return new FieldCodec<T>(delegate (ref ParseContext ctx)
        {
            // T val = parser.CreateTemplate();
            T val = new T();
            ctx.ReadMessage(val);
            return val;
        }, delegate (ref WriteContext output, T value)
        {
            output.WriteMessage(value);
        }, delegate (ref ParseContext ctx, ref T v)
        {
            if (v == null)
            {
                // v = parser.CreateTemplate();
                v = new T();
            }

            ctx.ReadMessage(v);
        }, delegate (ref T v, T v2)
        {
            if (v2 == null)
            {
                return false;
            }

            if (v == null)
            {
                // v = v2.Clone();
            }
            else
            {
                // v.MergeFrom(v2);
            }

            return true;
        }, (T message) => CodedOutputStream.ComputeMessageSize(message), tag);
    }
}