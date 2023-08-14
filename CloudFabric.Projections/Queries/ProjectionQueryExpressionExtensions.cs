using System.Linq.Expressions;

namespace CloudFabric.Projections.Queries;

public static class ProjectionQueryExpressionExtensions
{
    
    public static ProjectionQuery Where<T>(Expression<Func<T, bool>> expression)
    {
        var query = new ProjectionQuery();
        query.Filters.Add(FilterExpressionExtensions.Where<T>(expression));
        return query;
    }

    public static Expression<Func<TObject, bool>>? FiltersToExpression<TObject>(this ProjectionQuery projectionQuery)
    {
        var parameter = Expression.Parameter(typeof(TObject), "document");
        Expression? expressionToReturn = null;

        var filterExpressions = projectionQuery.Filters.Select(f => f.ToExpression<TObject>(parameter)).ToList();

        if (filterExpressions.Count == 0)
        {
            return null;
        }
        else if (filterExpressions.Count == 1)
        {
            expressionToReturn = filterExpressions[0].Item1;
        }
        else if (filterExpressions.Count > 1)
        {
            expressionToReturn = Expression.AndAlso(filterExpressions[0].Item1, filterExpressions[1].Item1);

            foreach (var filterToAdd in filterExpressions.Skip(2))
            {
                expressionToReturn = Expression.AndAlso(expressionToReturn, filterToAdd.Item1);
            }
        }

        return expressionToReturn != null
            ? (Expression<Func<TObject, bool>>)Expression.Lambda(expressionToReturn, parameter)
            : null;
    }
}