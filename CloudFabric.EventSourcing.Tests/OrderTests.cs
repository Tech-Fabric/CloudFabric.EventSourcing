using System.Diagnostics;
using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.EventSourcing.Tests.Domain;
using CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;
using CloudFabric.Projections;
using CloudFabric.Projections.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;

public abstract class OrderTests
{
    // Some projection engines take time to catch events and update projection records
    // (like cosmosdb with changefeed event observer)
    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(1000);
    protected abstract Task<IEventStore> GetEventStore();
    protected abstract IProjectionRepository<T> GetProjectionRepository<T>() where T : ProjectionDocument;
    
    protected abstract IProjectionRepository<ProjectionRebuildState> GetProjectionRebuildStateRepository();
    protected abstract IEventsObserver GetEventStoreEventsObserver();

    [TestCleanup]
    public async Task Cleanup()
    {
        var store = await GetEventStore();
        await store.DeleteAll();

        try
        {
            var projectionRepository = GetProjectionRepository<OrderListProjectionItem>();
            await projectionRepository.DeleteAll();

            var rebuildStateRepository = GetProjectionRebuildStateRepository();
            await rebuildStateRepository.DeleteAll();
        }
        catch
        {
        }
    }

    [TestMethod]
    public async Task TestPlaceOrder()
    {
        var orderRepository = new AggregateRepository<Order>(await GetEventStore());

        var userId = Guid.NewGuid();
        var userInfo = new EventUserInfo(userId);
        var id = Guid.NewGuid();
        var orderName = "Birthday Gift";
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "Caverna",
                12.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Dixit",
                6.59m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Patchwork",
                4.85m
            )
        };
        var order = new Order(id, orderName, items, userId, "john@gmail.com");

        await orderRepository.SaveAsync(userInfo, order);
        var order2 = await orderRepository.LoadAsync(id, PartitionKeys.GetOrderPartitionKey());
        order2.Id.Should().Be(id);
        order2.OrderName.Should().Be(orderName);
        order2.Items.Should().BeEquivalentTo(items);
        order2.Items.Count.Should().Be(3);
    }
    
    
    [TestMethod]
    public async Task TestOrderNotFound()
    {
        var orderRepository = new OrderRepository(await GetEventStore());

        var act = async () => await orderRepository.LoadOrder(Guid.Empty, PartitionKeys.GetOrderPartitionKey());

        await act.Should().ThrowAsync<EventStore.NotFoundException>();
    }

    [TestMethod]
    public async Task TestPlaceOrderAndAddItem()
    {
        var userId = Guid.NewGuid();
        var userInfo = new EventUserInfo(userId);
        var id = Guid.NewGuid();
        var orderName = "Birthday Gift";
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "Caverna",
                12.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Dixit",
                6.59m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Patchwork",
                4.85m
            )
        };

        var order = new Order(id, orderName, items, userId, "john@gmail.com");
        var orderRepository = new OrderRepository(await GetEventStore());

        // add another item:
        var addItem = new OrderItem(DateTime.UtcNow, "Eclipse", 6.95m);
        order.AddItem(addItem);
        await orderRepository.SaveOrder(userInfo, order);

        // update items so we can use it for comparison
        items.Add(addItem);

        var order2 = await orderRepository.LoadOrder(id, PartitionKeys.GetOrderPartitionKey());
        order2.Id.Should().Be(id);
        order2.OrderName.Should().Be(orderName);
        order2.Items.Should().BeEquivalentTo(items);
        order2.Items.Count.Should().Be(4);


        // add few other events
        for (var i = 0; i < 100; i++)
        {
            var addItemLoop = new OrderItem(DateTime.UtcNow, $"Eclipse-{i}", 6.95m + i);
            order2.AddItem(addItemLoop);
            items.Add(addItemLoop);
        }

        await orderRepository.SaveOrder(userInfo, order2);

        var order3 = await orderRepository.LoadOrder(id, PartitionKeys.GetOrderPartitionKey());
        order3.Id.Should().Be(id);
        order3.OrderName.Should().Be(orderName);
        order3.Items.Should().BeEquivalentTo(items);
        order3.Items.Count.Should().Be(104);
    }


    [TestMethod]
    public async Task TestPlaceOrderAndAddItemProjections()
    {
        var loggerFactory = new LoggerFactory();

        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);


        await projectionsEngine.StartAsync("TestInstance");


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

        orderProjection.Name.Should().Be(orderName);
        orderProjection.ItemsCount.Should().Be(items.Count);
        orderProjection.Items.Count.Should().Be(items.Count);
        orderProjection.CreatedBy.UserId.Should().Be(userId);

        var addItem = new OrderItem(DateTime.UtcNow, "Twilight Struggle", 6.95m);
        order.AddItem(addItem);
        order.AddItem(addItem);
        order.AddItem(addItem);
        order.AddItem(addItem);

        await orderRepository.SaveOrder(userInfo, order);

        await Task.Delay(ProjectionsUpdateDelay);

        items.Add(addItem);
        items.Add(addItem);
        items.Add(addItem);
        items.Add(addItem);
        
        var order2 = await orderRepository.LoadOrder(id, PartitionKeys.GetOrderPartitionKey());
        order2.Id.Should().Be(id);
        order2.OrderName.Should().Be(orderName);
        order2.Items.Should().BeEquivalentTo(items);
        order2.Items.Count.Should().Be(7);

        var orderProjection2 = await ordersListProjectionsRepository.Single(id, PartitionKeys.GetOrderPartitionKey());
        Debug.Assert(orderProjection2 != null, nameof(orderProjection2) + " != null");

        orderProjection2.Name.Should().Be(orderName);
        orderProjection2.ItemsCount.Should().Be(7);
        orderProjection2.CreatedBy.UserId.Should().Be(userId);

        var orderProjectionFromQuery =
            await ordersListProjectionsRepository.Query(
                ProjectionQuery.Where<OrderListProjectionItem>(d => d.Name == orderName)
            );
        orderProjectionFromQuery.TotalCount.Should().Be(1);
        orderProjectionFromQuery.Records.Count.Should().Be(1);
        orderProjectionFromQuery.Records.First().Name.Should().Be(orderName);

        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public async Task TestRebuildOrderDocumentProjection()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);


        await projectionsEngine.StartAsync("RebuildDocumentTestInstance");


        var userId = Guid.NewGuid();
        var userInfo = new EventUserInfo(userId);
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "RebuildDocumentItem",
                12.00m
            )
        };

        var firstOrder = new Order(Guid.NewGuid(), "Rebuild product first order", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, firstOrder);

        var secondOrder = new Order(Guid.NewGuid(), "Rebuild product second order", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        var firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        var secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().NotBeNull();
        
        // remove orders
        await ordersListProjectionsRepository.Delete(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        await ordersListProjectionsRepository.Delete(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());
        
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());
        firstOrderProjection.Should().BeNull();
        secondOrderProjection.Should().BeNull();

        // rebuild the firstOrder document
        await projectionsEngine.RebuildOneAsync(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());

        // check firstOrder document is rebuild and second is not
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().BeNull();

        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public async Task TestRebuildAllOrdersProjections()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);

        string instanceName = "RebuildOrdersTestInstance";
        
        await projectionsEngine.StartAsync(instanceName);


        var userId = Guid.NewGuid();
        var userInfo = new EventUserInfo(userId);
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "RebuildDocumentItem",
                12.00m
            )
        };

        var firstOrder = new Order(Guid.NewGuid(), "Rebuild orders first order", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, firstOrder);

        var secondOrder = new Order(Guid.NewGuid(), "Rebuild orders second order", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        var firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        var secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().NotBeNull();
        
        // remove orders
        await ordersListProjectionsRepository.Delete(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        await ordersListProjectionsRepository.Delete(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());
        
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());
        firstOrderProjection.Should().BeNull();
        secondOrderProjection.Should().BeNull();

        // rebuild the firstOrder document
        await projectionsEngine.RebuildAsync(instanceName, PartitionKeys.GetOrderPartitionKey());

        // wait for the rebuild state to be indexed
        await Task.Delay(ProjectionsUpdateDelay);

        // wait for the rebuild to finish
        ProjectionRebuildState rebuildState;
        do
        {
            rebuildState = await projectionsEngine.GetRebuildState(instanceName, PartitionKeys.GetOrderPartitionKey());
            await Task.Delay(10);
        }
        while (rebuildState.Status != RebuildStatus.Completed && rebuildState.Status != RebuildStatus.Failed);


        rebuildState.Status.Should().Be(RebuildStatus.Completed);

        // check firstOrder document is rebuild and second is not
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id, PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id, PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().NotBeNull();

        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public async Task TestProjectionsQuery()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);

        string instanceName = "ProjectionsQueryInstance";

        await projectionsEngine.StartAsync(instanceName);


        var userId = Guid.NewGuid();
        var userInfo = new EventUserInfo(userId);
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "Test",
                111.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Test",
                111.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Test",
                111.00m
            )
        };

        var firstOrder = new Order(Guid.NewGuid(), "First test order", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, firstOrder);

        // second order will contain only one item
        var secondOrder = new Order(Guid.NewGuid(), "Second queryable order", items.GetRange(0, 1), userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        var query = new ProjectionQuery
        {
            SearchText = "ORDER",
            Limit = 1
        };

        // query by name
        var orders = await ordersListProjectionsRepository.Query(query);
        orders.TotalCount.Should().Be(2);
        orders.Records.Count.Should().Be(1);

        query.SearchText = "queryable";
        query.Limit = 50;

        orders = await ordersListProjectionsRepository.Query(query);
        orders.TotalCount.Should().Be(1);
        orders.Records.Count.Should().Be(1);

        // add filter by count
        orders = await ordersListProjectionsRepository.Query(
            ProjectionQuery.Where<OrderListProjectionItem>(x => x.ItemsCount > 1)
        );
        orders.TotalCount.Should().Be(1);
        orders.Records.Count.Should().Be(1);

        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringMatching()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);

        string instanceName = "ProjectionsQueryInstance";

        await projectionsEngine.StartAsync(instanceName);


        var userId = Guid.NewGuid();
        var userInfo = new EventUserInfo(userId);
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "Test",
                111.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Test",
                111.00m
            ),
            new OrderItem(
                DateTime.UtcNow,
                "Test",
                111.00m
            )
        };

        var firstOrder = new Order(Guid.NewGuid(), "Qwerty123 order", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, firstOrder);

        // second order will contain only one item
        var secondOrder = new Order(Guid.NewGuid(), "Order Qwerty123", items.GetRange(0, 1), userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        var queryStartsWith = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("Qwerty123"))
            }
        };

        // query by name
        var ordersStartingWith = await ordersListProjectionsRepository.Query(queryStartsWith);
        ordersStartingWith.Records.Count.Should().Be(1);
        
        var queryEndsWith = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("Qwerty123"))
            }
        };
        
        var ordersEndingWith = await ordersListProjectionsRepository.Query(queryEndsWith);
        ordersEndingWith.Records.Count.Should().Be(1);
        
        var queryContaining = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.Contains("Qwerty123"))
            }
        };
        
        var ordersContaining = await ordersListProjectionsRepository.Query(queryContaining);
        ordersContaining.Records.Count.Should().Be(2);
        
        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public virtual async Task TestProjectionsNestedObjectsQuery()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);

        string instanceName = "ProjectionsNestedQueryInstance";

        await projectionsEngine.StartAsync(instanceName);


        var userInfo1 = new EventUserInfo(Guid.NewGuid());
        var userInfo2 = new EventUserInfo(Guid.NewGuid());
        var userInfo3 = new EventUserInfo(Guid.NewGuid());
        var firstOrderItems = new List<OrderItem>
        {
            new OrderItem(DateTime.UtcNow, "Colonizing Mars", 12.00m),
            new OrderItem(DateTime.UtcNow, "Patchwork", 6.59m),
            new OrderItem(DateTime.UtcNow, "Time Stories", 4.85m)
        };

        var firstOrder = new Order(Guid.NewGuid(), "New Years Gifts", firstOrderItems, userInfo1.UserId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo1, firstOrder);

        var secondOrderItems = new List<OrderItem>
        {
            new OrderItem(DateTime.UtcNow, "Caverna", 12.00m),
            new OrderItem(DateTime.UtcNow, "Dixit", 6.59m)
        };

        var secondOrder = new Order(Guid.NewGuid(), "Birthday Gifts", secondOrderItems, userInfo2.UserId, "will@gmail.com");
        await orderRepository.SaveOrder(userInfo2, secondOrder);

        var thirdOrder = new Order(Guid.NewGuid(), "Christmas Gifts", new List<OrderItem>(), userInfo3.UserId, "amy@gmail.com");
        await orderRepository.SaveOrder(userInfo3, thirdOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        // search by nested Items array
        var query = new ProjectionQuery
        {
            SearchText = "stories"
        };

        // query by name
        var orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.Count.Should().Be(1);
        orders.Records.First().Items.Count.Should().Be(3);

        query.SearchText = "dixit";
        orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.Count.Should().Be(1);
        orders.Records.First().Items.Count.Should().Be(2);

        query.SearchText = "amy@gmail.com";
        orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.Count.Should().Be(1);
        orders.Records.First().Items.Count.Should().Be(0);

        await projectionsEngine.StopAsync();
    }

    [TestMethod]
    public virtual async Task TestProjectionsNestedObjectsFilter()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);

        string instanceName = "ProjectionsNestedQueryInstance";

        await projectionsEngine.StartAsync(instanceName);


        var userInfo1 = new EventUserInfo(Guid.NewGuid());
        var userInfo2 = new EventUserInfo(Guid.NewGuid());
        var userInfo3 = new EventUserInfo(Guid.NewGuid());
        var firstOrderItems = new List<OrderItem>
        {
            new OrderItem(DateTime.UtcNow, "Colonizing Mars", 12.00m),
            new OrderItem(DateTime.UtcNow.AddDays(-7), "Patchwork", 6.59m),
            new OrderItem(DateTime.UtcNow, "Time Stories", 4.85m)
        };

        var firstOrder = new Order(Guid.NewGuid(), "New Years Gifts", firstOrderItems, userInfo1.UserId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo1, firstOrder);

        var secondOrderItems = new List<OrderItem>
        {
            new OrderItem(DateTime.UtcNow, "Caverna", 12.00m),
            new OrderItem(DateTime.UtcNow, "Dixit", 6.59m)
        };

        var secondOrder = new Order(Guid.NewGuid(), "Birthday Gifts", secondOrderItems, userInfo2.UserId, "will@gmail.com");
        await orderRepository.SaveOrder(userInfo2, secondOrder);

        var thirdOrder = new Order(Guid.NewGuid(), "Christmas Gifts", new List<OrderItem>(), userInfo3.UserId, "amy@gmail.com");
        await orderRepository.SaveOrder(userInfo3, thirdOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        // filter by creator id
        var query = new ProjectionQuery();
        query.Filters.Add(new Filter
        {
            PropertyName = "CreatedBy.UserId",
            Operator = FilterOperator.Equal,
            Value = userInfo2.UserId
        });

        var orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.Count.Should().Be(1);
        orders.Records.First().CreatedBy.UserId.Should().Be(userInfo2.UserId);

        // filter by items date
        query.Filters[0] = new Filter
        {
            PropertyName = "Items.AddedAt",
            Operator = FilterOperator.Lower,
            Value = DateTime.UtcNow.AddDays(-1)
        };

        orders = await ordersListProjectionsRepository.Query(query);

        orders.Records.Count.Should().Be(1);
        orders.Records.First().Items.Count.Should().Be(3);

        // filter by items Amount
        query.Filters[0] = new Filter
        {
            PropertyName = "Items.Amount",
            Operator = FilterOperator.GreaterOrEqual,
            Value = 5m
        };

        orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.Count.Should().Be(2);
        orders.Records.Any(x => x.Items.Count == 3).Should().BeTrue();
        orders.Records.Any(x => x.Items.Count == 2).Should().BeTrue();

        await projectionsEngine.StopAsync();
    }
    
    
    [TestMethod]
    public virtual async Task TestProjectionsNestedObjectsSorting()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

        // Repository containing projections - `view models` of orders
        var ordersListProjectionsRepository = GetProjectionRepository<OrderListProjectionItem>();
        var orderRepositoryEventsObserver = GetEventStoreEventsObserver();

        // Projections engine - takes events from events observer and passes them to multiple projection builders
        var projectionsEngine = new ProjectionsEngine(GetProjectionRebuildStateRepository());
        projectionsEngine.SetEventsObserver(orderRepositoryEventsObserver);

        var ordersListProjectionBuilder = new OrdersListProjectionBuilder(ordersListProjectionsRepository);
        projectionsEngine.AddProjectionBuilder(ordersListProjectionBuilder);

        string instanceName = "ProjectionsNestedQueryInstance";

        await projectionsEngine.StartAsync(instanceName);


        var userInfo1 = new EventUserInfo(Guid.NewGuid());
        var userInfo2 = new EventUserInfo(Guid.NewGuid());
        var userInfo3 = new EventUserInfo(Guid.NewGuid());
        var firstOrderItems = new List<OrderItem>
        {
            new OrderItem(DateTime.UtcNow, "Colonizing Mars", 12.00m),
            new OrderItem(DateTime.UtcNow, "Patchwork", 999m),
            new OrderItem(DateTime.UtcNow, "Time Stories", 4.85m)
        };

        var firstOrder = new Order(Guid.NewGuid(), "New Years Gifts", firstOrderItems, userInfo1.UserId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo1, firstOrder);

        var secondOrderItems = new List<OrderItem>
        {
            new OrderItem(DateTime.UtcNow, "Caverna", 999m),
            new OrderItem(DateTime.UtcNow, "Dixit", 6.59m)
        };

        var secondOrder = new Order(Guid.NewGuid(), "Birthday Gifts", secondOrderItems, userInfo2.UserId, "will@gmail.com");
        await orderRepository.SaveOrder(userInfo2, secondOrder);

        var thirdOrder = new Order(Guid.NewGuid(), "Christmas Gifts", new List<OrderItem>(), userInfo3.UserId, "amy@gmail.com");
        await orderRepository.SaveOrder(userInfo3, thirdOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        // search by nested Items array
        var query = new ProjectionQuery
        {
            OrderBy = new List<SortInfo>
            {
                new SortInfo
                {
                    KeyPath = "CreatedBy.Email",
                    Order = "desc"
                }
            }
        };

        // query by name
        var orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.Count.Should().Be(3);
        orders.Records.ElementAt(0).Id.Should().Be(secondOrder.Id);
        orders.Records.ElementAt(1).Id.Should().Be(firstOrder.Id);
        orders.Records.ElementAt(2).Id.Should().Be(thirdOrder.Id);

        // test sorting by array value with filter
        query.OrderBy = new List<SortInfo>
        {
            new SortInfo
            {
                KeyPath = "Items.Name",
                Order = "asc",
                Filters = new List<SortingFilter>
                {
                    new SortingFilter
                    {
                        FilterKeyPath = "Items.Amount",
                        FilterValue = 999m
                    }
                }
            }
        };
        
        orders = await ordersListProjectionsRepository.Query(query);
        orders.Records.ElementAt(0).Id.Should().Be(secondOrder.Id);
        orders.Records.ElementAt(1).Id.Should().Be(firstOrder.Id);
        orders.Records.ElementAt(2).Id.Should().Be(thirdOrder.Id);
        
        await projectionsEngine.StopAsync();
    }
}
