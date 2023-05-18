using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.AspNet;

public interface IEventSourcingBuilder
{
    IEventStore EventStore { get; set; }
    
    public AggregateRepositoryFactory AggregateRepositoryFactory { get; set; }

    IServiceCollection Services { get; set; }

    IEventsObserver ProjectionEventsObserver { get; set; }

    ProjectionsEngine ProjectionsEngine { get; set; }
    
    dynamic ConstructProjectionBuilder(Type projectionBuilderType, ProjectionRepositoryFactory projectionsRepositoryFactory);
}
