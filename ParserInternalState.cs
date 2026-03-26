namespace Kdsync;

internal struct ParserInternalState
{
    internal int bufferPos;

    internal int bufferSize;

    internal int bufferSizeAfterLimit;

    internal int currentLimit;

    internal int totalBytesRetired;

    internal int recursionDepth;

    internal SegmentedBufferHelper segmentedBufferHelper;

    internal uint lastTag;

    internal uint nextTag;

    internal bool hasNextTag;

    internal int sizeLimit;

    internal int recursionLimit;
}