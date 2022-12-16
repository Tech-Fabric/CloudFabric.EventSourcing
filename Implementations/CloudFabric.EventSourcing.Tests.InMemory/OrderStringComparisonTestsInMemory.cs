using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.InMemory;
using CloudFabric.Projections;
using CloudFabric.Projections.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.InMemory;

[TestClass]
public class OrderStringComparisonTestsInMemory : OrderStringComparisonTests
{
    private InMemoryEventStore? _eventStore;
    private InMemoryEventStoreEventObserver? _eventStoreEventsObserver;

    protected override async Task<IEventStore> GetEventStore()
    {
        if (_eventStore == null)
        {
            _eventStore = new InMemoryEventStore(new Dictionary<(Guid, string), List<string>>());
            await _eventStore.Initialize();
        }

        return _eventStore;
    }

    protected override IEventsObserver GetEventStoreEventsObserver()
    {
        if (_eventStoreEventsObserver == null)
        {
            _eventStoreEventsObserver = new InMemoryEventStoreEventObserver(_eventStore);
        }

        return _eventStoreEventsObserver;
    }

    protected override InMemoryProjectionRepositoryFactory GetProjectionRepositoryFactory()
    {
        return new InMemoryProjectionRepositoryFactory();
    }
}