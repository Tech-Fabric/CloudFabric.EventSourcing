using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.Postgresql;

[TestClass]
public class MetadataRepositoryTestsPostgresql : MetadataRepositoryTests
{
    private PostgresqlMetadataRepository? _store;

    protected override async Task<IMetadataRepository> GetStore()
    {
        if (_store == null)
        {
            _store = new PostgresqlMetadataRepository(
                TestsConnectionStrings.CONNECTION_STRING,
                "metadata"
            );
            await _store.Initialize();
        }

        return _store;
    }

}