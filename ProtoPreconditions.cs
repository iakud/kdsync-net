using System;

namespace Kdsync;

//
// 摘要:
//     Helper methods for throwing exceptions when preconditions are not met.
//
// 言论：
//     This class is used internally and by generated code; it is not particularly expected
//     to be used from application code, although nothing prevents it from being used
//     that way.
public static class ProtoPreconditions
{
    //
    // 摘要:
    //     Throws an ArgumentNullException if the given value is null, otherwise return
    //     the value to the caller.
    public static T CheckNotNull<T>(T value, string name) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(name);
        }

        return value;
    }

    //
    // 摘要:
    //     Throws an ArgumentNullException if the given value is null, otherwise return
    //     the value to the caller.
    //
    // 言论：
    //     This is equivalent to Google.Protobuf.ProtoPreconditions.CheckNotNull``1(``0,System.String)
    //     but without the type parameter constraint. In most cases, the constraint is useful
    //     to prevent you from calling CheckNotNull with a value type - but it gets in the
    //     way if either you want to use it with a nullable value type, or you want to use
    //     it with an unconstrained type parameter.
    internal static T CheckNotNullUnconstrained<T>(T value, string name)
    {
        if (value == null)
        {
            throw new ArgumentNullException(name);
        }

        return value;
    }
}