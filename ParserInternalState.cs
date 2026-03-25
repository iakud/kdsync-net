using Google.Protobuf;

namespace Kdsync;

internal struct ParserInternalState
{
    //
    // 摘要:
    //     The position within the current buffer (i.e. the next byte to read)
    internal int bufferPos;

    //
    // 摘要:
    //     Size of the current buffer
    internal int bufferSize;

    //
    // 摘要:
    //     If we are currently inside a length-delimited block, this is the number of bytes
    //     in the buffer that are still available once we leave the delimited block.
    internal int bufferSizeAfterLimit;

    //
    // 摘要:
    //     The absolute position of the end of the current length-delimited block (including
    //     totalBytesRetired)
    internal int currentLimit;

    //
    // 摘要:
    //     The total number of consumed before the start of the current buffer. The total
    //     bytes read up to the current position can be computed as totalBytesRetired +
    //     bufferPos.
    internal int totalBytesRetired;

    internal int recursionDepth;

    internal SegmentedBufferHelper segmentedBufferHelper;

    //
    // 摘要:
    //     The last tag we read. 0 indicates we've read to the end of the stream (or haven't
    //     read anything yet).
    internal uint lastTag;

    //
    // 摘要:
    //     The next tag, used to store the value read by PeekTag.
    internal uint nextTag;

    internal bool hasNextTag;

    internal int sizeLimit;

    internal int recursionLimit;

    internal CodedInputStream CodedInputStream => segmentedBufferHelper.CodedInputStream;

    //
    // 摘要:
    //     Internal-only property; when set to true, unknown fields will be discarded while
    //     parsing.
    internal bool DiscardUnknownFields { get; set; }

    //
    // 摘要:
    //     Internal-only property; provides extension identifiers to compatible messages
    //     while parsing.
    internal ExtensionRegistry ExtensionRegistry { get; set; }
}