namespace FrenchExDev.Net.Vos.Abstractions.Validation;

/// <summary>A single composable validation rule.</summary>
public interface IValidationRule<in T>
{
    IEnumerable<string> Validate(T target);
}

/// <summary>Provides all registered validation rules for a type.</summary>
public interface IValidationRuleProvider<T>
{
    IEnumerable<IValidationRule<T>> GetRules();
}
