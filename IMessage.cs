namespace Kdsync;

public interface IMessage
{
    void MergeFrom(ref ParseContext ctx);
    IEnumerable<KeyValuePair<string, object>> GetFields();
}