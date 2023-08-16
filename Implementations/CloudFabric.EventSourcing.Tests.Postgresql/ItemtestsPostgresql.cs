using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.Postgresql;

[TestClass]
public class ItemtestsPostgresql : ItemTests
{
    private PostgresqlStore? _store;

    protected override async Task<IStore> GetStore()
    {
        if (_store == null)
        {
            _store = new PostgresqlStore(
                TestsConnectionStrings.CONNECTION_STRING,
                "stored_items"
            );
            await _store.Initialize();
        }

        return _store;
    }

}
