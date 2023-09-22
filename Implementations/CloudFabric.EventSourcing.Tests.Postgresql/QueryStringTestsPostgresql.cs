using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.Postgresql;

[TestClass]
public class QueryStringTestsPostgresql : ProjectionQueryTest
{
    private ProjectionRepositoryFactory? _projectionRepositoryFactory;
    private PostgresqlEventStore? _eventStore;
    private PostgresqlEventStoreEventObserver? _eventStoreEventsObserver;

    protected override async Task<IEventStore> GetEventStore()
    {
        if (_eventStore == null)
        {
            _eventStore = new PostgresqlEventStore(
                TestsConnectionStrings.CONNECTION_STRING,
                "orders_events",
                "stored_items"
            );
            await _eventStore.Initialize();
        }

        return _eventStore;
    }

    protected override EventsObserver GetEventStoreEventsObserver()
    {
        if (_eventStoreEventsObserver == null)
        {
            _eventStoreEventsObserver = new PostgresqlEventStoreEventObserver(
                _eventStore, 
                NullLogger<PostgresqlEventStoreEventObserver>.Instance
            );
        }

        return _eventStoreEventsObserver;
    }

    protected override ProjectionRepositoryFactory GetProjectionRepositoryFactory()
    {
        if (_projectionRepositoryFactory == null)
        {
            _projectionRepositoryFactory = new PostgresqlProjectionRepositoryFactory(
                NullLoggerFactory.Instance,
                TestsConnectionStrings.CONNECTION_STRING,
                includeDebugInformation: true
            );
        }

        return _projectionRepositoryFactory;
    }
}