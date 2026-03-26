using System.Collections;
using System.Globalization;

namespace Kdsync;

public static class JsonFormatter
{
    private static readonly string[] CommonRepresentations;

    private const string Hex = "0123456789abcdef";

    static JsonFormatter()
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

    public static string Format(IMessage message)
    {
        return Format(message, 0);
    }

    public static string Format(IMessage message, int indentationLevel)
    {
        StringWriter stringWriter = new StringWriter();
        Format(message, stringWriter, indentationLevel);
        return stringWriter.ToString();
    }

    public static void Format(IMessage message, TextWriter writer)
    {
        Format(message, writer, 0);
    }

    public static void Format(IMessage message, TextWriter writer, int indentationLevel)
    {
        Preconditions.CheckNotNull(message, "message");
        Preconditions.CheckNotNull(writer, "writer");
        WriteMessage(writer, message, indentationLevel);
    }

    private static void WriteMessage(TextWriter writer, IMessage message, int indentationLevel)
    {
        if (message == null)
        {
            WriteNull(writer);
            return;
        }
        WriteBracketOpen(writer, '{');
        bool hasFields = WriteMessageFields(writer, message, indentationLevel + 1);
        WriteBracketClose(writer, '}', hasFields, indentationLevel);
    }

    public static bool WriteMessageFields(TextWriter writer, IMessage message, int indentationLevel)
    {
        bool written = false;
        var fields = message.GetFields();

        foreach (var field in fields)
        {
            MaybeWriteValueSeparator(writer, !written);
            MaybeWriteValueWhitespace(writer, indentationLevel);
            WriteString(writer, field.Key);
            writer.Write(": ");
            WriteValue(writer, field.Value, indentationLevel);
            written = true;
        }
        return written;
    }

    private static void MaybeWriteValueSeparator(TextWriter writer, bool first)
    {
        if (!first)
        {
            writer.Write(",");
        }
    }

    private static void WriteNull(TextWriter writer)
    {
        writer.Write("null");
    }

    public static void WriteValue(TextWriter writer, object value)
    {
        WriteValue(writer, value, 0);
    }

    public static void WriteValue(TextWriter writer, object value, int indentationLevel)
    {
        if (value == null)
        {
            WriteNull(writer);
        }
        else if (value is bool flag)
        {
            writer.Write(flag ? "true" : "false");
        }
        else if (value is byte[] bytes)
        {
            writer.Write('"');
            writer.Write(Convert.ToBase64String(bytes));
            writer.Write('"');
        }
        else if (value is string text)
        {
            WriteString(writer, text);
        }
        else if (value is IDictionary dictionary)
        {
            WriteDictionary(writer, dictionary, indentationLevel);
        }
        else if (value is IList list)
        {
            WriteList(writer, list, indentationLevel);
        }
        else if (value is int || value is uint)
        {
            IFormattable formattable = (IFormattable)value;
            writer.Write(formattable.ToString("d", CultureInfo.InvariantCulture));
        }
        else if (value is long || value is ulong)
        {
            writer.Write('"');
            IFormattable formattable2 = (IFormattable)value;
            writer.Write(formattable2.ToString("d", CultureInfo.InvariantCulture));
            writer.Write('"');
        }
        else if (value is System.Enum)
        {
            WriteValue(writer, (int)value);
        }
        else if (value is float || value is double)
        {
            string text2 = ((IFormattable)value).ToString("r", CultureInfo.InvariantCulture);
            switch (text2)
            {
                case "NaN":
                case "Infinity":
                case "-Infinity":
                    writer.Write('"');
                    writer.Write(text2);
                    writer.Write('"');
                    break;
                default:
                    writer.Write(text2);
                    break;
            }
        }
        else if (value is Timestamp || value is Duration || value is Empty)
        {
            writer.Write(value);
        }
        else
        {
            if (!(value is IMessage message))
            {
                throw new ArgumentException("Unable to format value of type " + value.GetType());
            }

            Format(message, writer, indentationLevel);
        }
    }

    internal static void WriteList(TextWriter writer, IList list, int indentationLevel = 0)
    {
        WriteBracketOpen(writer, '[');
        bool flag = true;
        foreach (object item in list)
        {
            MaybeWriteValueSeparator(writer, flag);
            MaybeWriteValueWhitespace(writer, indentationLevel + 1);
            WriteValue(writer, item, indentationLevel + 1);
            flag = false;
        }

        WriteBracketClose(writer, ']', !flag, indentationLevel);
    }

    internal static void WriteDictionary(TextWriter writer, IDictionary dictionary, int indentationLevel = 0)
    {
        WriteBracketOpen(writer, '{');
        bool flag = true;

        foreach (DictionaryEntry item in SortDictionary(dictionary))
        {
            string text2;
            if (item.Key is string text)
            {
                text2 = text;
            }
            else
            {
                object key = item.Key;
                if (key is bool)
                {
                    text2 = ((bool)key) ? "true" : "false";
                }
                else
                {
                    if (!(item.Key is int) && !(item.Key is uint) && !(item.Key is long) && !(item.Key is ulong))
                    {
                        if (item.Key == null)
                        {
                            throw new ArgumentException("Dictionary has entry with null key");
                        }

                        throw new ArgumentException("Unhandled dictionary key type: " + item.Key.GetType());
                    }

                    text2 = ((IFormattable)item.Key).ToString("d", CultureInfo.InvariantCulture);
                }
            }

            MaybeWriteValueSeparator(writer, flag);
            MaybeWriteValueWhitespace(writer, indentationLevel + 1);
            WriteString(writer, text2);
            writer.Write(": ");
            WriteValue(writer, item.Value, indentationLevel + 1);
            flag = false;
        }

        WriteBracketClose(writer, '}', !flag, indentationLevel);
    }


    private static DictionaryEntry[] SortDictionary(IDictionary dictionary)
    {
        var entries = new DictionaryEntry[dictionary.Count];
        int i = 0;
        foreach (DictionaryEntry item in dictionary)
        {
            entries[i++] = item;
        }

        Array.Sort(entries, (a, b) => (a.GetType() == typeof(string)) ? StringComparer.Ordinal.Compare(a.Key.ToString(), b.Key.ToString()) : Comparer.Default.Compare(a.Key, b.Key));
        return entries;
    }

    internal static void WriteString(TextWriter writer, string text)
    {
        writer.Write('"');
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c < '\u00a0')
            {
                writer.Write(CommonRepresentations[(uint)c]);
                continue;
            }

            if (char.IsHighSurrogate(c))
            {
                i++;
                if (i == text.Length || !char.IsLowSurrogate(text[i]))
                {
                    throw new ArgumentException("String contains low surrogate not followed by high surrogate");
                }

                HexEncodeUtf16CodeUnit(writer, c);
                HexEncodeUtf16CodeUnit(writer, text[i]);
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
                    HexEncodeUtf16CodeUnit(writer, c);
                    continue;
            }

            if ((c >= '\u0600' && c <= '\u0603') || (c >= '\u200b' && c <= '\u200f') || (c >= '\u2028' && c <= '\u202e') || (c >= '\u2060' && c <= '\u2064') || (c >= '\u206a' && c <= '\u206f'))
            {
                HexEncodeUtf16CodeUnit(writer, c);
            }
            else
            {
                writer.Write(c);
            }
        }

        writer.Write('"');
    }

    private static void HexEncodeUtf16CodeUnit(TextWriter writer, char c)
    {
        writer.Write("\\u");
        writer.Write("0123456789abcdef"[((int)c >> 12) & 0xF]);
        writer.Write("0123456789abcdef"[((int)c >> 8) & 0xF]);
        writer.Write("0123456789abcdef"[((int)c >> 4) & 0xF]);
        writer.Write("0123456789abcdef"[c & 0xF]);
    }

    private static void WriteBracketOpen(TextWriter writer, char openChar)
    {
        writer.Write(openChar);
    }

    private static void WriteBracketClose(TextWriter writer, char closeChar, bool hasFields, int indentationLevel)
    {
        if (hasFields)
        {
            writer.WriteLine();
            WriteIndentation(writer, indentationLevel);
        }

        writer.Write(closeChar);
    }

    private static void MaybeWriteValueWhitespace(TextWriter writer, int indentationLevel)
    {
        writer.WriteLine();
        WriteIndentation(writer, indentationLevel);
    }

    private static void WriteIndentation(TextWriter writer, int indentationLevel)
    {
        for (int i = 0; i < indentationLevel; i++)
        {
            writer.Write("  ");
        }
    }
}