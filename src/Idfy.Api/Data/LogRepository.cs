using System.Data;
using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Idfy.Api.Data;

/// <summary>Writes log rows to SQL Server. Schema: db/log-tables.sql.</summary>
public sealed class LogRepository(string connectionString)
{
    /// <summary>Bulk-inserts each batch: one round trip per table via SqlBulkCopy, all in one transaction.</summary>
    public async Task InsertAsync(
        IReadOnlyCollection<ApiCallLog> apiCalls,
        IReadOnlyCollection<RequestLog> requests,
        IReadOnlyCollection<ErrorLog> errors,
        IReadOnlyCollection<IdfyTaskLog> tasks,
        CancellationToken ct)
    {
        if (apiCalls.Count == 0 && requests.Count == 0 && errors.Count == 0 && tasks.Count == 0)
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        await BulkInsertAsync(connection, tx, "IdfyApiCallLogs", apiCalls, ct);
        await BulkInsertAsync(connection, tx, "IdfyRequestLogs", requests, ct);
        await BulkInsertAsync(connection, tx, "IdfyErrorLogs", errors, ct);
        await BulkInsertAsync(connection, tx, "IdfyTasks", tasks, ct);

        await tx.CommitAsync(ct);
    }

    private static async Task BulkInsertAsync<T>(
        SqlConnection connection, SqlTransaction tx, string table, IReadOnlyCollection<T> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        var props = EntityColumns<T>.Properties;
        using var dataTable = new DataTable();
        foreach (var p in props)
            dataTable.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);

        foreach (var row in rows)
        {
            var values = new object?[props.Length];
            for (var i = 0; i < props.Length; i++)
                values[i] = props[i].GetValue(row) ?? DBNull.Value;
            dataTable.Rows.Add(values);
        }

        // Identity Id column is not mapped, so SQL Server generates it.
        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, tx) { DestinationTableName = table };
        foreach (var p in props)
            bulk.ColumnMappings.Add(p.Name, p.Name);

        await bulk.WriteToServerAsync(dataTable, ct);
    }

    private static class EntityColumns<T>
    {
        public static readonly PropertyInfo[] Properties =
            typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    /// <summary>Verifies connectivity for the health check.</summary>
    public async Task PingAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
    }

    /// <summary>Deletes log rows older than the cutoff, in capped batches to avoid long locks.</summary>
    /// <returns>Total rows deleted across all log tables.</returns>
    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var total = 0;
        foreach (var table in new[] { "IdfyApiCallLogs", "IdfyRequestLogs", "IdfyErrorLogs", "IdfyTasks" })
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
