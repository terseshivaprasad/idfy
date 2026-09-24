-- Log tables for Idfy.Api. Idempotent: safe to run on every deploy.

IF OBJECT_ID(N'dbo.ApiCallLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiCallLogs (
        Id            bigint IDENTITY  NOT NULL CONSTRAINT PK_ApiCallLogs PRIMARY KEY,
        CreatedAt     datetimeoffset   NOT NULL,
        TraceId       nvarchar(64)     NULL,
        TaskId        nvarchar(64)     NULL,
        GroupId       nvarchar(64)     NULL,
        Method        nvarchar(16)     NOT NULL,
        Url           nvarchar(2048)   NOT NULL,
        RequestBody   nvarchar(max)    NULL,
        StatusCode    int              NULL,
        ResponseBody  nvarchar(max)    NULL,
        DurationMs    bigint           NOT NULL,
        Exception     nvarchar(max)    NULL
    );
    CREATE INDEX IX_ApiCallLogs_CreatedAt ON dbo.ApiCallLogs (CreatedAt);
    CREATE INDEX IX_ApiCallLogs_TaskId    ON dbo.ApiCallLogs (TaskId);
    CREATE INDEX IX_ApiCallLogs_TraceId   ON dbo.ApiCallLogs (TraceId);
END;

IF OBJECT_ID(N'dbo.ErrorLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErrorLogs (
        Id            bigint IDENTITY  NOT NULL CONSTRAINT PK_ErrorLogs PRIMARY KEY,
        CreatedAt     datetimeoffset   NOT NULL,
        TraceId       nvarchar(64)     NULL,
        Method        nvarchar(16)     NULL,
        Path          nvarchar(2048)   NULL,
        ExceptionType nvarchar(512)    NOT NULL,
        Message       nvarchar(max)    NOT NULL,
        Details       nvarchar(max)    NOT NULL
    );
    CREATE INDEX IX_ErrorLogs_CreatedAt ON dbo.ErrorLogs (CreatedAt);
    CREATE INDEX IX_ErrorLogs_TraceId   ON dbo.ErrorLogs (TraceId);
END;
