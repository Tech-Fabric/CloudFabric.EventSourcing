using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.InMemory;

[TestClass]
public class ItemTestInMemory : ItemTests
{
    private InMemoryEventStore? _eventStore = null;

    protected override async Task<IEventStore> GetEventStore()
    {
        if (_eventStore == null)
        {
            _eventStore = new InMemoryEventStore(
                new Dictionary<(Guid, string), List<string>>(),
                new Dictionary<(string, string), string>()
            );
            await _eventStore.Initialize();
        }

        return _eventStore;
    }
}
