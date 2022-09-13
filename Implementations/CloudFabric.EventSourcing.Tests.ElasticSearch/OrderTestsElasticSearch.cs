using System.Diagnostics;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.ElasticSearch;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.ElasticSearch;

/// <summary>
/// Elastic Search projections test with Postgresql event store
/// </summary>
[TestClass]
public class OrderTestsElasticSearch : OrderTests
{
    private readonly Dictionary<Type, object> _projectionsRepositories = new();
    private PostgresqlEventStore? _eventStore;
    private PostgresqlEventStoreEventObserver? _eventStoreEventsObserver;

    protected override async Task<IEventStore> GetEventStore()
    {
        if (_eventStore == null)
        {
            _eventStore = new PostgresqlEventStore(
                "Host=localhost;Username=cloudfabric_eventsourcing_test;Password=cloudfabric_eventsourcing_test;Database=cloudfabric_eventsourcing_test;Maximum Pool Size=1000",
                "orders_events"
            );
            await _eventStore.Initialize();
        }

        return _eventStore;
    }

    protected override IEventsObserver GetEventStoreEventsObserver()
    {
        if (_eventStoreEventsObserver == null)
        {
            _eventStoreEventsObserver = new PostgresqlEventStoreEventObserver(_eventStore);
        }

        return _eventStoreEventsObserver;
    }

    protected override IProjectionRepository<T> GetProjectionRepository<T>()
    {
        if (!_projectionsRepositories.ContainsKey(typeof(T)))
        {
            _projectionsRepositories[typeof(T)] = new ElasticSearchProjectionRepository<T>(
                "uri",
                "username",
                "password",
                new LoggerFactory()
            );
        }

        return (IProjectionRepository<T>)_projectionsRepositories[typeof(T)];
    }

    protected override IProjectionRepository<ProjectionRebuildState> GetProjectionRebuildStateRepository()
    {
        return new ElasticSearchProjectionRepository<ProjectionRebuildState>(
            "uri",
            "username",
            "password",
            new LoggerFactory()
        );
    }

    public async Task LoadTest()
    {
        var watch = Stopwatch.StartNew();

        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                tasks.Add(TestPlaceOrderAndAddItem());
            }
        }

        await Task.WhenAll(tasks);

        watch.Stop();

        Console.WriteLine($"It took {watch.Elapsed}!");
    }
}