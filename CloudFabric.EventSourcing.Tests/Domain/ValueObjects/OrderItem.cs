namespace CloudFabric.EventSourcing.Tests.Domain.ValueObjects;

public record OrderItem(DateTime AddedAt, string Name, decimal Amount);