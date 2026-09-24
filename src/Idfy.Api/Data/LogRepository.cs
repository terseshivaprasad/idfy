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

    /// <summary>Inserts a batch in one transaction.</summary>
    public async Task InsertAsync(IReadOnlyCollection<ApiCallLog> apiCalls, IReadOnlyCollection<ErrorLog> errors, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Dapper runs the statement once per element of a collection parameter.
        if (apiCalls.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(InsertApiCallLog, apiCalls, tx, cancellationToken: ct));
        if (errors.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(InsertErrorLog, errors, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }
}
