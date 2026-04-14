using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kdsync
{
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

        public const int KeyFieldNumber = 1;

        public const int ValueFieldNumber = 2;

        private static readonly EqualityComparer<TValue> ValueEqualityComparer = EqualityComparers.GetEqualityComparer<TValue>();

        private static readonly EqualityComparer<TKey> KeyEqualityComparer = EqualityComparers.GetEqualityComparer<TKey>();

        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(KeyEqualityComparer);

        private readonly LinkedList<KeyValuePair<TKey, TValue>> list = new LinkedList<KeyValuePair<TKey, TValue>>();

        private bool _clear = false;

        private readonly HashSet<TKey> _deletes = new HashSet<TKey>(KeyEqualityComparer);

        private readonly HashSet<TKey> _updates = new HashSet<TKey>(KeyEqualityComparer);

        public event Action<Map<TKey, TValue>, ChangedEvent>? OnChanged;

        public TValue this[TKey key]
        {
            get
            {
                Preconditions.CheckNotNullUnconstrained(key, "key");
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException();
            }
            set
            {
                Preconditions.CheckNotNullUnconstrained(key, "key");
                if (value == null)
                {
                    Preconditions.CheckNotNullUnconstrained(value, "value");
                }

                KeyValuePair<TKey, TValue> value2 = new KeyValuePair<TKey, TValue>(key, value);
                if (map.TryGetValue(key, out var value3))
                {
                    value3.Value = value2;
                    return;
                }

                value3 = list.AddLast(value2);
                map[key] = value3;
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
                Preconditions.CheckNotNull(key, "key");
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
            Preconditions.CheckNotNullUnconstrained(key, "key");
            return map.ContainsKey(key);
        }

        private bool ContainsValue(TValue value)
        {
            return list.Any((KeyValuePair<TKey, TValue> pair) => ValueEqualityComparer.Equals(pair.Value, value));
        }

        public bool Remove(TKey key)
        {
            Preconditions.CheckNotNullUnconstrained(key, "key");
            if (map.TryGetValue(key, out var value))
            {
                map.Remove(key);
                value.List.Remove(value);
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
            Preconditions.CheckNotNull(entries, "entries");
            foreach (KeyValuePair<TKey, TValue> entry in entries)
            {
                Add(entry.Key, entry.Value);
            }
        }

        public void MergeFrom(IDictionary<TKey, TValue> entries)
        {
            Preconditions.CheckNotNull(entries, "entries");
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

        public void MergeFrom(ref ParseContext ctx, Codec codec)
        {
            int byteLimit = ctx.ReadLength();
            if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
            {
                throw InvalidException.RecursionLimitExceeded();
            }

            int oldLimit = ctx.PushLimit(byteLimit);
            ctx.state.recursionDepth++;

            var clear = false;
            IEnumerable<TKey> deleteKeys = Enumerable.Empty<TKey>();
            List<ValueTuple<int, int>> entries = new List<ValueTuple<int, int>>();
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
                        deleteKeys = ParsingPrimitivesMessages.ReadMapDeleteKeys(ref ctx, codec);
                        break;
                    case EntriesFieldNumber:
                        int size = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
                        entries.Add((ctx.state.bufferPos, size));
                        ParsingPrimitives.SkipRawBytes(ref ctx.buffer, ref ctx.state, size);
                        break;
                    default:
                        ctx.SkipLastField();
                        break;
                }
            }
            if (clear)
            {
                Clear();
                _clear = true;
            }
            foreach (var deleteKey in deleteKeys)
            {
                Remove(deleteKey);
                _deletes.Add(deleteKey);
            }
            foreach (ref ValueTuple<int, int> entry in entries.ToArray().AsSpan())
            {
                ParseContext.Initialize(ctx.buffer.Slice(entry.Item1, entry.Item2), out var entryCtx);
                if (typeof(TValue) is IMessage)
                {
                    MergeMessageEntriesFrom(ref entryCtx, codec);
                }
                else
                {
                    MergeEntriesFrom(ref entryCtx, codec);
                }
            }

            ctx.CheckReadEndOfStreamTag();
            if (!ctx.ReachedLimit)
            {
                throw InvalidException.TruncatedMessage();
            }

            ctx.state.recursionDepth--;
            ctx.PopLimit(oldLimit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MergeMessageEntriesFrom(ref ParseContext ctx, Codec codec)
        {
            TKey key = codec.KeyCodec.DefaultValue;
            ReadOnlySpan<byte> valSpan = ParsingPrimitivesMessages.ZeroLengthMessageStreamData;
            uint tag;
            while ((tag = ctx.ReadTag()) != 0)
            {
                int num = WireFormat.GetTagFieldNumber(tag);
                if (num == KeyFieldNumber)
                {
                    key = codec.KeyCodec.Read(ref ctx);
                }
                else if (num == ValueFieldNumber)
                {
                    int size = ParsingPrimitives.ParseLength(ref ctx.buffer, ref ctx.state);
                    valSpan = ctx.buffer.Slice(ctx.state.bufferPos, size);
                    ParsingPrimitives.SkipRawBytes(ref ctx.buffer, ref ctx.state, size);
                }
                else
                {
                    ctx.SkipLastField();
                }
            }

            ParseContext.Initialize(valSpan, out var valueCtx);
            if (TryGetValue(key, out var value))
            {
                codec.ValueCodec.ValueMerger(ref valueCtx, ref value);
            }
            else
            {
                this[key] = codec.ValueCodec.Read(ref valueCtx);
            }
            _updates.Add(key);

            ctx.CheckReadEndOfStreamTag();
            if (!ctx.ReachedLimit)
            {
                throw InvalidException.TruncatedMessage();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MergeEntriesFrom(ref ParseContext ctx, Codec codec)
        {
            TKey key = codec.KeyCodec.DefaultValue;
            TValue val = codec.ValueCodec.DefaultValue;
            uint tag;
            while ((tag = ctx.ReadTag()) != 0)
            {
                int num = WireFormat.GetTagFieldNumber(tag);
                if (num == KeyFieldNumber)
                {
                    key = codec.KeyCodec.Read(ref ctx);
                }
                else if (num == ValueFieldNumber)
                {
                    val = codec.ValueCodec.Read(ref ctx);
                }
                else
                {
                    ctx.SkipLastField();
                }
            }

            this[key] = val;
            _updates.Add(key);

            ctx.CheckReadEndOfStreamTag();
            if (!ctx.ReachedLimit)
            {
                throw InvalidException.TruncatedMessage();
            }
        }

        public void RaiseChanged()
        {
            if (!_clear && _deletes.Count == 0 && _updates.Count == 0)
                return;
            OnChanged?.Invoke(this, new ChangedEvent(_clear, _deletes, _updates));
        }

        public void ClearChanged()
        {
            _clear = false;
            _deletes.Clear();
            _updates.Clear();
        }

        internal IEnumerable<KeyValuePair<TKey, TValue>> GetSortedListCopy(IEnumerable<KeyValuePair<TKey, TValue>> listToSort)
        {
            var obj = listToSort.ToList();
            obj.Sort((KeyValuePair<TKey, TValue> pair1, KeyValuePair<TKey, TValue> pair2) => (typeof(TKey) == typeof(string)) ? StringComparer.Ordinal.Compare(pair1.Key.ToString(), pair2.Key.ToString()) : Comparer<TKey>.Default.Compare(pair1.Key, pair2.Key));
            return obj;
        }

        public override string ToString()
        {
            JsonWriter writer = new JsonWriter();
            writer.WriteMapValue(this);
            return writer.ToString();
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
            Preconditions.CheckNotNull(key, "key");
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
}