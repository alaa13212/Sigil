namespace Sigil.Domain.Entities;

public class EventTag
{
    public long EventId { get; set; }
    public int TagValueId { get; set; }
    
    public CapturedEvent? Event { get; set; }
    public TagValue? TagValue { get; set; }
}