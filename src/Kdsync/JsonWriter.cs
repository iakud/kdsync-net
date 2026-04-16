using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Kdsync
{
    internal enum JsonTokenType
    {
        None,
        Name,
        Scalar,
        StartObject,
        EndObject,
        StartArray,
        EndArray
    }

    public sealed class JsonWriter : IDisposable
    {
        private static readonly string[] CommonRepresentations;

        private const string Hex = "0123456789abcdef";

        static JsonWriter()
        {
            CommonRepresentations = new string[160]
            {
            "\\u0000", "\\u0001", "\\u0002", "\\u0003", "\\u0004", "\\u0005", "\\u0006", "\\u0007", "\\b", "\\t",
            "\\n", "\\u000b", "\\f", "\\r", "\\u000e", "\\u000f", "\\u0010", "\\u0011", "\\u0012", "\\u0013",
            "\\u0014", "\\u0015", "\\u0016", "\\u0017", "\\u0018", "\\u0019", "\\u001a", "\\u001b", "\\u001c", "\\u001d",
            "\\u001e", "\\u001f", "", "", "\\\"", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "",
            "\\u003c", "", "\\u003e", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "",
            "", "", "\\\\", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "", "", "",
            "", "", "", "", "", "", "", "\\u007f", "\\u0080", "\\u0081",
            "\\u0082", "\\u0083", "\\u0084", "\\u0085", "\\u0086", "\\u0087", "\\u0088", "\\u0089", "\\u008a", "\\u008b",
            "\\u008c", "\\u008d", "\\u008e", "\\u008f", "\\u0090", "\\u0091", "\\u0092", "\\u0093", "\\u0094", "\\u0095",
            "\\u0096", "\\u0097", "\\u0098", "\\u0099", "\\u009a", "\\u009b", "\\u009c", "\\u009d", "\\u009e", "\\u009f"
            };
            for (int i = 0; i < CommonRepresentations.Length; i++)
            {
                if (CommonRepresentations[i].Length == 0)
                {
                    CommonRepresentations[i] = ((char)i).ToString();
                }
            }
        }

        public static void WriteName(JsonWriter writer, bool name) => writer.WriteName(name);
        public static void WriteName(JsonWriter writer, string name) => writer.WriteName(name);
        public static void WriteName(JsonWriter writer, int name) => writer.WriteName(name);
        public static void WriteName(JsonWriter writer, uint name) => writer.WriteName(name);
        public static void WriteName(JsonWriter writer, long name) => writer.WriteName(name);
        public static void WriteName(JsonWriter writer, ulong name) => writer.WriteName(name);

        public static void WriteBoolValue(JsonWriter writer, bool value) => writer.WriteBoolValue(value);
        public static void WriteIntValue(JsonWriter writer, int value) => writer.WriteIntValue(value);
        public static void WriteUIntValue(JsonWriter writer, uint value) => writer.WriteUIntValue(value);
        public static void WriteLongValue(JsonWriter writer, long value) => writer.WriteLongValue(value);
        public static void WriteULongValue(JsonWriter writer, ulong value) => writer.WriteULongValue(value);
        public static void WriteFloatValue(JsonWriter writer, float value) => writer.WriteFloatValue(value);
        public static void WriteDoubleValue(JsonWriter writer, double value) => writer.WriteDoubleValue(value);
        public static void WriteBytesValue(JsonWriter writer, byte[] value) => writer.WriteBase64Value(value);
        public static void WriteStringValue(JsonWriter writer, string value) => writer.WriteStringValue(value);
        public static void WriteTimestampValue(JsonWriter writer, Timestamp value) => writer.WriteTimestampValue(value);
        public static void WriteDurationValue(JsonWriter writer, Duration value) => writer.WriteDurationValue(value);
        public static void WriteEmptyValue(JsonWriter writer, Empty value) => writer.WriteEmptyValue(value);
        public static void WriteEnumValue<T>(JsonWriter writer, T value) where T : Enum => writer.WriteEnumValue(value);
        public static void WriteValue(JsonWriter writer, IMessage value) => writer.WriteValue(value);

        private readonly TextWriter _writer = new StringWriter();
        private readonly bool _indented;

        private int _currentDepth;
        private JsonTokenType _tokenType;

        private int _indentLength = 2;

        public JsonWriter()
        {
        }

        public JsonWriter(bool indented)
        {
            _indented = indented;
        }

        public int CurrentDepth => _currentDepth;

        public override string ToString()
        {
            return _writer.ToString();
        }

        public void Flush()
        {
            _writer.Flush();
        }

        public void Dispose()
        {
            _writer.Flush();
        }

        public void Reset()
        {
            _currentDepth = 0;
            _tokenType = JsonTokenType.None;
        }

        public void WriteStartObject()
        {
            WriteStart('{');
            _tokenType = JsonTokenType.StartObject;
        }

        public void WriteEndObject()
        {
            WriteEnd('}');
            _tokenType = JsonTokenType.EndObject;
        }

        public void WriteStartArray()
        {
            WriteStart('[');
            _tokenType = JsonTokenType.StartArray;
        }

        public void WriteEndArray()
        {
            WriteEnd(']');
            _tokenType = JsonTokenType.EndArray;
        }

        private void WriteStart(char token)
        {
            if (_indented)
            {
                WriteStartIndented(token);
            }
            else
            {
                WriteStartMinimized(token);
            }
        }

        private void WriteStartMinimized(char token)
        {
            if (_tokenType != JsonTokenType.None && _tokenType != JsonTokenType.Name && _tokenType != JsonTokenType.StartObject && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
            _writer.Write(token);
            _currentDepth++;
        }

        private void WriteStartIndented(char token)
        {
            if (_tokenType != JsonTokenType.None && _tokenType != JsonTokenType.Name && _tokenType != JsonTokenType.StartObject && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
            if (_tokenType != JsonTokenType.None && _tokenType != JsonTokenType.Name)
            {
                _writer.Write('\n');
                WriteIndentation(_currentDepth);
            }
            _writer.Write(token);
            _currentDepth++;
        }

        private void WriteEnd(char token)
        {
            if (_indented)
            {
                WriteEndIndented(token);
            }
            else
            {
                WriteEndMinimized(token);
            }
        }

        private void WriteEndMinimized(char token)
        {
            _currentDepth--;
            _writer.Write(token);
        }

        private void WriteEndIndented(char token)
        {
            _currentDepth--;

            if (_tokenType != JsonTokenType.StartObject && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write('\n');
                WriteIndentation(_currentDepth);
            }
            _writer.Write(token);
        }

        public void WriteName(string name)
        {
            if (_indented)
            {
                WriteNameEscapedIndented(name);
            }
            else
            {
                WriteNameEscapedMinimized(name);
            }
        }

        private void WriteName(bool key)
        {
            WriteName(key ? "true" : "false");
        }

        private void WriteName(int key)
        {
            WriteName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WriteName(uint key)
        {
            WriteName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WriteName(long key)
        {
            WriteName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WriteName(ulong key)
        {
            WriteName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WriteNameEscapedMinimized(string name)
        {
            if (_tokenType != JsonTokenType.StartObject)
            {
                _writer.Write(',');
            }
            WriteEscapedString(name);
            _writer.Write(':');
            _tokenType = JsonTokenType.Name;
        }

        private void WriteNameEscapedIndented(string name)
        {
            if (_tokenType != JsonTokenType.StartObject)
            {
                _writer.Write(',');
            }
            _writer.Write('\n');
            WriteIndentation(_currentDepth);
            WriteEscapedString(name);
            _writer.Write(':');
            _writer.Write(' ');
            _tokenType = JsonTokenType.Name;
        }

        public void WriteNull(string name)
        {
            WriteName(name);
            WriteNullValue();
        }

        public void WriteBool(string name, bool value)
        {
            WriteName(name);
            WriteBoolValue(value);
        }

        public void WriteInt(string name, int value)
        {
            WriteName(name);
            WriteIntValue(value);
        }

        public void WriteUInt(string name, uint value)
        {
            WriteName(name);
            WriteUIntValue(value);
        }

        public void WriteLong(string name, long value)
        {
            WriteName(name);
            WriteLongValue(value);
        }

        public void WriteULong(string name, ulong value)
        {
            WriteName(name);
            WriteULongValue(value);
        }

        public void WriteFloat(string name, float value)
        {
            WriteName(name);
            WriteFloatValue(value);
        }

        public void WriteDouble(string name, double value)
        {
            WriteName(name);
            WriteDoubleValue(value);
        }

        public void WriteString(string name, string value)
        {
            WriteName(name);
            WriteStringValue(value);
        }

        public void WriteBase64(string name, byte[] value)
        {
            WriteName(name);
            WriteBase64Value(value);
        }

        public void WriteEnum(string name, Enum value)
        {
            WriteInt(name, Convert.ToInt32(value));
        }

        public void WriteTimestamp(string name, Timestamp value)
        {
            WriteName(name);
            WriteTimestampValue(value);
        }

        public void WriteDuration(string name, Duration value)
        {
            WriteName(name);
            WriteDurationValue(value);
        }

        public void WriteEmpty(string name, Empty value)
        {
            WriteName(name);
            WriteEmptyValue(value);
        }

        public void WriteMessage(string name, IMessage value)
        {
            WriteName(name);
            WriteValue(value);
        }

        public void WriteRepeated<T>(string name, Repeated<T> value)
        {
            WriteName(name);
            value.Write(this);
        }

        public void WriteMap<TKey, TValue>(string name, Map<TKey, TValue> value)
        {
            WriteName(name);
            value.Write(this);
        }

        // Value-only methods
        public void WriteNullValue()
        {
            WriteValueSeparator();
            _writer.Write("null");
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteBoolValue(bool value)
        {
            WriteValueSeparator();
            _writer.Write(value ? "true" : "false");
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteIntValue(int value)
        {
            WriteValueSeparator();
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteUIntValue(uint value)
        {
            WriteValueSeparator();
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteLongValue(long value)
        {
            WriteValueSeparator();
            _writer.Write('"');
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _writer.Write('"');
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteULongValue(ulong value)
        {
            WriteValueSeparator();
            _writer.Write('"');
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _writer.Write('"');
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteFloatValue(float value)
        {
            WriteValueSeparator();
            _writer.Write(FormatFloat(value));
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteDoubleValue(double value)
        {
            WriteValueSeparator();
            _writer.Write(FormatDouble(value));
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteStringValue(string value)
        {
            WriteValueSeparator();
            WriteEscapedString(value);
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteBase64Value(byte[] value)
        {
            WriteValueSeparator();
            _writer.Write('"');
            _writer.Write(Convert.ToBase64String(value));
            _writer.Write('"');
            _tokenType = JsonTokenType.Scalar;
        }

        public void WriteEnumValue(Enum value)
        {
            WriteIntValue(Convert.ToInt32(value));
        }

        public void WriteTimestampValue(Timestamp value)
        {
            WriteStartObject();
            WriteLong("Seconds", value.Seconds);
            WriteInt("Nanos", value.Nanos);
            WriteEndObject();
        }

        public void WriteDurationValue(Duration value)
        {
            WriteStartObject();
            WriteLong("Seconds", value.Seconds);
            WriteInt("Nanos", value.Nanos);
            WriteEndObject();
        }

        public void WriteEmptyValue(Empty value)
        {
            WriteStartObject();
            WriteEndObject();
        }

        public void WriteValue(IMessage value)
        {
            value.Write(this);
        }

        private void WriteValueSeparator()
        {
            if (_indented)
            {
                WriteValueSeparatorIndented();
            }
            else
            {
                WriteValueSeparatorMinimized();
            }
        }

        private void WriteValueSeparatorMinimized()
        {
            if (_tokenType != JsonTokenType.Name && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
        }

        private void WriteValueSeparatorIndented()
        {
            if (_tokenType != JsonTokenType.Name && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
            if (_tokenType != JsonTokenType.Name)
            {
                _writer.Write('\n');
                WriteIndentation(_currentDepth);
            }
        }

        private void WriteIndentation(int depth)
        {
            for (int i = 0; i < depth * _indentLength; i++)
            {
                _writer.Write(' ');
            }
        }

        private static string FormatFloat(float value)
        {
            string text = value.ToString("r", CultureInfo.InvariantCulture);
            return text switch
            {
                "NaN" or "Infinity" or "-Infinity" => "\"" + text + "\"",
                _ => text
            };
        }

        private static string FormatDouble(double value)
        {
            string text = value.ToString("r", CultureInfo.InvariantCulture);
            return text switch
            {
                "NaN" or "Infinity" or "-Infinity" => "\"" + text + "\"",
                _ => text
            };
        }

        private void WriteEscapedString(string value)
        {
            _writer.Write('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < '\u00a0')
                {
                    _writer.Write(CommonRepresentations[(uint)c]);
                    continue;
                }

                if (char.IsHighSurrogate(c))
                {
                    i++;
                    if (i == value.Length || !char.IsLowSurrogate(value[i]))
                    {
                        throw new ArgumentException("String contains low surrogate not followed by high surrogate");
                    }

                    HexEncodeUtf16CodeUnit(_writer, c);
                    HexEncodeUtf16CodeUnit(_writer, value[i]);
                    continue;
                }

                if (char.IsLowSurrogate(c))
                {
                    throw new ArgumentException("String contains high surrogate not preceded by low surrogate");
                }

                switch (c)
                {
                    case (char)173u:
                    case (char)1757u:
                    case (char)1807u:
                    case (char)6068u:
                    case (char)6069u:
                    case (char)65279u:
                    case (char)65529u:
                    case (char)65530u:
                    case (char)65531u:
                        HexEncodeUtf16CodeUnit(_writer, c);
                        continue;
                }

                if ((c >= '\u0600' && c <= '\u0603') || (c >= '\u200b' && c <= '\u200f') || (c >= '\u2028' && c <= '\u202e') || (c >= '\u2060' && c <= '\u2064') || (c >= '\u206a' && c <= '\u206f'))
                {
                    HexEncodeUtf16CodeUnit(_writer, c);
                }
                else
                {
                    _writer.Write(c);
                }
            }
            _writer.Write('"');
        }

        private static void HexEncodeUtf16CodeUnit(TextWriter writer, char c)
        {
            writer.Write("\\u");
            writer.Write("0123456789abcdef"[((int)c >> 12) & 0xF]);
            writer.Write("0123456789abcdef"[((int)c >> 8) & 0xF]);
            writer.Write("0123456789abcdef"[((int)c >> 4) & 0xF]);
            writer.Write("0123456789abcdef"[c & 0xF]);
        }
    }
}