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

    public async Task On(OrderItemAdded @event, string partitionKey)
    {
        await UpdateDocument(@event.Id, partitionKey, (orderProjection) => { orderProjection.ItemsCount++; });
    }

    public async Task On(OrderItemRemoved @event, string partitionKey)
    {
        await UpdateDocument(@event.Id, partitionKey, (orderProjection) => { orderProjection.ItemsCount--; });
    }

    public async Task On(OrderPlaced @event, string partitionKey)
    {
        await UpsertDocument(new OrderListProjectionItem()
        {
            Id = @event.Id.ToString(),
            Name = @event.OrderName,
            ItemsCount = @event.Items.Count
        },
        partitionKey);
    }
}