using System;
using System.Runtime.CompilerServices;
using System.Security;

namespace Kdsync
{
    [SecuritySafeCritical]
    internal struct SegmentedBufferHelper
    {
        public static int PushLimit(ref ParserInternalState state, int byteLimit)
        {
            if (byteLimit < 0)
            {
                throw InvalidException.NegativeSize();
            }

            byteLimit += state.bufferPos;
            int currentLimit = state.currentLimit;
            if (byteLimit > currentLimit)
            {
                throw InvalidException.TruncatedMessage();
            }

            state.currentLimit = byteLimit;
            return currentLimit;
        }

        public static void PopLimit(ref ParserInternalState state, int oldLimit)
        {
            state.currentLimit = oldLimit;
        }

        public static bool IsReachedLimit(ref ParserInternalState state)
        {
            return state.bufferPos >= state.currentLimit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAtEnd(ref ReadOnlySpan<byte> buffer, ref ParserInternalState state)
        {
            return state.bufferPos == state.bufferSize;
        }

        private static void CheckCurrentBufferIsEmpty(ref ParserInternalState state)
        {
            if (state.bufferPos < state.bufferSize)
            {
                throw new InvalidOperationException("RefillBuffer() called when buffer wasn't empty.");
            }
        }
    }
}