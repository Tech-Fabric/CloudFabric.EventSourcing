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
            string uri,
            string username,
            string password,
            string certificateFingerprint,
            LoggerFactory loggerFactory,
            params Type[] projectionBuildersTypes
        )
        {
            var repositoryFactory = new ElasticSearchProjectionRepositoryFactory(
                uri,
                username,
                password,
                certificateFingerprint,
                loggerFactory
            );

            // TryAddScoped is used to be able to add a few event stores with separate calls of AddPostgresqlProjections
            builder.Services.TryAddScoped<ProjectionRepositoryFactory>((sp) => repositoryFactory);
            
            // add repository for saving rebuild states
            var projectionStateRepository = new ElasticSearchProjectionRepository<ProjectionRebuildState>(
                uri,
                username,
                password,
                certificateFingerprint,
                loggerFactory
            );

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
