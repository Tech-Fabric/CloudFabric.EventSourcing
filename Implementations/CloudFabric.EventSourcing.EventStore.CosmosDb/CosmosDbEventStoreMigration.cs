using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;

namespace CloudFabric.EventSourcing.EventStore.CosmosDb;

public class CosmosDbEventStoreMigration
{
    private readonly CosmosClient _client;
    private readonly string _container;
    private readonly string _database;

    public CosmosDbEventStoreMigration(CosmosClient client, string database, string container)
    {
        _client = client;
        _database = database;
        _container = container;
    }

    public async Task RunAsync()
    {
        await DeleteStoredProcedureAsync("spAppendToStream");
        await CreateStoredProcedureAsync("spAppendToStream");
    }

    private async Task CreateStoredProcedureAsync(string storedProcedureId)
    {
        var storedProcedureResponse = await _client
            .GetContainer(_database, _container)
            .Scripts
            .CreateStoredProcedureAsync(new StoredProcedureProperties
            {
                Id = storedProcedureId,
                Body = @"
            function appendToStream(streamId, expectedVersion, events)
            {

                var versionQuery =
                    {
                        'query' : 'SELECT Max(e.stream.version) FROM events e WHERE e.stream.id = @streamId',
                        'parameters' : [{ 'name': '@streamId', 'value': streamId }]
                    };

                const isAccepted = __.queryDocuments(__.getSelfLink(), versionQuery,
                    function(err, items, options) {
                        if (err)throw new Error('Unable to get stream version: ' + err.message);

                        if (!items || !items.length)
                        {
                            throw new Error('No results from stream version query.');
                        }

                        var currentVersion = items[0].$1;

                        // Concurrency check.
                        if ((!currentVersion && expectedVersion == 0)
                            || (currentVersion == expectedVersion))
                        {
                            // Everything's fine, bulk insert the events.
                            JSON.parse(events).forEach(event =>
                                __.createDocument(__.getSelfLink(), event));

                            __.response.setBody(true);
                        }
                        else
                        {
                            __.response.setBody(false);
                        }
                    });

                if (!isAccepted) throw new Error('The query was not accepted by the server.');
            }"
            });

        Console.WriteLine(storedProcedureResponse.StatusCode);
    }

    private async Task DeleteStoredProcedureAsync(string storedProcedureId)
    {
        try
        {
            var storedProcedureResponse = await _client
                .GetContainer(_database, _container)
                .Scripts
                .DeleteStoredProcedureAsync(storedProcedureId);
        }
        catch (CosmosException)
        {
        }
    }
}