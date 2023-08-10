using System.Runtime.CompilerServices;
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
            Dictionary<(Guid, string), List<string>> eventsContainer,
            Dictionary<(string, string), string> itemsContainer
        )
        {
            var eventStore = new InMemoryEventStore(eventsContainer, itemsContainer);
            eventStore.Initialize().Wait();

            // add events observer for projections
            var eventStoreObserver = new InMemoryEventStoreEventObserver(eventStore);
            
            AggregateRepositoryFactory aggregateRepositoryFactory = new AggregateRepositoryFactory(eventStore);
            services.AddScoped(sp => aggregateRepositoryFactory);

            IStoredItemsRepository storedItemsRepository = new StoredItemsRepository(eventStore);
            services.AddScoped(sp => storedItemsRepository);

            return new EventSourcingBuilder
            {
                EventStore = eventStore,
                Services = services,
                ProjectionEventsObserver = eventStoreObserver,
                AggregateRepositoryFactory = aggregateRepositoryFactory,
                StoredItemsRepository = storedItemsRepository
            };
        }

        public static IEventSourcingBuilder AddInMemoryEventStore(this IServiceCollection services)
        {
            return services.AddInMemoryEventStore(
                new Dictionary<(Guid, string), List<string>>(),
                new Dictionary<(string, string), string>()
            );
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
                var projectionBuilder = builder.ConstructProjectionBuilder(projectionBuilderType, projectionsRepositoryFactory);
                
                projectionsEngine.AddProjectionBuilder(projectionBuilder);
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
    }
}
