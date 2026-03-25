namespace Kdsync;

using Google.Protobuf;

public class Empty : IMessage
{
    public void MergeFrom(CodedInputStream input)
    {
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

    public void WriteTo(CodedOutputStream output)
    {
        
    }
    public int CalculateSize()
    {
        return 0;
    }

    public string ToString(string indent)
    {
        return "{}";
    }
}