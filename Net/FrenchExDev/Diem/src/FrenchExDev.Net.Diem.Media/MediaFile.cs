namespace FrenchExDev.Net.Diem.Media;

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
