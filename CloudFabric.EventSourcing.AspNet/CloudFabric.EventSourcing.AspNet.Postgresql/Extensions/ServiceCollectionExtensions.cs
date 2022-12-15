using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using Microsoft.Extensions.DependencyInjection;

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
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { builder.EventStore }));
            return builder;
        }

        public static IEventSourcingBuilder AddPostgresqlProjections<TDocument>(
            this IEventSourcingBuilder builder,
            string projectionsConnectionString,
            params Type[] projectionBuildersTypes
        ) where TDocument : ProjectionDocument
        {
            var projectionRepository = new PostgresqlProjectionRepository<TDocument>(projectionsConnectionString);
            builder.Services.AddScoped<IProjectionRepository<TDocument>>((sp) => projectionRepository);

            builder.Services.AddScoped<ProjectionRepositoryFactory>((sp) => 
                new PostgresqlProjectionRepositoryFactory(projectionsConnectionString)
            );
            
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
