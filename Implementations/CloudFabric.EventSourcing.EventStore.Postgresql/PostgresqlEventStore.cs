using System.Data;
using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;
using Npgsql;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStore : IEventStore
{
    private const int EVENTSTORE_TABLE_SCHEMA_VERSION = 1;
    private readonly string _connectionString;
    private readonly List<Func<IEvent, Task>> _eventAddedEventHandlers = new();
    private readonly string _eventsTableName;
    private readonly string _itemsTableName;

    public PostgresqlEventStore(string connectionString, string eventsTableName, string itemsTableName)
    {
        _connectionString = connectionString;
        _eventsTableName = eventsTableName;
        _itemsTableName = itemsTableName;
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);
    }

    public async Task DeleteAll(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var eventsTableCmd = new NpgsqlCommand($"DELETE FROM \"{_eventsTableName}\"", conn);

        try
        {
            await eventsTableCmd.ExecuteScalarAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState != PostgresErrorCodes.UndefinedTable)
            {
                throw;
            }
        }

        await using var itemsTableCmd = new NpgsqlCommand($"DELETE FROM \"{_itemsTableName}\"", conn);

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
    public async Task<bool> HardDeleteAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"DELETE FROM \"{_eventsTableName}\"" +
            $"WHERE stream_id = @streamId AND event_data->>'partitionKey' = @partitionKey",
            conn,
            transaction)
        {
            Parameters =
            {
                new("streamId", streamId),
                new("partitionKey", partitionKey)
            }
        };

        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
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
        
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<EventStream> LoadStreamAsyncOrThrowNotFound(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        var eventStream = await LoadStreamAsync(streamId, partitionKey, cancellationToken);

        if (!eventStream.Events.Any())
        {
            throw new NotFoundException();
        }

        return eventStream;
    }

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, stream_id, stream_version, event_type, event_data, user_info " +
            $"FROM \"{_eventsTableName}\" " +
            $"WHERE stream_id = @streamId AND event_data->>'partitionKey' = @partitionKey ORDER BY stream_version ASC", conn)
        {
            Parameters =
            {
                new("streamId", streamId),
                new("partitionKey", partitionKey)
            }
        };

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var version = 0;
            var events = new List<IEvent>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var eventWrapper = new EventWrapper()
                {
                    Id = reader.GetGuid("id"),
                    StreamInfo = new StreamInfo
                    {
                        Id = reader.GetGuid("stream_id"),
                        Version = reader.GetInt16("stream_version")
                    },
                    EventType = reader.GetString("event_type"),
                    EventData = JsonDocument.Parse(reader.GetString("event_data")).RootElement,
                    UserInfo = JsonDocument.Parse(reader.GetString("user_info")).RootElement,
                };

                version = eventWrapper.StreamInfo.Version;

                events.Add(eventWrapper.GetEvent());
            }

            return new EventStream(streamId, version, events);
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

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, int fromVersion, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, stream_id, stream_version, event_type, event_data, user_info, eventstore_schema_version " +
            $"FROM \"{_eventsTableName}\" WHERE stream_id = @streamId AND event_data->>'partitionKey' = @partitionKey AND stream_version >= @fromVersion", conn)
        {
            Parameters =
            {
                new("streamId", streamId),
                new("partitionKey", partitionKey)
            }
        };

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var version = 0;
        var events = new List<IEvent>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var streamInfo = JsonSerializer.Deserialize<StreamInfo>(reader.GetString(1), EventStoreSerializerOptions.Options);

            if(streamInfo == null) {
                throw new Exception("Failed to deserialize stream info");
            }

            var eventWrapper = new EventWrapper()
            {
                Id = reader.GetGuid(0),
                StreamInfo = streamInfo
            };

            version = eventWrapper.StreamInfo.Version;

            events.Add(eventWrapper.GetEvent());
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<List<IEvent>> LoadEventsAsync(string partitionKey, DateTime? dateFrom = null, DateTime? dateTo = null, CancellationToken cancellationToken = default)
    {        
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        string whereClause = $"event_data->>'partitionKey' = '{partitionKey}'";

        whereClause += dateFrom.HasValue
            ? $" && (event_data->>'timestamp')::timestamp without time zone >= '{dateFrom.Value:yyyy-MM-ddTHH:mm:ss.fffZ}'::timestamp without time zone "
            : "";
            
        whereClause += dateTo.HasValue
            ? $" && (event_data->>'timestamp')::timestamp without time zone <= '{dateTo.Value:yyyy-MM-ddTHH:mm:ss.fffZ}'::timestamp without time zone "
            : "";

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, event_type, event_data " +
            $"FROM \"{_eventsTableName}\" " +
            (!string.IsNullOrEmpty(whereClause) 
                ? $"WHERE {whereClause} " 
                : "") +
            $"ORDER BY stream_version ASC", conn);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var events = new List<IEvent>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var eventWrapper = new EventWrapper()
                {
                    Id = reader.GetGuid("id"),
                    EventType = reader.GetString("event_type"),
                    EventData = JsonDocument.Parse(reader.GetString("event_data")).RootElement
                };

                events.Add(eventWrapper.GetEvent());
            }

            return events;
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

    public async Task<bool> AppendToStreamAsync(EventUserInfo eventUserInfo, Guid streamId, int expectedVersion, IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.GroupBy(x => x.PartitionKey).Count() != 1)
        {
            throw new ArgumentException("Partition keys for all events in the stream must be the same");
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var transaction = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT MAX(stream_version) FROM \"{_eventsTableName}\" WHERE stream_id = @streamId", conn, transaction)
        {
            Parameters =
            {
                new("streamId", streamId)
            }
        };

        try
        {
            var version = await cmd.ExecuteScalarAsync(cancellationToken);

            if (version != null && version is not DBNull && (int)version != expectedVersion)
            {
                if(expectedVersion == 0) {
                    throw new Exception("Expected event stream to be empty but it already exists.");
                }

                throw new Exception("Event stream has new event which were not expected. This usually means that another thread/process appended events to the stream between read and write operations.");
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

        await using var batchInsert = new NpgsqlBatch(conn);

        foreach (var evt in events)
        {
            batchInsert.BatchCommands.Add(new NpgsqlBatchCommand($"" +
                $"INSERT INTO \"{_eventsTableName}\" " +
                $"(id, stream_id, stream_version, event_type, event_data, user_info, eventstore_schema_version) " +
                $"VALUES " +
                $"(@id, @stream_id, @stream_version, @event_type, @event_data, @user_info, @eventstore_schema_version)")
            {
                Parameters =
                {
                    new("id", Guid.NewGuid()),
                    new("stream_id", streamId),
                    new("stream_version", ++expectedVersion),
                    new("event_type", evt.GetType().AssemblyQualifiedName),
                    new NpgsqlParameter()
                    {
                        ParameterName = "event_data",
                        Value = JsonSerializer.Serialize(evt, evt.GetType(), EventStoreSerializerOptions.Options),
                        DataTypeName = "jsonb"
                    },
                    new NpgsqlParameter()
                    {
                        ParameterName = "user_info",
                        Value = JsonSerializer.Serialize(eventUserInfo, eventUserInfo.GetType(), EventStoreSerializerOptions.Options),
                        DataTypeName = "jsonb"
                    },
                    new NpgsqlParameter("eventstore_schema_version", EVENTSTORE_TABLE_SCHEMA_VERSION)
                }
            });
        }

        await batchInsert.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (var e in events)
        {
            foreach (var h in _eventAddedEventHandlers)
            {
                await h(e);
            }
        }

        return true;
    }

    public void SubscribeToEventAdded(Func<IEvent, Task> handler)
    {
        _eventAddedEventHandlers.Add(handler);
    }

    public void UnsubscribeFromEventAdded(Func<IEvent, Task> handler)
    {
        _eventAddedEventHandlers.Remove(handler);
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT 1 FROM \"{_eventsTableName}\"", conn)
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
                    $"CREATE TABLE \"{_eventsTableName}\" (" +
                    $"id uuid, " +
                    $"stream_id uuid, " +
                    $"stream_version integer, " +
                    $"event_type varchar(500), " +
                    $"event_data jsonb, " +
                    $"user_info jsonb, " +
                    $"eventstore_schema_version int NOT NULL" +
                    $");" +
                    $"CREATE INDEX \"{_eventsTableName}_stream_id_idx\" ON \"{_eventsTableName}\" (stream_id);" +
                    $"CREATE INDEX \"{_eventsTableName}_stream_id_with_partition_key_idx\" ON \"{_eventsTableName}\" (stream_id, ((event_data ->> 'partitionKey')::varchar(256)));"
                    
                , conn);

                await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using var cmdItemTable = new NpgsqlCommand(
            $"SELECT 1 FROM \"{_itemsTableName}\"", conn)
        {
        };

        try
        {
            await cmdItemTable.ExecuteScalarAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {                
                await using var createTableCommand = new NpgsqlCommand(
                    $"CREATE TABLE \"{_itemsTableName}\" (" +
                    $"id varchar(100) UNIQUE NOT NULL, " +
                    $"partition_key varchar(100) NOT NULL, " +
                    $"data jsonb" +
                    $");" +
                    $"CREATE INDEX \"{_itemsTableName}_id_idx\" ON \"{_itemsTableName}\" (id);" +
                    $"CREATE INDEX \"{_itemsTableName}_id_with_partition_key_idx\" ON \"{_itemsTableName}\" (id, partition_key);"

                , conn);

                await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    #region Snapshot Functionality

    // private async Task<TSnapshot> LoadSnapshotAsync<TSnapshot>(string streamId)
    // {
    //     Container container = _client.GetContainer(_databaseName, _tableName);
    //
    //     PartitionKey partitionKey = new PartitionKey(streamId);
    //
    //     var response = await container.ReadItemAsync<TSnapshot>(streamId, partitionKey);
    //     if (response.StatusCode == HttpStatusCode.OK)
    //     {
    //         return response.Resource;
    //     }
    //
    //     return default(TSnapshot);
    // }

    #endregion

    #region Item Functionality

    public async Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO \"{_itemsTableName}\" " +
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT * FROM \"{_itemsTableName}\" " +
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

    #endregion
}