using System;

namespace Kdsync
{
    public interface IMessage
    {
        void MergeFrom(ref ParseContext ctx);
        void RaiseChanged();
        void ClearChanged();
        bool CheckChangedParent();
        void SetChangedParent(Action changedParent);
        void ClearChangedParent();
        void Write(JsonWriter writer);
    }
}