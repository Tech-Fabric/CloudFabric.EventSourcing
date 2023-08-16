using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.InMemory;

[TestClass]
public class ItemTestInMemory : ItemTests
{
    private InMemoryStore? _store = null;

    protected override async Task<IStore> GetStore()
    {
        if (_store == null)
        {
            _store = new InMemoryStore(                
                new Dictionary<(string, string), string>()
            );
            await _store.Initialize();
        }

        return _store;
    }
}
