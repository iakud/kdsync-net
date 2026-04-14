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
        PropertyName,
        String,
        Number,
        True,
        False,
        Null,
        StartObject,
        EndObject,
        StartArray,
        EndArray,
        Comment
    }

    public sealed class JsonWriter : IDisposable
    {
        private static readonly string[] CommonRepresentations;

        private static readonly ConcurrentDictionary<Type, Action<JsonWriter, object?>> PropertyNameWriters = new ConcurrentDictionary<Type, Action<JsonWriter, object?>>
        {
            [typeof(bool)] = (w, v) => w.WritePropertyName((bool)v!),
            [typeof(string)] = (w, v) => w.WritePropertyName((string)v!),
            [typeof(int)] = (w, v) => w.WritePropertyName((int)v!),
            [typeof(uint)] = (w, v) => w.WritePropertyName((uint)v!),
            [typeof(long)] = (w, v) => w.WritePropertyName((long)v!),
            [typeof(ulong)] = (w, v) => w.WritePropertyName((ulong)v!),
        };

        private static readonly ConcurrentDictionary<Type, Action<JsonWriter, object?>> ValueWriters = new ConcurrentDictionary<Type, Action<JsonWriter, object?>>
        {
            [typeof(bool)] = (w, v) => w.WriteBooleanValue((bool)v!),
            [typeof(string)] = (w, v) => w.WriteStringValue((string)v!),
            [typeof(byte[])] = (w, v) => w.WriteBase64Value((byte[])v!),
            [typeof(int)] = (w, v) => w.WriteNumberValue((int)v!),
            [typeof(uint)] = (w, v) => w.WriteNumberValue((uint)v!),
            [typeof(long)] = (w, v) => w.WriteNumberValue((long)v!),
            [typeof(ulong)] = (w, v) => w.WriteNumberValue((ulong)v!),
            [typeof(float)] = (w, v) => w.WriteNumberValue((float)v!),
            [typeof(double)] = (w, v) => w.WriteNumberValue((double)v!),
            [typeof(Timestamp)] = (w, v) => w.WriteTimestampValue((Timestamp)v!),
            [typeof(Duration)] = (w, v) => w.WriteDurationValue((Duration)v!),
            [typeof(Empty)] = (w, v) => w.WriteEmptyValue((Empty)v!),
        };

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
            if (_tokenType != JsonTokenType.None && _tokenType != JsonTokenType.PropertyName && _tokenType != JsonTokenType.StartObject && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
            _writer.Write(token);
            _currentDepth++;
        }

        private void WriteStartIndented(char token)
        {
            if (_tokenType != JsonTokenType.None && _tokenType != JsonTokenType.PropertyName && _tokenType != JsonTokenType.StartObject && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
            if (_tokenType != JsonTokenType.None && _tokenType != JsonTokenType.PropertyName)
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

        public void WritePropertyName(string name)
        {
            if (_indented)
            {
                WritePropertyNameEscapedIndented(name);
            }
            else
            {
                WritePropertyNameEscapedMinimized(name);
            }
        }

        private void WritePropertyName(bool key)
        {
            WritePropertyName(key ? "true" : "false");
        }

        private void WritePropertyName(int key)
        {
            WritePropertyName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WritePropertyName(uint key)
        {
            WritePropertyName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WritePropertyName(long key)
        {
            WritePropertyName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WritePropertyName(ulong key)
        {
            WritePropertyName(key.ToString("d", CultureInfo.InvariantCulture));
        }

        private void WritePropertyNameEscapedMinimized(string name)
        {
            if (_tokenType != JsonTokenType.StartObject)
            {
                _writer.Write(',');
            }
            WriteEscapedString(name);
            _writer.Write(':');
            _tokenType = JsonTokenType.PropertyName;
        }

        private void WritePropertyNameEscapedIndented(string name)
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
            _tokenType = JsonTokenType.PropertyName;
        }

        public void WriteNull(string propertyName)
        {
            WritePropertyName(propertyName);
            WriteNullValue();
        }

        public void WriteBoolean(string propertyName, bool value)
        {
            WritePropertyName(propertyName);
            WriteBooleanValue(value);
        }

        public void WriteNumber(string propertyName, int value)
        {
            WritePropertyName(propertyName);
            WriteNumberValue(value);
        }

        public void WriteNumber(string propertyName, uint value)
        {
            WritePropertyName(propertyName);
            WriteNumberValue(value);
        }

        public void WriteNumber(string propertyName, long value)
        {
            WritePropertyName(propertyName);
            WriteNumberValue(value);
        }

        public void WriteNumber(string propertyName, ulong value)
        {
            WritePropertyName(propertyName);
            WriteNumberValue(value);
        }

        public void WriteNumber(string propertyName, float value)
        {
            WritePropertyName(propertyName);
            WriteNumberValue(value);
        }

        public void WriteNumber(string propertyName, double value)
        {
            WritePropertyName(propertyName);
            WriteNumberValue(value);
        }

        public void WriteString(string propertyName, string value)
        {
            WritePropertyName(propertyName);
            WriteStringValue(value);
        }

        public void WriteBase64(string propertyName, byte[] value)
        {
            WritePropertyName(propertyName);
            WriteBase64Value(value);
        }

        public void WriteEnum(string propertyName, Enum value)
        {
            WriteNumber(propertyName, Convert.ToInt32(value));
        }

        public void WriteTimestamp(string propertyName, Timestamp value)
        {
            WritePropertyName(propertyName);
            WriteTimestampValue(value);
        }

        public void WriteDuration(string propertyName, Duration value)
        {
            WritePropertyName(propertyName);
            WriteDurationValue(value);
        }

        public void WriteEmpty(string propertyName, Empty value)
        {
            WritePropertyName(propertyName);
            WriteEmptyValue(value);
        }

        public void WriteMessage(string propertyName, IMessage value)
        {
            WritePropertyName(propertyName);
            WriteMessageValue(value);
        }

        public void WriteRepeated<T>(string propertyName, Repeated<T> value)
        {
            WritePropertyName(propertyName);
            WriteRepeatedValue(value);
        }

        public void WriteMap<TKey, TValue>(string propertyName, Map<TKey, TValue> value)
        {
            WritePropertyName(propertyName);
            WriteMapValue(value);
        }

        // Value-only methods
        public void WriteNullValue()
        {
            WriteValueSeparator();
            _writer.Write("null");
            _tokenType = JsonTokenType.Null;
        }

        public void WriteBooleanValue(bool value)
        {
            WriteValueSeparator();
            _writer.Write(value ? "true" : "false");
            _tokenType = value ? JsonTokenType.True : JsonTokenType.False;
        }

        public void WriteNumberValue(int value)
        {
            WriteValueSeparator();
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _tokenType = JsonTokenType.Number;
        }

        public void WriteNumberValue(uint value)
        {
            WriteValueSeparator();
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _tokenType = JsonTokenType.Number;
        }

        public void WriteNumberValue(long value)
        {
            WriteValueSeparator();
            _writer.Write('"');
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _writer.Write('"');
            _tokenType = JsonTokenType.Number;
        }

        public void WriteNumberValue(ulong value)
        {
            WriteValueSeparator();
            _writer.Write('"');
            _writer.Write(value.ToString("d", CultureInfo.InvariantCulture));
            _writer.Write('"');
            _tokenType = JsonTokenType.Number;
        }

        public void WriteNumberValue(float value)
        {
            WriteValueSeparator();
            _writer.Write(FormatFloat(value));
            _tokenType = JsonTokenType.Number;
        }

        public void WriteNumberValue(double value)
        {
            WriteValueSeparator();
            _writer.Write(FormatDouble(value));
            _tokenType = JsonTokenType.Number;
        }

        public void WriteStringValue(string value)
        {
            WriteValueSeparator();
            WriteEscapedString(value);
            _tokenType = JsonTokenType.String;
        }

        public void WriteBase64Value(byte[] value)
        {
            WriteValueSeparator();
            _writer.Write('"');
            _writer.Write(Convert.ToBase64String(value));
            _writer.Write('"');
            _tokenType = JsonTokenType.String;
        }

        public void WriteEnumValue(Enum value)
        {
            WriteNumberValue(Convert.ToInt32(value));
        }

        public void WriteTimestampValue(Timestamp value)
        {
            WriteStartObject();
            WriteNumber("Seconds", value.Seconds);
            WriteNumber("Nanos", value.Nanos);
            WriteEndObject();
        }

        public void WriteDurationValue(Duration value)
        {
            WriteStartObject();
            WriteNumber("Seconds", value.Seconds);
            WriteNumber("Nanos", value.Nanos);
            WriteEndObject();
        }

        public void WriteEmptyValue(Empty value)
        {
            WriteStartObject();
            WriteEndObject();
        }

        public void WriteMessageValue(IMessage value)
        {
            value.Write(this);
        }

        public void WriteRepeatedValue<T>(Repeated<T> repeated)
        {
            WriteStartArray();
            Type valueType = typeof(T);
            if (ValueWriters.TryGetValue(valueType, out var valueWriter))
            {
                foreach (T value in repeated)
                {
                    valueWriter(this, value);
                }
            }
            else if (valueType.IsEnum)
            {
                foreach (T value in repeated)
                {
                    WriteEnumValue((Enum)(object)value!);
                }
            }
            else if (typeof(IMessage).IsAssignableFrom(valueType))
            {
                foreach (T value in repeated)
                {
                    WriteMessageValue((IMessage)value!);
                }
            }
            else
            {
                foreach (T value in repeated)
                {
                    WriteStringValue(value!.ToString());
                }
            }
            WriteEndArray();
        }

        public void WriteMapValue<TKey, TValue>(Map<TKey, TValue> map)
        {
            WriteStartObject();
            Type keyType = typeof(TKey);
            Type valueType = typeof(TValue);
            List<KeyValuePair<TKey, TValue>> kvps = map.ToList();
            kvps.Sort((KeyValuePair<TKey, TValue> pair1, KeyValuePair<TKey, TValue> pair2) => (keyType == typeof(string)) ? StringComparer.Ordinal.Compare(pair1.Key!.ToString(), pair2.Key!.ToString()) : Comparer<TKey>.Default.Compare(pair1.Key, pair2.Key));
            Action<JsonWriter, object?> propertyNameWriter = PropertyNameWriters.GetValueOrDefault(keyType, (writer, value) => writer.WriteStringValue(value!.ToString()));
            if (ValueWriters.TryGetValue(valueType, out var valueWriter))
            {
                foreach (var kvp in kvps)
                {
                    propertyNameWriter(this, kvp.Key);
                    valueWriter(this, kvp.Value);
                }
            }
            else if (valueType.IsEnum)
            {
                foreach (var kvp in kvps)
                {
                    propertyNameWriter(this, kvp.Key);
                    WriteEnumValue((Enum)(object)kvp.Value!);
                }
            }
            else if (typeof(IMessage).IsAssignableFrom(valueType))
            {
                foreach (var kvp in kvps)
                {
                    propertyNameWriter(this, kvp.Key);
                    WriteMessageValue((IMessage)kvp.Value!);
                }
            }
            else
            {
                foreach (var kvp in kvps)
                {
                    propertyNameWriter(this, kvp.Key);
                    WriteStringValue(kvp.Value!.ToString());
                }
            }
            WriteEndObject();
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
            if (_tokenType != JsonTokenType.PropertyName && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
        }

        private void WriteValueSeparatorIndented()
        {
            if (_tokenType != JsonTokenType.PropertyName && _tokenType != JsonTokenType.StartArray)
            {
                _writer.Write(',');
            }
            if (_tokenType != JsonTokenType.PropertyName)
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