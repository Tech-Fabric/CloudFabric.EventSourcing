using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudFabric.EventSourcing.AspNet.Postgresql.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddPostgresqlEventStore(
            this IServiceCollection services,
            string eventsConnectionString,
            string tableName
        )
        {
            var eventStore = new PostgresqlEventStore(eventsConnectionString, tableName);
            eventStore.Initialize().Wait();

            // add events observer for projections
            var eventStoreObserver = new PostgresqlEventStoreEventObserver(eventStore);

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
            if (builder.EventStore == null)
            {
                throw new ArgumentException("Event store is missing");
            }
            
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { builder.EventStore }));
            return builder;
        }

        // NOTE: projection repositories can't work with different databases for now
        public static IEventSourcingBuilder AddPostgresqlProjections(
            this IEventSourcingBuilder builder,
            string projectionsConnectionString,
            params Type[] projectionBuildersTypes
        )
        {
            var repositoryFactory = new PostgresqlProjectionRepositoryFactory(projectionsConnectionString);

            // TryAddScoped is used to be able to add a few event stores with separate calls of AddPostgresqlProjections
            builder.Services.TryAddScoped<ProjectionRepositoryFactory>((sp) => repositoryFactory);
            
            // add repository for saving rebuild states
            var projectionStateRepository = new PostgresqlProjectionRepository<ProjectionRebuildState>(projectionsConnectionString);

            var projectionsEngine = new ProjectionsEngine(projectionStateRepository);

            if (builder.ProjectionEventsObserver == null)
            {
                throw new ArgumentException("Projection events observer is missing");
            }

            projectionsEngine.SetEventsObserver(builder.ProjectionEventsObserver);

            foreach (var projectionBuilderType in projectionBuildersTypes)
            {
                projectionsEngine.AddProjectionBuilder(
                    (IProjectionBuilder<ProjectionDocument>)Activator.CreateInstance(projectionBuilderType, new object[] { repositoryFactory })
                );
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
    }
}
