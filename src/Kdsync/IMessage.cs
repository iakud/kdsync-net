namespace Kdsync
{
    public interface IMessage
    {
        void MergeFrom(ref ParseContext ctx);
        void Write(JsonWriter writer);
    }
}