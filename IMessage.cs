namespace Kdsync;

public interface IMessage
{
    void MergeFrom(ref ParseContext ctx);
    void WriteTo(CodedOutputStream output);
    int CalculateSize();
    IEnumerable<KeyValuePair<string, object>> GetFields();
}