using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.CosmosDb;
using CloudFabric.Projections;
using CloudFabric.Projections.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNet.CosmosDb.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddCosmosDbEventStore(
            this IServiceCollection services,
            string connectionString,
            CosmosClientOptions cosmosClientOptions,
            string databaseId,
            string containerId,
            CosmosClient leaseClient,
            string leaseDatabaseId,
            string leaseContainerId,
            string processorName
        )
        {
            services.AddScoped<AggregateRepositoryFactory>((sp) =>
            {
                var logger = sp.GetRequiredService<ILogger<CosmosDbEventStoreChangeFeedObserver>>();
                
                var cosmosClient = new CosmosClient(connectionString, cosmosClientOptions);

                var eventStore = new CosmosDbEventStore(cosmosClient, databaseId, containerId);
                eventStore.Initialize().Wait();

                var eventStoreObserver = new CosmosDbEventStoreChangeFeedObserver(
                    cosmosClient,
                    databaseId,
                    containerId,
                    leaseClient,
                    leaseDatabaseId,
                    leaseContainerId,
                    processorName,
                    logger
                );

                return new AggregateRepositoryFactory(eventStore);
            });
            
            return new EventSourcingBuilder
            {
                Services = services
            };
        }

        public static IEventSourcingBuilder AddCosmosDbEventStore(
            this IServiceCollection services,
            CosmosClient client,
            string databaseId,
            string containerId
        )
        {
            var eventStore = new CosmosDbEventStore(client, databaseId, containerId);
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
            if (builder.EventStore == null)
            {
                throw new ArgumentException("Event store is missing");
            }
            
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { builder.EventStore }));
            return builder;
        }

        // NOTE: projection repositories can't work with different databases for now
        public static IEventSourcingBuilder AddCosmosDbProjections(
            this IEventSourcingBuilder builder,
            CosmosProjectionRepositoryConnectionInfo projectionsConnectionInfo,
            params Type[] projectionBuildersTypes
        )
        {
            var projectionsRepositoryFactory = new CosmosDbProjectionRepositoryFactory(
                projectionsConnectionInfo.LoggerFactory,
                projectionsConnectionInfo.ConnectionString,
                projectionsConnectionInfo.CosmosClientOptions,
                projectionsConnectionInfo.DatabaseId,
                projectionsConnectionInfo.ContainerId
            );

            // TryAddScoped is used to be able to add a few event stores with separate calls of AddPostgresqlProjections
            builder.Services.AddScoped<ProjectionRepositoryFactory>((sp) => projectionsRepositoryFactory);
            
            // // add repository for saving rebuild states
            // var projectionStateRepository = new CosmosDbProjectionRepository<ProjectionRebuildState>(
            //     projectionsConnectionInfo.LoggerFactory,
            //     projectionsConnectionInfo.ConnectionString,
            //     projectionsConnectionInfo.CosmosClientOptions,
            //     projectionsConnectionInfo.DatabaseId,
            //     projectionsConnectionInfo.ContainerId
            // );

            var projectionsEngine = new ProjectionsEngine();

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
