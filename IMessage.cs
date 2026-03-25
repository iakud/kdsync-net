namespace Kdsync;

public interface IMessage
{
    public string ToString(string indent);

    void MergeFrom(CodedInputStream input);
    void WriteTo(CodedOutputStream output);
    int CalculateSize();
}