namespace Kdsync;

public class Timestamp : IMessage, IEquatable<Timestamp>
{
    public const int SecondsFieldNumber = 1;

    private long seconds_;

    public const int NanosFieldNumber = 2;

    private int nanos_;

    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const long BclSecondsAtUnixEpoch = 62135596800L;

    internal const long UnixSecondsAtBclMaxValue = 253402300799L;

    internal const long UnixSecondsAtBclMinValue = -62135596800L;

    internal const int MaxNanos = 999999999;

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

    public override bool Equals(object other)
    {
        return Equals(other as Timestamp);
    }

    public bool Equals(Timestamp other)
    {
        if ((object)other == null)
        {
            return false;
        }

        if ((object)other == this)
        {
            return true;
        }

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

    public void MergeFrom(ref ParseContext ctx)
    {
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                case SecondsFieldNumber:
                    Seconds = ctx.ReadInt64();
                    break;
                case NanosFieldNumber:
                    Nanos = ctx.ReadInt32();
                    break;
                default:
                    ctx.SkipLastField();
                    break;
            }
        }
    }

    public void WriteTo(CodedOutputStream output)
    {
        
    }
    public int CalculateSize()
    {
        return 0;
    }

    public string ToString(string indent)
    {
        return "{Seconds: " + Seconds + ", Nanos: " + Nanos + "}";
    }

    private static bool IsNormalized(long seconds, int nanoseconds)
    {
        if (nanoseconds >= 0 && nanoseconds <= 999999999 && seconds >= -62135596800L)
        {
            return seconds <= 253402300799L;
        }

        return false;
    }

    public static Duration operator -(Timestamp lhs, Timestamp rhs)
    {
        ProtoPreconditions.CheckNotNull(lhs, "lhs");
        ProtoPreconditions.CheckNotNull(rhs, "rhs");
        return checked(Duration.Normalize(lhs.Seconds - rhs.Seconds, lhs.Nanos - rhs.Nanos));
    }

    public static Timestamp operator +(Timestamp lhs, Duration rhs)
    {
        ProtoPreconditions.CheckNotNull(lhs, "lhs");
        ProtoPreconditions.CheckNotNull(rhs, "rhs");
        return checked(Normalize(lhs.Seconds + rhs.Seconds, lhs.Nanos + rhs.Nanos));
    }

    public static Timestamp operator -(Timestamp lhs, Duration rhs)
    {
        ProtoPreconditions.CheckNotNull(lhs, "lhs");
        ProtoPreconditions.CheckNotNull(rhs, "rhs");
        return checked(Normalize(lhs.Seconds - rhs.Seconds, lhs.Nanos - rhs.Nanos));
    }

    public DateTime ToDateTime()
    {
        if (!IsNormalized(Seconds, Nanos))
        {
            throw new InvalidOperationException("Timestamp contains invalid values: Seconds={Seconds}; Nanos={Nanos}");
        }

        return UnixEpoch.AddSeconds(Seconds).AddTicks(Nanos / 100);
    }

    public DateTimeOffset ToDateTimeOffset()
    {
        return new DateTimeOffset(ToDateTime(), TimeSpan.Zero);
    }

    public static Timestamp FromDateTime(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Conversion from DateTime to Timestamp requires the DateTime kind to be Utc", "dateTime");
        }

        long num = dateTime.Ticks / 10000000;
        int nanos = (int)(dateTime.Ticks % 10000000) * 100;
        return new Timestamp
        {
            Seconds = num - 62135596800L,
            Nanos = nanos
        };
    }

    public static Timestamp FromDateTimeOffset(DateTimeOffset dateTimeOffset)
    {
        return FromDateTime(dateTimeOffset.UtcDateTime);
    }

    internal static Timestamp Normalize(long seconds, int nanoseconds)
    {
        int num = nanoseconds / 1000000000;
        seconds += num;
        nanoseconds -= num * 1000000000;
        if (nanoseconds < 0)
        {
            nanoseconds += 1000000000;
            seconds--;
        }

        return new Timestamp
        {
            Seconds = seconds,
            Nanos = nanoseconds
        };
    }

    public int CompareTo(Timestamp other)
    {
        if (!(other == null))
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

    public static bool operator <(Timestamp a, Timestamp b)
    {
        return a.CompareTo(b) < 0;
    }

    public static bool operator >(Timestamp a, Timestamp b)
    {
        return a.CompareTo(b) > 0;
    }

    public static bool operator <=(Timestamp a, Timestamp b)
    {
        return a.CompareTo(b) <= 0;
    }

    public static bool operator >=(Timestamp a, Timestamp b)
    {
        return a.CompareTo(b) >= 0;
    }

    public static bool operator ==(Timestamp a, Timestamp b)
    {
        if ((object)a != b)
        {
            return a?.Equals(b) ?? ((object)b == null);
        }

        return true;
    }

    public static bool operator !=(Timestamp a, Timestamp b)
    {
        return !(a == b);
    }

}