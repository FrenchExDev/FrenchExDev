namespace FrenchExDev.Net.Diem.Tests;

using FrenchExDev.Net.Diem;
using FrenchExDev.Net.Diem.Media;
using FrenchExDev.Net.Diem.Media.FileSystem;
using FrenchExDev.Net.Diem.Search;
using FrenchExDev.Net.Diem.Identity;
using FrenchExDev.Net.Diem.Caching;
using FrenchExDev.Net.Diem.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class DiemCoreTests
{
    [Fact]
    public void AddDiem_registers_options()
    {
        var services = new ServiceCollection();
        services.AddDiem(o => o.SiteName = "Test Site");
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<DiemCmfOptions>();
        Assert.Equal("Test Site", options.SiteName);
    }

    [Fact]
    public void Default_options_have_sensible_values()
    {
        var options = new DiemCmfOptions();
        Assert.Equal("en", options.DefaultLocale);
        Assert.True(options.EnableMetrics);
        Assert.Equal("FileSystem", options.MediaStorageProvider);
    }
}

public class MediaTests
{
    [Fact]
    public async Task FileSystem_upload_and_download()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "diem-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new FileSystemMediaStorage(tempDir);
            var content = new MemoryStream("hello world"u8.ToArray());
            var path = await storage.UploadAsync("test.txt", content, "text/plain");
            Assert.True(await storage.ExistsAsync(path));

            {
                await using var downloaded = await storage.DownloadAsync(path);
                Assert.NotNull(downloaded);
                using var reader = new StreamReader(downloaded!);
                Assert.Equal("hello world", await reader.ReadToEndAsync());
            }

            await storage.DeleteAsync(path);
            Assert.False(await storage.ExistsAsync(path));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MediaFile_has_required_properties()
    {
        var file = new MediaFile
        {
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 12345,
            StoragePath = "2026/03/photo.jpg"
        };
        Assert.Equal("photo.jpg", file.FileName);
        Assert.Equal(12345, file.SizeBytes);
    }
}

public class SearchTests
{
    [Fact]
    public void SearchDocument_can_be_created()
    {
        var doc = new SearchDocument { Id = "1", Title = "Test", Body = "Content" };
        Assert.Equal("1", doc.Id);
    }

    [Fact]
    public void SearchQuery_has_defaults()
    {
        var query = new SearchQuery { Text = "hello" };
        Assert.Equal(20, query.MaxResults);
        Assert.Equal(0, query.Skip);
    }
}

public class IdentityTests
{
    [Fact]
    public void DiemRoles_has_builtin_roles()
    {
        Assert.Equal("Admin", DiemRoles.Admin);
        Assert.Equal("Editor", DiemRoles.Editor);
        Assert.Equal("Author", DiemRoles.Author);
        Assert.Equal("Publisher", DiemRoles.Publisher);
        Assert.Equal("Viewer", DiemRoles.Viewer);
    }

    [Fact]
    public void DiemUser_can_be_created()
    {
        var user = new DiemUser { UserName = "alice", Email = "alice@test.com" };
        Assert.True(user.IsActive);
        Assert.Empty(user.Roles);
    }
}

public class CachingTests
{
    [Fact]
    public void DiemCacheOptions_has_defaults()
    {
        var opts = new DiemCacheOptions();
        Assert.Equal(TimeSpan.FromMinutes(5), opts.DefaultExpiry);
        Assert.True(opts.EnableWidgetCache);
    }
}

public class NotificationTests
{
    [Fact]
    public void Notification_can_be_created()
    {
        var n = new Notification { Recipient = "a@b.com", Subject = "Hi", Body = "Hello" };
        Assert.Equal(NotificationType.Email, n.Type);
    }
}
