using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Postgresql;
using CloudFabric.Projections;
using CloudFabric.Projections.Postgresql;
using CloudFabric.Projections.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNet.Postgresql.Extensions
{
    class PostgresqlEventSourcingScope
    {
        public IEventStore EventStore { get; set; }
        public EventsObserver EventsObserver { get; set; }
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
                    // or log processing. That means that all events are happening in request context and we cannot have one global projections builder.
                    // There was an option to have one global projections builder with thread safe queues, but for now, creating a builder for every request 
                    // should just work.
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
            
            services.AddScoped<EventsObserver>(
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
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var connectionInformationProvider = sp.GetRequiredService<IPostgresqlEventStoreConnectionInformationProvider>();

                    return new PostgresqlProjectionRepositoryFactory(
                        loggerFactory,
                        connectionInformationProvider.GetConnectionInformation().ConnectionId,
                        projectionsConnectionString
                    );
                }
            );

            return builder;
        }

        public static IEventSourcingBuilder AddProjectionsRebuildProcessor(this IEventSourcingBuilder builder)
        {
            builder.Services.AddSingleton<ProjectionsRebuildProcessor>((sp) => {
                return new ProjectionsRebuildProcessor(
                    sp.GetRequiredService<ProjectionRepositoryFactory>().GetProjectionRepository(null),
                    async (string connectionId) =>
                    {
                        var connectionInformationProvider = sp.GetRequiredService<IPostgresqlEventStoreConnectionInformationProvider>();
                        var connectionInformation = connectionInformationProvider.GetConnectionInformation(connectionId);
                        var eventStore = new PostgresqlEventStore(connectionInformation.ConnectionString, connectionInformation.TableName);

                        var eventObserver = new PostgresqlEventStoreEventObserver(
                            (PostgresqlEventStore)eventStore,
                            sp.GetRequiredService<ILogger<PostgresqlEventStoreEventObserver>>()
                        );
                        
                        var projectionsEngine = new ProjectionsEngine();

                        foreach (var projectionBuilderType in builder.ProjectionBuilderTypes)
                        {
                            var projectionBuilder = builder.ConstructProjectionBuilder(
                                projectionBuilderType, 
                                sp.GetRequiredService<ProjectionRepositoryFactory>()
                            );

                            projectionsEngine.AddProjectionBuilder(projectionBuilder);
                        }
                        
                        projectionsEngine.SetEventsObserver(eventObserver);

                        // no need to listen - we are attaching this projections engine to test event store which is already being observed
                        // by tests projections engine (see PrepareProjections method)
                        //await projectionsEngine.StartAsync("TestInstance");

                        return projectionsEngine;
                    },
                    sp.GetRequiredService<ILogger<ProjectionsRebuildProcessor>>()
                );
            });

            builder.Services.AddHostedService<ProjectionsRebuildProcessorHostedService>();
            
            return builder;
        }
    }
}
