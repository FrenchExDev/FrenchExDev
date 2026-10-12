namespace FrenchExDev.Net.Diem;

/// <summary>
/// Configuration options for the Diem CMF.
/// </summary>
public sealed class DiemCmfOptions
{
    public string SiteName { get; set; } = "Diem Site";
    public string DefaultLocale { get; set; } = "en";
    public IList<string> SupportedLocales { get; set; } = ["en"];
    public bool EnableMetrics { get; set; } = true;
    public bool EnableRealTime { get; set; } = true;
    public string MediaStorageProvider { get; set; } = "FileSystem"; // FileSystem, Minio
}
