namespace CloudFabric.Projections.Queries;

public static class FilterOperator
{
    public const string Equal = "eq";
    public const string NotEqual = "ne";
    public const string Greater = "gt";
    public const string GreaterOrEqual = "ge";
    public const string Lower = "lt";
    public const string LowerOrEqual = "le";
    public const string StartsWith = "string-starts-with";
    public const string EndsWith = "string-ends-with";
    public const string Contains = "string-contains";
    public const string StartsWithIgnoreCase = "string-starts-with-ignore-case";
    public const string EndsWithIgnoreCase = "string-ends-with-ignore-case";
    public const string ContainsIgnoreCase = "string-contains-ignore-case";
    public const string ArrayContains = "array-contains";
}