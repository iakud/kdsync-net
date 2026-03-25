using System.Collections;
using System.Runtime.CompilerServices;
using System.Security;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace Kdsync
{
    public sealed class List<T> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable, IList, ICollection, IDeepCloneable<List<T>>, IEquatable<List<T>>, IReadOnlyList<T>, IReadOnlyCollection<T>
    {
        public class ChangedEvent
		{
		}

        private static readonly EqualityComparer<T> EqualityComparer = ProtobufEqualityComparers.GetEqualityComparer<T>();

        private static readonly T[] EmptyArray = new T[0];

        private const int MinArraySize = 8;

        private T[] array = EmptyArray;

        private int count;

        public event Action<List<T>, ChangedEvent>? OnChanged;

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

                array[index] = value;
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

        public List<T> Clone()
        {
            List<T> repeatedField = new List<T>();
            if (this.array != EmptyArray)
            {
                repeatedField.array = (T[])this.array.Clone();
                if (repeatedField.array is IDeepCloneable<T>[] array)
                {
                    for (int i = 0; i < count; i++)
                    {
                        repeatedField.array[i] = array[i].Clone();
                    }
                }
            }

            repeatedField.count = count;
            return repeatedField;
        }

        public void AddEntriesFrom(CodedInputStream input, FieldCodec<T> codec)
        {
            ParseContext.Initialize(input, out var ctx);
            try
            {
                AddEntriesFrom(ref ctx, codec);
            }
            finally
            {
                ctx.CopyStateTo(input);
            }
        }

        public void AddEntriesFrom(ref ParseContext ctx, FieldCodec<T> codec)
        {
            Clear();
            ValueReader<T> valueReader = codec.ValueReader;
			while (!SegmentedBufferHelper.IsReachedLimit(ref ctx.state))
			{
				Add(valueReader(ref ctx));
			}
        }

        public void RaiseChanged()
		{
			OnChanged?.Invoke(this, new ChangedEvent());
		}

		public void ClearChanged()
		{

		}

        public string ToString(string indent)
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("[\n");
			for (int i = 0; i < count; i++)
			{
				var v = array[i];
                if (v is IMessage message)
                {
                    sb.AppendLine(indent + "  " + message.ToString(indent + "  "));
                }
                else if (typeof(T).IsEnum)
                {
                    sb.AppendLine(indent + "  " + Convert.ToInt32(v).ToString());
                }
                else if (v is bool)
                {
                    sb.AppendLine(indent + "  " + v.ToString().ToLower());
                }
                else
                {
                    sb.AppendLine(indent + "  " + v.ToString());   
                }
			}
			sb.Append(indent + "]");
			return sb.ToString();
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
            EnsureSize(count + 1);
            array[count++] = item;
        }

        public void Clear()
        {
            Array.Clear(array, 0, count);
            count = 0;
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
            {
                return false;
            }

            Array.Copy(array, num + 1, array, num, count - num - 1);
            count--;
            array[count] = default(T);
            return true;
        }

        public void AddRange(IEnumerable<T> values)
        {
            ProtoPreconditions.CheckNotNull(values, "values");
            if (values is List<T> repeatedField)
            {
                EnsureSize(count + repeatedField.count);
                Array.Copy(repeatedField.array, 0, array, count, repeatedField.count);
                count += repeatedField.count;
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

                EnsureSize(count + num);
                collection.CopyTo(array, count);
                count += num;
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
            {
                return;
            }

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

            EnsureSize(count + source.Length);
            source.CopyTo(array.AsSpan(count));
            count += source.Length;
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
            return Equals(obj as List<T>);
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

        public bool Equals(List<T> other)
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

            EnsureSize(count + 1);
            Array.Copy(array, index, array, index + 1, count - index);
            array[index] = item;
            count++;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            Array.Copy(array, index + 1, array, index, count - index - 1);
            count--;
            array[count] = default(T);
        }

        public override string ToString()
        {
            StringWriter stringWriter = new StringWriter();
            // JsonFormatter.Default.WriteList(stringWriter, this);
            return stringWriter.ToString();
        }

        [SecuritySafeCritical]
        internal Span<T> AsSpan()
        {
            return array.AsSpan(0, count);
        }

        internal void SetCount(int targetCount)
        {
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
                Array.Clear(array, targetCount, count - targetCount);
            }

            count = targetCount;
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