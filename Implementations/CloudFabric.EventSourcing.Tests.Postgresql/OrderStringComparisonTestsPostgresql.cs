using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.Postgresql;

[TestClass]
public class OrderStringComparisonTestsPostgresql : OrderStringComparisonTests
{
    private PostgresqlEventStore? _eventStore;
    private PostgresqlEventStoreEventObserver? _eventStoreEventsObserver;

    protected override async Task<IEventStore> GetEventStore()
    {
        if (_eventStore == null)
        {
            _eventStore = new PostgresqlEventStore(
                TestsConnectionStrings.CONNECTION_STRING,
                "orders_events"
            );
            await _eventStore.Initialize();
        }

        return _eventStore;
    }
    
    protected override PostgresqlProjectionRepositoryFactory GetProjectionRepositoryFactory()
    {
        return new PostgresqlProjectionRepositoryFactory(
            TestsConnectionStrings.CONNECTION_STRING
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
}