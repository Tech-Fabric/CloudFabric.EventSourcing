namespace CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

public record OrderItem(DateTime TimeAdded, string Name, decimal Amount);