using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.AspNet;

public interface IEventSourcingBuilder
{
    IEventStore EventStore { get; set; }

    IServiceCollection Services { get; set; }

    IEventsObserver ProjectionEventsObserver { get; set; }

    ProjectionsEngine ProjectionsEngine { get; set; }
}
