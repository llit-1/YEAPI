-- Справочник: код отмены Яндекса → значение DELETED в SALEOBJECTSAGREGATORS.
-- Приложение только читает эту таблицу; новые коды добавляются вручную.
-- Скрипт можно запускать повторно: таблица создаётся, только если её нет,
-- а вставляются лишь отсутствующие коды.

IF OBJECT_ID(N'dbo.YECancelCodes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YECancelCodes
    (
        Code    nvarchar(100) NOT NULL CONSTRAINT PK_YECancelCodes PRIMARY KEY,
        Deleted int           NOT NULL
    );
END;
GO

INSERT INTO dbo.YECancelCodes (Code, Deleted)
SELECT v.Code, v.Deleted
FROM (VALUES
    (N'client.by_accident_order', 2),
    (N'client.self.accidentally_placed_order', 2),
    (N'client.self.did_not_pay', 2),
    (N'client.by_accident_order_from_app', 2),
    (N'client.self.accidentally_placed_order_from_app', 2),
    (N'client.refused_order_changes', 3),
    (N'place.late', 3),
    (N'place.missing_dish', 3),
    (N'place.other_reason', 3),
    (N'place.other_reason_from_app', 3),
    (N'place.high_load', 3),
    (N'place.closed_early', 3),
    (N'place.unable_to_call', 3),
    (N'place.wrong_order', 3),
    (N'place.technical_problem', 4),
    (N'place.everyday_problem', 4),
    (N'place.integration_failed', 4),
    (N'service.other', 4),
    (N'place.state_of_emergency', 4),
    (N'place.integration_other_reason', 4),
    (N'service.test_connection', 5),
    (N'client_no_show', 10),
    (N'courier.late', 10),
    (N'courier.some_other_reason', 10),
    (N'client.suspicious_activity', 10),
    (N'courier.not_found', 10),
    (N'client.some_other_reason', 10),
    (N'courier.refused_order', 10),
    (N'service.finalize.payment_to_partner', 10),
    (N'courier.cancellation_after_reassignment', 10),
    (N'client.changed_delivery_address', 10),
    (N'service.finalize.payment_to_partner_and_refund', 10),
    (N'service.recooking', 10)
) AS v (Code, Deleted)
WHERE NOT EXISTS (SELECT 1 FROM dbo.YECancelCodes c WHERE c.Code = v.Code);
GO

SELECT COUNT(*) AS CancelCodes FROM dbo.YECancelCodes;
GO
