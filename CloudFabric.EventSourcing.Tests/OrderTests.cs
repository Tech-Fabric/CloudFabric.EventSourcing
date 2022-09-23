using System.Diagnostics;
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
    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromSeconds(0);
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
        }
        catch
        {
        }
    }

    [TestMethod]
    public async Task TestPlaceOrder()
    {
        var orderRepository = new OrderRepository(await GetEventStore());

        var userId = new Guid().ToString();
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
        var order = new Order(id, orderName, items);

        await orderRepository.SaveOrder(userInfo, order);
        var order2 = await orderRepository.LoadOrder(id, PartitionKeys.GetOrderPartitionKey());
        order2.Id.Should().Be(id);
        order2.OrderName.Should().Be(orderName);
        order2.Items.Should().BeEquivalentTo(items);
        order2.Items.Count.Should().Be(3);
    }

    [TestMethod]
    public async Task TestPlaceOrderAndAddItem()
    {
        var userId = new Guid().ToString();
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

        var order = new Order(id, orderName, items);
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

        orderProjection.Name.Should().Be(orderName);
        orderProjection.ItemsCount.Should().Be(items.Count);

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

        orderProjection2.Name.Should().Be(orderName);
        orderProjection2.ItemsCount.Should().Be(4);

        var orderProjectionFromQuery =
            await ordersListProjectionsRepository.Query(
                ProjectionQuery.Where<OrderListProjectionItem>(d => d.Name == orderName)
            );
        orderProjectionFromQuery.Count.Should().Be(1);
        orderProjectionFromQuery.First().Name.Should().Be(orderName);

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


        var userId = new Guid().ToString();
        var userInfo = new EventUserInfo(userId);
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "RebuildDocumentItem",
                12.00m
            )
        };

        var firstOrder = new Order(Guid.NewGuid(), "Rebuild product first order", items);
        await orderRepository.SaveOrder(userInfo, firstOrder);

        var secondOrder = new Order(Guid.NewGuid(), "Rebuild product second order", items);
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        var firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        var secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().NotBeNull();
        
        // remove orders
        await ordersListProjectionsRepository.Delete(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        await ordersListProjectionsRepository.Delete(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        firstOrderProjection.Should().BeNull();
        secondOrderProjection.Should().BeNull();

        // rebuild the firstOrder document
        await projectionsEngine.RebuildOneAsync(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());

        // check firstOrder document is rebuild and second is not
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());

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


        var userId = new Guid().ToString();
        var userInfo = new EventUserInfo(userId);
        var items = new List<OrderItem>
        {
            new OrderItem(
                DateTime.UtcNow,
                "RebuildDocumentItem",
                12.00m
            )
        };

        var firstOrder = new Order(Guid.NewGuid(), "Rebuild orders first order", items);
        await orderRepository.SaveOrder(userInfo, firstOrder);

        var secondOrder = new Order(Guid.NewGuid(), "Rebuild orders second order", items);
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);

        var firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        var secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().NotBeNull();
        
        // remove orders
        await ordersListProjectionsRepository.Delete(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        await ordersListProjectionsRepository.Delete(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        firstOrderProjection.Should().BeNull();
        secondOrderProjection.Should().BeNull();

        // rebuild the firstOrder document
        await projectionsEngine.RebuildAsync(instanceName, PartitionKeys.GetOrderPartitionKey());

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
        firstOrderProjection = await ordersListProjectionsRepository.Single(firstOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());
        secondOrderProjection = await ordersListProjectionsRepository.Single(secondOrder.Id.ToString(), PartitionKeys.GetOrderPartitionKey());

        firstOrderProjection.Should().NotBeNull();
        secondOrderProjection.Should().NotBeNull();

        await projectionsEngine.StopAsync();
    }
}
