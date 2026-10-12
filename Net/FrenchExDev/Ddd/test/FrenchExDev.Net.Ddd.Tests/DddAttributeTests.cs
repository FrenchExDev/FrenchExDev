namespace FrenchExDev.Net.Ddd.Tests;

using FrenchExDev.Net.Ddd.Attributes;
using FrenchExDev.Net.Ddd.Attributes.Concepts;
using FrenchExDev.Net.Dsl;
using Xunit;

public class DddAttributeTests
{
    [Fact]
    public void AggregateRoot_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(AggregateRootAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(AggregateRootConcept), attr!.ConceptType);
    }

    [Fact]
    public void Entity_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(EntityAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void ValueObject_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(ValueObjectAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void Composition_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(CompositionAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void Invariant_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(InvariantAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void Command_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(CommandAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void DomainEvent_has_MetaConcept()
    {
        var attr = (MetaConceptAttribute?)Attribute.GetCustomAttribute(
            typeof(DomainEventAttribute), typeof(MetaConceptAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    public void AggregateRoot_inherits_Entity_at_metamodel_level()
    {
        var attr = (MetaInheritsAttribute?)Attribute.GetCustomAttribute(
            typeof(AggregateRootAttribute), typeof(MetaInheritsAttribute));
        Assert.NotNull(attr);
        Assert.Equal(typeof(EntityConcept), attr!.ParentConceptType);
    }

    [Fact]
    public void AggregateRoot_has_MustHaveId_constraint()
    {
        var attrs = Attribute.GetCustomAttributes(
            typeof(AggregateRootAttribute), typeof(MetaConstraintAttribute));
        Assert.Contains(attrs, a => ((MetaConstraintAttribute)a).Name == "MustHaveId");
    }
}

public class AggregateRootConceptTests
{
    [Fact]
    public void CanContain_entity()
    {
        var agg = new AggregateRootConcept();
        var entity = new EntityConcept();
        Assert.True(agg.CanContain(entity));
    }

    [Fact]
    public void CanContain_value_object()
    {
        var agg = new AggregateRootConcept();
        var vo = new ValueObjectConcept();
        Assert.True(agg.CanContain(vo));
    }

    [Fact]
    public void Cannot_contain_command()
    {
        var agg = new AggregateRootConcept();
        var cmd = new CommandConcept();
        Assert.False(agg.CanContain(cmd));
    }
}

public class MustHaveIdConstraintTests
{
    [Fact]
    public void Satisfied_when_EntityId_present()
    {
        var ctx = new ConceptValidationContext
        {
            ConceptName = "AggregateRoot",
            TypeName = "Order",
            Properties = new[]
            {
                new ConceptPropertyInfo { Name = "Id", TypeName = "OrderId", AttributeNames = new[] { "EntityId" } }
            }
        };
        var result = AggregateRootAttribute.MustHaveIdConstraint(ctx);
        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void Failed_when_no_EntityId()
    {
        var ctx = new ConceptValidationContext
        {
            ConceptName = "AggregateRoot",
            TypeName = "Order",
            Properties = new[]
            {
                new ConceptPropertyInfo { Name = "Name", TypeName = "string", AttributeNames = new[] { "Property" } }
            }
        };
        var result = AggregateRootAttribute.MustHaveIdConstraint(ctx);
        Assert.False(result.IsSatisfied);
    }
}

public class RuntimeTypeTests
{
    [Fact]
    public void IDomainEvent_has_OccurredAt()
    {
        var prop = typeof(IDomainEvent).GetProperty("OccurredAt");
        Assert.NotNull(prop);
        Assert.Equal(typeof(System.DateTimeOffset), prop!.PropertyType);
    }

    [Fact]
    public void ICommandHandler_is_generic()
    {
        Assert.True(typeof(ICommandHandler<,>).IsGenericTypeDefinition);
        Assert.Equal(2, typeof(ICommandHandler<,>).GetGenericArguments().Length);
    }
}
