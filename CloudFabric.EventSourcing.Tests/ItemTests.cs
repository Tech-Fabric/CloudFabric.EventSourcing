using CloudFabric.EventSourcing.EventStore;
using CloudFabric.EventSourcing.Tests.Domain;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloudFabric.EventSourcing.Tests;
public abstract class ItemTests
{
    protected abstract Task<IStore> GetStore();
    private IStore _store;

    [TestInitialize]
    public async Task Initialize()
    {
        _store = await GetStore();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _store.DeleteAll();
    }

    [TestMethod]
    public async Task SaveItem()
    {
        var item = new TestItem
        {
            Id = Guid.NewGuid(),
            Name = "Item1",
            Properties = new Dictionary<string, TestNestedItemClass>
            {
                { Guid.NewGuid().ToString(), new TestNestedItemClass() },
                { Guid.NewGuid().ToString(), new TestNestedItemClass() }
            }
        };

        Func<Task> action = async () => await _store.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);
        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task LoadItem()
    {
        var item = new TestItem
        {
            Id = Guid.NewGuid(),
            Name = "Item1",
            Properties = new Dictionary<string, TestNestedItemClass>
            {
                { Guid.NewGuid().ToString(), new TestNestedItemClass() }
            }
        };

        await _store.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);

        var loadedItem = await _store.LoadItem<TestItem>($"{item.Id}{item.Name}", $"{item.Id}{item.Name}");

        loadedItem.Id.Should().Be(item.Id);
        loadedItem.Name.Should().Be(item.Name);
        loadedItem.Properties.Keys.Should().BeEquivalentTo(item.Properties.Keys);
        loadedItem.Properties.Values.Should().BeEquivalentTo(item.Properties.Values);
    }

    [TestMethod]
    public async Task LoadItem_NullIfNotFound()
    {
        var loadedItem = await _store.LoadItem<TestItem>($"{Guid.NewGuid()}", $"{Guid.NewGuid()}");

        loadedItem.Should().BeNull();
    }

    [TestMethod]
    public async Task UpdateItem()
    {
        var item = new TestItem
        {
            Id = Guid.NewGuid(),
            Name = "Item1",
            Properties = new Dictionary<string, TestNestedItemClass>
            {
                { Guid.NewGuid().ToString(), new TestNestedItemClass() },
                { Guid.NewGuid().ToString(), new TestNestedItemClass() }
            }
        };

        await _store.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);

        string propertyGuid = Guid.NewGuid().ToString();

        item.Properties = new()
        {
            { propertyGuid, new TestNestedItemClass() }
        };

        Func<Task> action = async () => await _store.UpsertItem($"{item.Id}{item.Name}", $"{item.Id}{item.Name}", item);
        await action.Should().NotThrowAsync();

        var updatedItem = await _store.LoadItem<TestItem>($"{item.Id}{item.Name}", $"{item.Id}{item.Name}");
        updatedItem.Properties.ContainsKey(propertyGuid).Should().BeTrue();
    }
}
