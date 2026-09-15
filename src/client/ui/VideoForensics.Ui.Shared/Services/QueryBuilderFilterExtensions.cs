using Syncfusion.Blazor.QueryBuilder;

using System.Linq.Expressions;
using System.Reflection;

namespace VideoForensics.Ui.Shared.Services;

/// <summary>
/// Provides extension methods to convert Syncfusion QueryBuilder rules to LINQ predicates
/// for filtering collections of data.
/// </summary>
public static class QueryBuilderFilterExtensions
{
    /// <summary>
    /// Filters an enumerable collection based on the current rules in the QueryBuilder.
    /// If no rules are defined, returns the entire collection.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The source collection to filter.</param>
    /// <param name="rule">The root rule model from the QueryBuilder.</param>
    /// <returns>A filtered enumerable collection.</returns>
    public static IEnumerable<T> ApplyRules<T>(this IEnumerable<T> items, RuleModel? rule) where T : class
    {
        if (rule == null)
        {
            return items;
        }

        Expression<Func<T, bool>>? predicate = BuildPredicate<T>(rule);
        return predicate != null ? items.AsQueryable().Where(predicate) : items;
    }

    /// <summary>
    /// Builds a LINQ expression predicate from a Syncfusion QueryBuilder rule model.
    /// Recursively handles nested rules combined with AND/OR logic.
    /// </summary>
    /// <typeparam name="T">The type of items to filter.</typeparam>
    /// <param name="rule">The rule model to convert.</param>
    /// <returns>A compiled expression predicate, or null if the rule is empty.</returns>
    private static Expression<Func<T, bool>>? BuildPredicate<T>(RuleModel rule) where T : class
    {
        // Handle nested rules (groups with AND/OR logic)
        if (rule.Rules != null && rule.Rules.Count > 0)
        {
            var predicates = new List<Expression<Func<T, bool>>>();

            foreach (RuleModel? nestedRule in rule.Rules)
            {
                Expression<Func<T, bool>>? nestedPredicate = BuildPredicate<T>(nestedRule);
                if (nestedPredicate != null)
                {
                    predicates.Add(nestedPredicate);
                }
            }

            if (predicates.Count == 0)
            {
                return null;
            }

            if (predicates.Count == 1)
            {
                return predicates[0];
            }

            // Combine predicates using the specified condition (AND or OR)
            bool isAnd = rule.Condition?.Equals("and", StringComparison.OrdinalIgnoreCase) ?? true;
            return CombinePredicates(predicates, isAnd);
        }

        // Handle single field rule
        return string.IsNullOrEmpty(rule.Field) ? null : (Expression<Func<T, bool>>)BuildFieldPredicate<T>(rule.Field, rule.Operator, rule.Value);
    }

    /// <summary>
    /// Builds a predicate for a single field filter.
    /// Supports common operators: equal, notequal, contains, startswith, endswith,
    /// greaterthan, lessthan, greaterthanorequal, lessthanorequal.
    /// Supports nested properties using dot notation (e.g. "Account.ProviderName").
    /// </summary>
    private static Expression<Func<T, bool>>? BuildFieldPredicate<T>(string fieldName, string? op, object? value) where T : class
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

        // Handle nested properties with dot notation (e.g., "Account.ProviderName")
        Expression? memberAccess = BuildNestedPropertyAccess(parameter, typeof(T), fieldName);

        if (memberAccess == null)
        {
            return null; // Field not found
        }

        // Get the property type from the member access expression
        Type propertyType = memberAccess.Type;
        string operatorLower = op?.ToLowerInvariant() ?? "equal";

        Expression? comparison = operatorLower switch
        {
            "equal" => BuildEqualityComparison(memberAccess, value, propertyType),
            "notequal" => Expression.Not(BuildEqualityComparison(memberAccess, value, propertyType)),
            "contains" => BuildContainsComparison(memberAccess, value),
            "startswith" => BuildStartsWithComparison(memberAccess, value),
            "endswith" => BuildEndsWithComparison(memberAccess, value),
            "greaterthan" => BuildGreaterThanComparison(memberAccess, value, propertyType),
            "lessthan" => BuildLessThanComparison(memberAccess, value, propertyType),
            "greaterthanorequal" => BuildGreaterThanOrEqualComparison(memberAccess, value, propertyType),
            "lessthanorequal" => BuildLessThanOrEqualComparison(memberAccess, value, propertyType),
            _ => BuildEqualityComparison(memberAccess, value, propertyType),
        };

        return comparison == null ? null : Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }

    /// <summary>
    /// Builds a member access expression that handles nested properties with dot notation.
    /// For example, "Account.ProviderName" traverses Account first, then ProviderName.
    /// </summary>
    private static Expression? BuildNestedPropertyAccess(Expression parameter, Type type, string fieldName)
    {
        string[] parts = fieldName.Split('.');
        Expression current = parameter;

        foreach (string part in parts)
        {
            PropertyInfo? property = type.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public);
            if (property == null)
            {
                return null; // Property not found
            }

            current = Expression.Property(current, property);
            type = property.PropertyType;
        }

        return current;
    }

    private static Expression BuildEqualityComparison(Expression memberAccess, object? value, Type propertyType)
    {
        object? convertedValue = ConvertValue(value, propertyType);
        ConstantExpression constant = Expression.Constant(convertedValue, propertyType);
        return Expression.Equal(memberAccess, constant);
    }

    private static Expression? BuildContainsComparison(Expression memberAccess, object? value)
    {
        if (value == null)
        {
            return null;
        }

        string? stringValue = value.ToString();
        if (string.IsNullOrEmpty(stringValue))
        {
            return null;
        }

        // For string properties, use string.Contains
        MethodInfo? containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
        ConstantExpression constant = Expression.Constant(stringValue, typeof(string));

        return Expression.Call(
            Expression.Convert(memberAccess, typeof(string)),
            containsMethod!,
            constant);
    }

    private static Expression? BuildStartsWithComparison(Expression memberAccess, object? value)
    {
        if (value == null)
        {
            return null;
        }

        string? stringValue = value.ToString();
        if (string.IsNullOrEmpty(stringValue))
        {
            return null;
        }

        MethodInfo? startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
        ConstantExpression constant = Expression.Constant(stringValue, typeof(string));

        return Expression.Call(
            Expression.Convert(memberAccess, typeof(string)),
            startsWithMethod!,
            constant);
    }

    private static Expression? BuildEndsWithComparison(Expression memberAccess, object? value)
    {
        if (value == null)
        {
            return null;
        }

        string? stringValue = value.ToString();
        if (string.IsNullOrEmpty(stringValue))
        {
            return null;
        }

        MethodInfo? endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
        ConstantExpression constant = Expression.Constant(stringValue, typeof(string));

        return Expression.Call(
            Expression.Convert(memberAccess, typeof(string)),
            endsWithMethod!,
            constant);
    }

    private static Expression BuildGreaterThanComparison(Expression memberAccess, object? value, Type propertyType)
    {
        object? convertedValue = ConvertValue(value, propertyType);
        ConstantExpression constant = Expression.Constant(convertedValue, propertyType);
        return Expression.GreaterThan(memberAccess, constant);
    }

    private static Expression BuildLessThanComparison(Expression memberAccess, object? value, Type propertyType)
    {
        object? convertedValue = ConvertValue(value, propertyType);
        ConstantExpression constant = Expression.Constant(convertedValue, propertyType);
        return Expression.LessThan(memberAccess, constant);
    }

    private static Expression BuildGreaterThanOrEqualComparison(Expression memberAccess, object? value, Type propertyType)
    {
        object? convertedValue = ConvertValue(value, propertyType);
        ConstantExpression constant = Expression.Constant(convertedValue, propertyType);
        return Expression.GreaterThanOrEqual(memberAccess, constant);
    }

    private static Expression BuildLessThanOrEqualComparison(Expression memberAccess, object? value, Type propertyType)
    {
        object? convertedValue = ConvertValue(value, propertyType);
        ConstantExpression constant = Expression.Constant(convertedValue, propertyType);
        return Expression.LessThanOrEqual(memberAccess, constant);
    }

    /// <summary>
    /// Combines multiple predicates using AND or OR logic.
    /// </summary>
    private static Expression<Func<T, bool>> CombinePredicates<T>(List<Expression<Func<T, bool>>> predicates, bool isAnd) where T : class
    {
        if (predicates.Count == 0)
        {
            return x => true; // No filters, return all
        }

        if (predicates.Count == 1)
        {
            return predicates[0];
        }

        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (Expression<Func<T, bool>> predicate in predicates)
        {
            InvocationExpression invokedPredicate = Expression.Invoke(predicate, parameter);

            combined = combined == null
                ? invokedPredicate
                : isAnd
                    ? Expression.AndAlso(combined, invokedPredicate)
                    : Expression.OrElse(combined, invokedPredicate);
        }

        return Expression.Lambda<Func<T, bool>>(combined!, parameter);
    }

    /// <summary>
    /// Attempts to convert a value to the target property type.
    /// </summary>
    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return value;
        }
    }
}
