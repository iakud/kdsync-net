using Google.Protobuf;
using Google.Protobuf.Collections;

namespace Kdsync;

internal delegate void ValueWriter<T>(ref CodedOutputStream output, T value);

internal delegate TValue ValueReader<out TValue>(ref CodedInputStream input);

public sealed class FieldCodec<T>
{
    //
    // 摘要:
    //     Merges an input stream into a value
    internal delegate void InputMerger(ref CodedInputStream ctx, ref T value);

    //
    // 摘要:
    //     Merges a value into a reference to another value, returning a boolean if the
    //     value was set
    internal delegate bool ValuesMerger(ref T value, T other);

    private static readonly EqualityComparer<T> EqualityComparer;

    private static readonly T DefaultDefault;

    private static readonly bool TypeSupportsPacking;

    private readonly int tagSize;

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

    internal FieldCodec(ValueReader<T> reader, ValueWriter<T> writer, int fixedSize, uint tag, T defaultValue)
        : this(reader, writer, (Func<T, int>)((T _) => fixedSize), tag, defaultValue)
    {
        FixedSize = fixedSize;
    }

    internal FieldCodec(ValueReader<T> reader, ValueWriter<T> writer, Func<T, int> sizeCalculator, uint tag, T defaultValue)
        : this(reader, writer, (InputMerger)delegate (ref CodedInputStream input, ref T v)
        {
            v = reader(ref input);
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
    //     Retrieves a codec suitable for a bytes field with the given tag.
    //
    // 参数:
    //   tag:
    //     The tag.
    //
    // 返回结果:
    //     A codec for the given tag.
    public static FieldCodec<ByteString> ForBytes(uint tag)
    {
        return ForBytes(tag, ByteString.Empty);
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
        return new FieldCodec<string>(delegate (ref CodedInputStream input)
        {
            return input.ReadString();
        }, delegate (ref CodedOutputStream output, string value)
        {
            output.WriteString(value);
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
    public static FieldCodec<ByteString> ForBytes(uint tag, ByteString defaultValue)
    {
        return new FieldCodec<ByteString>(delegate (ref CodedInputStream input)
        {
            return input.ReadBytes();
        }, delegate (ref CodedOutputStream output, ByteString value)
        {
            output.WriteBytes(value);
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
        return new FieldCodec<bool>(delegate (ref CodedInputStream input)
        {
            return input.ReadBool();
        }, delegate (ref CodedOutputStream output, bool value)
        {
            output.WriteBool(value);
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
        return new FieldCodec<int>(delegate (ref CodedInputStream input)
        {
            return input.ReadInt32();
        }, delegate (ref CodedOutputStream output, int value)
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
        return new FieldCodec<int>(delegate (ref CodedInputStream input)
        {
            return input.ReadSInt32();
        }, delegate (ref CodedOutputStream output, int value)
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
        return new FieldCodec<uint>(delegate (ref CodedInputStream input)
        {
            return input.ReadFixed32();
        }, delegate (ref CodedOutputStream output, uint value)
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
        return new FieldCodec<int>(delegate (ref CodedInputStream input)
        {
            return input.ReadSFixed32();
        }, delegate (ref CodedOutputStream output, int value)
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
        return new FieldCodec<uint>(delegate (ref CodedInputStream input)
        {
            return input.ReadUInt32();
        }, delegate (ref CodedOutputStream output, uint value)
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
        return new FieldCodec<long>(delegate (ref CodedInputStream input)
        {
            return input.ReadInt64();
        }, delegate (ref CodedOutputStream output, long value)
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
        return new FieldCodec<long>(delegate (ref CodedInputStream input)
        {
            return input.ReadSInt64();
        }, delegate (ref CodedOutputStream output, long value)
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
        return new FieldCodec<ulong>(delegate (ref CodedInputStream input)
        {
            return input.ReadFixed64();
        }, delegate (ref CodedOutputStream output, ulong value)
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
        return new FieldCodec<long>(delegate (ref CodedInputStream input)
        {
            return input.ReadSFixed64();
        }, delegate (ref CodedOutputStream output, long value)
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
        return new FieldCodec<ulong>(delegate (ref CodedInputStream input)
        {
            return input.ReadUInt64();
        }, delegate (ref CodedOutputStream output, ulong value)
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
        return new FieldCodec<float>(delegate (ref CodedInputStream input)
        {
            return input.ReadFloat();
        }, delegate (ref CodedOutputStream output, float value)
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
        return new FieldCodec<double>(delegate (ref CodedInputStream input)
        {
            return input.ReadDouble();
        }, delegate (ref CodedOutputStream output, double value)
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
        return new FieldCodec<T>(delegate (ref CodedInputStream input)
        {
            return fromInt32(input.ReadEnum());
        }, delegate (ref CodedOutputStream output, T value)
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
        return new FieldCodec<T>(delegate (ref CodedInputStream input)
        {
            T val = new T();
            val.MergeFrom(input.ReadBytes().ToByteArray());
            return val;
        }, delegate (ref CodedOutputStream output, T value)
        {
            // FIXME: output.WriteMessage(value);
        }, delegate (ref CodedInputStream input, ref T v)
        {
            if (v == null)
            {
                v = new T();
            }

            v.MergeFrom(input.ReadBytes().ToByteArray());
        }, delegate (ref T v, T v2)
        {
            /*
            if (v2 == null)
            {
                return false;
            }

            if (v == null)
            {
                v = v2.Clone();
            }
            else
            {
                v.MergeFrom(v2);
            }
            */

            return true;
        }, (T message) => /*CodedOutputStream.ComputeMessageSize(message)*/ 0, tag);
    }
}