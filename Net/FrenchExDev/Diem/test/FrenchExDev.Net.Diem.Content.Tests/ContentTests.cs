namespace FrenchExDev.Net.Diem.Content.Tests;

using System.Reflection;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Diem.Content.Parts;
using FrenchExDev.Net.Diem.Content.Parts.Attributes;
using FrenchExDev.Net.Diem.Content.Blocks;
using FrenchExDev.Net.Diem.Content.Blocks.Attributes;
using FrenchExDev.Net.Diem.Content.StreamFields.Attributes;
using Xunit;

public class ContentTests
{
    // --- Part attribute MetaConcept tests ---

    [Fact]
    public void ContentPartAttribute_has_MetaConcept()
    {
        var attr = typeof(ContentPartAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("ContentPart", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void PartFieldAttribute_has_MetaConcept()
    {
        var attr = typeof(PartFieldAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("PartField", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void HasPartAttribute_has_MetaConcept()
    {
        var attr = typeof(HasPartAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("HasPart", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    // --- Block attribute MetaConcept tests ---

    [Fact]
    public void StructBlockAttribute_has_MetaConcept()
    {
        var attr = typeof(StructBlockAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("StructBlock", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void BlockFieldAttribute_has_MetaConcept()
    {
        var attr = typeof(BlockFieldAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("BlockField", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void ListBlockAttribute_has_MetaConcept()
    {
        var attr = typeof(ListBlockAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("ListBlock", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void StreamBlockAttribute_has_MetaConcept()
    {
        var attr = typeof(StreamBlockAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("StreamBlock", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void StreamFieldAttribute_has_MetaConcept()
    {
        var attr = typeof(StreamFieldAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("StreamField", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    // --- Built-in part field counts ---

    [Fact]
    public void RoutablePart_has_three_PartFields()
    {
        var fields = typeof(RoutablePart).GetProperties()
            .Where(p => p.GetCustomAttribute<PartFieldAttribute>() is not null)
            .ToList();
        Assert.Equal(3, fields.Count);
    }

    [Fact]
    public void RoutablePart_has_Slug_field()
    {
        var slug = typeof(RoutablePart).GetProperty("Slug");
        Assert.NotNull(slug);
        var attr = slug!.GetCustomAttribute<PartFieldAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("Slug", attr!.Name);
        Assert.True(attr.Required);
    }

    [Fact]
    public void VersionablePart_has_temporal_versioning_fields()
    {
        var props = typeof(VersionablePart).GetProperties()
            .Where(p => p.GetCustomAttribute<PartFieldAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("VersionNumber", props);
        Assert.Contains("ValidFrom", props);
        Assert.Contains("ValidTo", props);
        Assert.Contains("IsCurrent", props);
        Assert.Equal(4, props.Count);
    }

    [Fact]
    public void SeoablePart_has_four_PartFields()
    {
        var fields = typeof(SeoablePart).GetProperties()
            .Where(p => p.GetCustomAttribute<PartFieldAttribute>() is not null)
            .ToList();
        Assert.Equal(4, fields.Count);
    }

    // --- Built-in blocks implement IContentBlock ---

    [Theory]
    [InlineData(typeof(HeroBlock))]
    [InlineData(typeof(RichTextBlock))]
    [InlineData(typeof(TestimonialBlock))]
    [InlineData(typeof(ImageBlock))]
    public void BuiltInBlocks_implement_IContentBlock(Type blockType)
    {
        Assert.True(typeof(IContentBlock).IsAssignableFrom(blockType));
    }

    [Theory]
    [InlineData(typeof(HeroBlock), "Hero")]
    [InlineData(typeof(RichTextBlock), "RichText")]
    [InlineData(typeof(TestimonialBlock), "Testimonial")]
    [InlineData(typeof(ImageBlock), "Image")]
    public void BuiltInBlocks_have_correct_BlockType(Type blockType, string expectedBlockType)
    {
        var instance = (IContentBlock)Activator.CreateInstance(blockType)!;
        Assert.Equal(expectedBlockType, instance.BlockType);
    }

    // --- Constraint test ---

    [Fact]
    public void ContentPart_MustHaveFieldConstraint_fails_when_no_PartField()
    {
        var ctx = new ConceptValidationContext
        {
            ConceptName = "TestPart",
            TypeName = "TestPartClass",
            Properties = new[]
            {
                new ConceptPropertyInfo { Name = "Foo", TypeName = "string", AttributeNames = new[] { "Other" } }
            }
        };
        var result = ContentPartAttribute.MustHaveFieldConstraint(ctx);
        Assert.False(result.IsSatisfied);
    }

    [Fact]
    public void ContentPart_MustHaveFieldConstraint_succeeds_when_PartField_present()
    {
        var ctx = new ConceptValidationContext
        {
            ConceptName = "TestPart",
            TypeName = "TestPartClass",
            Properties = new[]
            {
                new ConceptPropertyInfo { Name = "Foo", TypeName = "string", AttributeNames = new[] { "PartField" } }
            }
        };
        var result = ContentPartAttribute.MustHaveFieldConstraint(ctx);
        Assert.True(result.IsSatisfied);
    }
}
