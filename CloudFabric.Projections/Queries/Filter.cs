using System.Linq.Expressions;
using System.Reflection;

namespace CloudFabric.Projections.Queries;

public class Filter
{
    public List<FilterConnector> Filters = new ();

    public Filter()
    {
    }

    /// <summary>
    /// Creates empty filter with specified tag. 
    /// </summary>
    /// <param name="tag"></param>
    public Filter(string tag)
    {
        Tag = tag;
    }

    public Filter(string propertyName, string oper, object value, string tag = "")
    {
        PropertyName = propertyName;
        Operator = oper;
        Value = value;
        Tag = tag;
    }

    public Filter(Filter filterToClone)
    {
        PropertyName = filterToClone.PropertyName;
        Operator = filterToClone.Operator;
        Value = filterToClone.Value;
        Tag = filterToClone.Tag;

        Filters = filterToClone.Filters.Select(f => new FilterConnector(f)).ToList();
    }

    public string? PropertyName { get; set; }
    public string? Operator { get; set; }
    public object? Value { get; set; }

    /// <summary>
    /// Optional tag - any string used for referencing this particular filter later. Can be useful when serializing to query string.
    /// </summary>
    public string? Tag { get; set; }

    public bool Visible { get; set; } = true;

    /// <summary>
    /// Converts this filter object to Lambda Expression which can be used for linq.
    /// </summary>
    /// <param name="parameter">lambda argument - an object which will be passed to this expression and available for filtering.</param>
    /// <typeparam name="TParameter">type of the argument</typeparam>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public Expression<Func<TParameter, bool>> ToLambdaExpression<TParameter>(ParameterExpression? parameter = null)
    {
        if (string.IsNullOrEmpty(PropertyName))
        {
            throw new InvalidOperationException(
                "ToExpression: can't convert a filter to expression - PropertyName is empty"
            );
        }

        var (expression, expressionParameter) = ToExpression<TParameter>(parameter);
        return (Expression<Func<TParameter, bool>>)Expression.Lambda(expression, expressionParameter);
    }

    # region Reflection methods cache
    private static readonly MethodInfo StringStartsWith = typeof(string).GetMethods()
        .First(m => m.Name == "StartsWith" && m.GetParameters().Length == 1);
    private static readonly MethodInfo StringStartsWithIgnoreCaseArgument = typeof(string).GetMethods()
        .First(m => m.Name == "StartsWith" && m.GetParameters().Length == 3);
    private static readonly MethodInfo StringStartsWithStringComparisonArgument = typeof(string).GetMethods()
        .First(m => m.Name == "StartsWith" && m.GetParameters().Length == 2);
    
    private static readonly MethodInfo StringEndsWith = typeof(string).GetMethods()
        .First(m => m.Name == "EndsWith" && m.GetParameters().Length == 1);
    private static readonly MethodInfo StringEndsWithIgnoreCaseArgument = typeof(string).GetMethods()
        .First(m => m.Name == "EndsWith" && m.GetParameters().Length == 3);
    private static readonly MethodInfo StringEndsWithStringComparisonArgument = typeof(string).GetMethods()
        .First(m => m.Name == "EndsWith" && m.GetParameters().Length == 2);
    
    private static readonly MethodInfo StringContains = typeof(string).GetMethods()
        .First(m => m.Name == "Contains" && m.GetParameters().Length == 1);
    private static readonly MethodInfo StringContainsStringComparisonArgument = typeof(string).GetMethods()
        .First(m => m.Name == "Contains" && m.GetParameters().Length == 2);
    #endregion
    
    public (Expression, ParameterExpression) ToExpression<TParameter>(ParameterExpression? parameter = null)
    {
        if (string.IsNullOrEmpty(PropertyName))
        {
            throw new InvalidOperationException(
                "ToExpression: can't convert a filter to expression - PropertyName is empty"
            );
        }

        parameter ??= Expression.Parameter(typeof(TParameter), "document");

        Expression property;

        if (typeof(TParameter).Name.Contains("Dictionary"))
        {
            var dictionaryIndexProperty = typeof(TParameter).GetProperty("Item");
            property = Expression.MakeIndex(
                parameter, dictionaryIndexProperty,
                new[] { Expression.Constant(PropertyName) }
            );
        }
        else
        {
            property = Expression.PropertyOrField(parameter, PropertyName);
        }

        var value = Expression.Constant(Value);
        var operand = Expression.Convert(property, Value.GetType());
        
        Expression thisExpression = Operator switch
        {
            FilterOperator.Equal => Expression.Equal(operand, value),
            FilterOperator.NotEqual => Expression.NotEqual(operand, value),
            FilterOperator.Greater => Expression.GreaterThan(operand, value),
            FilterOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(operand, value),
            FilterOperator.Lower => Expression.LessThan(operand, value),
            FilterOperator.LowerOrEqual => Expression.LessThanOrEqual(operand, value),
            FilterOperator.StartsWith => Expression.Call(operand, StringStartsWith, value),
            FilterOperator.EndsWith => Expression.Call(operand, StringEndsWith, value),
            FilterOperator.Contains => Expression.Call(operand, StringContains, value),
            FilterOperator.StartsWithIgnoreCase => Expression.Call(operand, StringStartsWithStringComparisonArgument, value, Expression.Constant(StringComparison.InvariantCultureIgnoreCase)),
            FilterOperator.EndsWithIgnoreCase => Expression.Call(operand, StringEndsWithStringComparisonArgument, value, Expression.Constant(StringComparison.InvariantCultureIgnoreCase)),
            FilterOperator.ContainsIgnoreCase => Expression.Call(operand, StringContainsStringComparisonArgument, value, Expression.Constant(StringComparison.InvariantCultureIgnoreCase)),
            _ => throw new Exception(
                $"Cannot create an expression. Filter's operator is either incorrect or not supported: {Operator}"
            )
        };

        if (Filters.Count <= 0)
        {
            return (thisExpression, parameter);
        }

        foreach (var filter in Filters)
        {
            var (filterExpression, filterParameter) = filter.Filter.ToExpression<TParameter>(parameter);
            thisExpression = filter.Logic switch
            {
                FilterLogic.And => Expression.AndAlso(thisExpression, filterExpression),
                FilterLogic.Or => Expression.OrElse(thisExpression, filterExpression),
                _ => thisExpression
            };
        }

        return (thisExpression, parameter);
    }

    public static Filter Where<T>(Expression<Func<T, bool>> expression, string tag = "")
    {
        if (expression.NodeType == ExpressionType.Lambda)
        {
            var filter = ConstructFilterFromExpression(expression.Body);

            filter.Tag = tag;

            return filter;
        }

        throw new Exception($"Unsupported expression type: {expression.NodeType}");
    }

    public static object? GetExpressionValue(Expression expression)
    {
        if (expression.NodeType == ExpressionType.Constant)
        {
            return (expression as ConstantExpression)?.Value;
        }
        else if (expression.NodeType == ExpressionType.MemberAccess)
        {
            MemberInfo? memberInfo = (expression as MemberExpression)?.Member;

            if (memberInfo?.MemberType == MemberTypes.Field)
            {
                FieldInfo fieldInfo = (FieldInfo)memberInfo;
                var expressionValue = GetExpressionValue((expression as MemberExpression).Expression);
                var fieldValue = fieldInfo.GetValue(expressionValue);
                return fieldValue;
            }
            else if (memberInfo?.MemberType == MemberTypes.Property)
            {
                if ((expression as MemberExpression).Expression.NodeType == ExpressionType.Parameter)
                {
                    return (expression as MemberExpression).Member.Name;
                }

                // property is just a wrapper around field right?
                PropertyInfo propertyInfo = (PropertyInfo)memberInfo;
                var propertyValue = GetExpressionValue((expression as MemberExpression).Expression);
                var fieldValue = propertyInfo.GetValue(propertyValue);
                
                // TODO@sergey: nested objects member access
                return fieldValue;
            }
            else
            {
                throw new Exception($"Expression member type is not supported: {memberInfo.MemberType}");
            }
        }
        else if (expression.NodeType == ExpressionType.Call)
        {
            var methodCallExpression = (MethodCallExpression)expression;

            object[] args = methodCallExpression.Arguments.Select(e => GetExpressionValue(e)).ToArray();
            var obj = methodCallExpression.Object == null ? null : GetExpressionValue(methodCallExpression.Object);
            var result = methodCallExpression.Method.Invoke(obj, args);
            return result;
        }
        else if (expression.NodeType == ExpressionType.Convert)
        {
            var targetType = (expression as UnaryExpression).Type;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            var operand = (expression as UnaryExpression).Operand;
            object expressionValue = GetExpressionValue(operand);

            try
            {
                return Convert.ChangeType(expressionValue, targetType);
            }
            catch (Exception)
            {
            }

            return expressionValue;
        }
        else
        {
            throw new Exception($"Cannot get value from expression of type {expression.NodeType.ToString()}");
        }
    }

    private static Filter ConstructFilterFromExpression(Expression expression)
    {
        switch (expression.NodeType)
        {
            case ExpressionType.Equal:
                var filterEq = new Filter();
                filterEq.Operator = FilterOperator.Equal;
                filterEq.PropertyName = GetExpressionValue((expression as BinaryExpression).Left).ToString();
                filterEq.Value = GetExpressionValue((expression as BinaryExpression).Right);
                return filterEq;
            case ExpressionType.NotEqual:
                var filterNEq = new Filter();
                filterNEq.Operator = FilterOperator.NotEqual;
                filterNEq.PropertyName = GetExpressionValue((expression as BinaryExpression).Left).ToString();
                filterNEq.Value = GetExpressionValue((expression as BinaryExpression).Right);
                return filterNEq;
            case ExpressionType.GreaterThan:
                var filterGt = new Filter();
                filterGt.Operator = FilterOperator.Greater;
                filterGt.PropertyName = GetExpressionValue((expression as BinaryExpression).Left).ToString();
                filterGt.Value = GetExpressionValue((expression as BinaryExpression).Right);
                return filterGt;
            case ExpressionType.GreaterThanOrEqual:
                var filterGe = new Filter();
                filterGe.Operator = FilterOperator.GreaterOrEqual;
                filterGe.PropertyName = GetExpressionValue((expression as BinaryExpression).Left).ToString();
                filterGe.Value = GetExpressionValue((expression as BinaryExpression).Right);
                return filterGe;
            case ExpressionType.LessThan:
                var filterLt = new Filter();
                filterLt.Operator = FilterOperator.Lower;
                filterLt.PropertyName = GetExpressionValue((expression as BinaryExpression).Left).ToString();
                filterLt.Value = GetExpressionValue((expression as BinaryExpression).Right);
                return filterLt;
            case ExpressionType.LessThanOrEqual:
                var filterLe = new Filter();
                filterLe.Operator = FilterOperator.LowerOrEqual;
                filterLe.PropertyName = GetExpressionValue((expression as BinaryExpression).Left).ToString();
                filterLe.Value = GetExpressionValue((expression as BinaryExpression).Right);
                return filterLe;
            case ExpressionType.AndAlso:
                var leftAnd = ConstructFilterFromExpression((expression as BinaryExpression).Left);
                var rightAnd = ConstructFilterFromExpression((expression as BinaryExpression).Right);

                return leftAnd.And(rightAnd);
            case ExpressionType.OrElse:
                var leftOr = ConstructFilterFromExpression((expression as BinaryExpression).Left);
                var rightOr = ConstructFilterFromExpression((expression as BinaryExpression).Right);

                return leftOr.Or(rightOr);
            case ExpressionType.Call:
                var e = expression as MethodCallExpression;
                var property = (string)GetExpressionValue(e.Object);
                
                if (e.Method.Name == "StartsWith" && e.Method.DeclaringType == typeof(string))
                {
                    var filterOperator = FilterOperator.StartsWith;
                    
                    // this is for public bool StartsWith(string value, StringComparison comparisonType)
                    // overload
                    if (e.Arguments.Count == 2)
                    {
                        switch ((StringComparison)GetExpressionValue(e.Arguments[1])!)
                        {
                            case StringComparison.Ordinal:
                            case StringComparison.CurrentCulture:
                            case StringComparison.InvariantCulture:
                                filterOperator = FilterOperator.StartsWith;
                                break;
                            case StringComparison.OrdinalIgnoreCase:
                            case StringComparison.CurrentCultureIgnoreCase:
                            case StringComparison.InvariantCultureIgnoreCase:
                                filterOperator = FilterOperator.StartsWithIgnoreCase;
                                break;
                        }
                    }
                    // this is for public bool StartsWith(string value, bool ignoreCase, CultureInfo? culture)
                    // overload
                    else if (e.Arguments.Count == 3)
                    {
                        switch ((bool)GetExpressionValue(e.Arguments[1])!)
                        {
                            case false:
                                filterOperator = FilterOperator.StartsWith;
                                break;
                            case true:
                                filterOperator = FilterOperator.StartsWithIgnoreCase;
                                break;
                        }
                    }

                    return new Filter()
                    {
                        Operator = filterOperator,
                        PropertyName = property,
                        Value = GetExpressionValue(e.Arguments.First())
                    };
                }
                else if (e.Method.Name == "EndsWith" && e.Method.DeclaringType == typeof(string))
                {
                    var filterOperator = FilterOperator.EndsWith;
                    
                    // this is for public bool EndsWith(string value, StringComparison comparisonType)
                    // overload
                    if (e.Arguments.Count == 2)
                    {
                        switch ((StringComparison)GetExpressionValue(e.Arguments[1])!)
                        {
                            case StringComparison.Ordinal:
                            case StringComparison.CurrentCulture:
                            case StringComparison.InvariantCulture:
                                filterOperator = FilterOperator.EndsWith;
                                break;
                            case StringComparison.OrdinalIgnoreCase:
                            case StringComparison.CurrentCultureIgnoreCase:
                            case StringComparison.InvariantCultureIgnoreCase:
                                filterOperator = FilterOperator.EndsWithIgnoreCase;
                                break;
                        }
                    }
                    // this is for public bool EndsWith(string value, bool ignoreCase, CultureInfo? culture)
                    // overload
                    else if (e.Arguments.Count == 3)
                    {
                        switch ((bool)GetExpressionValue(e.Arguments[1])!)
                        {
                            case false:
                                filterOperator = FilterOperator.EndsWith;
                                break;
                            case true:
                                filterOperator = FilterOperator.EndsWithIgnoreCase;
                                break;
                        }
                    }
                    
                    return new Filter()
                    {
                        Operator = filterOperator,
                        PropertyName = property,
                        Value = GetExpressionValue(e.Arguments.First())
                    };
                }
                else if (e.Method.Name == "Contains" && e.Method.DeclaringType == typeof(string))
                {
                    var filterOperator = FilterOperator.Contains;
                    
                    // this is for public bool StartsWith(string value, StringComparison comparisonType)
                    // overload
                    if (e.Arguments.Count == 2)
                    {
                        switch ((StringComparison)GetExpressionValue(e.Arguments[1])!)
                        {
                            case StringComparison.Ordinal:
                            case StringComparison.CurrentCulture:
                            case StringComparison.InvariantCulture:
                                filterOperator = FilterOperator.Contains;
                                break;
                            case StringComparison.OrdinalIgnoreCase:
                            case StringComparison.CurrentCultureIgnoreCase:
                            case StringComparison.InvariantCultureIgnoreCase:
                                filterOperator = FilterOperator.ContainsIgnoreCase;
                                break;
                        }
                    }
                    
                    return new Filter()
                    {
                        Operator = filterOperator,
                        PropertyName = property,
                        Value = GetExpressionValue(e.Arguments.First())
                    };
                }
                else
                {
                    throw new Exception($"Unsupported method call in expression: {e.Method.Name}");
                }
            default:
                throw new Exception($"Unsupported expression type: {expression.NodeType}");
        }
    }

    public Filter Or(string propertyName, string oper, object value)
    {
        var filter = new Filter(propertyName, oper, value);
        return Or(filter);
    }

    public Filter Or(Filter f)
    {
        var connector = new FilterConnector(FilterLogic.Or, f);
        Filters.Add(connector);
        return this;
    }

    public Filter And(string propertyName, string oper, object value)
    {
        var filter = new Filter(propertyName, oper, value);
        return And(filter);
    }

    public Filter And(Filter f)
    {
        var connector = new FilterConnector(FilterLogic.And, f);
        Filters.Add(connector);
        return this;
    }

    public object Serialize()
    {
        var obj = new
        {
            p = PropertyName,
            o = Operator,
            v = Value,
            vi = Visible,
            t = Tag,
            f = Filters.Select(f => f.Serialize()).ToList()
        };

        return obj;
    }

    public static Filter Deserialize(dynamic f)
    {
        Filter filter;

        filter = new Filter(f.p.ToString(), f.o.ToString(), f.v, f.t.ToString());
        if (f.vi != null)
        {
            filter.Visible = f.vi;
        }

        if (f.f != null && f.f.Count > 0)
        {
            filter.Filters = new List<FilterConnector>();

            foreach (var ff in f.f)
            {
                filter.Filters.Add(FilterConnector.Deserialize(ff));
            }
        }

        return filter;
    }
}
