using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;
public abstract class ItemTests
{
    protected abstract Task<IEventStore> GetEventStore();
    private IEventStore _eventStore;

    protected TimeSpan ProjectionsUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(1000);

    [TestInitialize]
    public async Task Initialize()
    {
        _eventStore = await GetEventStore();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _eventStore.DeleteAll();
    }

    [TestMethod]
    public async Task SaveItem()
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = "Item1",
            Properties = new Dictionary<string, NestedItemClass>
            {
                { Guid.NewGuid().ToString(), new NestedItemClass() },
                { Guid.NewGuid().ToString(), new NestedItemClass() }
            }
        };

        Func<Task> action = async () => await _eventStore.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);
        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task LoadItem()
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = "Item1",
            Properties = new Dictionary<string, NestedItemClass>
            {
                { Guid.NewGuid().ToString(), new NestedItemClass() }
            }
        };

        await _eventStore.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);

        var loadedItem = await _eventStore.LoadItem<Item>($"{item.Id}{item.Name}", $"{item.Id}{item.Name}");

        loadedItem.Id.Should().Be(item.Id);
        loadedItem.Name.Should().Be(item.Name);
        loadedItem.Properties.Keys.Should().BeEquivalentTo(item.Properties.Keys);
        loadedItem.Properties.Values.Should().BeEquivalentTo(item.Properties.Values);
    }

    [TestMethod]
    public async Task LoadItem_NullIfNotFound()
    {
        var loadedItem = await _eventStore.LoadItem<Item>($"{Guid.NewGuid()}", $"{Guid.NewGuid()}");

        loadedItem.Should().BeNull();
    }

    [TestMethod]
    public async Task UpdateItem()
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = "Item1",
            Properties = new Dictionary<string, NestedItemClass>
            {
                { Guid.NewGuid().ToString(), new NestedItemClass() },
                { Guid.NewGuid().ToString(), new NestedItemClass() }
            }
        };

        await _eventStore.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);

        string propertyGuid = Guid.NewGuid().ToString();

        item.Properties = new()
        {
            { propertyGuid, new NestedItemClass() }
        };

        Func<Task> action = async () => await _eventStore.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);
        await action.Should().NotThrowAsync();

        var updatedItem = await _eventStore.LoadItem<Item>($"{item.Id}{item.Name}", $"{item.Id}{item.Name}");
        updatedItem.Properties.ContainsKey(propertyGuid).Should().BeTrue();
    }
}
