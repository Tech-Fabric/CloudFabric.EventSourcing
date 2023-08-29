using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.Enums;
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
            string eventsTableName,
            string itemsTableName
        )
        {
            return services.AddPostgresqlEventStore(
                (sp) =>
                    new PostgresqlEventStoreStaticConnectionInformationProvider(eventsConnectionString, eventsTableName, itemsTableName)
            );
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

            builder.ProjectionsConnectionString = "yoyo";

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

                        if (builder.ProjectionBuilderTypes != null)
                        {
                            foreach (var projectionBuilderType in builder.ProjectionBuilderTypes)
                            {
                                var projectionBuilder = builder.ConstructProjectionBuilder(
                                    projectionBuilderType, 
                                    projectionsRepositoryFactory, 
                                    new AggregateRepositoryFactory(scope.EventStore),
                                    sp,
                                    ProjectionOperationIndexSelector.Write
                                );

                                scope.ProjectionsEngine.AddProjectionBuilder(projectionBuilder);
                            }
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

            services.AddScoped<AggregateRepositoryFactory>(
                (sp) =>
                {
                    var eventSourcingScope = sp.GetRequiredService<PostgresqlEventSourcingScope>();

                    return new AggregateRepositoryFactory(eventSourcingScope.EventStore);
                }
            );

            return builder;
        }

        /// <summary>
        /// This extension overload initialize event store with default item event store table name.
        /// </summary>
        public static IEventSourcingBuilder AddPostgresqlEventStore(
            this IServiceCollection services,
            string eventsConnectionString,
            string eventsTableName
        )
        {
            return services.AddPostgresqlEventStore(
                eventsConnectionString,
                eventsTableName,
                string.Concat(eventsTableName, ItemsEventStoreNameSuffix.TableNameSuffix)
            );
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
                        connectionInformationProvider.GetConnectionInformation().ConnectionString,
                        connectionInformationProvider.GetConnectionInformation().ConnectionId
                    );
                }
            );

            return builder;
        }

        public static IEventSourcingBuilder AddProjectionsRebuildProcessor(this IEventSourcingBuilder builder)
        {
            builder.Services.AddSingleton<ProjectionsRebuildProcessor>(
                (sp) =>
                {
                    var rebuildProcessorScope = sp.CreateScope();

                    return new ProjectionsRebuildProcessor(
                        rebuildProcessorScope.ServiceProvider.GetRequiredService<ProjectionRepositoryFactory>().GetProjectionsIndexStateRepository(),
                        async (string connectionId) =>
                        {
                            var connectionInformationProvider =
                                rebuildProcessorScope.ServiceProvider.GetRequiredService<IPostgresqlEventStoreConnectionInformationProvider>();
                            var connectionInformation = connectionInformationProvider.GetConnectionInformation(connectionId);
                            var eventStore = new PostgresqlEventStore(
                                connectionInformation.ConnectionString, connectionInformation.TableName, connectionInformation.ItemsTableName
                            );

                            var eventObserver = new PostgresqlEventStoreEventObserver(
                                (PostgresqlEventStore)eventStore,
                                rebuildProcessorScope.ServiceProvider.GetRequiredService<ILogger<PostgresqlEventStoreEventObserver>>()
                            );

                            var projectionsEngine = new ProjectionsEngine();

                            foreach (var projectionBuilderType in builder.ProjectionBuilderTypes)
                            {
                                var projectionBuilder = builder.ConstructProjectionBuilder(
                                    projectionBuilderType,
                                    rebuildProcessorScope.ServiceProvider.GetRequiredService<ProjectionRepositoryFactory>(),
                                    new AggregateRepositoryFactory(eventStore),
                                    rebuildProcessorScope.ServiceProvider,
                                    ProjectionOperationIndexSelector.ProjectionRebuild
                                );

                                projectionsEngine.AddProjectionBuilder(projectionBuilder);
                            }

                            projectionsEngine.SetEventsObserver(eventObserver);

                            // no need to listen - we are attaching this projections engine to test event store which is already being observed
                            // by tests projections engine (see PrepareProjections method)
                            //await projectionsEngine.StartAsync("TestInstance");

                            return projectionsEngine;
                        },
                        rebuildProcessorScope.ServiceProvider.GetRequiredService<ILogger<ProjectionsRebuildProcessor>>()
                    );
                }
            );

            builder.Services.AddHostedService<ProjectionsRebuildProcessorHostedService>();

            return builder;
        }
    }
}