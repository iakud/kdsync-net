namespace Kdsync;

public class Empty : IMessage
{
    public void MergeFrom(ref ParseContext ctx)
    {
        uint tag;
        while ((tag = ctx.ReadTag()) != 0)
        {
            var num = WireFormat.GetTagFieldNumber(tag);
            switch (num)
            {
                default:
                    ctx.SkipLastField();
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