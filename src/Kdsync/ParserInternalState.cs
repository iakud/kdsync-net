namespace Kdsync
{
    internal struct ParserInternalState
    {
        internal int bufferPos;

        internal int bufferSize;

        internal int currentLimit;

        internal int recursionDepth;

        internal uint lastTag;

        internal uint nextTag;

        internal bool hasNextTag;

        internal int recursionLimit;
    }
}