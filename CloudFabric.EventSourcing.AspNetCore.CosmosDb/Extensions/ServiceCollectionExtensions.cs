using CloudFabric.EventSourcing.EventStore.CosmosDb;
using CloudFabric.Projections;
using CloudFabric.Projections.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNetCore.CosmosDb.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddCosmosDbEventStore(
            this IServiceCollection services,
            string connectionString,
            CosmosClientOptions cosmosClientOptions,
            string databaseId,
            string containerId
        )
        {
            var eventStore = new CosmosDbEventStore(connectionString, cosmosClientOptions, databaseId, containerId);
            eventStore.Initialize().Wait();

            return new EventSourcingBuilder
            {
                EventStore = eventStore,
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
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<TRepo>(sp, new object[] { builder.EventStore }));
            return builder;
        }

        public static IEventSourcingBuilder AddCosmosDbProjections<TDocument>(
            this IEventSourcingBuilder builder,
            CosmosChangeFeedObserverConnectionInfo changeFeedConnectionInfo,
            CosmosProjectionRepositoryConnectionInfo projectionsConnectionInfo,
            params Type[] projectionBuildersTypes
        ) where TDocument : ProjectionDocument
        {
            var eventStoreObserver = new CosmosDbEventStoreChangeFeedObserver(
                changeFeedConnectionInfo.EventsClient,
                changeFeedConnectionInfo.EventsDatabaseId,
                changeFeedConnectionInfo.EventsContainerId,
                changeFeedConnectionInfo.LeaseClient,
                changeFeedConnectionInfo.LeaseDatabaseId,
                changeFeedConnectionInfo.LeaseContainerId,
                changeFeedConnectionInfo.ProcessorName
            );

            var projectionRepository = new CosmosDbProjectionRepository<TDocument>(
                projectionsConnectionInfo.LoggerFactory,
                projectionsConnectionInfo.ConnectionString,
                projectionsConnectionInfo.CosmosClientOptions,
                projectionsConnectionInfo.DatabaseId,
                projectionsConnectionInfo.ContainerId,
                projectionsConnectionInfo.PartitionKey
            );
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
