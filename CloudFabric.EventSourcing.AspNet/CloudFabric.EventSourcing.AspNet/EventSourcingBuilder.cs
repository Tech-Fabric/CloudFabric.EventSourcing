using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFabric.EventSourcing.AspNet;

public class EventSourcingBuilder : IEventSourcingBuilder
{
    public IEventStore EventStore { get; set; }

    public IServiceCollection Services { get; set; }

    public ProjectionsEngine ProjectionsEngine { get; set; }
}
