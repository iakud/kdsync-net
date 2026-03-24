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

        //
        // 摘要:
        //     Gets and sets the capacity of the RepeatedField's internal array. When set, the
        //     internal array is reallocated to the given capacity. The new value is less than
        //     Google.Protobuf.Collections.RepeatedField`1.Count.
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

        //
        // 摘要:
        //     Gets the number of elements contained in the collection.
        public int Count => count;

        //
        // 摘要:
        //     Gets a value indicating whether the collection is read-only.
        public bool IsReadOnly => false;

        //
        // 摘要:
        //     Gets or sets the item at the specified index.
        //
        // 参数:
        //   index:
        //     The zero-based index of the element to get or set.
        //
        // 返回结果:
        //     The item at the specified index.
        //
        // 值:
        //     The element at the specified index.
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

        //
        // 摘要:
        //     Creates a deep clone of this repeated field.
        //
        // 返回结果:
        //     A deep clone of this repeated field.
        //
        // 言论：
        //     If the field type is a message type, each element is also cloned; otherwise,
        //     it is assumed that the field type is primitive (including string and bytes, both
        //     of which are immutable) and so a simple copy is equivalent to a deep clone.
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

        public void MergeFrom(byte[] buffer, FieldCodec<T> codec)
        {
            Clear();
            var input = new CodedInputStream(buffer);
            ValueReader<T> valueReader = codec.ValueReader;
			while (!input.IsAtEnd)
			{
				Add(valueReader(ref input));
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

        //
        // 摘要:
        //     Adds the specified item to the collection.
        //
        // 参数:
        //   item:
        //     The item to add.
        public void Add(T item)
        {
            EnsureSize(count + 1);
            array[count++] = item;
        }

        //
        // 摘要:
        //     Removes all items from the collection.
        public void Clear()
        {
            Array.Clear(array, 0, count);
            count = 0;
        }

        //
        // 摘要:
        //     Determines whether this collection contains the given item.
        //
        // 参数:
        //   item:
        //     The item to find.
        //
        // 返回结果:
        //     true if this collection contains the given item; false otherwise.
        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        //
        // 摘要:
        //     Copies this collection to the given array.
        //
        // 参数:
        //   array:
        //     The array to copy to.
        //
        //   arrayIndex:
        //     The first index of the array to copy to.
        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(this.array, 0, array, arrayIndex, count);
        }

        //
        // 摘要:
        //     Removes the specified item from the collection
        //
        // 参数:
        //   item:
        //     The item to remove.
        //
        // 返回结果:
        //     true if the item was found and removed; false otherwise.
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

        //
        // 摘要:
        //     Adds all of the specified values into this collection.
        //
        // 参数:
        //   values:
        //     The values to add to this collection.
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

        //
        // 摘要:
        //     Adds the elements of the specified span to the end of the collection.
        //
        // 参数:
        //   source:
        //     The span whose elements should be added to the end of the collection.
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

        //
        // 摘要:
        //     Adds all of the specified values into this collection. This method is present
        //     to allow repeated fields to be constructed from queries within collection initializers.
        //     Within non-collection-initializer code, consider using the equivalent Google.Protobuf.Collections.RepeatedField`1.AddRange(System.Collections.Generic.IEnumerable{`0})
        //     method instead for clarity.
        //
        // 参数:
        //   values:
        //     The values to add to this collection.
        public void Add(IEnumerable<T> values)
        {
            AddRange(values);
        }

        //
        // 摘要:
        //     Returns an enumerator that iterates through the collection.
        //
        // 返回结果:
        //     An enumerator that can be used to iterate through the collection.
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return array[i];
            }
        }

        //
        // 摘要:
        //     Determines whether the specified System.Object, is equal to this instance.
        //
        // 参数:
        //   obj:
        //     The System.Object to compare with this instance.
        //
        // 返回结果:
        //     true if the specified System.Object is equal to this instance; otherwise, false.
        public override bool Equals(object obj)
        {
            return Equals(obj as List<T>);
        }

        //
        // 摘要:
        //     Returns an enumerator that iterates through a collection.
        //
        // 返回结果:
        //     An System.Collections.IEnumerator object that can be used to iterate through
        //     the collection.
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        //
        // 摘要:
        //     Returns a hash code for this instance.
        //
        // 返回结果:
        //     A hash code for this instance, suitable for use in hashing algorithms and data
        //     structures like a hash table.
        public override int GetHashCode()
        {
            int num = 0;
            for (int i = 0; i < count; i++)
            {
                num = num * 31 + array[i].GetHashCode();
            }

            return num;
        }

        //
        // 摘要:
        //     Compares this repeated field with another for equality.
        //
        // 参数:
        //   other:
        //     The repeated field to compare this with.
        //
        // 返回结果:
        //     true if other refers to an equal repeated field; false otherwise.
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

        //
        // 摘要:
        //     Returns the index of the given item within the collection, or -1 if the item
        //     is not present.
        //
        // 参数:
        //   item:
        //     The item to find in the collection.
        //
        // 返回结果:
        //     The zero-based index of the item, or -1 if it is not found.
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

        //
        // 摘要:
        //     Inserts the given item at the specified index.
        //
        // 参数:
        //   index:
        //     The index at which to insert the item.
        //
        //   item:
        //     The item to insert.
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

        //
        // 摘要:
        //     Removes the item at the given index.
        //
        // 参数:
        //   index:
        //     The zero-based index of the item to remove.
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

        //
        // 摘要:
        //     Returns a string representation of this repeated field, in the same way as it
        //     would be represented by the default JSON formatter.
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