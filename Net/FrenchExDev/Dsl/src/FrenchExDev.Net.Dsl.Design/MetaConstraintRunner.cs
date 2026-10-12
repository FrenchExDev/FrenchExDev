namespace FrenchExDev.Net.Dsl.Design;

using System.Reflection;

/// <summary>
/// Invokes constraint methods on concept companion classes at design time.
/// Uses reflection to find and call the static methods referenced by [MetaConstraint].
/// </summary>
public static class MetaConstraintRunner
{
    public static ConstraintResult RunConstraints(Type attributeType, ConceptValidationContext context)
    {
        var results = new List<ConstraintResult>();

        foreach (var attr in attributeType.GetCustomAttributes<MetaConstraintAttribute>())
        {
            var method = attributeType.GetMethod(attr.ConstraintMethodName,
                BindingFlags.Public | BindingFlags.Static);

            if (method is null) continue;

            var result = method.Invoke(null, new object[] { context });
            if (result is ConstraintResult cr)
                results.Add(cr);
        }

        return ConstraintResult.Aggregate(results);
    }
}
