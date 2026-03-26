namespace Kdsync;

public interface IMessage
{
    public string ToString(string indent);

    void MergeFrom(ref ParseContext ctx);
    void WriteTo(CodedOutputStream output);
    int CalculateSize();
}