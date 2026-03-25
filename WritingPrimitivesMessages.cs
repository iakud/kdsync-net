
using System.Runtime.CompilerServices;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     Writing messages / groups.
[SecuritySafeCritical]
internal static class WritingPrimitivesMessages
{
    //
    // 摘要:
    //     Writes a message, without a tag. The data is length-prefixed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteMessage(ref WriteContext ctx, IMessage value)
    {
        WritingPrimitives.WriteLength(ref ctx.buffer, ref ctx.state, value.CalculateSize());
        WriteRawMessage(ref ctx, value);
    }

    //
    // 摘要:
    //     Writes a group, without a tag.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteGroup(ref WriteContext ctx, IMessage value)
    {
        WriteRawMessage(ref ctx, value);
    }

    //
    // 摘要:
    //     Writes a message, without a tag. Message will be written without a length prefix.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteRawMessage(ref WriteContext ctx, IMessage message)
    {
        if (ctx.state.CodedOutputStream == null)
        {
            throw new InvalidException("Message " + message.GetType().Name + " doesn't provide the generated method that enables WriteContext-based serialization. You might need to regenerate the generated protobuf code.");
        }

        ctx.CopyStateTo(ctx.state.CodedOutputStream);
        try
        {
            message.WriteTo(ctx.state.CodedOutputStream);
        }
        finally
        {
            ctx.LoadStateFrom(ctx.state.CodedOutputStream);
        }
    }
}