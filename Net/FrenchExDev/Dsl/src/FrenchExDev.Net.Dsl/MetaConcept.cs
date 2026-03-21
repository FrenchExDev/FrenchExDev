namespace FrenchExDev.Net.Dsl;

/// <summary>
/// Base class for behavioral companion classes.
/// Every DSL concept has a companion that carries validation, containment rules,
/// and lifecycle hooks. The Ecore EClass equivalent.
/// </summary>
public abstract class MetaConcept
{
    public abstract string Name { get; }
    public abstract Type AttributeType { get; }

    public virtual IReadOnlyList<Type> SuperTypes => Array.Empty<Type>();

    /// <summary>
    /// Validates an M1 instance of this concept.
    /// Override to add behavioral validation.
    /// </summary>
    public virtual ConstraintResult Validate(ConceptValidationContext context) => ConstraintResult.Satisfied();

    /// <summary>
    /// Checks if a child concept can be contained by this concept.
    /// </summary>
    public virtual bool CanContain(MetaConcept child) => true;

    /// <summary>
    /// Checks metamodel inheritance.
    /// </summary>
    public virtual bool IsSuperTypeOf(MetaConcept other)
    {
        if (other.GetType() == GetType()) return true;
        foreach (var superType in other.SuperTypes)
        {
            if (superType == GetType()) return true;
        }
        return false;
    }

    // Lifecycle gates
    public virtual void OnDiscovered(ConceptValidationContext context) { }
    public virtual void OnBeforeValidation(ConceptValidationContext context) { }
    public virtual void OnAfterValidation(ConceptValidationContext context, ConstraintResult result) { }
}
