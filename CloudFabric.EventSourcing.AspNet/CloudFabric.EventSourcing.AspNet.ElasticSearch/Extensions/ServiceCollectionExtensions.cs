using CloudFabric.Projections;
using CloudFabric.Projections.ElasticSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNet.ElasticSearch.Extensions
{
    public static class ServiceCollectionExtensions
    {
        // NOTE: projection repositories can't work with different databases for now
        public static IEventSourcingBuilder AddElasticSearchProjections(
            this IEventSourcingBuilder builder,
            ElasticSearchBasicAuthConnectionSettings basicAuthConnectionSettings,
            ILoggerFactory loggerFactory,
            bool disableRequestStreaming = false,
            params Type[] projectionBuildersTypes
        )
        {
            var projectionsRepositoryFactory = new ElasticSearchProjectionRepositoryFactory(
                basicAuthConnectionSettings,
                loggerFactory
            );

            // TryAddScoped is used to be able to add a few event stores with separate calls of AddPostgresqlProjections
            builder.Services.TryAddScoped<ProjectionRepositoryFactory>((sp) => projectionsRepositoryFactory);
            
            // add repository for saving rebuild states
            var projectionStateRepository = new ElasticSearchProjectionRepository<ProjectionRebuildState>(
                basicAuthConnectionSettings,
                loggerFactory,
                disableRequestStreaming
            );

            var projectionsEngine = new ProjectionsEngine(projectionStateRepository);

            if (builder.ProjectionEventsObserver == null)
            {
                throw new ArgumentException("Projection events observer is missing");
            }

            projectionsEngine.SetEventsObserver(builder.ProjectionEventsObserver);

            foreach (var projectionBuilderType in projectionBuildersTypes)
            {
                var projectionBuilder = (IProjectionBuilder<ProjectionDocument>?)Activator.CreateInstance(
                    projectionBuilderType, new object[]
                    {
                        projectionsRepositoryFactory, builder.AggregateRepositoryFactory
                    }
                );

                if (projectionBuilder == null)
                {
                    throw new Exception("Failed to create projection builder instance: Activator.CreateInstance returned null");
                }
                
                projectionsEngine.AddProjectionBuilder(projectionBuilder);
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
        
        public static IEventSourcingBuilder AddElasticSearchProjections(
            this IEventSourcingBuilder builder,
            ElasticSearchApiKeyAuthConnectionSettings apiKeyAuthConnectionSettings,
            ILoggerFactory loggerFactory,
            bool disableRequestStreaming = false,
            params Type[] projectionBuildersTypes
        )
        {
            var projectionsRepositoryFactory = new ElasticSearchProjectionRepositoryFactory(
                apiKeyAuthConnectionSettings,
                loggerFactory
            );

            // TryAddScoped is used to be able to add a few event stores with separate calls of AddPostgresqlProjections
            builder.Services.TryAddScoped<ProjectionRepositoryFactory>((sp) => projectionsRepositoryFactory);
            
            // add repository for saving rebuild states
            var projectionStateRepository = new ElasticSearchProjectionRepository<ProjectionRebuildState>(
                apiKeyAuthConnectionSettings,
                loggerFactory,
                disableRequestStreaming
            );

            var projectionsEngine = new ProjectionsEngine(projectionStateRepository);

            if (builder.ProjectionEventsObserver == null)
            {
                throw new ArgumentException("Projection events observer is missing");
            }

            projectionsEngine.SetEventsObserver(builder.ProjectionEventsObserver);

            foreach (var projectionBuilderType in projectionBuildersTypes)
            {
                var projectionBuilder = (IProjectionBuilder<ProjectionDocument>?)Activator.CreateInstance(
                    projectionBuilderType, new object[]
                    {
                        projectionsRepositoryFactory, builder.AggregateRepositoryFactory
                    }
                );

                if (projectionBuilder == null)
                {
                    throw new Exception("Failed to create projection builder instance: Activator.CreateInstance returned null");
                }
                
                projectionsEngine.AddProjectionBuilder(projectionBuilder);
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
    }
}
