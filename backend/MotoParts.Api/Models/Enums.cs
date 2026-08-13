using System.ComponentModel;

namespace MotoParts.Api.Models;

/// <summary>
/// Значения соответствуют реальным Id из таблицы DeliveryStatuses (см. DbSeeder — created=1,
/// sent=2, completed=3, canceled=4). Раньше enum был 0-based и не совпадал с сидом: код,
/// использующий (short)DeliveryStatusEnum.created, писал 0 — значение, которого в таблице
/// не существует (FK либо падал, либо статус молча оставался null и заказ пропадал из
/// списка отправлений). Не меняйте нумерацию без синхронизации с DbSeeder.
/// </summary>
public enum DeliveryStatusEnum
{
    [Description("created")]
    created = 1,
    [Description("sent")]
    sent = 2,
    [Description("completed")]
    completed = 3,
    [Description("canceled")]
    canceled = 4
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

/// <summary>Категории операций — соответствуют строкам справочника OperationType.</summary>
public enum OperationTypeEnum : short
{
    [Description("Движение товара")]
    StockMovement = 1,
    [Description("Переоценка")]
    Repricing = 2,
    [Description("Аудит")]
    Audit = 3
}

/// <summary>Конкретные операции — соответствуют строкам справочника Operations.</summary>
public enum OperationEnum : short
{
    [Description("Приход")]
    Income = 1,
    [Description("Продажа")]
    Sale = 2,
    [Description("Возврат")]
    Refund = 3,
    [Description("Списание")]
    WriteOff = 4,
    [Description("Коррекция остатка")]
    Correction = 5,
    [Description("Наценка")]
    Markup = 6,
    [Description("Уценка")]
    Markdown = 7,
    [Description("Прочее")]
    Other = 8
}