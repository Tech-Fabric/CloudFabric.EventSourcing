using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain.Events;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

#pragma warning disable CS8618

namespace CloudFabric.EventSourcing.Tests.Domain;

public class Order : AggregateBase
{
    public Order(IEnumerable<IEvent> events) : base(events)
    {
    }

    public Order(Guid id, string orderName, List<OrderItem> items)
    {
        Apply(new OrderPlaced(id, orderName, items));
    }

    public Guid Id { get; private set; }
    public string OrderName { get; private set; }
    public List<OrderItem> Items { get; private set; }

    public void AddItem(OrderItem item)
    {
        Apply(new OrderItemAdded(this.Id, item));
    }

    public void RemoveItem(string name)
    {
        if (Items.Any(x => x.Name == name))
        {
            Apply(new OrderItemRemoved(this.Id, name));
        }
    }

    #region Event Handlers

    protected void On(OrderPlaced @event)
    {
        Id = @event.Id;
        OrderName = @event.OrderName;
        Items = @event.Items;
    }

    protected void On(OrderItemAdded @event)
    {
        // build new list
        var items = new List<OrderItem>(Items) { @event.Item };
        // set to list with new item
        Items = items;
    }

    protected void On(OrderItemRemoved @event)
    {
        // build new list
        var items = new List<OrderItem>();
        items.AddRange(Items.Where(x => x.Name != @event.Name));
        // set to list without item
        Items = items;
    }

    protected override void RaiseEvent(IEvent @event)
    {
        ((dynamic)this).On((dynamic)@event);
    }

    #endregion
}