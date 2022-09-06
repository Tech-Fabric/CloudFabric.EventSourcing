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

        public static IEventSourcingBuilder AddPostgresqlProjections<TDocument>(
            this IEventSourcingBuilder builder,
            string projectionsConnectionString,
            params Type[] projectionBuildersTypes
        ) where TDocument : ProjectionDocument
        {
            var eventStoreObserver = new PostgresqlEventStoreEventObserver((PostgresqlEventStore)builder.EventStore);

            var projectionRepository = new PostgresqlProjectionRepository<TDocument>(projectionsConnectionString);
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
