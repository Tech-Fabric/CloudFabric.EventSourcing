using System.Collections.ObjectModel;
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

    public Order(Guid id, string orderName, List<OrderItem> items, Guid createdById, string createdByEmail)
    {
        Apply(new OrderPlaced(id, orderName, PartitionKey, items, createdById, createdByEmail));
    }

    public override string PartitionKey => PartitionKeys.GetOrderPartitionKey();

    public string OrderName { get; private set; }
    
    /// <summary>
    /// It should not be possible to modify the collection from outside.
    /// The only way to modify the collection is by calling aggregate methods AddItem and RemoveItem.
    /// </summary>
    public ReadOnlyCollection<OrderItem> Items { get; private set; }
    public Guid CreatedById { get; private set; }

    public void AddItem(OrderItem item)
    {
        Apply(new OrderItemAdded(Id, item, PartitionKey));
    }

    public void RemoveItem(string name)
    {
        if (Items.Any(x => x.Name == name))
        {
            Apply(new OrderItemRemoved(Id, name, PartitionKey));
        }
    }

    #region Event Handlers

    public void On(OrderPlaced @event)
    {
        Id = @event.AggregateId!.Value;
        OrderName = @event.OrderName;
        Items = new ReadOnlyCollection<OrderItem>(@event.Items);
        CreatedById = @event.CreatedById;
    }

    public void On(OrderItemAdded @event)
    {
        // build new list
        var items = new List<OrderItem>(Items) { @event.Item };
        // set to list with new item
        Items = items.AsReadOnly();
    }

    public void On(OrderItemRemoved @event)
    {
        // build new list
        var items = new List<OrderItem>();
        items.AddRange(Items.Where(x => x.Name != @event.Name));
        // set to list without item
        Items = items.AsReadOnly();
    }

    #endregion
}