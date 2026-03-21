namespace FrenchExDev.Net.Diem.Caching;

public interface IDiemCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task InvalidateByTagAsync(string tag, CancellationToken ct = default);
}

public sealed class DiemCacheOptions
{
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableWidgetCache { get; set; } = true;
    public bool EnablePageCache { get; set; } = true;
    public bool EnableQueryCache { get; set; } = true;
}
