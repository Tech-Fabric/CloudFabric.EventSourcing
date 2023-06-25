using System.Data;
using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;
using Npgsql;

namespace CloudFabric.EventSourcing.EventStore.Postgresql;

public class PostgresqlEventStore : IEventStore
{
    private const int EVENTSTORE_TABLE_SCHEMA_VERSION = 1;
    
    private readonly List<Func<IEvent, Task>> _eventAddedEventHandlers = new();
    
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

    public PostgresqlEventStore(string connectionString, string tableName)
    {
        _connectionInformation = new PostgresqlEventStoreConnectionInformation()
        {
            ConnectionString = connectionString,
            TableName = tableName
        };
    }

    public PostgresqlEventStore(IPostgresqlEventStoreConnectionInformationProvider connectionInformationProvider)
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

        await using var cmd = new NpgsqlCommand($"DELETE FROM \"{connectionInformation.TableName}\"", conn);

        await cmd.ExecuteScalarAsync(cancellationToken);
    }
    public async Task<bool> HardDeleteAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        var connectionInformation = ConnectionInformation;
        
        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"DELETE FROM \"{connectionInformation.TableName}\"" +
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
        var connectionInformation = ConnectionInformation;
        
        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, stream_id, stream_version, event_type, event_data, user_info " +
            $"FROM \"{connectionInformation.TableName}\" " +
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
        var connectionInformation = ConnectionInformation;
        
        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, stream_id, stream_version, event_type, event_data, user_info, eventstore_schema_version " +
            $"FROM \"{connectionInformation.TableName}\" " +
            $"WHERE stream_id = @streamId AND event_data->>'partitionKey' = @partitionKey AND stream_version >= @fromVersion", conn)
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
            var streamInfo = JsonSerializer.Deserialize<StreamInfo>(reader.GetString(1), EventSerializerOptions.Options);

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

    public async Task<List<IEvent>> LoadEventsAsync(
        string? partitionKey, 
        DateTime? dateFrom = null, 
        int limit = 250, 
        CancellationToken cancellationToken = default
    ) {
        var connectionInformation = ConnectionInformation;
        
        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        string whereClause = "";

        if (!string.IsNullOrEmpty(partitionKey))
        {
            whereClause += $"event_data->>'partitionKey' = '{partitionKey}'";
        }

        whereClause += dateFrom.HasValue
            ? $" AND (event_data->>'timestamp')::timestamp without time zone >= '{dateFrom.Value:yyyy-MM-ddTHH:mm:ss.fffZ}'::timestamp without time zone "
            : "";

        await using var cmd = new NpgsqlCommand(
            $"SELECT id, event_type, event_data " +
            $"FROM \"{connectionInformation.TableName}\" " +
            (!string.IsNullOrEmpty(whereClause) 
                ? $"WHERE {whereClause} " 
                : "") +
            $"ORDER BY stream_version ASC " +
            $"LIMIT {limit}", conn);

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
        var connectionInformation = ConnectionInformation;

        if (events.GroupBy(x => x.PartitionKey).Count() != 1)
        {
            throw new ArgumentException("Partition keys for all events in the stream must be the same");
        }

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var transaction = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT MAX(stream_version) FROM \"{connectionInformation.TableName}\" WHERE stream_id = @streamId", conn, transaction)
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
                $"INSERT INTO \"{connectionInformation.TableName}\" " +
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
                        Value = JsonSerializer.Serialize(evt, evt.GetType(), EventSerializerOptions.Options),
                        DataTypeName = "jsonb"
                    },
                    new NpgsqlParameter()
                    {
                        ParameterName = "user_info",
                        Value = JsonSerializer.Serialize(eventUserInfo, eventUserInfo.GetType(), EventSerializerOptions.Options),
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
        var connectionInformation = ConnectionInformation;

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"SELECT 1 FROM \"{connectionInformation.TableName}\"", conn)
        {
        };

        try
        {
            var tableExists = await cmd.ExecuteScalarAsync(cancellationToken);
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                await using var createTableCommand = new NpgsqlCommand(
                    "CREATE OR REPLACE FUNCTION to_timestamp_utc(text)" +
                    "RETURNS timestamp with time zone AS" +
                    "    $func$" +
                    "SELECT $1::timestamp without time zone at time zone 'UTC'" +
                    "    $func$ LANGUAGE sql IMMUTABLE;" +
                    "" +
                    $"CREATE TABLE \"{connectionInformation.TableName}\" (" +
                    $"  id uuid, " +
                    $"  stream_id uuid, " +
                    $"  stream_version integer, " +
                    $"  event_type varchar(500), " +
                    $"  event_data jsonb, " +
                    $"  user_info jsonb, " +
                    $"  eventstore_schema_version int NOT NULL" +
                    $");" +
                    $"CREATE INDEX \"{connectionInformation.TableName}_stream_id_idx\" " +
                    $"  ON \"{connectionInformation.TableName}\" (stream_id);" +
                    $"CREATE INDEX \"{connectionInformation.TableName}_stream_id_with_partition_key_idx\" " +
                    $"  ON \"{connectionInformation.TableName}\" (stream_id, ((event_data ->> 'partitionKey')::varchar(256)));" +
                    $"CREATE INDEX \"{connectionInformation.TableName}_timestamp_utc\" " +
                    $"  ON \"{connectionInformation.TableName}\" (to_timestamp_utc(event_data->>'timestamp'));"
                    
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

    public async Task<EventStoreStatistics> GetStatistics(CancellationToken cancellationToken = default)
    {
        var connectionInformation = ConnectionInformation;
        var stats = new EventStoreStatistics();

        await using var conn = new NpgsqlConnection(connectionInformation.ConnectionString);
        await conn.OpenAsync();

        await using var countCmd = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM \"{connectionInformation.TableName}\"", conn
        );
        await using var firstEventTimestampCmd = new NpgsqlCommand(
            $"SELECT to_timestamp_utc(event_data->>'timestamp') " +
            $"FROM \"{connectionInformation.TableName}\" " +
            $"ORDER BY to_timestamp_utc(event_data->>'timestamp') ASC " +
            $"LIMIT 1", conn
        );
        await using var lastEventTimestampCmd = new NpgsqlCommand(
            $"SELECT to_timestamp_utc(event_data->>'timestamp') " +
            $"FROM \"{connectionInformation.TableName}\" " +
            $"ORDER BY to_timestamp_utc(event_data->>'timestamp') DESC " +
            $"LIMIT 1", conn
        );
    
        var count = await countCmd.ExecuteScalarAsync(cancellationToken);

        if (count != null)
        {
            stats.TotalEventsCount = (long)count;
        }
        
        var firstEventDateTime = await firstEventTimestampCmd.ExecuteScalarAsync(cancellationToken);

        if (firstEventDateTime != null)
        {
            stats.FirstEventCreatedAt = (DateTime)firstEventDateTime;
        }
        
        var lastEventDateTime = await lastEventTimestampCmd.ExecuteScalarAsync(cancellationToken);

        if (lastEventDateTime != null)
        {
            stats.LastEventCreatedAt = (DateTime)lastEventDateTime;
        }

        return stats;
    }
}