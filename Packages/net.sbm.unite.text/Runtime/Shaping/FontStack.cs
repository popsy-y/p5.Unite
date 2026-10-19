using System;
using Unite.Text.Fonts;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// The fonts text is drawn with: the primary font, the fallbacks given to TextFont()
    /// (each at the same axis values) and, past those, the system fonts found for missing
    /// characters. Stacks are shared per instance sequence and their <see cref="Id"/> keys
    /// the shaping caches.
    /// </summary>
    internal sealed class FontStack
    {
        private const int Capacity = 256;

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int[] _ids;
            private readonly int _hash;

            public Key(int[] ids)
            {
                _ids = ids;
                int hash = 17;
                foreach (int id in ids)
                    hash = hash * 31 + id;
                _hash = hash;
            }

            public bool Equals(Key other)
            {
                if (_hash != other._hash || _ids.Length != other._ids.Length)
                    return false;
                for (int index = 0; index < _ids.Length; index++)
                {
                    if (_ids[index] != other._ids[index])
                        return false;
                }
                return true;
            }

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => _hash;
        }

        private static readonly LruTable<Key, FontStack> Stacks = new(Capacity);
        private static readonly int[] SingleScratch = new int[1];
        private static int _nextId;

        private FontStack(FontInstance[] instances)
        {
            Instances = instances;
            Id = ++_nextId;
        }

        public FontInstance[] Instances { get; }

        public int Count => Instances.Length;

        public int Id { get; }

        public FontInstance Primary => Instances[0];

        /// <summary>
        /// The instance of a face index: the stack's own fonts first, then the system
        /// fallback fonts (see <see cref="SystemFonts"/>) at their default axis values.
        /// </summary>
        public FontInstance this[int face]
            => face < Instances.Length ? Instances[face] : SystemFonts.FaceAt(face - Instances.Length).Default;

        public static FontStack Single(FontInstance instance)
        {
            SingleScratch[0] = instance.Id;
            // An equal id means equal axis values, so an older instance object serves as well.
            if (Stacks.TryGet(new Key(SingleScratch), out var stack))
                return stack;
            return Create(new[] { instance });
        }

        public static FontStack Of(FontInstance[] instances)
        {
            if (instances.Length == 1)
                return Single(instances[0]);

            var ids = new int[instances.Length];
            for (int index = 0; index < ids.Length; index++)
                ids[index] = instances[index].Id;
            if (Stacks.TryGet(new Key(ids), out var stack))
                return stack;
            return Create((FontInstance[])instances.Clone());
        }

        private static FontStack Create(FontInstance[] instances)
        {
            var ids = new int[instances.Length];
            for (int index = 0; index < ids.Length; index++)
                ids[index] = instances[index].Id;
            var stack = new FontStack(instances);
            int slot = Stacks.Acquire(new Key(ids), out _);
            Stacks.SetValue(slot, stack);
            return stack;
        }
    }
}
