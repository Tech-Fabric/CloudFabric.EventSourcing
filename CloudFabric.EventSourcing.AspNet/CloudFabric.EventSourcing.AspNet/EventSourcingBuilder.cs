using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.AspNet;

public class EventSourcingBuilder : IEventSourcingBuilder
{
    public IEventStore EventStore { get; set; }

    public AggregateRepositoryFactory AggregateRepositoryFactory { get; set; }

    public IServiceCollection Services { get; set; }

    public ProjectionsEngine ProjectionsEngine { get; set; }

    public IEventsObserver ProjectionEventsObserver { get; set; }

    public dynamic ConstructProjectionBuilder(Type projectionBuilderType, ProjectionRepositoryFactory projectionsRepositoryFactory)
    {
        dynamic? projectionBuilder = null;
        // There are two types of projection builders: 
        // First one is ProjectionBuilder<ProjectionDocument> and works with strict projection documents represented by class
        // Second one is just ProjectionBuilder - those projections do not have strict schema and work with raw dictionary {key: value} type of documents.
        if (projectionBuilderType?.BaseType?.GenericTypeArguments[0]?.BaseType == typeof(ProjectionDocument))
        {
            projectionBuilder = (IProjectionBuilder<ProjectionDocument>?)Activator.CreateInstance(
                projectionBuilderType, new object[]
                {
                    projectionsRepositoryFactory, this.AggregateRepositoryFactory
                }
            );
        }
        else
        {
            projectionBuilder = (IProjectionBuilder)Activator.CreateInstance(
                projectionBuilderType, new object[]
                {
                    projectionsRepositoryFactory, this.AggregateRepositoryFactory
                }
            );
        }

        if (projectionBuilder == null)
        {
            throw new Exception("Failed to create projection builder instance: Activator.CreateInstance returned null");
        }

        return projectionBuilder;
    }

}
