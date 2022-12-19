using CloudFabric.Projections;
using CloudFabric.Projections.ElasticSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudFabric.EventSourcing.AspNet.ElasticSearch.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IEventSourcingBuilder AddElasticSearchProjections<TDocument>(
            this IEventSourcingBuilder builder,
            string uri,
            string username,
            string password,
            string certificateFingerprint,
            LoggerFactory loggerFactory,
            params Type[] projectionBuildersTypes
        ) where TDocument : ProjectionDocument
        {
            var projectionRepository = new ElasticSearchProjectionRepository<TDocument>(
                uri,
                username,
                password,
                certificateFingerprint,
                loggerFactory
            );

            builder.Services.AddScoped<IProjectionRepository<TDocument>>((sp) => projectionRepository);

            var repositoryFactory = new ElasticSearchProjectionRepositoryFactory(
                uri,
                username,
                password,
                certificateFingerprint,
                loggerFactory
            );
            builder.Services.AddScoped<ProjectionRepositoryFactory>((sp) => repositoryFactory);
            
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
                if (!typeof(ProjectionBuilder<TDocument>).IsAssignableFrom(projectionBuilderType))
                {
                    throw new ArgumentException($"Invalid projection builder type: {projectionBuilderType.Name}");
                }

                projectionsEngine.AddProjectionBuilder(
                    (IProjectionBuilder<ProjectionDocument>)Activator.CreateInstance(projectionBuilderType, new object[] { repositoryFactory })
                );
            }

            builder.ProjectionsEngine = projectionsEngine;

            return builder;
        }
    }
}
