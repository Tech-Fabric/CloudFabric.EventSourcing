using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.InMemory;
using CloudFabric.Projections;
using CloudFabric.Projections.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudFabric.EventSourcing.AspNet.InMemory.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddInMemoryEventStore(
            this IServiceCollection services,
            Dictionary<(Guid, string), List<string>> eventsContainer
        )
        {
            var eventStore = new InMemoryEventStore(eventsContainer);
            eventStore.Initialize().Wait();

            // add events observer for projections
            var eventStoreObserver = new InMemoryEventStoreEventObserver(eventStore);
            
            AggregateRepositoryFactory aggregateRepositoryFactory = new AggregateRepositoryFactory(eventStore);
            services.AddScoped(sp => aggregateRepositoryFactory);

            return new EventSourcingBuilder
            {
                EventStore = eventStore,
                Services = services,
                ProjectionEventsObserver = eventStoreObserver,
                AggregateRepositoryFactory = aggregateRepositoryFactory
            };
        }

        public static IEventSourcingBuilder AddRepository<TRepo>(this IEventSourcingBuilder builder)
            where TRepo : class
        {
            if (builder.EventStore == null)
            {
                throw new ArgumentException("Event store is missing");
            }
            
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { builder.EventStore }));
            return builder;
        }

        // NOTE: projection repositories can't work with different databases for now
        public static IEventSourcingBuilder AddInMemoryProjections(
            this IEventSourcingBuilder builder,
            params Type[] projectionBuildersTypes
        )
        {
            var projectionsRepositoryFactory = new InMemoryProjectionRepositoryFactory();

            builder.Services.TryAddScoped<ProjectionRepositoryFactory>((sp) => projectionsRepositoryFactory);
            
            // add repository for saving rebuild states
            var projectionStateRepository = new InMemoryProjectionRepository<ProjectionRebuildState>();

            var projectionsEngine = new ProjectionsEngine(projectionStateRepository);

            if (builder.ProjectionEventsObserver == null)
            {
                throw new ArgumentException("Projection events observer is missing");
            }

            projectionsEngine.SetEventsObserver(builder.ProjectionEventsObserver);

            foreach (var projectionBuilderType in projectionBuildersTypes)
            {
                var projectionBuilder = (IProjectionBuilder<ProjectionDocument>?)Activator.CreateInstance(
                    projectionBuilderType, new object[]
                    {
                        projectionsRepositoryFactory, builder.AggregateRepositoryFactory
                    }
                );

                if (projectionBuilder == null)
                {
                    throw new Exception("Failed to create projection builder instance: Activator.CreateInstance returned null");
                }
                
                projectionsEngine.AddProjectionBuilder(projectionBuilder);
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
    }
}
