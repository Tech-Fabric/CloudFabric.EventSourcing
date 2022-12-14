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
public class OrderStringComparisonTestsElasticSearch : OrderStringComparisonTests
{
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

    protected override ProjectionRepositoryFactory GetProjectionRepositoryFactory()
    {
        return new ElasticSearchProjectionRepositoryFactory(
            "http://127.0.0.1:9200",
            "",
            "",
            "",
            new LoggerFactory()
        );
    }

    protected override IEventsObserver GetEventStoreEventsObserver()
    {
        if (_eventStoreEventsObserver == null)
        {
            _eventStoreEventsObserver = new PostgresqlEventStoreEventObserver(_eventStore);
        }

        return _eventStoreEventsObserver;
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringContainsCaseInsensitive()
    {
        return;
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringEndsWithCaseInSensitiveIgnoreCaseArgument()
    {
        return;
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringEndsWithCaseInSensitiveStringComparisonEnumInvariantCultureIgnoreCase()
    {
        return;
    }


    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseInsensitiveIgnoreCaseArgument()
    {
        return;
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseInsensitiveStringComparisonEnumInvariantCultureIgnoreCase()
    {
        return;
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseInsensitiveStringComparisonEnumOrdinal()
    {
        return;
    }
}