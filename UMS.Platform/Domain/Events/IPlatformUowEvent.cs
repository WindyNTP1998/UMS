namespace UMS.Platform.Domain.Events;

public interface IPlatformUowEvent
{
    public string SourceUowId { get; set; }
}