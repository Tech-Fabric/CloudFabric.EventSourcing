using CloudFabric.EventSourcing.Tests.Domain.Events;
using CloudFabric.Projections;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;

public class OrdersListProjectionBuilder : ProjectionBuilder<OrderListProjectionItem>,
    IHandleEvent<OrderPlaced>,
    IHandleEvent<OrderItemAdded>,
    IHandleEvent<OrderItemRemoved>
{
    public OrdersListProjectionBuilder(IProjectionRepository<OrderListProjectionItem> repository) : base(repository)
    {
    }

    public async Task On(OrderItemAdded @event)
    {
        await UpdateDocument(@event.Id, @event.PartitionKey, (orderProjection) => { orderProjection.ItemsCount++; });
    }

    public async Task On(OrderItemRemoved @event)
    {
        await UpdateDocument(@event.Id, @event.PartitionKey, (orderProjection) => { orderProjection.ItemsCount--; });
    }

    public async Task On(OrderPlaced @event)
    {
        await UpsertDocument(new OrderListProjectionItem()
        {
            Id = @event.Id,
            Name = @event.OrderName,
            ItemsCount = @event.Items.Count
        },
        @event.PartitionKey);
    }
}