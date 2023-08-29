namespace CloudFabric.EventSourcing.EventStore.Enums;

public static class ItemsStoreNameDefaults
{
    public const string TableNameSuffix = "-item";

    public static string AddDefaultTableNameSuffix(string tableName) 
        => string.Concat(tableName, TableNameSuffix);
}
