using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.Common;

public interface IEventSourcingBuilder
{
    IEventStore EventStore { get; set; }

    IServiceCollection Services { get; set; }

    ProjectionsEngine ProjectionsEngine { get; set; }
}
