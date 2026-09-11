# Diem CMF -- HOW-TO Guide

This guide covers every sub-DSL in Diem with practical, copy-paste examples that
reference the **actual** classes, attributes, and interfaces in the codebase.

> **Namespace root:** `FrenchExDev.Net.Diem`
>
> **Solution:** `Diem/FrenchExDev.Net.Diem.slnx`

---

## Content Sub-DSL

The Content sub-DSL is split into three layers: **Parts** (reusable field groups
that attach to entities), **Blocks** (structured content units for page bodies),
and **StreamFields** (polymorphic lists of blocks).

---

### 1. Create a Content Part

A Content Part is a reusable group of fields that can be composed onto any
aggregate root. Define one with `[ContentPart]` on the class and `[PartField]`
on each property.

**Attribute overview:**

| Attribute | Target | Package |
|---|---|---|
| `ContentPartAttribute` | class | `FrenchExDev.Net.Diem.Content.Parts.Attributes` |
| `PartFieldAttribute` | property | `FrenchExDev.Net.Diem.Content.Parts.Attributes` |

`ContentPartAttribute` carries a `[MetaConstraint]` named `MustHaveField` that
enforces at least one `[PartField]` is present. If you forget to add fields, the
DSL validator will reject the part at design time.

**Custom part example -- `ReviewablePart`:**

```csharp
using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[ContentPart("Reviewable", Description = "User review support")]
public partial class ReviewablePart
{
    [PartField("Rating", Required = true, HelpText = "1-5 star rating")]
    public int Rating { get; set; }

    [PartField("ReviewBody", DisplayName = "Review Text", MaxLength = 2000)]
    public string ReviewBody { get; set; } = "";

    [PartField("ReviewerName", DisplayName = "Reviewer")]
    public string ReviewerName { get; set; } = "";

    [PartField("ReviewedAt", DisplayName = "Review Date")]
    public DateTimeOffset ReviewedAt { get; set; }
}
```

**`PartFieldAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Field identifier (required, positional) |
| `DisplayName` | `string` | Human-readable label for admin UI |
| `Required` | `bool` | Whether the field must be non-empty |
| `MaxLength` | `int` | Maximum string length (0 = unlimited) |
| `HelpText` | `string` | Tooltip or description shown in the admin form |

**The 9 built-in parts:**

| Part class | Fields | Purpose |
|---|---|---|
| `RoutablePart` | `Slug` (required, max 200), `UrlPath`, `IsCanonical` | URL routing with slugs |
| `SeoablePart` | `MetaTitle` (max 70), `MetaDescription` (max 160), `OgImage`, `NoIndex` | SEO metadata |
| `TaggablePart` | `Tags`, `Taxonomy` | Taxonomy tagging |
| `AuditablePart` | `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt` | Audit trail tracking |
| `VersionablePart` | `VersionNumber`, `ValidFrom`, `ValidTo`, `IsCurrent` | Temporal data versioning |
| `LocalizablePart` | `Culture` (required), `LocalizationSet`, `IsDefault` | Localization and culture support |
| `SortablePart` | `SortOrder`, `SortGroup` | Manual ordering support |
| `SchedulablePart` | `PublishAt`, `UnpublishAt`, `IsPublished` | Publication scheduling |
| `MediablePart` | `MediaPath`, `AltText` (max 250), `MimeType`, `FileSizeBytes` | Media attachment support |

Each built-in part lives in the `FrenchExDev.Net.Diem.Content.Parts` namespace.
For example, `RoutablePart`:

```csharp
// From: FrenchExDev.Net.Diem.Content.Parts/RoutablePart.cs
[ContentPart("Routable", Description = "URL routing with slugs")]
public partial class RoutablePart
{
    [PartField("Slug", Required = true, MaxLength = 200, HelpText = "URL-friendly identifier")]
    public string Slug { get; set; } = "";

    [PartField("UrlPath", DisplayName = "URL Path")]
    public string UrlPath { get; set; } = "";

    [PartField("IsCanonical", DisplayName = "Canonical URL")]
    public bool IsCanonical { get; set; }
}
```

And `VersionablePart` with its temporal versioning fields:

```csharp
// From: FrenchExDev.Net.Diem.Content.Parts/VersionablePart.cs
[ContentPart("Versionable", Description = "Temporal data versioning")]
public partial class VersionablePart
{
    [PartField("VersionNumber")]
    public int VersionNumber { get; set; } = 1;

    [PartField("ValidFrom")]
    public DateTimeOffset ValidFrom { get; set; }

    [PartField("ValidTo")]
    public DateTimeOffset? ValidTo { get; set; }

    [PartField("IsCurrent")]
    public bool IsCurrent { get; set; } = true;
}
```

---

### 2. Attach Parts to Entities

Use `[HasPart(typeof(...))]` on an aggregate root class to compose one or more
parts onto it. `HasPartAttribute` is `AllowMultiple = true`, so you can stack
as many parts as needed.

```csharp
using FrenchExDev.Net.Diem.Content.Parts;
using FrenchExDev.Net.Diem.Content.Parts.Attributes;

[HasPart(typeof(RoutablePart))]
[HasPart(typeof(SeoablePart))]
[HasPart(typeof(AuditablePart))]
[HasPart(typeof(TaggablePart))]
[HasPart(typeof(VersionablePart))]
[HasPart(typeof(SchedulablePart))]
public class Article
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}
```

At design/generation time, the source generator reads every `[HasPart]` and wires
the part's fields into the entity. The DSL validator ensures each referenced type
actually carries `[ContentPart]`.

For a simpler entity that only needs routing and media:

```csharp
[HasPart(typeof(RoutablePart))]
[HasPart(typeof(MediablePart))]
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
```

---

### 3. Create a Content Block

Content blocks are structured units of content (hero banners, rich text
sections, testimonials, etc.) used inside StreamFields. Define one with
`[StructBlock]` on the class and `[BlockField]` on each property. Every block
must implement `IContentBlock`.

**Attribute overview:**

| Attribute | Target | Package |
|---|---|---|
| `StructBlockAttribute` | class | `FrenchExDev.Net.Diem.Content.Blocks.Attributes` |
| `BlockFieldAttribute` | property | `FrenchExDev.Net.Diem.Content.Blocks.Attributes` |
| `ListBlockAttribute` | class | `FrenchExDev.Net.Diem.Content.Blocks.Attributes` |
| `StreamBlockAttribute` | class | `FrenchExDev.Net.Diem.Content.Blocks.Attributes` |

**Custom block example -- `PricingBlock`:**

```csharp
using FrenchExDev.Net.Diem.Content.Blocks;
using FrenchExDev.Net.Diem.Content.Blocks.Attributes;

[StructBlock("Pricing", Description = "Pricing tier block", Icon = "dollar")]
public partial class PricingBlock : IContentBlock
{
    public string BlockType => "Pricing";

    [BlockField("TierName", Required = true, DisplayName = "Tier Name")]
    public string TierName { get; set; } = "";

    [BlockField("Price", Required = true)]
    public decimal Price { get; set; }

    [BlockField("Features", HelpText = "Comma-separated feature list")]
    public string Features { get; set; } = "";

    [BlockField("IsHighlighted", DisplayName = "Highlighted")]
    public bool IsHighlighted { get; set; }
}
```

**`BlockFieldAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Field identifier (required, positional) |
| `DisplayName` | `string` | Human-readable label |
| `Required` | `bool` | Whether the field is mandatory |
| `MaxLength` | `int` | Maximum string length |
| `HelpText` | `string` | Description for the admin editor |

**The 4 built-in blocks:**

All live in `FrenchExDev.Net.Diem.Content.Blocks` and implement `IContentBlock`:

| Block | Fields | Icon |
|---|---|---|
| `HeroBlock` | `Heading` (required, max 120), `Subheading`, `BackgroundImage`, `CtaText`, `CtaUrl` | `star` |
| `RichTextBlock` | `Body` (required), `Format` (default `"html"`) | `text` |
| `TestimonialBlock` | `Quote` (required), `Author` (required), `Role`, `AvatarUrl` | `quote` |
| `ImageBlock` | `Src` (required), `Alt` (required, max 250), `Caption`, `Width`, `Height` | `image` |

Example -- the built-in `HeroBlock`:

```csharp
// From: FrenchExDev.Net.Diem.Content.Blocks/HeroBlock.cs
[StructBlock("Hero", Description = "Hero banner with heading and call-to-action", Icon = "star")]
public partial class HeroBlock : IContentBlock
{
    public string BlockType => "Hero";

    [BlockField("Heading", Required = true, MaxLength = 120)]
    public string Heading { get; set; } = "";

    [BlockField("Subheading", DisplayName = "Sub-heading")]
    public string? Subheading { get; set; }

    [BlockField("BackgroundImage", DisplayName = "Background Image")]
    public string? BackgroundImage { get; set; }

    [BlockField("CtaText", DisplayName = "CTA Text")]
    public string? CtaText { get; set; }

    [BlockField("CtaUrl", DisplayName = "CTA URL")]
    public string? CtaUrl { get; set; }
}
```

**Additional block types:**

Beyond `[StructBlock]`, the DSL provides two more block-level concepts:

- **`[ListBlock]`** -- a repeating list of a single item type.
  Takes `Name`, `Description`, and `ItemType` (the `Type` of each list element).

  ```csharp
  [ListBlock("LinkList", Description = "List of navigation links", ItemType = typeof(LinkItem))]
  public partial class LinkListBlock : IContentBlock
  {
      public string BlockType => "LinkList";
  }
  ```

- **`[StreamBlock]`** -- a heterogeneous stream of allowed block types (a block
  that contains other blocks).
  Takes `Name`, `Description`, and `AllowedBlockTypes`.

  ```csharp
  [StreamBlock("ContentStream", AllowedBlockTypes = new[] { typeof(RichTextBlock), typeof(ImageBlock) })]
  public partial class ContentStreamBlock : IContentBlock
  {
      public string BlockType => "ContentStream";
  }
  ```

---

### 4. Use StreamFields

A `[StreamField]` marks a property as a polymorphic, ordered list of content
blocks. The `AllowedBlockTypes` property controls which block types the editor
can insert.

```csharp
using FrenchExDev.Net.Diem.Content.Blocks;
using FrenchExDev.Net.Diem.Content.StreamFields.Attributes;

public class LandingPage
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";

    [StreamField("Body", AllowedBlockTypes = new[]
    {
        typeof(HeroBlock),
        typeof(RichTextBlock),
        typeof(TestimonialBlock),
        typeof(ImageBlock)
    })]
    public IList<IContentBlock> Body { get; set; } = new List<IContentBlock>();

    [StreamField("Sidebar", AllowedBlockTypes = new[]
    {
        typeof(RichTextBlock),
        typeof(ImageBlock)
    })]
    public IList<IContentBlock> Sidebar { get; set; } = new List<IContentBlock>();
}
```

**`StreamFieldAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Field identifier (required, positional) |
| `AllowedBlockTypes` | `Type[]` | Block types the editor may insert |

The source generator emits a JSON converter and stream field runtime types to
handle serialization of polymorphic block lists.

---

## Admin Sub-DSL

The Admin sub-DSL generates list views, forms, filters, and batch actions for
managing entities through an admin panel.

---

### 5. Create an Admin Module

An admin module declares a list view for a specific aggregate type. Use
`[AdminModule]` on a class, and optionally add `[AdminFilter]` for filterable
columns.

**Attribute overview:**

| Attribute | Target | Package |
|---|---|---|
| `AdminModuleAttribute` | class | `FrenchExDev.Net.Diem.Admin.Lists.Attributes` |
| `AdminFilterAttribute` | class | `FrenchExDev.Net.Diem.Admin.Lists.Attributes` |

```csharp
using FrenchExDev.Net.Diem.Admin.Lists.Attributes;

[AdminModule("Products", typeof(Product), Icon = "box", Group = "Catalog")]
[AdminFilter("Name", FilterType = "Text", DisplayName = "Product Name")]
[AdminFilter("Price", FilterType = "Range", DisplayName = "Price Range")]
[AdminFilter("IsPublished", FilterType = "Boolean")]
public class ProductAdminModule
{
}
```

**`AdminModuleAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Module name (required, positional) |
| `Aggregate` | `Type` | The entity type this module manages (required, positional) |
| `Icon` | `string?` | Icon identifier for the sidebar |
| `Group` | `string?` | Navigation group (e.g., `"Catalog"`, `"Content"`) |
| `PageSize` | `int` | Items per page (default: `25`) |

**`AdminFilterAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `FieldName` | `string` | Property name to filter on (required, positional) |
| `FilterType` | `string` | Filter control type: `"Text"`, `"Range"`, `"Boolean"`, `"Select"` (default: `"Text"`) |
| `DisplayName` | `string?` | Label shown in the filter bar |

At runtime, the source generator produces an `AdminModuleDescriptor`:

```csharp
// From: FrenchExDev.Net.Diem.Admin.Lists/AdminModuleDescriptor.cs
public class AdminModuleDescriptor
{
    public required string Name { get; init; }
    public required Type AggregateType { get; init; }
    public string? Icon { get; init; }
    public string? Group { get; init; }
    public int PageSize { get; init; } = 25;
    public IReadOnlyList<AdminFilterDescriptor> Filters { get; init; } = [];
}
```

---

### 6. Customize Admin Fields

Use `[AdminField]` on entity properties to control how each field appears in
list views and edit forms.

```csharp
using FrenchExDev.Net.Diem.Admin.Forms.Attributes;

public class Product
{
    public Guid Id { get; set; }

    [AdminField("Name", DisplayType = "TextInput", DisplayName = "Product Name", Order = 1)]
    public string Name { get; set; } = "";

    [AdminField("Description", DisplayType = "RichText", Order = 2, HideInList = true)]
    public string Description { get; set; } = "";

    [AdminField("Sku", DisplayName = "SKU", ReadOnly = true, Order = 3)]
    public string Sku { get; set; } = "";

    [AdminField("Price", DisplayType = "Currency", Order = 4)]
    public decimal Price { get; set; }

    [AdminField("CreatedAt", DisplayName = "Created", ReadOnly = true, HideInForm = true, Order = 5)]
    public DateTimeOffset CreatedAt { get; set; }
}
```

**`AdminFieldAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Property name (required, positional) |
| `DisplayType` | `string?` | Control type: `"TextInput"`, `"RichText"`, `"Currency"`, `"Boolean"`, `"Select"` |
| `DisplayName` | `string?` | Label override |
| `ReadOnly` | `bool` | Prevent editing |
| `HideInList` | `bool` | Omit from the list/table view |
| `HideInForm` | `bool` | Omit from the edit form |
| `Order` | `int` | Display order (lower = first) |

At runtime, the source generator produces an `AdminFieldDescriptor` for each
annotated property:

```csharp
// From: FrenchExDev.Net.Diem.Admin.Forms/AdminFieldDescriptor.cs
public class AdminFieldDescriptor
{
    public required string Name { get; init; }
    public string? DisplayType { get; init; }
    public string? DisplayName { get; init; }
    public bool ReadOnly { get; init; }
    public bool HideInList { get; init; }
    public bool HideInForm { get; init; }
    public int Order { get; init; }
}
```

---

### 7. Add Batch Actions

Batch actions let admin users perform operations on selected items (publish,
archive, delete, etc.). Use `[AdminAction]` on the admin module class.

```csharp
using FrenchExDev.Net.Diem.Admin.Lists.Attributes;
using FrenchExDev.Net.Diem.Admin.Actions.Attributes;

[AdminModule("Articles", typeof(Article), Icon = "file-text", Group = "Content")]
[AdminFilter("Title")]
[AdminFilter("IsPublished", FilterType = "Boolean")]
[AdminAction("Publish", Command = "PublishArticle", RequiresRole = "Editor",
    Icon = "send", ConfirmationMessage = "Publish the selected articles?")]
[AdminAction("Archive", Command = "ArchiveArticle", RequiresRole = "Admin",
    Icon = "archive", ConfirmationMessage = "Archive the selected articles?")]
[AdminAction("Delete", Command = "DeleteArticle", RequiresRole = "Admin",
    Icon = "trash", ConfirmationMessage = "Permanently delete the selected articles?")]
public class ArticleAdminModule
{
}
```

**`AdminActionAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Action label (required, positional) |
| `Command` | `string` | Command identifier dispatched to the backend |
| `Icon` | `string?` | Icon identifier |
| `ConfirmationMessage` | `string?` | Modal dialog text before execution |
| `RequiresRole` | `string?` | Role required to execute this action |

At runtime, each action becomes an `AdminActionDescriptor`:

```csharp
// From: FrenchExDev.Net.Diem.Admin.Actions/AdminActionDescriptor.cs
public class AdminActionDescriptor
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public string? Icon { get; init; }
    public string? ConfirmationMessage { get; init; }
    public string? RequiresRole { get; init; }
}
```

---

## Pages Sub-DSL

The Pages sub-DSL provides page trees, layouts with areas and zones, widget
placement, URL routing via materialized paths, and entity binding.

---

### 8. Define a Page Widget

A page widget is a configurable component that can be placed in any zone. Use
`[PageWidget]` on the class and `[WidgetConfig]` on configuration properties.

**Attribute overview:**

| Attribute | Target | Package |
|---|---|---|
| `PageWidgetAttribute` | class | `FrenchExDev.Net.Diem.Pages.Widgets.Attributes` |
| `WidgetConfigAttribute` | property | `FrenchExDev.Net.Diem.Pages.Widgets.Attributes` |

```csharp
using FrenchExDev.Net.Diem.Pages.Widgets.Attributes;

[PageWidget("ProductList", Module = "Product", Description = "Displays a filterable product grid", Icon = "grid")]
public class ProductListWidget
{
    [WidgetConfig(DisplayName = "Category Filter", HelpText = "Only show products in this category", Required = false)]
    public string? CategorySlug { get; set; }

    [WidgetConfig(DisplayName = "Max Items", DefaultValue = "12")]
    public int MaxItems { get; set; } = 12;

    [WidgetConfig(DisplayName = "Show Prices")]
    public bool ShowPrices { get; set; } = true;

    [WidgetConfig(DisplayName = "Sort By", DefaultValue = "Name")]
    public string SortBy { get; set; } = "Name";
}
```

**`PageWidgetAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Widget identifier (required, positional) |
| `Module` | `string` | The domain module this widget belongs to |
| `Description` | `string` | Human-readable description |
| `Icon` | `string` | Icon identifier |

**`WidgetConfigAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `DisplayName` | `string` | Label shown in the widget settings panel |
| `HelpText` | `string` | Tooltip or description |
| `Required` | `bool` | Whether the config value must be set |
| `DefaultValue` | `string` | Default value (as string) |

At runtime, the source generator produces a `WidgetDescriptor` with a list of
`WidgetConfigDescriptor` entries:

```csharp
// From: FrenchExDev.Net.Diem.Pages.Widgets/WidgetDescriptor.cs
public sealed class WidgetDescriptor
{
    public required string Name { get; init; }
    public required Type ComponentType { get; init; }
    public required string Module { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<WidgetConfigDescriptor> ConfigProperties { get; init; } = [];
}

public sealed class WidgetConfigDescriptor
{
    public required string PropertyName { get; init; }
    public required Type PropertyType { get; init; }
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public string? DisplayName { get; init; }
}
```

---

### 9. Understand the Page Tree

The page tree is a hierarchy of five entity classes that live in
`FrenchExDev.Net.Diem.Pages.Layouts`:

```
Page --> Layout --> Area --> Zone --> WidgetInstance
```

**`Page`** -- a node in the content tree:

```csharp
// From: FrenchExDev.Net.Diem.Pages.Layouts/Page.cs
public class Page
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string MaterializedPath { get; set; } = "";   // e.g. "/products"
    public Guid? ParentId { get; set; }
    public Guid LayoutId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public Page? Parent { get; set; }
    public ICollection<Page> Children { get; set; } = new List<Page>();
    public Layout? Layout { get; set; }
    public ICollection<WidgetInstance> WidgetInstances { get; set; } = new List<WidgetInstance>();
}
```

**`Layout`** -- a template that defines the visual structure:

```csharp
// From: FrenchExDev.Net.Diem.Pages.Layouts/Layout.cs
public class Layout
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string TemplateComponent { get; set; } = "";   // Blazor component name
    public ICollection<Area> Areas { get; set; } = new List<Area>();
}
```

**`Area`** -- a named region within a layout (e.g., "Header", "Main", "Footer"):

```csharp
// From: FrenchExDev.Net.Diem.Pages.Layouts/Area.cs
public class Area
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public string Name { get; set; } = "";
    public Layout? Layout { get; set; }
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
}
```

**`Zone`** -- a drop target within an area that holds widgets:

```csharp
// From: FrenchExDev.Net.Diem.Pages.Layouts/Zone.cs
public class Zone
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public string Name { get; set; } = "";
    public int MaxWidgets { get; set; } = 10;
    public Area? Area { get; set; }
}
```

**`WidgetInstance`** -- a placed widget with its configuration:

```csharp
// From: FrenchExDev.Net.Diem.Pages.Layouts/WidgetInstance.cs
public class WidgetInstance
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid ZoneId { get; set; }
    public string WidgetType { get; set; } = "";           // References WidgetDescriptor.Name
    public int SortOrder { get; set; }
    public string ConfigurationJson { get; set; } = "{}";  // Serialized config

    public Page? Page { get; set; }
    public Zone? Zone { get; set; }
}
```

**Declaring layouts with attributes:**

Layouts, areas, and zones can also be declared via DSL attributes for
design-time scaffolding:

```csharp
using FrenchExDev.Net.Diem.Pages.Layouts.Attributes;

[Layout("TwoColumn", Description = "Header + two-column main + footer")]
[Area("Header")]
[Area("MainLeft")]
[Area("MainRight")]
[Area("Footer")]
[Zone("HeaderBanner", MaxWidgets = 1)]
[Zone("LeftContent", MaxWidgets = 5)]
[Zone("RightSidebar", MaxWidgets = 3)]
[Zone("FooterLinks")]
public class TwoColumnLayout { }
```

**Composing a page at runtime:**

```csharp
var layout = new Layout
{
    Id = Guid.NewGuid(),
    Name = "Standard",
    TemplateComponent = "StandardLayout",
    Areas =
    {
        new Area
        {
            Id = Guid.NewGuid(),
            Name = "Main",
            Zones =
            {
                new Zone { Id = Guid.NewGuid(), Name = "Content", MaxWidgets = 10 },
                new Zone { Id = Guid.NewGuid(), Name = "Sidebar", MaxWidgets = 5 },
            }
        }
    }
};

var page = new Page
{
    Id = Guid.NewGuid(),
    Title = "Products",
    Slug = "products",
    MaterializedPath = "/products",
    LayoutId = layout.Id,
    Layout = layout,
    IsPublished = true,
};
```

---

### 10. URL Routing with Materialized Paths

`PageRouter` resolves incoming URL paths to pages using materialized paths.
It supports two resolution modes:

1. **Exact match** -- the `MaterializedPath` of a published page matches the
   URL path exactly.
2. **Parent + slug fallback** -- if no exact match is found, the router strips
   the last segment as an entity slug and looks for a parent page.

**The actual implementation:**

```csharp
// From: FrenchExDev.Net.Diem.Pages.Routing/PageRouter.cs
public class PageRouter
{
    public PageRouteResult? Resolve(string path, IReadOnlyList<Page> pages)
    {
        // Exact match
        var exact = pages.FirstOrDefault(p => p.MaterializedPath == path && p.IsPublished);
        if (exact != null)
            return new PageRouteResult { Page = exact };

        // Parent match with entity slug
        // e.g., /products/running-shoes -> page=/products, slug=running-shoes
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash > 0)
        {
            var parentPath = path[..lastSlash];
            var slug = path[(lastSlash + 1)..];
            var parent = pages.FirstOrDefault(p => p.MaterializedPath == parentPath && p.IsPublished);
            if (parent != null)
                return new PageRouteResult { Page = parent, EntitySlug = slug };
        }

        return null;
    }
}

public class PageRouteResult
{
    public required Page Page { get; init; }
    public string? EntitySlug { get; init; }
}
```

**Usage example:**

```csharp
var router = new PageRouter();
var pages = new List<Page>
{
    new Page { MaterializedPath = "/",         IsPublished = true, Title = "Home" },
    new Page { MaterializedPath = "/about",    IsPublished = true, Title = "About" },
    new Page { MaterializedPath = "/products", IsPublished = true, Title = "Products" },
    new Page { MaterializedPath = "/draft",    IsPublished = false, Title = "Draft" },
};

// Exact match
var result1 = router.Resolve("/about", pages);
// result1.Page.Title == "About", result1.EntitySlug == null

// Parent + slug fallback
var result2 = router.Resolve("/products/running-shoes", pages);
// result2.Page.Title == "Products", result2.EntitySlug == "running-shoes"

// Unpublished pages are invisible
var result3 = router.Resolve("/draft", pages);
// result3 == null

// Unknown paths return null
var result4 = router.Resolve("/nonexistent", pages);
// result4 == null
```

---

### 11. Bind Entities to Pages

Use `[BoundEntity]` to declare that a page's child URLs resolve to a specific
entity type. The `UrlPattern` property defines the URL template with
placeholders.

```csharp
using FrenchExDev.Net.Diem.Pages.Routing.Attributes;

[BoundEntity(typeof(Product), UrlPattern = "/catalog/{Slug}")]
public class CatalogPage
{
}
```

**`BoundEntityAttribute` properties:**

| Property | Type | Description |
|---|---|---|
| `EntityType` | `Type` | The entity type bound to this page (required, positional) |
| `UrlPattern` | `string` | URL pattern with `{Property}` placeholders |

When the `PageRouter` resolves `/catalog/running-shoes`, the framework knows to
load the `Product` with `Slug == "running-shoes"` and pass it to the page's
widget context.

Multiple bindings on different pages:

```csharp
[BoundEntity(typeof(Article), UrlPattern = "/blog/{Slug}")]
public class BlogPage { }

[BoundEntity(typeof(Product), UrlPattern = "/shop/{Slug}")]
public class ShopPage { }
```

---

## Workflow Sub-DSL

The Workflow sub-DSL provides a declarative state machine with stages,
transitions, gates, scheduling, and per-locale tracking.

---

### 12. Define a Workflow

A workflow is declared with `[Workflow]`, its stages with `[Stage]`, and
transitions between stages with `[Transition]`. Gate attributes
(`[RequiresRole]`, `[RequiresApproval]`) guard specific transitions.

**Full editorial workflow example:**

```csharp
using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes;
using FrenchExDev.Net.Diem.Workflow.Gates.Attributes;
using FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes;
using FrenchExDev.Net.Diem.Workflow.Locales.Attributes;

[Workflow("Editorial", Description = "Standard editorial approval workflow")]
[Stage("Draft",     IsInitial = true, Color = "gray",   Description = "Work in progress")]
[Stage("Review",                      Color = "yellow", Description = "Awaiting editorial review")]
[Stage("Approved",                    Color = "blue",   Description = "Approved, awaiting publication")]
[Stage("Published",  IsFinal = true,  Color = "green",  Description = "Live on site")]
[Stage("Archived",   IsFinal = true,  Color = "red",    Description = "Removed from site")]
[Transition("Submit",  From = "Draft",     To = "Review",    Description = "Submit for review")]
[Transition("Approve", From = "Review",    To = "Approved",  Description = "Approve content")]
[Transition("Reject",  From = "Review",    To = "Draft",     Description = "Send back for revisions")]
[Transition("Publish", From = "Approved",  To = "Published", Description = "Publish to site")]
[Transition("Archive", From = "Published", To = "Archived",  Description = "Take offline")]
[RequiresRole("Approve", "Editor")]
[RequiresRole("Publish", "Publisher")]
[RequiresRole("Archive", "Admin")]
[RequiresApproval("Approve", 2)]
[ScheduledTransition("Approved", "Published", "PublishDate")]
[ForEachLocale("Review")]
public class EditorialWorkflow { }
```

**Attribute reference:**

**`WorkflowAttribute`:**

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Workflow identifier (required, positional) |
| `Description` | `string?` | Human-readable description |

**`StageAttribute`** (AllowMultiple):

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Stage identifier (required, positional) |
| `IsInitial` | `bool` | Entry point of the workflow |
| `IsFinal` | `bool` | Terminal state (no outgoing transitions) |
| `Color` | `string?` | Color hint for admin UI |
| `Description` | `string?` | Human-readable description |

**`TransitionAttribute`** (AllowMultiple):

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Transition/action name (required, positional) |
| `From` | `string` | Source stage name |
| `To` | `string` | Target stage name |
| `Description` | `string?` | Human-readable description |

**Binding a workflow to an entity:**

Use `[HasWorkflow]` on the entity class:

```csharp
using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes;

[HasWorkflow("Editorial")]
public class Article
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string CurrentStage { get; set; } = "Draft";
    public DateTimeOffset? PublishDate { get; set; }
}
```

---

### 13. Use the WorkflowEngine

`WorkflowEngine` is a lightweight, in-memory state machine. It stores
transitions as `(From, Action) -> To` tuples and provides three methods:

```csharp
// From: FrenchExDev.Net.Diem.Workflow.StateMachine/WorkflowEngine.cs
public class WorkflowEngine
{
    public WorkflowEngine(string workflowName);
    public string WorkflowName { get; }

    // Register a transition: from stage + action -> to stage
    public void RegisterTransition(string from, string action, string to);

    // Resolve the target stage for a given current stage and action.
    // Returns null if the transition is not registered.
    public string? TryGetTarget(string currentStage, string action);

    // List all actions available from a given stage.
    public IReadOnlyList<string> GetAvailableActions(string currentStage);
}
```

**Usage:**

```csharp
using FrenchExDev.Net.Diem.Workflow.StateMachine;

// 1. Create engine
var engine = new WorkflowEngine("Editorial");

// 2. Register transitions (typically done from [Transition] metadata at startup)
engine.RegisterTransition("Draft",     "Submit",  "Review");
engine.RegisterTransition("Review",    "Approve", "Published");
engine.RegisterTransition("Review",    "Reject",  "Draft");

// 3. Query available actions for a stage
var actions = engine.GetAvailableActions("Review");
// actions == ["Approve", "Reject"]

// 4. Resolve a specific transition
var target = engine.TryGetTarget("Review", "Approve");
// target == "Published"

// 5. Invalid transitions return null
var invalid = engine.TryGetTarget("Draft", "Approve");
// invalid == null

// 6. Terminal stages have no actions
var terminalActions = engine.GetAvailableActions("Published");
// terminalActions == [] (empty)
```

**Transition events:**

When a transition occurs, you can emit a `WorkflowTransitionedEvent`:

```csharp
// From: FrenchExDev.Net.Diem.Workflow.StateMachine/WorkflowTransitionedEvent.cs
public sealed class WorkflowTransitionedEvent
{
    public required Guid EntityId { get; init; }
    public required string WorkflowName { get; init; }
    public required string Action { get; init; }
    public required string FromStage { get; init; }
    public required string ToStage { get; init; }
    public required DateTimeOffset TransitionedAt { get; init; }
    public string? TransitionedBy { get; init; }
}
```

Example:

```csharp
var evt = new WorkflowTransitionedEvent
{
    EntityId = article.Id,
    WorkflowName = engine.WorkflowName,
    Action = "Approve",
    FromStage = "Review",
    ToStage = "Published",
    TransitionedAt = DateTimeOffset.UtcNow,
    TransitionedBy = currentUser.UserName,
};
```

---

### 14. Add Gates

Gates guard transitions. The base interface is `IGateEvaluator`, and the
framework ships two specialized gate attributes: `[RequiresRole]` and
`[RequiresApproval]`.

**`IGateEvaluator`:**

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Gates/IGateEvaluator.cs
public interface IGateEvaluator
{
    Task<GateResult> EvaluateAsync(string action, Guid entityId, CancellationToken ct = default);
}

public sealed class GateResult
{
    public bool IsAllowed { get; }
    public string? Reason { get; }
    public static GateResult Allowed();
    public static GateResult Denied(string reason);
}
```

**`[RequiresRole]`** -- restricts a transition to users in a specific role:

```csharp
using FrenchExDev.Net.Diem.Workflow.Gates.Attributes;

// On the workflow class:
[RequiresRole("Approve", "Editor")]
[RequiresRole("Publish", "Publisher")]
[RequiresRole("Archive", "Admin")]
```

| Property | Type | Description |
|---|---|---|
| `Transition` | `string` | The transition name this gate applies to (required, positional) |
| `Role` | `string` | Required role name (required, positional) |

**`[RequiresApproval]`** -- requires N approvers before the transition fires:

```csharp
[RequiresApproval("Publish", 2)]
```

| Property | Type | Description |
|---|---|---|
| `Transition` | `string` | The transition name (required, positional) |
| `MinApprovers` | `int` | Minimum number of approvals needed (required, positional) |

**Custom gate with `[Gate]`:**

For arbitrary gate logic, use the base `[Gate]` attribute:

```csharp
[Gate("ContentLengthCheck", Transition = "Publish", GateType = "Custom")]
```

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Gate identifier (required, positional) |
| `Transition` | `string` | The transition name |
| `GateType` | `string` | Gate category (default: `"Custom"`) |

Both `RequiresRole` and `RequiresApproval` inherit from `GateConcept` via
`[MetaInherits(typeof(GateConcept))]`, so the DSL knows they are gate
specializations.

**Implementing a custom gate evaluator:**

```csharp
public class RoleGateEvaluator : IGateEvaluator
{
    private readonly IDiemUserContext _userContext;
    private readonly string _requiredRole;

    public RoleGateEvaluator(IDiemUserContext userContext, string requiredRole)
    {
        _userContext = userContext;
        _requiredRole = requiredRole;
    }

    public Task<GateResult> EvaluateAsync(string action, Guid entityId, CancellationToken ct = default)
    {
        return Task.FromResult(
            _userContext.IsInRole(_requiredRole)
                ? GateResult.Allowed()
                : GateResult.Denied($"Requires role '{_requiredRole}'")
        );
    }
}
```

---

### 15. Schedule Transitions

`[ScheduledTransition]` declares that a transition should fire automatically
when a date property reaches the current time.

```csharp
using FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes;

// On the workflow class:
[ScheduledTransition("Approved", "Published", "PublishDate")]
[ScheduledTransition("Published", "Archived", "UnpublishDate")]
```

| Property | Type | Description |
|---|---|---|
| `From` | `string` | Source stage (required, positional) |
| `To` | `string` | Target stage (required, positional) |
| `DateProperty` | `string` | Entity property name holding the `DateTimeOffset` (required, positional) |

At runtime, `IScheduledTransitionService` processes pending transitions:

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Scheduling/IScheduledTransitionService.cs
public interface IScheduledTransitionService
{
    Task ProcessPendingTransitionsAsync(CancellationToken ct = default);
}
```

This service is typically invoked by a background hosted service on a periodic
timer. It scans entities in the `From` stage whose `DateProperty` is in the
past, and fires the corresponding transition.

---

### 16. Track Locale Progress

`[ForEachLocale]` declares that a stage must be completed independently for
every configured locale before the workflow can advance.

```csharp
using FrenchExDev.Net.Diem.Workflow.Locales.Attributes;

// On the workflow class -- the "Translation" stage requires all locales to complete:
[ForEachLocale("Translation")]
```

| Property | Type | Description |
|---|---|---|
| `Stage` | `string` | The stage that requires per-locale completion (required, positional) |

At runtime, `ILocaleTracker` manages locale progress:

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Locales/ILocaleTracker.cs
public enum LocaleStatus { Pending, InProgress, Complete }

public class LocaleProgress
{
    public required string Locale { get; init; }
    public LocaleStatus Status { get; set; } = LocaleStatus.Pending;
}

public interface ILocaleTracker
{
    Task<bool> MarkLocaleCompleteAsync(
        Guid entityId, string stage, string locale, CancellationToken ct = default);
    Task<IReadOnlyList<LocaleProgress>> GetProgressAsync(
        Guid entityId, string stage, CancellationToken ct = default);
}
```

**Usage:**

```csharp
// Check translation progress for an article
var progress = await localeTracker.GetProgressAsync(articleId, "Translation");
// progress == [
//   { Locale = "en", Status = Complete },
//   { Locale = "fr", Status = InProgress },
//   { Locale = "de", Status = Pending },
// ]

// Mark French translation as complete
var allDone = await localeTracker.MarkLocaleCompleteAsync(articleId, "Translation", "fr");
// allDone == false (German still pending)

// After German is marked complete:
var finished = await localeTracker.MarkLocaleCompleteAsync(articleId, "Translation", "de");
// finished == true (all locales complete -- workflow can advance)
```

---

## Infrastructure

---

### 17. Upload and Manage Media

The media system is built around the `IMediaStorage` interface and the
`MediaFile` record.

**`IMediaStorage`:**

```csharp
// From: FrenchExDev.Net.Diem.Media/IMediaStorage.cs
public interface IMediaStorage
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task<bool> ExistsAsync(string path, CancellationToken ct = default);
    string GetPublicUrl(string path);
}
```

**`MediaFile`:**

```csharp
// From: FrenchExDev.Net.Diem.Media/MediaFile.cs
public sealed class MediaFile
{
    public Guid Id { get; set; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required string StoragePath { get; init; }
    public string? AltText { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? UploadedBy { get; set; }
}
```

**`ThumbnailOptions`:**

```csharp
// From: FrenchExDev.Net.Diem.Media/ThumbnailOptions.cs
public sealed class ThumbnailOptions
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public ThumbnailMethod Method { get; set; } = ThumbnailMethod.Fit;
    public int Quality { get; set; } = 85;
}

public enum ThumbnailMethod { Fit, Center, Scale, Inflate }
```

**`FileSystemMediaStorage` -- the built-in implementation:**

The `FileSystemMediaStorage` stores files on the local filesystem under a
date-partitioned directory structure (`yyyy/MM/filename`).

```csharp
// From: FrenchExDev.Net.Diem.Media.FileSystem/FileSystemMediaStorage.cs
public sealed class FileSystemMediaStorage : IMediaStorage
{
    public FileSystemMediaStorage(string rootPath, string publicUrlBase = "/media");
    // ...
}
```

**Full upload/download/delete flow:**

```csharp
using FrenchExDev.Net.Diem.Media;
using FrenchExDev.Net.Diem.Media.FileSystem;

// 1. Create storage pointing to a local directory
var storage = new FileSystemMediaStorage("/var/diem/media", publicUrlBase: "/media");

// 2. Upload a file
await using var fileStream = File.OpenRead("/tmp/photo.jpg");
var storagePath = await storage.UploadAsync("photo.jpg", fileStream, "image/jpeg");
// storagePath == "2026/03/photo.jpg"

// 3. Get the public URL
var url = storage.GetPublicUrl(storagePath);
// url == "/media/2026/03/photo.jpg"

// 4. Check existence
var exists = await storage.ExistsAsync(storagePath);
// exists == true

// 5. Download the file
await using var downloaded = await storage.DownloadAsync(storagePath);
if (downloaded is not null)
{
    using var reader = new StreamReader(downloaded);
    // ... process content
}

// 6. Delete the file
await storage.DeleteAsync(storagePath);
// File is removed from disk

// 7. Track metadata
var record = new MediaFile
{
    Id = Guid.NewGuid(),
    FileName = "photo.jpg",
    ContentType = "image/jpeg",
    SizeBytes = fileStream.Length,
    StoragePath = storagePath,
    AltText = "Product photo",
    Width = 1200,
    Height = 800,
    UploadedBy = "alice",
};
```

Two storage providers are available:

| Provider | Package | Notes |
|---|---|---|
| `FileSystemMediaStorage` | `FrenchExDev.Net.Diem.Media.FileSystem` | Local disk, date-partitioned |
| MinIO | `FrenchExDev.Net.Diem.Media.Minio` | S3-compatible object storage |

---

### 18. Search Content

The search system is built around `ISearchEngine` and its supporting types.

**`ISearchEngine`:**

```csharp
// From: FrenchExDev.Net.Diem.Search/ISearchEngine.cs
public interface ISearchEngine
{
    Task IndexAsync(SearchDocument document, CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
    Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken ct = default);
}
```

**Supporting types:**

```csharp
public sealed class SearchDocument
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? EntityType { get; init; }
    public string? Url { get; init; }
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed class SearchQuery
{
    public required string Text { get; init; }
    public int MaxResults { get; init; } = 20;
    public int Skip { get; init; }
    public string? EntityTypeFilter { get; init; }
}

public sealed class SearchResults
{
    public required IReadOnlyList<SearchHit> Hits { get; init; }
    public required int TotalCount { get; init; }
}

public sealed class SearchHit
{
    public required string DocumentId { get; init; }
    public required string Title { get; init; }
    public required string Snippet { get; init; }
    public required float Score { get; init; }
    public string? Url { get; init; }
}
```

**Usage:**

```csharp
using FrenchExDev.Net.Diem.Search;

// 1. Index a document
await searchEngine.IndexAsync(new SearchDocument
{
    Id = article.Id.ToString(),
    Title = article.Title,
    Body = article.Body,
    EntityType = "Article",
    Url = $"/blog/{article.Slug}",
    Metadata = new Dictionary<string, string>
    {
        ["author"] = article.Author,
        ["tags"] = "dotnet,cms",
    }
});

// 2. Search with filters
var results = await searchEngine.SearchAsync(new SearchQuery
{
    Text = "content management",
    MaxResults = 10,
    Skip = 0,
    EntityTypeFilter = "Article",
});

// 3. Process results
foreach (var hit in results.Hits)
{
    Console.WriteLine($"[{hit.Score:F2}] {hit.Title} -- {hit.Snippet}");
    Console.WriteLine($"  URL: {hit.Url}");
}

// 4. Remove from index
await searchEngine.DeleteAsync(article.Id.ToString());
```

A Lucene-based implementation is available in `FrenchExDev.Net.Diem.Search.Lucene`.

---

### 19. Configure Caching

Caching is built around `IDiemCache` and `DiemCacheOptions`.

**`IDiemCache`:**

```csharp
// From: FrenchExDev.Net.Diem.Caching/DiemCaching.cs
public interface IDiemCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task InvalidateByTagAsync(string tag, CancellationToken ct = default);
}
```

**`DiemCacheOptions`:**

```csharp
// From: FrenchExDev.Net.Diem.Caching/DiemCaching.cs
public sealed class DiemCacheOptions
{
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableWidgetCache { get; set; } = true;
    public bool EnablePageCache { get; set; } = true;
    public bool EnableQueryCache { get; set; } = true;
}
```

**Usage:**

```csharp
using FrenchExDev.Net.Diem.Caching;

// 1. Read from cache, falling back to the database
var page = await cache.GetAsync<Page>($"page:{pageId}");
if (page is null)
{
    page = await LoadPageFromDatabase(pageId);
    await cache.SetAsync($"page:{pageId}", page, expiry: TimeSpan.FromMinutes(10));
}

// 2. Invalidate a specific key
await cache.RemoveAsync($"page:{pageId}");

// 3. Invalidate all entries tagged with a category
await cache.InvalidateByTagAsync("product-list");
```

Configure caching during startup:

```csharp
services.AddDiem(o =>
{
    // ... other options
});

// Configure cache options separately
services.AddSingleton(new DiemCacheOptions
{
    DefaultExpiry = TimeSpan.FromMinutes(10),
    EnableWidgetCache = true,
    EnablePageCache = true,
    EnableQueryCache = false,  // Disable query caching for dev
});
```

---

### 20. Track Metrics

`DiemMetrics` exposes pre-defined `System.Diagnostics.Metrics` counters and
histograms for observability.

```csharp
// From: FrenchExDev.Net.Diem.Metrics/DiemMetrics.cs
public sealed class DiemMetrics
{
    public static readonly Meter Meter = new("FrenchExDev.Net.Diem", "1.0.0");

    public static readonly Counter<long> PageViews =
        Meter.CreateCounter<long>("diem.page.views", description: "Page views");
    public static readonly Histogram<double> WidgetRenderDuration =
        Meter.CreateHistogram<double>("diem.widget.render_duration", "ms", "Widget render time");
    public static readonly Counter<long> ContentEdits =
        Meter.CreateCounter<long>("diem.content.edits", description: "Content edits");
    public static readonly Counter<long> WorkflowTransitions =
        Meter.CreateCounter<long>("diem.workflow.transitions", description: "Workflow transitions");
    public static readonly Counter<long> SearchQueries =
        Meter.CreateCounter<long>("diem.search.queries", description: "Search queries");
    public static readonly Counter<long> MediaUploads =
        Meter.CreateCounter<long>("diem.media.uploads", description: "Media uploads");
}
```

**Available instruments:**

| Metric name | Type | Description |
|---|---|---|
| `diem.page.views` | Counter | Total page views |
| `diem.widget.render_duration` | Histogram (ms) | Widget rendering latency |
| `diem.content.edits` | Counter | Content edit operations |
| `diem.workflow.transitions` | Counter | Workflow state transitions |
| `diem.search.queries` | Counter | Search query count |
| `diem.media.uploads` | Counter | Media upload count |

**Usage in your code:**

```csharp
using FrenchExDev.Net.Diem.Metrics;

// Increment page views
DiemMetrics.PageViews.Add(1, new KeyValuePair<string, object?>("page", "/products"));

// Record widget render time
var sw = Stopwatch.StartNew();
// ... render widget ...
sw.Stop();
DiemMetrics.WidgetRenderDuration.Record(sw.Elapsed.TotalMilliseconds,
    new KeyValuePair<string, object?>("widget", "ProductList"));

// Track content edits
DiemMetrics.ContentEdits.Add(1, new KeyValuePair<string, object?>("type", "Article"));

// Track workflow transitions
DiemMetrics.WorkflowTransitions.Add(1,
    new KeyValuePair<string, object?>("workflow", "Editorial"),
    new KeyValuePair<string, object?>("action", "Publish"));

// Track search queries
DiemMetrics.SearchQueries.Add(1);

// Track media uploads
DiemMetrics.MediaUploads.Add(1, new KeyValuePair<string, object?>("type", "image/jpeg"));
```

These metrics integrate with OpenTelemetry, Prometheus, or any
`System.Diagnostics.Metrics`-compatible collector.

---

### 21. Set Up the CMF

Register all Diem services with `AddDiem()` and configure them via
`DiemCmfOptions`.

**`DiemCmfOptions`:**

```csharp
// From: FrenchExDev.Net.Diem/DiemCmfOptions.cs
public sealed class DiemCmfOptions
{
    public string SiteName { get; set; } = "Diem Site";
    public string DefaultLocale { get; set; } = "en";
    public IList<string> SupportedLocales { get; set; } = ["en"];
    public bool EnableMetrics { get; set; } = true;
    public bool EnableRealTime { get; set; } = true;
    public string MediaStorageProvider { get; set; } = "FileSystem"; // FileSystem, Minio
}
```

**`AddDiem()` extension method:**

```csharp
// From: FrenchExDev.Net.Diem/DiemServiceCollectionExtensions.cs
public static class DiemServiceCollectionExtensions
{
    public static IServiceCollection AddDiem(
        this IServiceCollection services, Action<DiemCmfOptions>? configure = null);
}
```

**Startup configuration:**

```csharp
using FrenchExDev.Net.Diem;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDiem(options =>
{
    options.SiteName = "Contoso Commerce";
    options.DefaultLocale = "en-US";
    options.SupportedLocales = ["en-US", "fr-FR", "de-DE"];
    options.EnableMetrics = true;
    options.EnableRealTime = true;
    options.MediaStorageProvider = "Minio";  // or "FileSystem"
});

var app = builder.Build();
```

**`DiemComponentBase`** -- base class for all Diem components (admin modules,
front modules, widgets). Provides access to DI services and logging:

```csharp
// From: FrenchExDev.Net.Diem/DiemComponentBase.cs
public abstract class DiemComponentBase
{
    protected IServiceProvider Services { get; }
    protected ILogger Logger { get; }

    protected DiemComponentBase(IServiceProvider services, ILogger logger);
    protected T GetService<T>() where T : notnull;
}
```

**Identity types** (`FrenchExDev.Net.Diem.Identity`):

```csharp
public sealed class DiemUser
{
    public Guid Id { get; set; }
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; set; }
    public IList<string> Roles { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public static class DiemRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Author = "Author";
    public const string Publisher = "Publisher";
    public const string Viewer = "Viewer";
}

public interface IDiemUserContext
{
    DiemUser? CurrentUser { get; }
    bool IsInRole(string role);
    string? UserId { get; }
}
```

**Notifications** (`FrenchExDev.Net.Diem.Notifications`):

```csharp
public interface INotificationService
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

public sealed class Notification
{
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Email;
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public enum NotificationType { Email, Push, InApp }
```

---

## CLI

---

### 22. cmf Commands

The Diem CLI (`cmf`) provides 10 commands, registered in `CmfCommands`:

```csharp
// From: FrenchExDev.Net.Diem.Cli.Lib/CmfCommands.cs
public static class CmfCommands
{
    public static readonly string[] AvailableCommands =
    [
        "new", "add", "generate", "validate", "migrate", "report", "design", "install", "list", "uninstall"
    ];
}
```

| Command | Description |
|---|---|
| `cmf new` | Scaffold a new Diem project from a template |
| `cmf add` | Add a sub-DSL feature (part, block, widget, workflow, admin module) to the project |
| `cmf generate` | Run source generators and emit code |
| `cmf validate` | Validate all DSL attributes (MetaConcept constraints, missing fields, etc.) |
| `cmf migrate` | Generate or apply database migrations for the current model |
| `cmf report` | Generate a summary report of all DSL elements (parts, blocks, workflows, pages, admin modules) |
| `cmf design` | Launch the design-time scaffolder UI |
| `cmf install` | Install a Diem extension or theme package |
| `cmf list` | List installed extensions, registered parts, blocks, widgets, or workflows |
| `cmf uninstall` | Remove an installed extension |

---

## Testing

---

### 23. Test Attributes Have MetaConcept

Every DSL attribute carries a `[MetaConcept]` that links it to a concept type.
Use a `[Theory]` with `[InlineData]` to verify this across all attributes in a
sub-DSL.

**Pattern from `WorkflowAttributeTests`:**

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Tests/WorkflowAttributeTests.cs
using System.Reflection;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Diem.Workflow.StateMachine.Attributes;
using FrenchExDev.Net.Diem.Workflow.Gates.Attributes;
using FrenchExDev.Net.Diem.Workflow.Scheduling.Attributes;
using FrenchExDev.Net.Diem.Workflow.Locales.Attributes;
using Xunit;

public class WorkflowAttributeTests
{
    [Theory]
    [InlineData(typeof(WorkflowAttribute))]
    [InlineData(typeof(HasWorkflowAttribute))]
    [InlineData(typeof(StageAttribute))]
    [InlineData(typeof(TransitionAttribute))]
    [InlineData(typeof(GateAttribute))]
    [InlineData(typeof(RequiresRoleAttribute))]
    [InlineData(typeof(RequiresApprovalAttribute))]
    [InlineData(typeof(ScheduledTransitionAttribute))]
    [InlineData(typeof(ForEachLocaleAttribute))]
    public void AllWorkflowAttributes_HaveMetaConceptAttribute(Type attributeType)
    {
        var metaConcept = attributeType.GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(metaConcept);
        Assert.NotNull(metaConcept.ConceptType);
    }
}
```

**The same pattern for Content attributes:**

```csharp
// From: FrenchExDev.Net.Diem.Content.Tests/ContentTests.cs
[Fact]
public void ContentPartAttribute_has_MetaConcept()
{
    var attr = typeof(ContentPartAttribute).GetCustomAttribute<MetaConceptAttribute>();
    Assert.NotNull(attr);
    Assert.Equal("ContentPart", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
}
```

**Testing MetaInherits (gate specializations):**

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Tests/WorkflowAttributeTests.cs
[Fact]
public void RequiresRoleAttribute_InheritsFromGateConcept()
{
    var metaInherits = typeof(RequiresRoleAttribute).GetCustomAttribute<MetaInheritsAttribute>();
    Assert.NotNull(metaInherits);
    Assert.Equal(typeof(GateConcept), metaInherits.ParentConceptType);
}
```

**Testing MetaConstraint validation:**

```csharp
// From: FrenchExDev.Net.Diem.Content.Tests/ContentTests.cs
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
```

---

### 24. Test Workflow Transitions

Test the `WorkflowEngine` by registering transitions and verifying resolution,
available actions, and edge cases.

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Tests/WorkflowEngineTests.cs
using FrenchExDev.Net.Diem.Workflow.StateMachine;
using Xunit;

public class WorkflowEngineTests
{
    [Fact]
    public void RegisterTransition_StoresTransition()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");

        var target = engine.TryGetTarget("Draft", "Submit");
        Assert.Equal("Review", target);
    }

    [Fact]
    public void TryGetTarget_ReturnsCorrectTarget()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");
        engine.RegisterTransition("Review", "Approve", "Published");
        engine.RegisterTransition("Review", "Reject", "Draft");

        Assert.Equal("Review", engine.TryGetTarget("Draft", "Submit"));
        Assert.Equal("Published", engine.TryGetTarget("Review", "Approve"));
        Assert.Equal("Draft", engine.TryGetTarget("Review", "Reject"));
    }

    [Fact]
    public void TryGetTarget_ReturnsNull_ForInvalidTransition()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");

        Assert.Null(engine.TryGetTarget("Draft", "Approve"));
        Assert.Null(engine.TryGetTarget("Published", "Submit"));
    }

    [Fact]
    public void GetAvailableActions_ReturnsCorrectActions()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Review", "Approve", "Published");
        engine.RegisterTransition("Review", "Reject", "Draft");
        engine.RegisterTransition("Draft", "Submit", "Review");

        var actions = engine.GetAvailableActions("Review");
        Assert.Equal(2, actions.Count);
        Assert.Contains("Approve", actions);
        Assert.Contains("Reject", actions);
    }

    [Fact]
    public void GetAvailableActions_ReturnsEmpty_ForTerminalStage()
    {
        var engine = new WorkflowEngine("TestWorkflow");
        engine.RegisterTransition("Draft", "Submit", "Review");

        var actions = engine.GetAvailableActions("Published");
        Assert.Empty(actions);
    }

    [Fact]
    public void WorkflowName_ReturnsConfiguredName()
    {
        var engine = new WorkflowEngine("ArticleWorkflow");
        Assert.Equal("ArticleWorkflow", engine.WorkflowName);
    }
}
```

**Testing GateResult:**

```csharp
// From: FrenchExDev.Net.Diem.Workflow.Tests/GateResultTests.cs
using FrenchExDev.Net.Diem.Workflow.Gates;
using Xunit;

public class GateResultTests
{
    [Fact]
    public void Allowed_ReturnsIsAllowedTrue()
    {
        var result = GateResult.Allowed();
        Assert.True(result.IsAllowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Denied_ReturnsIsAllowedFalse_WithReason()
    {
        var result = GateResult.Denied("Insufficient permissions");
        Assert.False(result.IsAllowed);
        Assert.Equal("Insufficient permissions", result.Reason);
    }
}
```

---

### 25. Test Page Routing

Test the `PageRouter` by constructing a list of `Page` entities and verifying
exact matches, slug fallbacks, unpublished filtering, and 404 cases.

```csharp
// From: FrenchExDev.Net.Diem.Pages.Tests/PagesSubDslTests.cs
using FrenchExDev.Net.Diem.Pages.Layouts;
using FrenchExDev.Net.Diem.Pages.Routing;
using Xunit;

public class PageRouterTests
{
    private readonly PageRouter _router = new();

    private static List<Page> CreateTestPages() =>
    [
        new Page { MaterializedPath = "/", IsPublished = true, Title = "Home" },
        new Page { MaterializedPath = "/about", IsPublished = true, Title = "About" },
        new Page { MaterializedPath = "/products", IsPublished = true, Title = "Products" },
        new Page { MaterializedPath = "/draft", IsPublished = false, Title = "Draft" },
    ];

    [Fact]
    public void Resolve_ExactPath_ReturnsPage()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/about", pages);

        Assert.NotNull(result);
        Assert.Equal("About", result.Page.Title);
        Assert.Null(result.EntitySlug);
    }

    [Fact]
    public void Resolve_ParentPath_WithEntitySlug()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/products/running-shoes", pages);

        Assert.NotNull(result);
        Assert.Equal("Products", result.Page.Title);
        Assert.Equal("running-shoes", result.EntitySlug);
    }

    [Fact]
    public void Resolve_UnmatchedPath_ReturnsNull()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/nonexistent", pages);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_UnpublishedPage_ReturnsNull()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/draft", pages);

        Assert.Null(result);
    }
}
```

**Testing page tree entities:**

```csharp
// From: FrenchExDev.Net.Diem.Pages.Tests/PagesSubDslTests.cs
[Fact]
public void Page_Has_Required_Properties()
{
    var page = new Page();
    Assert.Equal(Guid.Empty, page.Id);
    Assert.Equal("", page.Title);
    Assert.Equal("", page.Slug);
    Assert.Equal("", page.MaterializedPath);
    Assert.Null(page.ParentId);
}

[Fact]
public void Layout_Has_Areas_Collection()
{
    var layout = new Layout();
    Assert.NotNull(layout.Areas);
    Assert.Empty(layout.Areas);
}

[Fact]
public void Zone_MaxWidgets_Defaults_To_10()
{
    var zone = new Zone();
    Assert.Equal(10, zone.MaxWidgets);
}
```
