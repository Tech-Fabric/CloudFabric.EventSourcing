using System.Diagnostics;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.EventSourcing.Tests.Domain;
using CloudFabric.EventSourcing.Tests.Domain.Events;
using CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;
using CloudFabric.Projections;
using CloudFabric.Projections.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;

public class OrderListsDynamicProjectionBuilder : ProjectionBuilder, 
    IHandleEvent<OrderPlaced>,
    IHandleEvent<OrderItemAdded>,
    IHandleEvent<OrderItemRemoved>
{
    public OrderListsDynamicProjectionBuilder(IProjectionRepository repository) : base(repository)
    {
    }
    
    public async Task On(OrderPlaced @event)
    {
        await UpsertDocument(new Dictionary<string, object?>()
        {
            { "Id", @event.Id.ToString() },
            { "Name", @event.OrderName },
            { "ItemsCount", @event.Items.Count }
        },
        @event.PartitionKey);
    }

    public async Task On(OrderItemAdded @event)
    {
        await UpdateDocument(@event.Id, @event.PartitionKey, (orderProjection) =>
        {
            orderProjection["ItemsCount"] = (int)(orderProjection["ItemsCount"] ?? 0) + 1;
        });
    }

    public async Task On(OrderItemRemoved @event)
    {
        await UpdateDocument(@event.Id, @event.PartitionKey, (orderProjection) =>
        {
            orderProjection["ItemsCount"] = (int)(orderProjection["ItemsCount"] ?? 0) - 1;
        });
    }
}

public abstract class DynamicProjectionSchemaTests
{
    // Some projection engines take time to catch events and update projection records
    // (like cosmosdb with changefeed event observer)
    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(1000);
    protected abstract Task<IEventStore> GetEventStore();
    protected abstract IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema schema);
    
    protected abstract IProjectionRepository<ProjectionRebuildState> GetProjectionRebuildStateRepository();
    protected abstract IEventsObserver GetEventStoreEventsObserver();

    private const string _projectionsSchemaName = "orders-projections";

    [TestCleanup]
    public async Task Cleanup()
    {
        var store = await GetEventStore();
        await store.DeleteAll();

        try
        {
            var projectionRepository = GetProjectionRepository(new ProjectionDocumentSchema 
            {
                SchemaName = _projectionsSchemaName
            });
            await projectionRepository.DeleteAll();

            var rebuildStateRepository = GetProjectionRebuildStateRepository();
            await rebuildStateRepository.DeleteAll();
        }
        catch
        {
        }
    }

    [TestMethod]
    public async Task TestPlaceOrderAndAddItemtoDynamicProjection()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        var ordersProjectionSchema = new ProjectionDocumentSchema()
        {
            SchemaName = _projectionsSchemaName,
            Properties = new List<ProjectionDocumentPropertySchema>()
            {
                new ProjectionDocumentPropertySchema()
                {
                    PropertyName = "Id",
                    IsKey = true,
                    PropertyType = TypeCode.String
                },
                new ProjectionDocumentPropertySchema()
                {
                    PropertyName = "Name",
                    IsFilterable = true,
                    IsSearchable = true,
                    PropertyType = TypeCode.String
                },
                new ProjectionDocumentPropertySchema()
                {
                    PropertyName = "ItemsCount",
                    IsFilterable = true,
                    PropertyType = TypeCode.Int32
                }
            }
        };

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository(ordersProjectionSchema);

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrderListsDynamicProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);


        await projectionsEngine.StartAsync("TestInstance");


        var userId = new Guid().ToString();
        var userInfo = new EventUserInfo(userId);
        var id = Guid.NewGuid();
        var orderName = "New Year's Gifts";
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "Colonizing Mars",
                12.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Dixit",
                6.59m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Time Stories",
                4.85m
            )
        };

        var order = new Order(id, orderName, items);

        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        var orderProjection = await ordersListProjectionsRepository.Single(id.ToString(), PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection != null, nameof(orderProjection) + " != null");

        orderProjection["Id"].Should().Be(order.Id.ToString());
        orderProjection["ItemsCount"].Should().Be(items.Count);

        var addItem = new OrderItem(DateTime.UtcNow, "Twilight Struggle", 6.95m);
        order.AddItem(addItem);

        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        items.Add(addItem);
        var order2 = await orderRepository.LoadOrder(id, PartitionKeys.GetOrderPartitionKey());
        order2.Id.Should().Be(id);
        order2.OrderName.Should().Be(orderName);
        order2.Items.Should().BeEquivalentTo(items);
        order2.Items.Count.Should().Be(4);

        var orderProjection2 = await ordersListProjectionsRepository.Single(id.ToString(), PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection2 != null, nameof(orderProjection2) + " != null");

        orderProjection2["Name"].Should().Be(orderName);
        orderProjection2["ItemsCount"].Should().Be(4);

        var orderProjectionFromQuery =
            await ordersListProjectionsRepository.Query(
                ProjectionQuery.Where<OrderListProjectionItem>(d => d.Name == orderName)
            );
        orderProjectionFromQuery.Count.Should().Be(1);
        orderProjectionFromQuery.First()["Name"].Should().Be(orderName);

        await projectionsEngine.StopAsync();
    }
    
    [TestMethod]
    public async Task TestPlaceOrderAndAddItemtoDynamicProjectionWithCreatingNewProjectionField()
    {
        // todo: same as previous, but with additional step:
        // adding new projection field and then making sure it's added to underlying projection storage (new column in postgresql/new index in elastic etc)
    }
    
    [TestMethod]
    public async Task TestPlaceOrderAndAddItemtoDynamicProjectionWithRemovingProjectionField()
    {
        // todo: same as previous, but with additional step:
        // removing a projection field and then making sure it's removed from underlying projection storage (new column in postgresql/new index in elastic etc)
    }
}