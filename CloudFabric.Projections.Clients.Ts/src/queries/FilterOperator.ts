export enum FilterOperator {
    Equal = "eq",
    EqualIgnoreCase = "eq-ignor-case",
    NotEqual = "ne",
    Greater = "gt",
    GreaterOrEqual = "ge",
    Lower = "lt",
    LowerOrEqual = "le",
    StartsWith = "string-starts-with",
    EndsWith = "string-ends-with",
    Contains = "string-contains",
    StartsWithIgnoreCase = "string-starts-with-ignore-case",
    EndsWithIgnoreCase = "string-ends-with-ignore-case",
    ContainsIgnoreCase = "string-contains-ignore-case",
    ArrayContains = "array-contains",
    ArrayContainsIgnoreCase = "array-contains-ignor-case"
}