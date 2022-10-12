using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.InMemory;
using CloudFabric.Projections;
using CloudFabric.Projections.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.InMemory;

[TestClass]
public class OrderTestsInMemory : OrderTests
{
    private readonly Dictionary<Type, object> _projectionsRepositories = new();
    private InMemoryEventStore? _eventStore = null;
    private InMemoryEventStoreEventObserver? _eventStoreEventsObserver = null;

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

    protected override IProjectionRepository<T> GetProjectionRepository<T>()
    {
        if (!_projectionsRepositories.ContainsKey(typeof(T)))
        {
            _projectionsRepositories[typeof(T)] = new InMemoryProjectionRepository<T>();
        }

        return (IProjectionRepository<T>)_projectionsRepositories[typeof(T)];
    }

    protected override IProjectionRepository<ProjectionRebuildState> GetProjectionRebuildStateRepository()
    {
        return new InMemoryProjectionRepository<ProjectionRebuildState>();
    }
}