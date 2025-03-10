using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.AspNet;

public interface IEventSourcingBuilder
{
    string EventStoreKey { get; set; }
    
    IEventStore EventStore { get; set; }

    IServiceCollection Services { get; set; }

    EventsObserver ProjectionEventsObserver { get; set; }

    ProjectionsEngine ProjectionsEngine { get; set; }
    string ProjectionsConnectionString { get; set; }
    Type[] ProjectionBuilderTypes { get; set; }

    dynamic ConstructProjectionBuilder(
        Type projectionBuilderType, 
        ProjectionRepositoryFactory projectionsRepositoryFactory, 
        AggregateRepositoryFactory aggregateRepositoryFactory,
        IServiceProvider serviceProvider,
        ProjectionOperationIndexSelector indexSelector
    );

    Task InitializeEventStore(IServiceProvider serviceProvider);
    Task EnsureProjectionIndexFor<T>(IServiceProvider serviceProvider) where T : ProjectionDocument;
}