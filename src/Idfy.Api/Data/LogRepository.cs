using Dapper;
using Microsoft.Data.SqlClient;

namespace Idfy.Api.Data;

/// <summary>Writes log rows to SQL Server with Dapper. Schema: db/log-tables.sql.</summary>
public sealed class LogRepository(string connectionString)
{
    private const string InsertApiCallLog = """
        INSERT INTO ApiCallLogs
            (CreatedAt, TraceId, TaskId, GroupId, Method, Url, RequestBody, StatusCode, ResponseBody, DurationMs, Exception)
        VALUES
            (@CreatedAt, @TraceId, @TaskId, @GroupId, @Method, @Url, @RequestBody, @StatusCode, @ResponseBody, @DurationMs, @Exception)
        """;

    private const string InsertErrorLog = """
        INSERT INTO ErrorLogs
            (CreatedAt, TraceId, Method, Path, ExceptionType, Message, Details)
        VALUES
            (@CreatedAt, @TraceId, @Method, @Path, @ExceptionType, @Message, @Details)
        """;

    private const string InsertIdfyTask = """
        INSERT INTO IdfyTasks
            (CreatedAt, TraceId, TaskId, GroupId, RequestId, TaskType, Action, Status, HttpStatus, ErrorCode, DurationMs, IdfyCreatedAt, IdfyCompletedAt)
        VALUES
            (@CreatedAt, @TraceId, @TaskId, @GroupId, @RequestId, @TaskType, @Action, @Status, @HttpStatus, @ErrorCode, @DurationMs, @IdfyCreatedAt, @IdfyCompletedAt)
        """;

    /// <summary>Inserts a batch in one transaction.</summary>
    public async Task InsertAsync(
        IReadOnlyCollection<ApiCallLog> apiCalls,
        IReadOnlyCollection<ErrorLog> errors,
        IReadOnlyCollection<IdfyTaskLog> tasks,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Dapper runs the statement once per element of a collection parameter.
        if (apiCalls.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(InsertApiCallLog, apiCalls, tx, cancellationToken: ct));
        if (errors.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(InsertErrorLog, errors, tx, cancellationToken: ct));
        if (tasks.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(InsertIdfyTask, tasks, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }

    /// <summary>Verifies connectivity for the health check.</summary>
    public async Task PingAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
    }

    /// <summary>Deletes log rows older than the cutoff, in capped batches to avoid long locks.</summary>
    /// <returns>Total rows deleted across both tables.</returns>
    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var total = 0;
        foreach (var table in new[] { "ApiCallLogs", "ErrorLogs", "IdfyTasks" })
        {
            int deleted;
            do
            {
                deleted = await connection.ExecuteAsync(new CommandDefinition(
                    $"DELETE TOP (@batchSize) FROM {table} WHERE CreatedAt < @cutoff",
                    new { batchSize, cutoff }, cancellationToken: ct));
                total += deleted;
            }
            while (deleted == batchSize && !ct.IsCancellationRequested);
        }

        return total;
    }
}
