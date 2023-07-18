using System.Diagnostics;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.EventSourcing.Tests.Domain;
using CloudFabric.EventSourcing.Tests.Domain.Events;
using CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;
using CloudFabric.Projections;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Worker;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        ProjectionDocumentSchema projectionDocumentSchema,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) : base(projectionRepositoryFactory, indexSelector)
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
        
        if (_projectionDocumentSchema.Properties.Any(p => p.PropertyName == "Tags"))
        {
            document["Tags"] = @event.Items.Select(i => i.Name);
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
                // so we only modify the dictionary when property exists in schema. That property will be added/removed to the schema by tests.
                if (_projectionDocumentSchema.Properties.Any(p => p.PropertyName == "TotalPrice"))
                {
                    if (!orderProjection.ContainsKey("TotalPrice"))
                    {
                        orderProjection.Add("TotalPrice", 0m);
                    }
                    orderProjection["TotalPrice"] = (decimal)(orderProjection["TotalPrice"] ?? 0m) + @event.Item.Amount;
                }
                
                if (_projectionDocumentSchema.Properties.Any(p => p.PropertyName == "Tags"))
                {
                    if (!orderProjection.ContainsKey("Tags"))
                    {
                        orderProjection.Add("Tags", new List<string>());
                    }
                    (orderProjection["Tags"] as List<object>)?.Add(@event.Item.Name);
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
                // so we only modify the dictionary when property exists in schema. That property will be added/removed to the schema by tests.
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
    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(10000);
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
        }
        catch
        {
        }
    }

    private async Task<(ProjectionsEngine, IProjectionRepository)> PrepareProjections(IEventsObserver eventsObserver, ProjectionDocumentSchema schema)
    {
        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepositoryFactory()
            .GetProjectionRepository(schema);

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine();
        projectionsEngine.SetEventsObserver(eventsObserver);

        var ordersListProjectionBuilder = new OrderListsDynamicProjectionBuilder(
            GetProjectionRepositoryFactory(),
            schema
        );
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);
        
        await projectionsEngine.StartAsync("TestInstance");

        return (projectionsEngine, ordersListProjectionsRepository);
    }

    private ProjectionsRebuildProcessor PrepareProjectionsRebuildProcessor(IEventsObserver eventsObserver, ProjectionDocumentSchema projectionDocumentSchema)
    {
        return new ProjectionsRebuildProcessor(
            GetProjectionRepositoryFactory().GetProjectionRepository(null),
            async (string connectionId) =>
            {
                var projectionsEngine = new ProjectionsEngine();

                var ordersListProjectionBuilder = new OrderListsDynamicProjectionBuilder(
                    GetProjectionRepositoryFactory(),
                    projectionDocumentSchema,
                    ProjectionOperationIndexSelector.ProjectionRebuild
                );
                projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);
        
                projectionsEngine.SetEventsObserver(eventsObserver);

                // no need to listen - we are attaching this projections engine to test event store which is already being observed
                // by tests projections engine (see PrepareProjections method)
                //await projectionsEngine.StartAsync("TestInstance");

                return projectionsEngine;
            },
            NullLogger<ProjectionsRebuildProcessor>.Instance
        );
    }

    [TestMethod]
    public async Task TestPlaceOrderAndAddItemToDynamicProjection()
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
        
        var (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjections(orderRepositoryEventsObserver, ordersProjectionSchema);
        var projectionsRebuildProcessor = PrepareProjectionsRebuildProcessor(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        await projectionsRebuildProcessor.RebuildProjectionsThatRequireRebuild();
        
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
    public async Task TestArrayAttributeDynamicProjection()
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
                },
                new ProjectionDocumentPropertySchema()
                {
                    PropertyName = "Tags",
                    IsFilterable = true,
                    IsNestedArray = true,
                    ArrayElementType = TypeCode.String
                }
            }
        };
        
        var (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjections(orderRepositoryEventsObserver, ordersProjectionSchema);
        var projectionsRebuildProcessor = PrepareProjectionsRebuildProcessor(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        await projectionsRebuildProcessor.RebuildProjectionsThatRequireRebuild();
        
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
            )
        };

        var order = new Order(id, orderName, items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, order);

        var orderTimeStories = new Order(Guid.NewGuid(), "Second Order - Time Stories", new List<OrderItem>()
        {
            new OrderItem(
                DateTime.UtcNow,
                "Time Stories",
                4.85m
            )
        }, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, orderTimeStories);
        
        await Task.Delay(ProjectionsUpdateDelay);

        var orderProjection = await ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection != null, nameof(orderProjection) + " != null");
        
        var query = new ProjectionQuery();
        query.Filters = new List<Filter>()
        {
            new Filter("Tags", FilterOperator.ArrayContains, "Dixit")
        };
        
        var orderProjectionLookupByTag = await ordersListProjectionsRepository.Query(
            query, PartitionKeys.GetOrderPartitionKey()
        );

        orderProjectionLookupByTag.Records.Count.Should().Be(1);
        
        orderProjectionLookupByTag.Records.First().Document!["Name"].Should().Be(orderName);
        orderProjectionLookupByTag.Records.First().Document!["ItemsCount"].Should().Be(2);

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
    public async Task TestPlaceOrderAndAddItemToDynamicProjectionWithCreatingNewProjectionField()
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

        var (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjections(orderRepositoryEventsObserver, ordersProjectionSchema);
        var projectionsRebuildProcessor = PrepareProjectionsRebuildProcessor(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        await projectionsRebuildProcessor.RebuildProjectionsThatRequireRebuild();

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

        var orderProjection = await TestHelpers.RepeatUntilNotNull(() => // repeat getting an item until it appears in the projection 
            ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey()), 
            TimeSpan.FromSeconds(10)
        );
        
        Debug.Assert(orderProjection != null, nameof(orderProjection) + " != null");

        orderProjection["Id"].Should().Be(order.Id);
        orderProjection["ItemsCount"].Should().Be(3);
        
        order.AddItem(new OrderItem(DateTime.UtcNow, "Caverna", 12m));

        var itemsCountShouldBe = 4; // since we added fourth item
        
        await orderRepository.SaveOrder(userInfo, order);
        
        var orderProjectionWithNewItem = await TestHelpers.RepeatUntil(   // projection does not update immediately, we will need to 
            () => ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey()),  // wait a couple of seconds depending on hardware
            a => a != null && a["ItemsCount"] as dynamic == itemsCountShouldBe, 
            ProjectionsUpdateDelay
        );
        
        Debug.Assert(orderProjectionWithNewItem != null, nameof(orderProjection) + " != null");

        orderProjectionWithNewItem["Id"].Should().Be(order.Id);
        orderProjectionWithNewItem["ItemsCount"].Should().Be(itemsCountShouldBe);
        
        await projectionsEngine.StopAsync();
        
        // Step 2 - Add new property to projection schema
        
        ordersProjectionSchema.Properties.Add( new ProjectionDocumentPropertySchema()
        {
            PropertyName = "TotalPrice",
            IsFilterable = true,
            PropertyType = TypeCode.Decimal
        });

        (projectionsEngine, ordersListProjectionsRepository) = await PrepareProjections(orderRepositoryEventsObserver, ordersProjectionSchema);

        await ordersListProjectionsRepository.EnsureIndex();
        //await builder.RebuildProjectionsThatRequireRebuild();
        
        var addItem = new OrderItem(DateTime.UtcNow, "Twilight Struggle", 6.95m);
        order.AddItem(addItem);

        await orderRepository.SaveOrder(userInfo, order);
        
        var orderProjectionWithNewSchemaTotalPrice = await ordersListProjectionsRepository
            .Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjectionWithNewSchemaTotalPrice != null, nameof(orderProjectionWithNewSchemaTotalPrice) + " != null");

        orderProjectionWithNewSchemaTotalPrice["Id"].Should().Be(order.Id);
        orderProjectionWithNewSchemaTotalPrice["ItemsCount"].Should().Be(5);
        
        // Important! After we added a new projection field it's required
        // to re-run all projection builders from the first event (rebuild all projections)
        // Since we didn't rebuild projections, new field will only have data for events that happened after the field was added
        // Hence total price is nly the price of last added item.
        orderProjectionWithNewSchemaTotalPrice["TotalPrice"].Should().Be(6.95m);

        var query = new ProjectionQuery();
        query.Filters = new List<Filter>()
        {
            new Filter("TotalPrice", FilterOperator.Greater, 6m)
        };

        var searchResult = await TestHelpers.RepeatUntil(
            () => ordersListProjectionsRepository.Query(query),
            (r) => r.Records.Count == 1,
            ProjectionsUpdateDelay
        );
        searchResult.Records.Count.Should().Be(1);

        await projectionsRebuildProcessor.RebuildProjectionsThatRequireRebuild();

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