namespace CloudFabric.EventSourcing.Tests.Domain;

public class TestItem
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Dictionary<string, TestNestedItemClass> Properties { get; set; }
}

public class TestNestedItemClass
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = nameof(Name).ToLowerInvariant();

    public DateTime date { get; } = DateTime.MinValue;
}