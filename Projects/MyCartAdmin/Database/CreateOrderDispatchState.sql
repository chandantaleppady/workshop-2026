CREATE TABLE dbo.OrderDispatchState
(
    OrderId uniqueidentifier NOT NULL
        CONSTRAINT PK_OrderDispatchState PRIMARY KEY,
    DispatchStatus nvarchar(20) NOT NULL
        CONSTRAINT DF_OrderDispatchState_Status DEFAULT N'Pending',
    AttemptStartedAtUtc datetime2(0) NULL,
    SentToWarehouseAtUtc datetime2(0) NULL,
    WarehouseBlobName nvarchar(500) NULL,
    LastDispatchError nvarchar(1000) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_OrderDispatchState_Status
        CHECK (DispatchStatus IN (N'Pending', N'Sending', N'Sent', N'Failed'))
);

CREATE INDEX IX_OrderDispatchState_Status ON dbo.OrderDispatchState (DispatchStatus);