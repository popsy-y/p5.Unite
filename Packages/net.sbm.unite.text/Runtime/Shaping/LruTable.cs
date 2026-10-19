using System;
using System.Collections.Generic;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Fixed-capacity least-recently-used table. Slots and their values are recycled: when
    /// the table is full, <see cref="Acquire"/> hands back the evicted slot with its old
    /// value so the caller can reuse its buffers. Lookups and evictions are O(1) and
    /// allocate nothing once the table is full.
    /// </summary>
    internal sealed class LruTable<TKey, TValue> where TKey : IEquatable<TKey> where TValue : class
    {
        private readonly Dictionary<TKey, int> _slots;
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private readonly int[] _previous;
        private readonly int[] _next;
        private int _head = -1; // most recently used
        private int _tail = -1; // least recently used
        private int _count;

        public LruTable(int capacity)
        {
            _slots = new Dictionary<TKey, int>(capacity);
            _keys = new TKey[capacity];
            _values = new TValue[capacity];
            _previous = new int[capacity];
            _next = new int[capacity];
        }

        public int Count => _count;

        public bool TryGet(in TKey key, out TValue value)
        {
            if (_slots.TryGetValue(key, out int slot))
            {
                MoveToFront(slot);
                value = _values[slot];
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Stores <paramref name="key"/> (which must not be present) and returns its slot as
        /// the most recently used one; <paramref name="recycled"/> is the value of the
        /// evicted entry, or null when the table still had room.
        /// </summary>
        public int Acquire(in TKey key, out TValue recycled)
        {
            int slot;
            if (_count < _keys.Length)
            {
                slot = _count++;
                recycled = null;
            }
            else
            {
                slot = _tail;
                Unlink(slot);
                _slots.Remove(_keys[slot]);
                recycled = _values[slot];
            }

            _keys[slot] = key;
            _slots.Add(key, slot);
            LinkFront(slot);
            return slot;
        }

        public void SetValue(int slot, TValue value) => _values[slot] = value;

        public void Clear()
        {
            _slots.Clear();
            Array.Clear(_keys, 0, _keys.Length);
            Array.Clear(_values, 0, _values.Length);
            _head = _tail = -1;
            _count = 0;
        }

        private void MoveToFront(int slot)
        {
            if (slot == _head)
                return;
            Unlink(slot);
            LinkFront(slot);
        }

        private void Unlink(int slot)
        {
            int previous = _previous[slot];
            int next = _next[slot];
            if (previous >= 0)
                _next[previous] = next;
            else
                _head = next;
            if (next >= 0)
                _previous[next] = previous;
            else
                _tail = previous;
        }

        private void LinkFront(int slot)
        {
            _previous[slot] = -1;
            _next[slot] = _head;
            if (_head >= 0)
                _previous[_head] = slot;
            _head = slot;
            if (_tail < 0)
                _tail = slot;
        }
    }
}
