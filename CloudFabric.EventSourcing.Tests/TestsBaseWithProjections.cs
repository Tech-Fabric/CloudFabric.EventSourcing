using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using CloudFabric.Projections.Worker;
using Microsoft.Extensions.Logging.Abstractions;
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
    protected ProjectionsRebuildProcessor ProjectionsRebuildProcessor;
    
    [TestInitialize]
    public async Task Initialize()
    {
        var store = await GetEventStore();
        
        // Repository containing projections - `view models` of orders
        ProjectionsRepository = GetProjectionRepositoryFactory().GetProjectionRepository<TProjectionDocument>();

        await store.DeleteAll();

        try
        {
            await ProjectionsRepository.DeleteAll();
        }
        catch
        {
        }
        
        var repositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        ProjectionsEngine = new ProjectionsEngine();
        ProjectionsEngine.SetEventsObserver(repositoryEventsObserver);

        ProjectionBuilder = (TProjectionBuilder)Activator.CreateInstance(
            typeof(TProjectionBuilder), 
            GetProjectionRepositoryFactory(),
            ProjectionOperationIndexSelector.Write
        )! ?? throw new InvalidOperationException("Could not create projection builder.");
        
        ProjectionsEngine.AddProjectionBuilder(ProjectionBuilder);
        
        await ProjectionsEngine.StartAsync("Test");

        ProjectionsRebuildProcessor = new ProjectionsRebuildProcessor(
            GetProjectionRepositoryFactory().GetProjectionRepository(null),
            async (string connectionId) =>
            {
                var rebuildProjectionsEngine = new ProjectionsEngine();
                rebuildProjectionsEngine.SetEventsObserver(repositoryEventsObserver);

                var rebuildProjectionBuilder = (TProjectionBuilder)Activator.CreateInstance(
                                                   typeof(TProjectionBuilder), 
                                                   GetProjectionRepositoryFactory(),
                                                   ProjectionOperationIndexSelector.ProjectionRebuild
                                               )! ?? throw new InvalidOperationException("Could not create projection builder.");
        
                rebuildProjectionsEngine.AddProjectionBuilder(rebuildProjectionBuilder);

                return rebuildProjectionsEngine;
            },
            NullLogger<ProjectionsRebuildProcessor>.Instance
        );

        await ProjectionsRepository.EnsureIndex();
        await ProjectionsRebuildProcessor.RebuildProjectionsThatRequireRebuild();
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
        }
        catch
        {
        }
    }
}