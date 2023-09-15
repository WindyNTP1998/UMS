using UMS.Platform.Common.Dtos;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Validations;

namespace UMS.Platform.Infrastructures.PushNotification;

public class PushNotificationPlatformMessage : IPlatformDto<PushNotificationPlatformMessage>
{
    public string DeviceId { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public int? Badge { get; set; }
    public Dictionary<string, string> Data { get; set; }

    public PlatformValidationResult<PushNotificationPlatformMessage> Validate()
    {
        return PlatformValidationResult.Valid(this)
            .And(p => DeviceId.IsNotNullOrEmpty(), "DeviceId is missing")
            .And(p => Title.IsNotNullOrEmpty(), "Title is missing")
            .And(p => Body.IsNotNullOrEmpty(), "Body is missing");
    }
}