namespace DockAi.Api.Indexing;

/// <summary>
/// Lucene field name constants used across the index.
/// </summary>
public static class IndexSchema
{
    public const string Id = "id";
    public const string Name = "name";
    public const string FileName = "file_name";
    public const string FilePath = "file_path";
    public const string FileType = "file_type";
    public const string FileSize = "file_size";
    public const string Date = "date";
    public const string Content = "content";
    public const string Entities = "entities";
    public const string EntityTypes = "entity_types";
    public const string EntityRoles = "entity_roles";
    public const string Tracks = "tracks";
    public const string Themes = "themes";
    public const string Categories = "categories";
    public const string Tags = "tags";
    public const string LinkedDocs = "linked_docs";
    public const string LinkTypes = "link_types";
}
