# DIEM-CMF — How-To

Practical recipes for building on a declaration-first CMF.

## 1. Define an Aggregate Root with Parts

Combine the DDD sub-DSL with content parts for cross-cutting concerns:

```csharp
[AggregateRoot("Product", BoundedContext = "Catalog")]
[HasPart(typeof(RoutablePart))]      // adds Slug, CanonicalPath
[HasPart(typeof(SeoablePart))]       // adds MetaTitle, MetaDescription, OgTags
[HasPart(typeof(VersionablePart))]   // adds ValidFrom/ValidTo, history
[HasPart(typeof(AuditablePart))]     // adds CreatedAt, CreatedBy, etc.
public partial class Product
{
    [EntityId]
    public partial ProductId Id { get; }

    [Property("Name", Required = true, MaxLength = 200)]
    public partial string Name { get; }

    [Property("Price", Required = true)]
    public partial decimal Price { get; }

    [Composition]
    public partial IReadOnlyList<ProductVariant> Variants { get; }

    [Invariant("Price must be positive")]
    private Result PriceIsPositive() =>
        Price > 0 ? Result.Success() : Result.Failure("Price must be > 0");
}
```

The compiler generates: backing fields, constructor, builder, persistence configuration, repository interface, command handlers, and invariant enforcement.

## 2. Declare an Admin Module

```csharp
[AdminModule("Products", typeof(Product), Icon = "box", Group = "Catalog")]
[AdminFilter("Category", FilterType = "Dropdown")]
[AdminFilter("PriceRange", FilterType = "Range")]
[AdminAction("Publish", Command = "PublishProduct", RequiresRole = "Editor")]
[AdminAction("Archive", Command = "ArchiveProduct", RequiresRole = "Editor")]
public partial class ProductsAdminModule { }
```

The compiler generates: a list page with pagination + filters, a create form, an edit form, a detail page, batch action handlers, and route registration.

## 3. Add a Custom Field Display Type

```csharp
[AdminModule("Products", typeof(Product))]
public partial class ProductsAdminModule { }

[AdminField("Price", DisplayType = "Currency", Currency = "EUR")]
[AdminField("ReleaseDate", DisplayType = "Date", Format = "yyyy-MM-dd")]
[AdminField("Description", DisplayType = "RichText")]
public partial class Product { }
```

Display types map to admin form/list renderers. Add custom display types by registering them in the admin module's options.

## 4. Compose Page Bodies with StreamFields

```csharp
[ContentBlock("Hero")]
public partial class HeroBlock : IContentBlock
{
    [BlockField] public partial string Heading { get; }
    [BlockField] public partial string Subheading { get; }
    [BlockField] public partial string ImageUrl { get; }
}

[ContentBlock("RichText")]
public partial class RichTextBlock : IContentBlock
{
    [BlockField] public partial string Html { get; }
}

[AggregateRoot("Article")]
public partial class Article
{
    [StreamField(typeof(HeroBlock), typeof(RichTextBlock), typeof(TestimonialBlock))]
    public partial IReadOnlyList<IContentBlock> Body { get; }
}
```

The compiler generates: JSON serialization, a polymorphic deserializer, validation that only allowed block types are stored, an editor binding, and the rendering pipeline.

## 5. Define a Page Widget

```csharp
[PageWidget("ProductList", Module = "Product", Icon = "grid")]
public partial class ProductListWidget
{
    [WidgetConfig(DisplayName = "Category", Required = false)]
    public string? Category { get; set; }

    [WidgetConfig(DisplayName = "Page Size")]
    public int PageSize { get; set; } = 12;

    [WidgetConfig(DisplayName = "Sort By")]
    public string SortBy { get; set; } = "name";
}
```

The compiler generates: the widget registration, the configuration form, the placement metadata, and the bindings to the page editor.

## 6. Declare an Editorial Workflow

```csharp
[Workflow("Editorial")]
[Stage("Draft", IsInitial = true)]
[Stage("Review")]
[Stage("Translation")]
[Stage("Published", IsFinal = true)]
[Transition("Submit",  From = "Draft",       To = "Review")]
[Transition("Approve", From = "Review",      To = "Translation")]
[Transition("Publish", From = "Translation", To = "Published")]
[Transition("Reject",  From = "Review",      To = "Draft")]
[RequiresRole("Approve", Role = "Editor")]
[RequiresRole("Publish", Role = "Publisher")]
[RequiresApproval("Publish", Count = 2)]
public partial class EditorialWorkflow { }
```

The compiler generates: a `WorkflowEngine<Article>`, gate evaluation, transition validation, and domain events.

## 7. Override Generated Behavior (Layer 4)

The customization contract: never edit `*.g.cs`. Add a partial class file:

```csharp
// ProductComponents.cs (developer-owned, never overwritten)
public partial class ProductComponents
{
    // Override the virtual hook the SG inserted
    protected override IQueryable<Product> OnCustomizeListQuery(IQueryable<Product> query)
    {
        return query.Where(p => !p.IsArchived);
    }

    // Add new behavior the SG didn't generate
    public Task<int> CountFeaturedAsync(CancellationToken ct)
        => Repository.CountAsync(p => p.IsFeatured, ct);
}
```

## 8. Trace a Requirement

```csharp
public static class ProductRequirements
{
    [Requirement("REQ-PROD-001", "Products must have a unique SKU")]
    public const string UniqueSku = "REQ-PROD-001";
}

[TracedBy("REQ-PROD-001")]
public partial class Product { }

[TracedBy("REQ-PROD-001")]
public class ProductSkuUniquenessTests { /* ... */ }
```

Run `cmf report --requirements`. A requirement without a covering test triggers a warning.

## 9. Add a Custom Part

A part is just an entity class with a concept attribute. Built-ins are not special:

```csharp
[ContentPart("Reviewable")]
public partial class ReviewablePart
{
    [Property("AverageRating")] public partial decimal AverageRating { get; }
    [Property("ReviewCount")]   public partial int     ReviewCount   { get; }
}

// Now any entity can attach it:
[HasPart(typeof(ReviewablePart))]
public partial class Product { }
```

The Content SG handles the rest: schema, persistence, query helpers.

## 10. Add a Custom Block

```csharp
[ContentBlock("VideoEmbed")]
public partial class VideoEmbedBlock : IContentBlock
{
    [BlockField(Required = true)] public partial string VideoUrl { get; }
    [BlockField] public partial string? Caption { get; }
    [BlockField] public partial bool Autoplay { get; }
}
```

Then allow it in StreamFields:

```csharp
[StreamField(typeof(HeroBlock), typeof(RichTextBlock), typeof(VideoEmbedBlock))]
public partial IReadOnlyList<IContentBlock> Body { get; }
```

## 11. Test a Generated Component

Use partial-class extension and the testing project:

```csharp
[Fact]
public async Task CreateProduct_emits_ProductCreated_event()
{
    var harness = new ProductTestHarness();
    var cmd = new CreateProductCommand("Widget", 9.99m);

    var result = await harness.Handler.HandleAsync(cmd);

    Assert.True(result.IsSuccess);
    Assert.Contains(harness.Events, e => e is ProductCreated);
}
```

`ProductTestHarness` is provided by the `<Sub>.Testing` project — it wires fakes and exposes captured events.

## 12. CLI Workflow

```bash
cmf new MyCmfProject              # scaffold
cmf add admin --module Product    # scaffold an admin module
cmf validate                      # validate DSL model consistency
cmf migrate                       # generate persistence migrations
cmf report                        # print model + traceability report
```

## Common Pitfalls

- **Editing `*.g.cs`** — always overwritten; use Layer 4 partial classes.
- **Using strings where `nameof` works** — kills refactor safety.
- **Forgetting `partial`** — the SG augments partial classes; non-partial classes are silently skipped.
- **Mixing two display types on one field** — last attribute wins; the diagnostic helps.
- **Dropping `[Invariant]` for "performance"** — invariants are compiled into the constructor; there is no runtime cost.
- **Skipping `[TracedBy]`** — the requirement is still satisfied, but the matrix shows a gap.
