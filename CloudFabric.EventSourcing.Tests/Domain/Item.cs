namespace CloudFabric.EventSourcing.Tests.Domain;

public class Item
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Dictionary<string, NestedItemClass> Properties { get; set; }
}

public class NestedItemClass
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = nameof(Name).ToLowerInvariant();

    public DateTime date { get; } = DateTime.MinValue;
}