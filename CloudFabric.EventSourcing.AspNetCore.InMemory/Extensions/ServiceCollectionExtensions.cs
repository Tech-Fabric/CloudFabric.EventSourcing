using CloudFabric.EventSourcing.EventStore.InMemory;
using CloudFabric.Projections;
using CloudFabric.Projections.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.AspNetCore.InMemory.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddInMemoryEventStore(
            this IServiceCollection services,
            Dictionary<string, List<string>> eventsContainer
        )
        {
            var eventStore = new InMemoryEventStore(eventsContainer);
            eventStore.Initialize().Wait();

            return new EventSourcingBuilder
            {
                EventStore = eventStore,
                Services = services
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
            var eventStoreObserver = new InMemoryEventStoreEventObserver((InMemoryEventStore)builder.EventStore);

            var projectionRepository = new InMemoryProjectionRepository<TDocument>();
            builder.Services.AddScoped<IProjectionRepository<TDocument>>((sp) => projectionRepository);

            var projectionsEngine = new ProjectionsEngine();
            projectionsEngine.SetEventsObserver(eventStoreObserver);

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
