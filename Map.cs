using System.Buffers;
using System.Collections;
using System.Security;
using Google.Protobuf.Collections;

namespace Kdsync;

public sealed class Map<TKey, TValue> : IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable, IEquatable<Map<TKey, TValue>>, IDictionary, ICollection, IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>
{
    private sealed class DictionaryEnumerator : IDictionaryEnumerator, IEnumerator
    {
        private readonly IEnumerator<KeyValuePair<TKey, TValue>> enumerator;

        public object Current => Entry;

        public DictionaryEntry Entry => new DictionaryEntry(Key, Value);

        public object Key => enumerator.Current.Key;

        public object Value => enumerator.Current.Value;

        internal DictionaryEnumerator(IEnumerator<KeyValuePair<TKey, TValue>> enumerator)
        {
            this.enumerator = enumerator;
        }

        public bool MoveNext()
        {
            return enumerator.MoveNext();
        }

        public void Reset()
        {
            enumerator.Reset();
        }
    }

    public sealed class Codec
    {
        private readonly FieldCodec<TKey> keyCodec;

        private readonly FieldCodec<TValue> valueCodec;

        private readonly uint mapTag;

        internal FieldCodec<TKey> KeyCodec => keyCodec;

        internal FieldCodec<TValue> ValueCodec => valueCodec;

        internal uint MapTag => mapTag;

        public Codec(FieldCodec<TKey> keyCodec, FieldCodec<TValue> valueCodec, uint mapTag)
        {
            this.keyCodec = keyCodec;
            this.valueCodec = valueCodec;
            this.mapTag = mapTag;
        }
    }

    private class MapView<T> : ICollection<T>, IEnumerable<T>, IEnumerable, ICollection
    {
        private readonly Map<TKey, TValue> parent;

        private readonly Func<KeyValuePair<TKey, TValue>, T> projection;

        private readonly Func<T, bool> containsCheck;

        public int Count => parent.Count;

        public bool IsReadOnly => true;

        public bool IsSynchronized => false;

        public object SyncRoot => parent;

        internal MapView(Map<TKey, TValue> parent, Func<KeyValuePair<TKey, TValue>, T> projection, Func<T, bool> containsCheck)
        {
            this.parent = parent;
            this.projection = projection;
            this.containsCheck = containsCheck;
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return containsCheck(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException("arrayIndex");
            }

            if (arrayIndex + Count > array.Length)
            {
                throw new ArgumentException("Not enough space in the array", "array");
            }

            using IEnumerator<T> enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                T current = enumerator.Current;
                array[arrayIndex++] = current;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return parent.list.Select(projection).GetEnumerator();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            if (index + Count > array.Length)
            {
                throw new ArgumentException("Not enough space in the array", "array");
            }

            using IEnumerator<T> enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                T current = enumerator.Current;
                array.SetValue(current, index++);
            }
        }
    }

    public class ChangedEvent
    {
        private bool _clear;
        public bool Clear => _clear;

        private ICollection<TKey> _deletes;
        public ICollection<TKey> Deletes => _deletes;

        private ICollection<TKey> _updates;
        public ICollection<TKey> Updates => _updates;

        public ChangedEvent(bool clear, ICollection<TKey> deletes, ICollection<TKey> updates)
        {
            _clear = clear;
            _deletes = deletes;
            _updates = updates;
        }
    }

    public const int ClearFieldNumber = 1;

    public const int DeletesFieldNumber = 2;

    public const int EntriesFieldNumber = 3;

    private static readonly EqualityComparer<TValue> ValueEqualityComparer = ProtobufEqualityComparers.GetEqualityComparer<TValue>();

    private static readonly EqualityComparer<TKey> KeyEqualityComparer = ProtobufEqualityComparers.GetEqualityComparer<TKey>();

    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(KeyEqualityComparer);

    private readonly LinkedList<KeyValuePair<TKey, TValue>> list = new LinkedList<KeyValuePair<TKey, TValue>>();

    private bool _clear = false;

    private readonly HashSet<TKey> _deletes = new HashSet<TKey>(KeyEqualityComparer);

    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _updates = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(KeyEqualityComparer);

    public event Action<Map<TKey, TValue>, ChangedEvent>? OnChanged;

    public TValue this[TKey key]
    {
        get
        {
            ProtoPreconditions.CheckNotNullUnconstrained(key, "key");
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException();
        }
        set
        {
            ProtoPreconditions.CheckNotNullUnconstrained(key, "key");
            if (value == null)
            {
                ProtoPreconditions.CheckNotNullUnconstrained(value, "value");
            }

            KeyValuePair<TKey, TValue> value2 = new KeyValuePair<TKey, TValue>(key, value);
            if (map.TryGetValue(key, out var value3))
            {
                value3.Value = value2;
                _updates[key] = value3;
                _deletes.Remove(key);
                return;
            }

            value3 = list.AddLast(value2);
            map[key] = value3;
            _updates[key] = value3;
            _deletes.Remove(key);
        }
    }

    public ICollection<TKey> Keys => new MapView<TKey>(this, (KeyValuePair<TKey, TValue> pair) => pair.Key, ContainsKey);

    public ICollection<TValue> Values => new MapView<TValue>(this, (KeyValuePair<TKey, TValue> pair) => pair.Value, ContainsValue);

    public int Count => list.Count;

    public bool IsReadOnly => false;

    bool IDictionary.IsFixedSize => false;

    ICollection IDictionary.Keys => (ICollection)Keys;

    ICollection IDictionary.Values => (ICollection)Values;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    object IDictionary.this[object key]
    {
        get
        {
            ProtoPreconditions.CheckNotNull(key, "key");
            if (key is TKey key2)
            {
                TryGetValue(key2, out var value);
                return value;
            }

            return null;
        }
        set
        {
            this[(TKey)key] = (TValue)value;
        }
    }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    public void Add(TKey key, TValue value)
    {
        if (ContainsKey(key))
        {
            throw new ArgumentException("Key already exists in map", "key");
        }

        this[key] = value;
    }

    public bool ContainsKey(TKey key)
    {
        ProtoPreconditions.CheckNotNullUnconstrained(key, "key");
        return map.ContainsKey(key);
    }

    private bool ContainsValue(TValue value)
    {
        return list.Any((KeyValuePair<TKey, TValue> pair) => ValueEqualityComparer.Equals(pair.Value, value));
    }

    public bool Remove(TKey key)
    {
        ProtoPreconditions.CheckNotNullUnconstrained(key, "key");
        if (map.TryGetValue(key, out var value))
        {
            map.Remove(key);
            value.List.Remove(value);
            _updates.Remove(key);
            _deletes.Add(key);
            return true;
        }

        return false;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (map.TryGetValue(key, out var value2))
        {
            value = value2.Value.Value;
            return true;
        }

        value = default(TValue);
        return false;
    }

    public void Add(IDictionary<TKey, TValue> entries)
    {
        ProtoPreconditions.CheckNotNull(entries, "entries");
        foreach (KeyValuePair<TKey, TValue> entry in entries)
        {
            Add(entry.Key, entry.Value);
        }
    }

    public void MergeFrom(IDictionary<TKey, TValue> entries)
    {
        ProtoPreconditions.CheckNotNull(entries, "entries");
        foreach (KeyValuePair<TKey, TValue> entry in entries)
        {
            this[entry.Key] = entry.Value;
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        list.Clear();
        map.Clear();
        _updates.Clear();
        _deletes.Clear();
        _clear = true;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
    {
        if (TryGetValue(item.Key, out var value))
        {
            return ValueEqualityComparer.Equals(item.Value, value);
        }

        return false;
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        list.CopyTo(array, arrayIndex);
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        if (item.Key == null)
        {
            throw new ArgumentException("Key is null", "item");
        }

        if (map.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(item.Value, value.Value.Value))
        {
            map.Remove(item.Key);
            value.List.Remove(value);
            return true;
        }

        return false;
    }

    public override bool Equals(object other)
    {
        return Equals(other as Map<TKey, TValue>);
    }

    public override int GetHashCode()
    {
        EqualityComparer<TKey> keyEqualityComparer = KeyEqualityComparer;
        EqualityComparer<TValue> valueEqualityComparer = ValueEqualityComparer;
        int num = 0;
        foreach (KeyValuePair<TKey, TValue> item in list)
        {
            num ^= keyEqualityComparer.GetHashCode(item.Key) * 31 + valueEqualityComparer.GetHashCode(item.Value);
        }

        return num;
    }

    public bool Equals(Map<TKey, TValue> other)
    {
        if (other == null)
        {
            return false;
        }

        if (other == this)
        {
            return true;
        }

        if (other.Count != Count)
        {
            return false;
        }

        EqualityComparer<TValue> valueEqualityComparer = ValueEqualityComparer;
        using (IEnumerator<KeyValuePair<TKey, TValue>> enumerator = GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                KeyValuePair<TKey, TValue> current = enumerator.Current;
                if (!other.TryGetValue(current.Key, out var value))
                {
                    return false;
                }

                if (!valueEqualityComparer.Equals(value, current.Value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void AddEntriesFrom(ref ParseContext ctx, Codec codec)
    {
        int byteLimit = ctx.ReadLength();
        if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
        {
            throw InvalidException.RecursionLimitExceeded();
        }

        int oldLimit = ctx.PushLimit(byteLimit);
        ctx.state.recursionDepth++;
        MergeFrom(ref ctx, codec);
        ctx.CheckReadEndOfStreamTag();
        if (!ctx.ReachedLimit)
        {
            throw InvalidException.TruncatedMessage();
        }

        ctx.state.recursionDepth--;
        ctx.PopLimit(oldLimit);
    }

    [SecuritySafeCritical]
    private void MergeFrom(ref ParseContext ctx, Codec codec)
    {
        var clear = false;
        TKey[] deletes = new TKey[0];
        ParserInternalState[] entries = new ParserInternalState[0];
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                case ClearFieldNumber:
                    clear = ctx.ReadBool();
                    break;
                case DeletesFieldNumber:

                    int byteLimit1 = ctx.ReadLength();
                    int oldLimit1 = ctx.PushLimit(byteLimit1);
                    ctx.state.recursionDepth++;
                    while (!ctx.ReachedLimit)
                    {
                        deletes = deletes.Append(codec.KeyCodec.ValueReader(ref ctx)).ToArray();
                    }
                    ctx.state.recursionDepth--;
                    ctx.PopLimit(oldLimit1);
                    break;
                case EntriesFieldNumber:
                    int byteLimit2 = ctx.ReadLength();

                    int oldLimit2 = ctx.PushLimit(byteLimit2);
                    ctx.state.recursionDepth++;
                    entries = entries.Append(ctx.state).ToArray();
                    // FIXME:
                    ParsingPrimitives.SkipRawBytes(ref ctx.buffer, ref ctx.state, byteLimit2);
                    ctx.state.recursionDepth--;
                    ctx.PopLimit(oldLimit2);
                    break;
                default:
                    ctx.SkipLastField();
                    break;
            }
        }
        if (clear)
        {
            Clear();
        }
        foreach (var key in deletes)
        {
            Remove(key);
        }

        foreach (ParserInternalState entry in entries)
        {
            ParserInternalState state = entry;
            ParseContext.Initialize(ctx.buffer, ref state, out var entryCtx);
            AddEntryFrom(ref entryCtx, codec);
        }
    }

    private void AddEntryFrom(ref ParseContext ctx, Codec codec)
    {
        TKey key = codec.KeyCodec.DefaultValue;
        // TValue val = codec.ValueCodec.DefaultValue;

        ParseContext.Initialize(new ReadOnlySequence<byte>(new byte[1]), out var valCtx);
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            int num = WireFormat.GetTagFieldNumber(tag);
            if (num == codec.KeyCodec.Tag)
            {
                key = codec.KeyCodec.Read(ref ctx);
            }
            else if (num == codec.ValueCodec.Tag)
            {
                valCtx.buffer = ctx.buffer;
                valCtx.state = ctx.state;
                ctx.SkipLastField();
            }
            else
            {
                ctx.SkipLastField();
            }
        }

        ctx.CheckReadEndOfStreamTag();
        if (!ctx.ReachedLimit)
        {
            throw InvalidException.TruncatedMessage();
        }
        if (TryGetValue(key, out var value))
        {
            if (typeof(TValue) is IMessage)
            {
                codec.ValueCodec.ValueMerger(ref valCtx, ref value);
            }
            else
            {
                this[key] = codec.ValueCodec.Read(ref valCtx);
            }
        }
        else
        {
            this[key] = codec.ValueCodec.Read(ref valCtx);
        }
    }

    public string ToString(string indent)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[\n");
        var sortedKeys = map.Keys.ToList();
        sortedKeys.Sort();
        foreach (var k in sortedKeys)
        {
            var v = map[k].Value.Value;
            var key = k is bool ? k.ToString().ToLower() : k.ToString();

            if (v is IMessage message)
            {
                sb.AppendLine(indent + "  " + key + " = " + message.ToString(indent + "  "));
            }
            else if (typeof(TValue).IsEnum)
            {
                sb.AppendLine(indent + "  " + key + " = " + Convert.ToInt32(v).ToString());
            }
            else if (v is bool)
            {
                sb.AppendLine(indent + "  " + key + " = " + v.ToString().ToLower());
            }
            else
            {
                sb.AppendLine(indent + "  " + key + " = " + v.ToString());
            }
        }
        sb.Append(indent + "]");
        return sb.ToString();
    }

    public void RaiseChanged()
    {
        if (!_clear && _deletes.Count == 0 && _updates.Count == 0)
            return;
        OnChanged?.Invoke(this, new ChangedEvent(_clear, _deletes, _updates.Keys));
    }

    public void ClearChanged()
    {
        _clear = false;
        _deletes.Clear();
        _updates.Clear();
    }

    public void WriteTo(CodedOutputStream output, Codec codec)
    {
        WriteContext.Initialize(output, out var ctx);
        try
        {
            IEnumerable<KeyValuePair<TKey, TValue>> sortedListCopy = list;
            if (output.Deterministic)
            {
                sortedListCopy = GetSortedListCopy(list);
            }

            WriteTo(ref ctx, codec, sortedListCopy);
        }
        finally
        {
            ctx.CopyStateTo(output);
        }
    }

    internal IEnumerable<KeyValuePair<TKey, TValue>> GetSortedListCopy(IEnumerable<KeyValuePair<TKey, TValue>> listToSort)
    {
        var obj = listToSort.ToList();
        obj.Sort((KeyValuePair<TKey, TValue> pair1, KeyValuePair<TKey, TValue> pair2) => (typeof(TKey) == typeof(string)) ? StringComparer.Ordinal.Compare(pair1.Key.ToString(), pair2.Key.ToString()) : Comparer<TKey>.Default.Compare(pair1.Key, pair2.Key));
        return obj;
    }

    [SecuritySafeCritical]
    public void WriteTo(ref WriteContext ctx, Codec codec)
    {
        IEnumerable<KeyValuePair<TKey, TValue>> sortedListCopy = list;
        CodedOutputStream codedOutputStream = ctx.state.CodedOutputStream;
        if (codedOutputStream != null && codedOutputStream.Deterministic)
        {
            sortedListCopy = GetSortedListCopy(list);
        }

        WriteTo(ref ctx, codec, sortedListCopy);
    }

    [SecuritySafeCritical]
    private void WriteTo(ref WriteContext ctx, Codec codec, IEnumerable<KeyValuePair<TKey, TValue>> listKvp)
    {
        foreach (KeyValuePair<TKey, TValue> item in listKvp)
        {
            ctx.WriteTag(codec.MapTag);
            WritingPrimitives.WriteLength(ref ctx.buffer, ref ctx.state, CalculateEntrySize(codec, item));
            codec.KeyCodec.WriteTagAndValue(ref ctx, item.Key);
            codec.ValueCodec.WriteTagAndValue(ref ctx, item.Value);
        }
    }

    public int CalculateSize(Codec codec)
    {
        if (Count == 0)
        {
            return 0;
        }

        int num = 0;
        foreach (KeyValuePair<TKey, TValue> item in list)
        {
            int num2 = CalculateEntrySize(codec, item);
            num += CodedOutputStream.ComputeRawVarint32Size(codec.MapTag);
            num += CodedOutputStream.ComputeLengthSize(num2) + num2;
        }

        return num;
    }

    private static int CalculateEntrySize(Codec codec, KeyValuePair<TKey, TValue> entry)
    {
        return codec.KeyCodec.CalculateSizeWithTag(entry.Key) + codec.ValueCodec.CalculateSizeWithTag(entry.Value);
    }

    public override string ToString()
    {
        StringWriter stringWriter = new StringWriter();
        // JsonFormatter.Default.WriteDictionary(stringWriter, this);
        return stringWriter.ToString();
    }

    void IDictionary.Add(object key, object value)
    {
        Add((TKey)key, (TValue)value);
    }

    bool IDictionary.Contains(object key)
    {
        if (key is TKey key2)
        {
            return ContainsKey(key2);
        }

        return false;
    }

    IDictionaryEnumerator IDictionary.GetEnumerator()
    {
        return new DictionaryEnumerator(GetEnumerator());
    }

    void IDictionary.Remove(object key)
    {
        ProtoPreconditions.CheckNotNull(key, "key");
        if (key is TKey key2)
        {
            Remove(key2);
        }
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ((ICollection)this.Select((KeyValuePair<TKey, TValue> pair) => new DictionaryEntry(pair.Key, pair.Value)).ToList()).CopyTo(array, index);
    }
}