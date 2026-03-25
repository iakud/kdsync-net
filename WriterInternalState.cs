using Google.Protobuf;

namespace Kdsync;

internal struct WriterInternalState
{
    internal int limit;

    internal int position;

    internal WriteBufferHelper writeBufferHelper;

    internal CodedOutputStream CodedOutputStream => writeBufferHelper.CodedOutputStream;
}