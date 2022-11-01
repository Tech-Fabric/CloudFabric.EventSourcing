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
        await UpdateDocument(@event.Id,
            @event.PartitionKey,
            (orderProjection) => 
            {
                orderProjection.Items.Add(new OrderListProjectionOrderItem
                {
                    Name = @event.Item.Name,
                    Amount = @event.Item.Amount,
                    TimeAdded = @event.Item.TimeAdded
                });

                orderProjection.ItemsCount++;
            });
    }

    public async Task On(OrderItemRemoved @event)
    {
        await UpdateDocument(@event.Id,
            @event.PartitionKey,
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
            Id = @event.Id,
            Name = @event.OrderName,
            ItemsCount = @event.Items.Count
        };

        projectionItem.Items = @event.Items.Select(
            x =>
            new OrderListProjectionOrderItem
            {
                Amount = x.Amount,
                Name = x.Name,
                TimeAdded = x.TimeAdded
            }
        ).ToList();

        projectionItem.CreatorInfo = new OrderListProjectionUserInfo
        {
            UserId = @event.CreatedById
        };

        await UpsertDocument(projectionItem, @event.PartitionKey);
    }
}