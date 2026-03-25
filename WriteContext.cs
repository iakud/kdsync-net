using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security;
using Google.Protobuf;

namespace Kdsync;

//
// 摘要:
//     An opaque struct that represents the current serialization state and is passed
//     along as the serialization proceeds. All the public methods are intended to be
//     invoked only by the generated code, users should never invoke them directly.
[SecuritySafeCritical]
public ref struct WriteContext
{
    internal Span<byte> buffer;

    internal WriterInternalState state;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ref Span<byte> buffer, ref WriterInternalState state, out WriteContext ctx)
    {
        ctx.buffer = buffer;
        ctx.state = state;
    }

    //
    // 摘要:
    //     Creates a WriteContext instance from CodedOutputStream. WARNING: internally this
    //     copies the CodedOutputStream's state, so after done with the WriteContext, the
    //     CodedOutputStream's state needs to be updated.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(CodedOutputStream output, out WriteContext ctx)
    {
        ctx.buffer = new Span<byte>(output.InternalBuffer);
        ctx.state = output.InternalState;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(IBufferWriter<byte> output, out WriteContext ctx)
    {
        ctx.buffer = default(Span<byte>);
        ctx.state = default(WriterInternalState);
        WriteBufferHelper.Initialize(output, out ctx.state.writeBufferHelper, out ctx.buffer);
        ctx.state.limit = ctx.buffer.Length;
        ctx.state.position = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Initialize(ref Span<byte> buffer, out WriteContext ctx)
    {
        ctx.buffer = buffer;
        ctx.state = default(WriterInternalState);
        ctx.state.limit = ctx.buffer.Length;
        ctx.state.position = 0;
        WriteBufferHelper.InitializeNonRefreshable(out ctx.state.writeBufferHelper);
    }

    //
    // 摘要:
    //     Writes a double field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteDouble(double value)
    {
        WritingPrimitives.WriteDouble(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a float field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteFloat(float value)
    {
        WritingPrimitives.WriteFloat(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a uint64 field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteUInt64(ulong value)
    {
        WritingPrimitives.WriteUInt64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an int64 field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteInt64(long value)
    {
        WritingPrimitives.WriteInt64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an int32 field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteInt32(int value)
    {
        WritingPrimitives.WriteInt32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a fixed64 field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteFixed64(ulong value)
    {
        WritingPrimitives.WriteFixed64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a fixed32 field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteFixed32(uint value)
    {
        WritingPrimitives.WriteFixed32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a bool field value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteBool(bool value)
    {
        WritingPrimitives.WriteBool(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a string field value, without a tag. The data is length-prefixed.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteString(string value)
    {
        WritingPrimitives.WriteString(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a message, without a tag. The data is length-prefixed.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteMessage(IMessage value)
    {
        WritingPrimitivesMessages.WriteMessage(ref this, value);
    }

    //
    // 摘要:
    //     Writes a group, without a tag, to the stream.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteGroup(IMessage value)
    {
        WritingPrimitivesMessages.WriteGroup(ref this, value);
    }

    //
    // 摘要:
    //     Write a byte string, without a tag, to the stream. The data is length-prefixed.
    //
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteBytes(byte[] value)
    {
        WritingPrimitives.WriteBytes(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a uint32 value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteUInt32(uint value)
    {
        WritingPrimitives.WriteUInt32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an enum value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteEnum(int value)
    {
        WritingPrimitives.WriteEnum(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sfixed32 value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write.
    public void WriteSFixed32(int value)
    {
        WritingPrimitives.WriteSFixed32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sfixed64 value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteSFixed64(long value)
    {
        WritingPrimitives.WriteSFixed64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sint32 value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteSInt32(int value)
    {
        WritingPrimitives.WriteSInt32(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes an sint64 value, without a tag.
    //
    // 参数:
    //   value:
    //     The value to write
    public void WriteSInt64(long value)
    {
        WritingPrimitives.WriteSInt64(ref buffer, ref state, value);
    }

    //
    // 摘要:
    //     Writes a length (in bytes) for length-delimited data.
    //
    // 参数:
    //   length:
    //     Length value, in bytes.
    //
    // 言论：
    //     This method simply writes a rawint, but exists for clarity in calling code.
    public void WriteLength(int length)
    {
        WritingPrimitives.WriteLength(ref buffer, ref state, length);
    }

    //
    // 摘要:
    //     Encodes and writes a tag.
    //
    // 参数:
    //   fieldNumber:
    //     The number of the field to write the tag for
    //
    //   type:
    //     The wire format type of the tag to write
    public void WriteTag(int fieldNumber, WireFormat.WireType type)
    {
        WritingPrimitives.WriteTag(ref buffer, ref state, fieldNumber, type);
    }

    //
    // 摘要:
    //     Writes an already-encoded tag.
    //
    // 参数:
    //   tag:
    //     The encoded tag
    public void WriteTag(uint tag)
    {
        WritingPrimitives.WriteTag(ref buffer, ref state, tag);
    }

    //
    // 摘要:
    //     Writes the given single-byte tag.
    //
    // 参数:
    //   b1:
    //     The encoded tag
    public void WriteRawTag(byte b1)
    {
        WritingPrimitives.WriteRawTag(ref buffer, ref state, b1);
    }

    //
    // 摘要:
    //     Writes the given two-byte tag.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2)
    {
        WritingPrimitives.WriteRawTag(ref buffer, ref state, b1, b2);
    }

    //
    // 摘要:
    //     Writes the given three-byte tag.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    //
    //   b3:
    //     The third byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2, byte b3)
    {
        WritingPrimitives.WriteRawTag(ref buffer, ref state, b1, b2, b3);
    }

    //
    // 摘要:
    //     Writes the given four-byte tag.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    //
    //   b3:
    //     The third byte of the encoded tag
    //
    //   b4:
    //     The fourth byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2, byte b3, byte b4)
    {
        WritingPrimitives.WriteRawTag(ref buffer, ref state, b1, b2, b3, b4);
    }

    //
    // 摘要:
    //     Writes the given five-byte tag.
    //
    // 参数:
    //   b1:
    //     The first byte of the encoded tag
    //
    //   b2:
    //     The second byte of the encoded tag
    //
    //   b3:
    //     The third byte of the encoded tag
    //
    //   b4:
    //     The fourth byte of the encoded tag
    //
    //   b5:
    //     The fifth byte of the encoded tag
    public void WriteRawTag(byte b1, byte b2, byte b3, byte b4, byte b5)
    {
        WritingPrimitives.WriteRawTag(ref buffer, ref state, b1, b2, b3, b4, b5);
    }

    internal void Flush()
    {
        WriteBufferHelper.Flush(ref buffer, ref state);
    }

    internal void CheckNoSpaceLeft()
    {
        WriteBufferHelper.CheckNoSpaceLeft(ref state);
    }

    internal void CopyStateTo(CodedOutputStream output)
    {
        output.InternalState = state;
    }

    internal void LoadStateFrom(CodedOutputStream output)
    {
        state = output.InternalState;
    }
}