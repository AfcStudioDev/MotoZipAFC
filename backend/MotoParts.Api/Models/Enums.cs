using System.ComponentModel;

namespace MotoParts.Api.Models;

public enum DeliveryStatusEnum
{
    [Description("created")]
    created = 0,
    [Description("sent")]
    sent = 1,
    [Description("completed")]
    completed = 2,
    [Description("canceled")]
    canceled = 3
}

public enum PaymentStatusEnum
{
    [Description("pending")]
    pending = 0,
    [Description("waiting for capture")]
    waiting_for_capture = 1,
    [Description("succeeded")]
    succeeded = 2,
    [Description("canceled")]
    canceled = 3
}