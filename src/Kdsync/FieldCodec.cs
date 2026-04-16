using System;

namespace Kdsync
{
    internal delegate TValue ValueReader<out TValue>(ref ParseContext ctx);

    public sealed class FieldCodec<T>
    {
        internal delegate void InputMerger(ref ParseContext ctx, ref T value);

        internal delegate bool ValuesMerger(ref T value, T other);

        internal delegate void JsonValueWriter(JsonWriter writer, T value);

        private static readonly T DefaultDefault;

        internal ValueReader<T> ValueReader { get; }

        internal InputMerger ValueMerger { get; }

        internal JsonValueWriter JsonWriter { get; }

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

        internal FieldCodec(ValueReader<T> reader, JsonValueWriter jsonWriter, T defaultValue)
            : this(reader, delegate (ref ParseContext ctx, ref T v)
            {
                v = reader(ref ctx);
            }, jsonWriter, defaultValue)
        {
        }

        internal FieldCodec(ValueReader<T> reader, InputMerger inputMerger, JsonValueWriter jsonWriter)
            : this(reader, inputMerger, jsonWriter, DefaultDefault)
        {
        }

        internal FieldCodec(ValueReader<T> reader, InputMerger inputMerger, JsonValueWriter jsonWriter, T defaultValue)
        {
            ValueReader = reader;
            ValueMerger = inputMerger;
            JsonWriter = jsonWriter;
            DefaultValue = defaultValue;
        }

        public T Read(ref ParseContext ctx)
        {
            return ValueReader(ref ctx);
        }
    }

    public static class FieldCodec
    {
        public static FieldCodec<bool> ForBoolKey()
        {
            return new FieldCodec<bool>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadBool();
            }, JsonWriter.WriteName, false);
        }

        public static FieldCodec<int> ForIntKey()
        {
            return new FieldCodec<int>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadInt32();
            }, JsonWriter.WriteName, 0);
        }

        public static FieldCodec<uint> ForUIntKey()
        {
            return new FieldCodec<uint>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadUInt32();
            }, JsonWriter.WriteName, 0u);
        }

        public static FieldCodec<long> ForLongKey()
        {
            return new FieldCodec<long>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadInt64();
            }, JsonWriter.WriteName, 0L);
        }

        public static FieldCodec<ulong> ForULongKey()
        {
            return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadUInt64();
            }, JsonWriter.WriteName, 0uL);
        }

        public static FieldCodec<string> ForStringKey()
        {
            return new FieldCodec<string>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadString();
            }, JsonWriter.WriteName, "");
        }

        // Value Codec

        public static FieldCodec<bool> ForBoolValue()
        {
            return new FieldCodec<bool>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadBool();
            }, JsonWriter.WriteBoolValue, false);
        }

        public static FieldCodec<int> ForIntValue()
        {
            return new FieldCodec<int>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadInt32();
            }, JsonWriter.WriteIntValue, 0);
        }

        public static FieldCodec<uint> ForUIntValue()
        {
            return new FieldCodec<uint>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadUInt32();
            }, JsonWriter.WriteUIntValue, 0u);
        }

        public static FieldCodec<long> ForLongValue()
        {
            return new FieldCodec<long>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadInt64();
            }, JsonWriter.WriteLongValue, 0L);
        }

        public static FieldCodec<ulong> ForULongValue()
        {
            return new FieldCodec<ulong>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadUInt64();
            }, JsonWriter.WriteULongValue, 0uL);
        }

        public static FieldCodec<float> ForFloatValue()
        {
            return new FieldCodec<float>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadFloat();
            }, JsonWriter.WriteFloatValue, 0f);
        }

        public static FieldCodec<double> ForDoubleValue()
        {
            return new FieldCodec<double>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadDouble();
            }, JsonWriter.WriteDoubleValue, 0.0);
        }

        public static FieldCodec<byte[]> ForBytesValue()
        {
            return new FieldCodec<byte[]>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadBytes();
            }, JsonWriter.WriteBytesValue, Array.Empty<byte>());
        }

        public static FieldCodec<string> ForStringValue()
        {
            return new FieldCodec<string>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadString();
            }, JsonWriter.WriteStringValue, "");
        }

        public static FieldCodec<Timestamp> ForTimestampValue()
        {
            return new FieldCodec<Timestamp>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadTimestamp();
            }, JsonWriter.WriteTimestampValue, default);
        }

        public static FieldCodec<Duration> ForDurationValue()
        {
            return new FieldCodec<Duration>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadDuration();
            }, JsonWriter.WriteDurationValue, default);
        }

        public static FieldCodec<Empty> ForEmptyValue()
        {
            return new FieldCodec<Empty>(delegate (ref ParseContext ctx)
            {
                return ctx.ReadEmpty();
            }, JsonWriter.WriteEmptyValue, default);
        }

        public static FieldCodec<T> ForEnum<T>() where T : Enum
        {
            return new FieldCodec<T>(delegate (ref ParseContext ctx)
            {
                return (T)(object)ctx.ReadEnum();
            }, JsonWriter.WriteEnumValue, default);
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
            }, JsonWriter.WriteValue);
        }
    }
}