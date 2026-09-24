-- Log tables for Idfy.Api. Idempotent: safe to run on every deploy.

IF OBJECT_ID(N'dbo.IdfyApiCallLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdfyApiCallLogs (
        Id            bigint IDENTITY  NOT NULL CONSTRAINT PK_IdfyApiCallLogs PRIMARY KEY,
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
    CREATE INDEX IX_IdfyApiCallLogs_CreatedAt ON dbo.IdfyApiCallLogs (CreatedAt);
    CREATE INDEX IX_IdfyApiCallLogs_TaskId    ON dbo.IdfyApiCallLogs (TaskId);
    CREATE INDEX IX_IdfyApiCallLogs_TraceId   ON dbo.IdfyApiCallLogs (TraceId);
END;

-- Structured, PII-free record per IDfy task (queryable domain view of the raw call log).
IF OBJECT_ID(N'dbo.IdfyTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdfyTasks (
        Id              bigint IDENTITY  NOT NULL CONSTRAINT PK_IdfyTasks PRIMARY KEY,
        CreatedAt       datetimeoffset   NOT NULL,
        TraceId         nvarchar(64)     NULL,
        TaskId          nvarchar(64)     NULL,
        GroupId         nvarchar(64)     NULL,
        RequestId       nvarchar(64)     NULL,
        TaskType        nvarchar(64)     NULL,
        Action          nvarchar(32)     NULL,
        Status          nvarchar(32)     NULL,
        HttpStatus      int              NULL,
        ErrorCode       nvarchar(64)     NULL,
        DurationMs      bigint           NOT NULL,
        IdfyCreatedAt   datetimeoffset   NULL,
        IdfyCompletedAt datetimeoffset   NULL
    );
    CREATE INDEX IX_IdfyTasks_CreatedAt ON dbo.IdfyTasks (CreatedAt);
    CREATE INDEX IX_IdfyTasks_TaskId    ON dbo.IdfyTasks (TaskId);
    CREATE INDEX IX_IdfyTasks_TaskType  ON dbo.IdfyTasks (TaskType);
    CREATE INDEX IX_IdfyTasks_Status    ON dbo.IdfyTasks (Status);
END;

IF OBJECT_ID(N'dbo.IdfyErrorLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdfyErrorLogs (
        Id            bigint IDENTITY  NOT NULL CONSTRAINT PK_IdfyErrorLogs PRIMARY KEY,
        CreatedAt     datetimeoffset   NOT NULL,
        TraceId       nvarchar(64)     NULL,
        Method        nvarchar(16)     NULL,
        Path          nvarchar(2048)   NULL,
        ExceptionType nvarchar(512)    NOT NULL,
        Message       nvarchar(max)    NOT NULL,
        Details       nvarchar(max)    NOT NULL
    );
    CREATE INDEX IX_IdfyErrorLogs_CreatedAt ON dbo.IdfyErrorLogs (CreatedAt);
    CREATE INDEX IX_IdfyErrorLogs_TraceId   ON dbo.IdfyErrorLogs (TraceId);
END;
