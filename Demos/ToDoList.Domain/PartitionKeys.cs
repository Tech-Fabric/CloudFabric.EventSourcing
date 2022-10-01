namespace ToDoList.Domain;

public static class PartitionKeys
{
    public static string GetTaskPartitionKey() => "Task";

    public static string GetTaskListPartitionKey() => "TaskList";

    public static string GetUserAccountEmailAddressPartitionKey() => "UserAccountEmailAddress";

    public static string GetOrderPartitionKey() => "Order";
}
