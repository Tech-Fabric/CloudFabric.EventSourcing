using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNet.Postgresql.Extensions
{
    class PostgresqlEventSourcingScope
    {
        public IEventStore EventStore { get; set; }
        public IEventsObserver EventsObserver { get; set; }
        public ProjectionsEngine? ProjectionsEngine { get; set; }
    }

    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddPostgresqlEventStore(
            this IServiceCollection services,
            string eventsConnectionString,
            string tableName
        )
        {
            services.AddPostgresqlEventStore((sp) => new PostgresqlEventStoreStaticConnectionInformationProvider(eventsConnectionString, tableName));

            return new EventSourcingBuilder
            {
                Services = services
            };
        }

        public static IEventSourcingBuilder AddPostgresqlEventStore(
            this IServiceCollection services,
            Func<IServiceProvider, IPostgresqlEventStoreConnectionInformationProvider> connectionInformationProviderFactory
        )
        {
            var builder = new EventSourcingBuilder
            {
                Services = services
            };

            services.AddScoped<IPostgresqlEventStoreConnectionInformationProvider>(connectionInformationProviderFactory);

            services.AddScoped<PostgresqlEventSourcingScope>(
                (sp) =>
                {
                    var scope = new PostgresqlEventSourcingScope();

                    var connectionInformationProvider = sp.GetRequiredService<IPostgresqlEventStoreConnectionInformationProvider>();

                    scope.EventStore = new PostgresqlEventStore(connectionInformationProvider);

                    scope.EventsObserver = new PostgresqlEventStoreEventObserver(
                        (PostgresqlEventStore)scope.EventStore,
                        sp.GetRequiredService<ILogger<PostgresqlEventStoreEventObserver>>()
                    );

                    var projectionsRepositoryFactory = sp.GetService<ProjectionRepositoryFactory>();

                    // Postgresql's event observer is synchronous - it just handles all calls to npgsql commands, there is no delay
                    // or log processing. That means that all events are happening in request context, possibly on multiple threads,
                    // so having one global projections builder is more complicated than simply creating new projections builder for each request.
                    if (projectionsRepositoryFactory != null)
                    {
                        scope.ProjectionsEngine = new ProjectionsEngine();
                        scope.ProjectionsEngine.SetEventsObserver(scope.EventsObserver);

                        foreach (var projectionBuilderType in builder.ProjectionBuilderTypes)
                        {
                            var projectionBuilder = builder.ConstructProjectionBuilder(projectionBuilderType, projectionsRepositoryFactory);

                            scope.ProjectionsEngine.AddProjectionBuilder(projectionBuilder);
                        }

                        scope.ProjectionsEngine.StartAsync(connectionInformationProvider.GetConnectionInformation().ConnectionId).GetAwaiter().GetResult();
                    }

                    return scope;
                }
            );

            services.AddScoped<IEventStore>(
                (sp) =>
                {
                    var eventSourcingScope = sp.GetRequiredService<PostgresqlEventSourcingScope>();

                    return eventSourcingScope.EventStore;
                }
            );
            
            services.AddScoped<IEventsObserver>(
                (sp) =>
                {
                    var eventSourcingScope = sp.GetRequiredService<PostgresqlEventSourcingScope>();

                    return eventSourcingScope.EventsObserver;
                }
            );

            return builder;
        }

        public static IEventSourcingBuilder AddRepository<TRepo>(this IEventSourcingBuilder builder)
            where TRepo : class
        {
            builder.Services.AddScoped(
                sp =>
                {
                    var eventStore = sp.GetRequiredService<IEventStore>();
                    return ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { eventStore });
                }
            );

            return builder;
        }

        public static IEventSourcingBuilder AddPostgresqlProjections(
            this IEventSourcingBuilder builder,
            string projectionsConnectionString,
            params Type[] projectionBuildersTypes
        )
        {
            builder.ProjectionsConnectionString = projectionsConnectionString;
            builder.ProjectionBuilderTypes = projectionBuildersTypes;

            builder.Services.AddScoped<ProjectionRepositoryFactory>(
                (sp) =>
                {
                    var connectionInformationProvider = sp.GetRequiredService<IPostgresqlEventStoreConnectionInformationProvider>();

                    return new PostgresqlProjectionRepositoryFactory(
                        connectionInformationProvider.GetConnectionInformation().ConnectionId,
                        projectionsConnectionString
                    );
                }
            );

            return builder;
        }
    }
}
