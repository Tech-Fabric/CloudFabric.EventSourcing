using CloudFabric.EventSourcing.EventStore.InMemory;
using CloudFabric.Projections;
using CloudFabric.Projections.InMemory;
using Microsoft.Extensions.DependencyInjection;

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

            return new EventSourcingBuilder
            {
                EventStore = eventStore,
                Services = services,
                ProjectionEventsObserver = eventStoreObserver
            };
        }

        public static IEventSourcingBuilder AddRepository<TRepo>(this IEventSourcingBuilder builder)
            where TRepo : class
        {
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { builder.EventStore }));
            return builder;
        }

        public static IEventSourcingBuilder AddInMemoryProjections<TDocument>(
            this IEventSourcingBuilder builder,
            params Type[] projectionBuildersTypes
        ) where TDocument : ProjectionDocument
        {
            var projectionRepository = new InMemoryProjectionRepository<TDocument>();
            builder.Services.AddScoped<IProjectionRepository<TDocument>>((sp) => projectionRepository);

            builder.Services.AddScoped<ProjectionRepositoryFactory>((sp) => 
                new InMemoryProjectionRepositoryFactory()
            );
            
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
                if (!typeof(ProjectionBuilder<TDocument>).IsAssignableFrom(projectionBuilderType))
                {
                    throw new ArgumentException($"Invalid projection builder type: {projectionBuilderType.Name}");
                }

                projectionsEngine.AddProjectionBuilder(
                    (IProjectionBuilder<ProjectionDocument>)Activator.CreateInstance(projectionBuilderType, new object[] { projectionRepository })
                );
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
    }
}
