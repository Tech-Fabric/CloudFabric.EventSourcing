using System.Globalization;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.EventSourcing.Tests.Domain;
using CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
using CloudFabric.EventSourcing.Tests.Domain.ValueObjects;
using CloudFabric.Projections.Queries;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;

public abstract class OrderStringComparisonTests: TestsBaseWithProjections<OrderListProjectionItem, OrdersListProjectionBuilder>
{
    [TestInitialize]
    public new async Task Initialize()
    {
        await base.Initialize();
        
        await PrepareTestOrders();
    }

    public async Task PrepareTestOrders()
    {
        // Event sourced repository storing streams of events. Main source of truth for orders.
        var orderRepository = new OrderRepository(await GetEventStore());

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

        var firstOrder = new Order(Guid.NewGuid(), "QwerTy123", items, userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, firstOrder);

        // second order will contain only one item
        var secondOrder = new Order(Guid.NewGuid(), "QwerTy123", items.GetRange(0, 1), userId, "john@gmail.com");
        await orderRepository.SaveOrder(userInfo, secondOrder);

        await Task.Delay(ProjectionsUpdateDelay);
    }

    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseSensitive()
    {
        var queryStartsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("Qwer"))
            }
        };

        // query by name
        var ordersStartingWithMatchCase = await ProjectionsRepository.Query(queryStartsWithMatchCase);
        ordersStartingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryStartsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer"))
            }
        };

        // query by name
        var ordersStartingWithDifferentCase = await ProjectionsRepository.Query(queryStartsWithDifferentCase);
        ordersStartingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseInsensitiveStringComparisonEnumOrdinal()
    {
        var queryStartsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer", StringComparison.OrdinalIgnoreCase))
            }
        };

        // query by name
        var ordersStartingWithMatchCase = await ProjectionsRepository.Query(queryStartsWithMatchCase);
        ordersStartingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryStartsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer", StringComparison.Ordinal))
            }
        };

        // query by name
        var ordersStartingWithDifferentCase = await ProjectionsRepository.Query(queryStartsWithDifferentCase);
        ordersStartingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseInsensitiveIgnoreCaseArgument()
    {
        var queryStartsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer", true, CultureInfo.InvariantCulture))
            }
        };

        // query by name
        var ordersStartingWithMatchCase = await ProjectionsRepository.Query(queryStartsWithMatchCase);
        ordersStartingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryStartsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer", false, CultureInfo.InvariantCulture))
            }
        };

        // query by name
        var ordersStartingWithDifferentCase = await ProjectionsRepository.Query(queryStartsWithDifferentCase);
        ordersStartingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringStartsWithCaseInsensitiveStringComparisonEnumInvariantCultureIgnoreCase()
    {
        var queryStartsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer", StringComparison.InvariantCultureIgnoreCase))
            }
        };

        // query by name
        var ordersStartingWithMatchCase = await ProjectionsRepository.Query(queryStartsWithMatchCase);
        ordersStartingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryStartsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.StartsWith("qwer", StringComparison.InvariantCulture))
            }
        };

        // query by name
        var ordersStartingWithDifferentCase = await ProjectionsRepository.Query(queryStartsWithDifferentCase);
        ordersStartingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringEndsWithCaseSensitive()
    {
        var queryEndsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("Ty123"))
            }
        };

        // query by name
        var ordersEndingWithMatchCase = await ProjectionsRepository.Query(queryEndsWithMatchCase);
        ordersEndingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryEndsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("ty123"))
            }
        };

        // query by name
        var ordersEndingWithDifferentCase = await ProjectionsRepository.Query(queryEndsWithDifferentCase);
        ordersEndingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringEndsWithCaseInSensitiveIgnoreCaseArgument()
    {
        var queryEndsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("ty123", true, CultureInfo.InvariantCulture))
            }
        };

        // query by name
        var ordersEndingWithMatchCase = await ProjectionsRepository.Query(queryEndsWithMatchCase);
        ordersEndingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryEndsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("ty123", false, CultureInfo.InvariantCulture))
            }
        };

        // query by name
        var ordersEndingWithDifferentCase = await ProjectionsRepository.Query(queryEndsWithDifferentCase);
        ordersEndingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringEndsWithCaseInSensitiveStringComparisonEnumInvariantCultureIgnoreCase()
    {
        var queryEndsWithMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("ty123", StringComparison.InvariantCultureIgnoreCase))
            }
        };

        // query by name
        var ordersEndingWithMatchCase = await ProjectionsRepository.Query(queryEndsWithMatchCase);
        ordersEndingWithMatchCase.Records.Count.Should().Be(2);
        
        var queryEndsWithDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.EndsWith("ty123", StringComparison.InvariantCulture))
            }
        };

        // query by name
        var ordersEndingWithDifferentCase = await ProjectionsRepository.Query(queryEndsWithDifferentCase);
        ordersEndingWithDifferentCase.Records.Count.Should().Be(0);
    }
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringContainsCaseSensitive()
    {
        var queryContainsMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.Contains("rTy"))
            }
        };

        // query by name
        var ordersContainingMatchCase = await ProjectionsRepository.Query(queryContainsMatchCase);
        ordersContainingMatchCase.Records.Count.Should().Be(2);
        
        var queryContainingDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.Contains("rty"))
            }
        };

        // query by name
        var ordersContainingDifferentCase = await ProjectionsRepository.Query(queryContainingDifferentCase);
        ordersContainingDifferentCase.Records.Count.Should().Be(0);
    }
    
    
    [TestMethod]
    public async Task TestProjectionsQueryFilterStringContainsCaseInsensitive()
    {
        var queryContainsMatchCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.Contains("rty", StringComparison.InvariantCultureIgnoreCase))
            }
        };

        // query by name
        var ordersContainingMatchCase = await ProjectionsRepository.Query(queryContainsMatchCase);
        ordersContainingMatchCase.Records.Count.Should().Be(2);
        
        var queryContainingDifferentCase = new ProjectionQuery
        {
            Filters = new List<Filter>()
            {
                Filter.Where<OrderListProjectionItem>(f => f.Name.Contains("rty", StringComparison.InvariantCulture))
            }
        };

        // query by name
        var ordersContainingDifferentCase = await ProjectionsRepository.Query(queryContainingDifferentCase);
        ordersContainingDifferentCase.Records.Count.Should().Be(0);
    }
}
