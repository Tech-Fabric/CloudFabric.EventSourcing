using Npgsql;
using System.Data;
using System.Text.Json;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlStore: IStore
{
    private readonly PostgresqlEventStoreConnectionInformation _connectionInformation;
    private readonly IPostgresqlEventStoreConnectionInformationProvider? _connectionInformationProvider = null;

    private PostgresqlEventStoreConnectionInformation ConnectionInformation
    {
        get
        {
            if (_connectionInformationProvider != null)
            {
                return _connectionInformationProvider.GetConnectionInformation();
            }
            else
            {
                return _connectionInformation;
            }
        }
    }

    public PostgresqlStore(string connectionString, string itemsTableName)
    {
        _connectionInformation = new PostgresqlEventStoreConnectionInformation()
        {
            ConnectionString = connectionString,
            ItemsTableName = itemsTableName
        };
    }

    public PostgresqlStore(IPostgresqlEventStoreConnectionInformationProvider connectionInformationProvider)
    {
        _connectionInformationProvider = connectionInformationProvider;
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);
    }

    public async Task DeleteAll(CancellationToken cancellationToken = default)
    {
        var connectionInformation = ConnectionInformation;

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync();

        await using var itemsTableCmd = new NpgsqlCommand($"DELETE FROM \"{connectionInformation.ItemsTableName}\"", conn);

        try
        {
            await itemsTableCmd.ExecuteScalarAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState != PostgresErrorCodes.UndefinedTable)
            {
                throw;
            }
        }
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
    {
        var connectionInformation = ConnectionInformation;

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT 1 FROM \"{connectionInformation.ItemsTableName}\"", conn)
        {
        };

        try
        {
            await cmd.ExecuteScalarAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                await using var createTableCommand = new NpgsqlCommand(
                    $"CREATE TABLE \"{connectionInformation.ItemsTableName}\" (" +
                    $"id varchar(100) UNIQUE NOT NULL, " +
                    $"partition_key varchar(100) NOT NULL, " +
                    $"data jsonb" +
                    $");" +
                    $"CREATE INDEX \"{connectionInformation.ItemsTableName}_id_idx\" ON \"{connectionInformation.ItemsTableName}\" (id);" +
                    $"CREATE INDEX \"{connectionInformation.ItemsTableName}_id_with_partition_key_idx\" ON \"{connectionInformation.ItemsTableName}\" (id, partition_key);"
                    , conn);

                await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default)
    {
        var connectionInformation = ConnectionInformation;

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO \"{connectionInformation.ItemsTableName}\" " +
            $"(id, partition_key, data) " +
            $"VALUES" +
            $"(@id, @partition_key, @data)" +
            $"ON CONFLICT (id) " +
            $"DO UPDATE " +
            $"SET data = @data, partition_key = @partition_key; "
            , conn
        )
        {
            Parameters =
            {
                new("id", id),
                new("partition_key", partitionKey),
                new NpgsqlParameter()
                {
                    ParameterName = "data",
                    Value = JsonSerializer.Serialize(item, EventStoreSerializerOptions.Options),
                    DataTypeName = "jsonb"
                }
            }
        };

        try
        {
            int insertItemResult = await cmd.ExecuteNonQueryAsync(cancellationToken);

            if (insertItemResult == -1)
            {
                throw new Exception("Upsert item failed.");
            }
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                throw new Exception(
                    "EventStore table not found, please make sure to call Initialize() on event store first.",
                    ex);
            }

            throw;
        }
    }

    public async Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        var connectionInformation = ConnectionInformation;

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT * FROM \"{connectionInformation.ItemsTableName}\" " +
            $"WHERE id = @id AND partition_key = @partition_key LIMIT 1; "
            , conn)
        {
            Parameters =
                {
                    new("id", id),
                    new("partition_key", partitionKey)
                }
        };

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var item = JsonDocument.Parse(reader.GetString("data")).RootElement;

                return JsonSerializer.Deserialize<T>(item, EventStoreSerializerOptions.Options);
            }

            return default;
        }

        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                throw new Exception(
                    "EventStore table not found, please make sure to call Initialize() on event store first.",
                    ex);
            }

            throw;
        }
    }
}
