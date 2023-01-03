using CloudFabric.EventSourcing.Tests.Domain.Events;
using CloudFabric.Projections;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;

public class OrdersListProjectionBuilder : ProjectionBuilder<OrderListProjectionItem>,
    IHandleEvent<OrderPlaced>,
    IHandleEvent<OrderItemAdded>,
    IHandleEvent<OrderItemRemoved>,
    IHandleEvent<AggregateUpdatedEvent<Order>>
{
    public OrdersListProjectionBuilder(ProjectionRepositoryFactory projectionRepositoryFactory) 
        : base(projectionRepositoryFactory)
    {
    }

    public async Task On(OrderItemAdded @event)
    {
        await UpdateDocument(@event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (orderProjection) => 
            {
                orderProjection.Items.Add(new OrderListProjectionOrderItem
                {
                    Name = @event.Item.Name,
                    Amount = @event.Item.Amount,
                    AddedAt = @event.Item.AddedAt
                });

                orderProjection.ItemsCount++;
            });
    }

    public async Task On(OrderItemRemoved @event)
    {
        await UpdateDocument(@event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (orderProjection) =>
            {
                var item = orderProjection.Items.FirstOrDefault(x => x.Name == @event.Name);

                if (item != null)
                {
                    orderProjection.Items.Remove(item);

                    orderProjection.ItemsCount--;
                }
            });
    }

    public async Task On(OrderPlaced @event)
    {
        var projectionItem = new OrderListProjectionItem()
        {
            Id = @event.AggregateId,
            Name = @event.OrderName,
            ItemsCount = @event.Items.Count
        };

        projectionItem.Items = @event.Items.Select(
            x =>
            new OrderListProjectionOrderItem
            {
                Amount = x.Amount,
                Name = x.Name,
                AddedAt = x.AddedAt
            }
        ).ToList();

        projectionItem.CreatedBy = new OrderListProjectionUserInfo
        {
            UserId = @event.CreatedById,
            Email = @event.CreatedByEmail
        };

        await UpsertDocument(projectionItem, @event.PartitionKey, @event.Timestamp);
    }

    public async Task On(AggregateUpdatedEvent<Order> @event)
    {
        await SetDocumentUpdatedAt(@event.AggregateId, @event.PartitionKey, @event.UpdatedAt);
    }
}