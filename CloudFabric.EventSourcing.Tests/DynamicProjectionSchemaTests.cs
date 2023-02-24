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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;

public class OrderListsDynamicProjectionBuilder : ProjectionBuilder,
    IHandleEvent<OrderPlaced>,
    IHandleEvent<OrderItemAdded>,
    IHandleEvent<OrderItemRemoved>
{
    private readonly ProjectionDocumentSchema _projectionDocumentSchema;

    public OrderListsDynamicProjectionBuilder(
        ProjectionRepositoryFactory projectionRepositoryFactory,
        ProjectionDocumentSchema projectionDocumentSchema
    ) : base(projectionRepositoryFactory)
    {
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public async Task On(OrderPlaced @event)
    {
        var document = new Dictionary<string, object?>()
        {
            { "Id", @event.AggregateId },
            { "Name", @event.OrderName },
            { "ItemsCount", @event.Items.Count }
        };
        
        if (_projectionDocumentSchema.Properties.Any(p => p.PropertyName == "TotalPrice"))
        {
            document["TotalPrice"] = (decimal)@event.Items.Sum(i => i.Amount);
        }
        
        await UpsertDocument(
            _projectionDocumentSchema,
            document,
            @event.PartitionKey,
            @event.Timestamp
        );
    }

    public async Task On(OrderItemAdded @event)
    {
        await UpdateDocument(
            _projectionDocumentSchema,
            @event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (orderProjection) =>
            {
                orderProjection["ItemsCount"] = (int)(orderProjection["ItemsCount"] ?? 0) + 1;

                // This is a test projection builder and the goal is to simulate addition/removal of a projection field.
                if (_projectionDocumentSchema.Properties.Any(p => p.PropertyName == "TotalPrice"))
                {
                    if (!orderProjection.ContainsKey("TotalPrice"))
                    {
                        orderProjection.Add("TotalPrice", 0m);
                    }
                    orderProjection["TotalPrice"] = (decimal)(orderProjection["TotalPrice"] ?? 0m) + @event.Item.Amount;
                }
            }
        );
    }

    public async Task On(OrderItemRemoved @event)
    {
        await UpdateDocument(
            _projectionDocumentSchema,
            @event.AggregateId, 
            @event.PartitionKey,
            @event.Timestamp,
            (orderProjection) =>
            {
                orderProjection["ItemsCount"] = (int)(orderProjection["ItemsCount"] ?? 0) - 1;
                
                // This is a test projection builder and the goal is to simulate addition/removal of a projection field.
                if (_projectionDocumentSchema.Properties.Any(p => p.PropertyName == "TotalPrice"))
                {
                    orderProjection["TotalPrice"] = (decimal)(orderProjection["TotalPrice"] ?? 0m) - @event.Item.Amount;
                }
            }
        );
    }
    
    public async Task On(AggregateUpdatedEvent<Order> @event)
    {
        await SetDocumentUpdatedAt(_projectionDocumentSchema, @event.AggregateId, @event.PartitionKey, @event.Timestamp);
    }
}

public abstract class DynamicProjectionSchemaTests
{
    // Some projection engines take time to catch events and update projection records
    // (like cosmosdb with changefeed event observer)
    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(1000);
    protected abstract Task<IEventStore> GetEventStore();

    protected abstract ProjectionRepositoryFactory GetProjectionRepositoryFactory();

    protected abstract IEventsObserver GetEventStoreEventsObserver();

    private const string _projectionsSchemaName = "orders-projections";

    [TestCleanup]
    [TestInitialize]
    public async Task Cleanup()
    {
        var store = await GetEventStore();
        await store.DeleteAll();

        try
        {
            var projectionRepository = GetProjectionRepositoryFactory()
                .GetProjectionRepository(
                    new ProjectionDocumentSchema
                    {
                        SchemaName = _projectionsSchemaName
                    }
                );
            await projectionRepository.DeleteAll();

            var rebuildStateRepository = GetProjectionRepositoryFactory()
                .GetProjectionRepository<ProjectionRebuildState>();

            await rebuildStateRepository.DeleteAll();
        }
        catch
        {
        }
    }

    private async Task<(ProjectionsEngine, IProjectionRepository)> PrepareProjection(IEventsObserver eventsObserver, ProjectionDocumentSchema schema)
    {
        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepositoryFactory()
            .GetProjectionRepository(schema);

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(
            GetProjectionRepositoryFactory().GetProjectionRepository<ProjectionRebuildState>()
        );
        projectionsEngine.SetEventsObserver(eventsObserver);

        var ordersListProjectionBuilder = new OrderListsDynamicProjectionBuilder(
            GetProjectionRepositoryFactory(),
            schema
        );
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);
        
        await projectionsEngine.StartAsync("TestInstance");

        return (projectionsEngine, ordersListProjectionsRepository);
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
                    PropertyType = TypeCode.Object
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
        
        var (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjection(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        
        var userId = Guid.NewGuid();
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

        var order = new Order(id, orderName, items, userId, "john@gmail.com");

        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        var orderProjection = await ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection != null, nameof(orderProjection) + " != null");

        orderProjection["Id"].Should().Be(order.Id);
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

        var orderProjection2 = await ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection2 != null, nameof(orderProjection2) + " != null");

        orderProjection2["Name"].Should().Be(orderName);
        orderProjection2["ItemsCount"].Should().Be(4);

        var orderProjectionFromQuery =
            await ordersListProjectionsRepository.Query(
                ProjectionQueryExpressionExtensions.Where<OrderListProjectionItem>(d => d.Name == orderName)
            );
        orderProjectionFromQuery.TotalRecordsFound.Should().Be(1);
        orderProjectionFromQuery.Records.Count.Should().Be(1);
        orderProjectionFromQuery.Records.First().Document["Name"].Should().Be(orderName);

        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public async Task TestPlaceOrderAndAddItemtoDynamicProjectionWithCreatingNewProjectionField()
    {
        // Step 1 - store one order and confirm it's projection is created
        
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
                    PropertyType = TypeCode.Object
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

        var (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjection(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        
        var userId = Guid.NewGuid();
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

        var order = new Order(id, orderName, items, userId, "john@gmail.com");

        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        var orderProjection = await ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection != null, nameof(orderProjection) + " != null");

        orderProjection["Id"].Should().Be(order.Id);
        orderProjection["ItemsCount"].Should().Be(3);
        
        order.AddItem(new OrderItem(DateTime.UtcNow, "Caverna", 12m));
        
        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        var orderProjectionWithNewItem = await ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjectionWithNewItem != null, nameof(orderProjectionWithNewItem) + " != null");

        orderProjectionWithNewItem["Id"].Should().Be(order.Id);
        orderProjectionWithNewItem["ItemsCount"].Should().Be(4);
        
        await projectionsEngine.StopAsync();
        
        // Step 2 - Add new property to projection schema
        
        ordersProjectionSchema.Properties.Add( new ProjectionDocumentPropertySchema()
        {
            PropertyName = "TotalPrice",
            IsFilterable = true,
            PropertyType = TypeCode.Decimal
        });
        
        (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjection(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        
        var addItem = new OrderItem(DateTime.UtcNow, "Twilight Struggle", 6.95m);
        order.AddItem(addItem);

        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        var orderProjectionWithNewSchemaTotalPrice = await ordersListProjectionsRepository
            .Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjectionWithNewSchemaTotalPrice != null, nameof(orderProjectionWithNewSchemaTotalPrice) + " != null");

        orderProjectionWithNewSchemaTotalPrice["Id"].Should().Be(order.Id);
        orderProjectionWithNewSchemaTotalPrice["ItemsCount"].Should().Be(5);
        
        // Important! After we added a new projection field it's required
        // to re-run all projection builders from the first event (rebuild all projections)
        // Since we didn't rebuild projections, new field will only have data for events that happened after the field was added.
        orderProjectionWithNewSchemaTotalPrice["TotalPrice"].Should().Be(6.95m);

        var query = new ProjectionQuery();
        query.Filters = new List<Filter>()
        {
            new Filter("TotalPrice", FilterOperator.Greater, 6m)
        };

        var searchResult = await ordersListProjectionsRepository.Query(query);
        searchResult.Records.Count.Should().Be(1);
        
        await projectionsEngine.StartRebuildAsync("rebuild", PartitionKeys.GetOrderPartitionKey());

        // wait for the rebuild state to be indexed
        await Task.Delay(ProjectionsUpdateDelay);

        // wait for the rebuild to finish
        ProjectionRebuildState rebuildState;
        do
        {
            rebuildState = await projectionsEngine.GetRebuildState("rebuild", PartitionKeys.GetOrderPartitionKey());
            await Task.Delay(10);
        } while (rebuildState.Status != RebuildStatus.Completed && rebuildState.Status != RebuildStatus.Failed);

        rebuildState.Status.Should().Be(RebuildStatus.Completed);
        
        var orderProjectionWithNewSchemaTotalPriceAfterRebuild = await ordersListProjectionsRepository
            .Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjectionWithNewSchemaTotalPriceAfterRebuild != null, nameof(orderProjectionWithNewSchemaTotalPriceAfterRebuild) + " != null");

        orderProjectionWithNewSchemaTotalPriceAfterRebuild["Id"].Should().Be(order.Id);
        orderProjectionWithNewSchemaTotalPriceAfterRebuild["ItemsCount"].Should().Be(5);
        
        // Projections were rebuilt, that means TotalPrice should have all items summed up
        orderProjectionWithNewSchemaTotalPriceAfterRebuild["TotalPrice"].Should().Be(42.39m);
    }

    [TestMethod]
    public async Task TestPlaceOrderAndAddItemtoDynamicProjectionWithRemovingProjectionField()
    {
        // todo: same as previous, but with additional step:
        // removing a projection field and then making sure it's removed from underlying projection storage (new column in postgresql/new index in elastic etc)
    }
}
