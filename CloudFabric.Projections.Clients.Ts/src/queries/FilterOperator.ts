export enum FilterOperator {
    EQUAL = 'eq',
    NOT_EQUAL = 'ne',
    GREATER = 'gt',
    GREATER_OR_EQUAL = 'ge',
    LOWER = 'lt',
    LOWER_OR_EQUAL = 'le',
    STARTS_WITH = 'string-starts-with',
    ENDS_WITH = 'string-ends-with',
    CONTAINS = 'string-contains',
    STARTS_WITH_IGNORE_CASE = 'string-starts-with-ignore-case',
    ENDS_WITH_IGNORE_CASE = 'string-ends-with-ignore-case',
    CONTAINS_IGNORE_CASE = 'string-contains-ignore-case',
    ARRAY_CONTAINS = 'array-contains'
}