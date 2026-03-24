namespace Kdsync;

public interface IMessage
{
    public void MergeFrom(byte[] buffer);

    public string ToString(string indent);
}