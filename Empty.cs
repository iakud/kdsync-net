namespace Kdsync;

using Google.Protobuf;

public class Empty : IMessage
{
    public void MergeFrom(byte[] buffer)
    {
        CodedInputStream input = new CodedInputStream(buffer);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    public string ToString(string indent)
    {
        return "{}";
    }
}