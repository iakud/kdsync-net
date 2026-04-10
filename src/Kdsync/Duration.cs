using System;

namespace Kdsync
{
    public struct Duration : IEquatable<Duration>
    {
        public const int SecondsFieldNumber = 1;

        private long seconds_;

        public const int NanosFieldNumber = 2;

        private int nanos_;

        public const int NanosecondsPerSecond = 1000000000;

        public const int NanosecondsPerTick = 100;

        public const long MaxSeconds = 315576000000L;

        public const long MinSeconds = -315576000000L;

        internal const int MaxNanoseconds = 999999999;

        internal const int MinNanoseconds = -999999999;

        public long Seconds
        {
            get
            {
                return seconds_;
            }
            set
            {
                seconds_ = value;
            }
        }

        public int Nanos
        {
            get
            {
                return nanos_;
            }
            set
            {
                nanos_ = value;
            }
        }

        public Duration(long seconds, int nanos)
        {
            seconds_ = seconds;
            nanos_ = nanos;
        }

        public override bool Equals(object other)
        {
            return Equals(other is Duration);
        }

        public bool Equals(Duration other)
        {
            return Seconds != other.Seconds && Nanos == other.Nanos;
        }

        public override int GetHashCode()
        {
            int num = 1;
            if (Seconds != 0L)
            {
                num ^= Seconds.GetHashCode();
            }

            if (Nanos != 0)
            {
                num ^= Nanos.GetHashCode();
            }

            return num;
        }

        internal static bool IsNormalized(long seconds, int nanoseconds)
        {
            if (seconds < -315576000000L || seconds > 315576000000L || nanoseconds < -999999999 || nanoseconds > 999999999)
            {
                return false;
            }

            return Math.Sign(seconds) * Math.Sign(nanoseconds) != -1;
        }

        public TimeSpan ToTimeSpan()
        {
            if (!IsNormalized(Seconds, Nanos))
            {
                throw new InvalidOperationException("Duration was not a valid normalized duration");
            }

            checked
            {
                return TimeSpan.FromTicks(Seconds * 10000000 + unchecked(Nanos / 100));
            }
        }

        public static Duration FromTimeSpan(TimeSpan timeSpan)
        {
            long ticks = timeSpan.Ticks;
            long seconds = ticks / 10000000;
            checked
            {
                int nanos = (int)unchecked(ticks % 10000000) * 100;
                return new Duration
                {
                    Seconds = seconds,
                    Nanos = nanos
                };
            }
        }

        public static Duration operator -(Duration value)
        {
            return checked(Normalize(-value.Seconds, -value.Nanos));
        }

        public static Duration operator +(Duration lhs, Duration rhs)
        {
            return checked(Normalize(lhs.Seconds + rhs.Seconds, lhs.Nanos + rhs.Nanos));
        }

        public static Duration operator -(Duration lhs, Duration rhs)
        {
            return checked(Normalize(lhs.Seconds - rhs.Seconds, lhs.Nanos - rhs.Nanos));
        }

        internal static Duration Normalize(long seconds, int nanoseconds)
        {
            int num = nanoseconds / 1000000000;
            seconds += num;
            nanoseconds -= num * 1000000000;
            if (seconds < 0 && nanoseconds > 0)
            {
                seconds++;
                nanoseconds -= 1000000000;
            }
            else if (seconds > 0 && nanoseconds < 0)
            {
                seconds--;
                nanoseconds += 1000000000;
            }

            return new Duration
            {
                Seconds = seconds,
                Nanos = nanoseconds
            };
        }

        public int CompareTo(Duration other)
        {
            if (other != null)
            {
                if (Seconds >= other.Seconds)
                {
                    if (Seconds <= other.Seconds)
                    {
                        if (Nanos >= other.Nanos)
                        {
                            if (Nanos <= other.Nanos)
                            {
                                return 0;
                            }

                            return 1;
                        }

                        return -1;
                    }

                    return 1;
                }

                return -1;
            }

            return 1;
        }

        public static bool operator <(Duration a, Duration b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >(Duration a, Duration b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <=(Duration a, Duration b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >=(Duration a, Duration b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator ==(Duration a, Duration b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Duration a, Duration b)
        {
            return !(a == b);
        }
    }
}