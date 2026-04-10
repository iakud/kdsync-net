using System;

namespace Kdsync
{
    public struct Empty : IEquatable<Empty>
    {
        public override bool Equals(object other)
        {
            return Equals(other is Empty);
        }

        public bool Equals(Empty other)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return 1;
        }

        public static bool operator ==(Empty a, Empty b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Empty a, Empty b)
        {
            return !(a == b);
        }
    }
}