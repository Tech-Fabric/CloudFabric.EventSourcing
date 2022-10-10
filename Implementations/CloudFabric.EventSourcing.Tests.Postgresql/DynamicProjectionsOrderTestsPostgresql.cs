using System.Diagnostics;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests.Postgresql;

[TestClass]
public class DynamicProjectionsOrderTestsPostgresql : DynamicProjectionSchemaTests
{
    private readonly Dictionary<string, IProjectionRepository> _projectionsRepositories = new();
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

    protected override IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema schema)
    {
        if (!_projectionsRepositories.ContainsKey(schema.SchemaName))
        {
            _projectionsRepositories[schema.SchemaName] = new PostgresqlProjectionRepository(
                "Host=localhost;Username=cloudfabric_eventsourcing_test;Password=cloudfabric_eventsourcing_test;Database=cloudfabric_eventsourcing_test;Maximum Pool Size=1000",
                schema
            );
        }

        return _projectionsRepositories[schema.SchemaName];
    }

    protected override IEventsObserver GetEventStoreEventsObserver()
    {
        if (_eventStoreEventsObserver == null)
        {
            _eventStoreEventsObserver = new PostgresqlEventStoreEventObserver(_eventStore);
        }

        return _eventStoreEventsObserver;
    }

    protected override IProjectionRepository<ProjectionRebuildState> GetProjectionRebuildStateRepository()
    {
        return new PostgresqlProjectionRepository<ProjectionRebuildState>(
            "Host=localhost;Username=cloudfabric_eventsourcing_test;Password=cloudfabric_eventsourcing_test;Database=cloudfabric_eventsourcing_test;Maximum Pool Size=1000"
        );
    }
}