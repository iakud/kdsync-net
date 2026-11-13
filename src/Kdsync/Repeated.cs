using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security;

namespace Kdsync
{
    public sealed class Repeated<T> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable, IList, ICollection, IEquatable<Repeated<T>>, IReadOnlyList<T>, IReadOnlyCollection<T>
    {
        public class ChangedEvent
        {
        }

        private static readonly EqualityComparer<T> EqualityComparer = EqualityComparers.GetEqualityComparer<T>();

        private static readonly bool IsMessage = typeof(IMessage).IsAssignableFrom(typeof(T));

        private static readonly T[] EmptyArray = new T[0];

        private const int MinArraySize = 8;

        private T[] array = EmptyArray;

        private int count;

        private bool _changed;

        private Action? _changedParent;

        public event Action<Repeated<T>, ChangedEvent>? OnChanged;

        public int Capacity
        {
            get
            {
                return array.Length;
            }
            set
            {
                if (value < count)
                {
                    throw new ArgumentOutOfRangeException("Capacity", value, $"Cannot set Capacity to a value smaller than the current item count, {count}");
                }

                if (value >= 0 && value != array.Length)
                {
                    SetSize(value);
                }
            }
        }

        public int Count => count;

        public bool IsReadOnly => false;

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= count)
                {
                    throw new ArgumentOutOfRangeException("index");
                }

                return array[index];
            }
            set
            {
                if (index < 0 || index >= count)
                {
                    throw new ArgumentOutOfRangeException("index");
                }

                var item = array[index];
                if (EqualityComparer.Equals(item, value))
                    return;

                if (IsMessage)
                {
                    if (value != null && ((IMessage)(object)value).CheckChangedParent())
                        throw new InvalidOperationException("the component should be removed from its original place first");
                    if (item != null)
                        ((IMessage)(object)item).ClearChangedParent();
                    if (value != null)
                        ((IMessage)(object)value).SetChangedParent(UpdateChanged);
                }
                array[index] = value;
                UpdateChanged();
            }
        }

        bool IList.IsFixedSize => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        object IList.this[int index]
        {
            get
            {
                return this[index];
            }
            set
            {
                this[index] = (T)value;
            }
        }

        public void Init(Action changedParent)
        {
            _changedParent = changedParent;
        }

        public void MergeFrom(ref ParseContext ctx, FieldCodec<T> fieldCodec)
        {
            int byteLimit = ctx.ReadLength();
            if (ctx.state.recursionDepth >= ctx.state.recursionLimit)
            {
                throw InvalidException.RecursionLimitExceeded();
            }
            int oldLimit = ctx.PushLimit(byteLimit);
            ctx.state.recursionDepth++;
            MergeEntriesFrom(ref ctx, fieldCodec);
            if (!ctx.ReachedLimit)
            {
                throw InvalidException.TruncatedMessage();
            }

            ctx.state.recursionDepth--;
            ctx.PopLimit(oldLimit);
        }

        private void MergeEntriesFrom(ref ParseContext ctx, FieldCodec<T> fieldCodec)
        {
            Clear();
            ValueReader<T> valueReader = fieldCodec.ValueReader;
            while (!ctx.ReachedLimit)
            {
                Add(valueReader(ref ctx));
            }
        }

        private void UpdateChanged()
        {
            if (_changed)
                return;

            _changed = true;
            _changedParent?.Invoke();
        }

        public void RaiseChanged()
        {
            if (!_changed)
                return;

            if (IsMessage)
            {
                for (int i = 0; i < count; i++)
                {
                    var item = array[i];
                    if (item != null)
                        ((IMessage)(object)item).RaiseChanged();
                }
            }

            OnChanged?.Invoke(this, new ChangedEvent());
        }

        public void ClearChanged()
        {
            if (!_changed)
                return;

            if (IsMessage)
            {
                for (int i = 0; i < count; i++)
                {
                    var item = array[i];
                    if (item != null)
                        ((IMessage)(object)item).ClearChanged();
                }
            }

            _changed = false;
        }

        private void EnsureSize(int size)
        {
            if (array.Length < size)
            {
                size = Math.Max(size, 8);
                int size2 = Math.Max(array.Length * 2, size);
                SetSize(size2);
            }
        }

        private void SetSize(int size)
        {
            if (size != array.Length)
            {
                T[] destinationArray = new T[size];
                Array.Copy(array, 0, destinationArray, 0, count);
                array = destinationArray;
            }
        }

        public void Add(T item)
        {
            if (IsMessage && item != null && ((IMessage)(object)item).CheckChangedParent())
                throw new InvalidOperationException("the component should be removed from its original place first");

            EnsureSize(count + 1);
            array[count++] = item;
            if (IsMessage && item != null)
                ((IMessage)(object)item).SetChangedParent(UpdateChanged);
            UpdateChanged();
        }

        public void Clear()
        {
            if (count == 0)
                return;

            if (IsMessage)
            {
                for (int i = 0; i < count; i++)
                {
                    var item = array[i];
                    if (item != null)
                        ((IMessage)(object)item).ClearChangedParent();
                }
            }

            Array.Clear(array, 0, count);
            count = 0;
            UpdateChanged();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(this.array, 0, array, arrayIndex, count);
        }

        public bool Remove(T item)
        {
            int num = IndexOf(item);
            if (num == -1)
                return false;

            if (IsMessage)
            {
                item = array[num];
                if (item != null)
                    ((IMessage)(object)item).ClearChangedParent();
            }

            Array.Copy(array, num + 1, array, num, count - num - 1);
            count--;
            array[count] = default(T);

            UpdateChanged();
            return true;
        }

        public void AddRange(IEnumerable<T> values)
        {
            Preconditions.CheckNotNull(values, "values");
            if (values is Repeated<T> repeatedField)
            {
                if (IsMessage)
                {
                    for (var i = 0; i < repeatedField.count; i++)
                    {
                        var item = repeatedField[i];
                        if (item != null && ((IMessage)(object)item).CheckChangedParent())
                            throw new InvalidOperationException("the component should be removed from its original place first");
                    }
                    for (var i = 0; i < repeatedField.count; i++)
                    {
                        var item = repeatedField[i];
                        if (item != null)
                            ((IMessage)(object)item).SetChangedParent(UpdateChanged);
                    }
                }

                EnsureSize(count + repeatedField.count);
                Array.Copy(repeatedField.array, 0, array, count, repeatedField.count);
                count += repeatedField.count;
                UpdateChanged();
                return;
            }

            if (values is ICollection { Count: var num } collection)
            {
                if (default(T) == null)
                {
                    foreach (object item in collection)
                    {
                        if (item == null)
                        {
                            throw new ArgumentException("Sequence contained null element", "values");
                        }
                    }
                }

                if (IsMessage)
                {
                    foreach (var item in collection)
                    {
                        if (item != null && ((IMessage)(object)item).CheckChangedParent())
                            throw new InvalidOperationException("the component should be removed from its original place first");
                    }
                    foreach (var item in collection)
                    {
                        if (item != null)
                            ((IMessage)(object)item).SetChangedParent(UpdateChanged);
                    }
                }

                EnsureSize(count + num);
                collection.CopyTo(array, count);
                count += num;
                UpdateChanged();
                return;
            }

            foreach (T value in values)
            {
                Add(value);
            }
        }

        [SecuritySafeCritical]
        internal void AddRangeSpan(ReadOnlySpan<T> source)
        {
            if (source.IsEmpty)
                return;

            if (default(T) == null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null)
                    {
                        throw new ArgumentException("ReadOnlySpan contained null element", "source");
                    }
                }
            }

            if (IsMessage)
            {
                foreach (var item in source)
                {
                    if (item != null && ((IMessage)(object)item).CheckChangedParent())
                        throw new InvalidOperationException("the component should be removed from its original place first");
                }
                foreach (var item in source)
                {
                    if (item != null)
                        ((IMessage)(object)item).SetChangedParent(UpdateChanged);
                }
            }

            EnsureSize(count + source.Length);
            source.CopyTo(array.AsSpan(count));
            count += source.Length;
            UpdateChanged();
        }

        public void Add(IEnumerable<T> values)
        {
            AddRange(values);
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return array[i];
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Repeated<T>);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override int GetHashCode()
        {
            int num = 0;
            for (int i = 0; i < count; i++)
            {
                num = num * 31 + array[i].GetHashCode();
            }

            return num;
        }

        public bool Equals(Repeated<T> other)
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

            EqualityComparer<T> equalityComparer = EqualityComparer;
            for (int i = 0; i < count; i++)
            {
                if (!equalityComparer.Equals(array[i], other.array[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int IndexOf(T item)
        {
            EqualityComparer<T> equalityComparer = EqualityComparer;
            for (int i = 0; i < count; i++)
            {
                if (equalityComparer.Equals(array[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            if (index < 0 || index > count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            if (IsMessage)
            {
                if (item != null && ((IMessage)(object)item).CheckChangedParent())
                    throw new InvalidOperationException("the component should be removed from its original place first");
                if (item != null)
                    ((IMessage)(object)item).SetChangedParent(UpdateChanged);
            }

            EnsureSize(count + 1);
            Array.Copy(array, index, array, index + 1, count - index);
            array[index] = item;
            count++;
            UpdateChanged();
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            if (IsMessage)
            {
                var item = array[index];
                if (item != null)
                    ((IMessage)(object)item).ClearChangedParent();
            }

            Array.Copy(array, index + 1, array, index, count - index - 1);
            count--;
            array[count] = default(T);
            UpdateChanged();
        }

        public void Write(JsonWriter writer)
        {
            var valueType = typeof(T);
            writer.WriteStartArray();
            if (typeof(IMessage).IsAssignableFrom(valueType))
            {
                foreach (T value in this)
                {
                    writer.WriteValue((IMessage)value!);
                }
            }
            else if (valueType.IsEnum)
            {
                foreach (T value in this)
                {
                    writer.WriteIntValue(Convert.ToInt32(value));
                }
            }
            else
            {
                var valueWriter = JsonWriter.ValueWriter<T>();
                foreach (T value in this)
                {
                    valueWriter(writer, value);
                }
            }
            writer.WriteEndArray();
        }

        public override string ToString()
        {
            JsonWriter writer = new JsonWriter();
            Write(writer);
            return writer.ToString();
        }

        [SecuritySafeCritical]
        internal Span<T> AsSpan()
        {
            return array.AsSpan(0, count);
        }

        internal void SetCount(int targetCount)
        {
            if (count == targetCount)
                return;

            if (targetCount < 0)
            {
                throw new ArgumentOutOfRangeException("targetCount", targetCount, "Non-negative number required.");
            }

            if (targetCount > Capacity)
            {
                EnsureSize(targetCount);
            }
            else if (targetCount < count && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                if (IsMessage)
                {
                    for (int i = targetCount; i < count; i++)
                    {
                        var item = array[i];
                        if (item != null)
                            ((IMessage)(object)item).ClearChangedParent();
                    }
                }
                Array.Clear(array, targetCount, count - targetCount);
            }

            count = targetCount;
            UpdateChanged();
        }

        void ICollection.CopyTo(Array array, int index)
        {
            Array.Copy(this.array, 0, array, index, count);
        }

        int IList.Add(object value)
        {
            Add((T)value);
            return count - 1;
        }

        bool IList.Contains(object value)
        {
            if (value is T item)
            {
                return Contains(item);
            }

            return false;
        }

        int IList.IndexOf(object value)
        {
            if (!(value is T item))
            {
                return -1;
            }

            return IndexOf(item);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (T)value);
        }

        void IList.Remove(object value)
        {
            if (value is T item)
            {
                Remove(item);
            }
        }
    }
}