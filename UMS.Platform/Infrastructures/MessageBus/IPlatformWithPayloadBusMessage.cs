namespace UMS.Platform.Infrastructures.MessageBus;

public interface IPlatformWithPayloadBusMessage<TPayload> : IPlatformMessage where TPayload : class, new()
{
    public TPayload Payload { get; set; }
}