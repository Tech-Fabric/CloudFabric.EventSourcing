using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;

public abstract class TestsBaseWithProjections<TProjectionDocument, TProjectionBuilder> 
    where TProjectionDocument: ProjectionDocument
    where TProjectionBuilder: ProjectionBuilder<TProjectionDocument>
{
    // Some projection engines take time to catch events and update projection records
    // (like cosmosdb with changefeed event observer)
    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(1000);
    protected abstract Task<IEventStore> GetEventStore();
    protected abstract ProjectionRepositoryFactory GetProjectionRepositoryFactory();
    protected abstract IEventsObserver GetEventStoreEventsObserver();

    protected ProjectionsEngine ProjectionsEngine;
    protected IProjectionRepository<TProjectionDocument> ProjectionsRepository;
    protected TProjectionBuilder ProjectionBuilder;
    
    [TestInitialize]
    public async Task Initialize()
    {
        var store = await GetEventStore();
        // Repository containing projections - `view models` of orders
        ProjectionsRepository = GetProjectionRepositoryFactory().GetProjectionRepository<TProjectionDocument>();
        var repositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        ProjectionsEngine = new ProjectionsEngine(GetProjectionRepositoryFactory().GetProjectionRepository<ProjectionRebuildState>());
        ProjectionsEngine.SetEventsObserver(repositoryEventsObserver);

        ProjectionBuilder = (TProjectionBuilder)Activator.CreateInstance(typeof(TProjectionBuilder), GetProjectionRepositoryFactory()) 
                             ?? throw new InvalidOperationException("Could not create projection builder.");
        
        ProjectionsEngine.AddProjectionBuilder(ProjectionBuilder);
        
        await ProjectionsEngine.StartAsync("Test");
    }
    
    [TestCleanup]
    public async Task Cleanup()
    {
        await ProjectionsEngine.StopAsync();
        
        var store = await GetEventStore();
        await store.DeleteAll();

        try
        {
            var projectionRepository = GetProjectionRepositoryFactory().GetProjectionRepository<TProjectionDocument>();
            await projectionRepository.DeleteAll();

            var rebuildStateRepository = GetProjectionRepositoryFactory().GetProjectionRepository<ProjectionRebuildState>();
            await rebuildStateRepository.DeleteAll();
        }
        catch
        {
        }
    }
}
