using System.Security.Cryptography;
using System.Text;
using Hato.Modules.Delivery.Infrastructure.Persistence.Services;
using Xunit;

namespace Hato.Modules.Delivery.UnitTests;

public class LocalArtifactStorageTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly LocalArtifactStorage _storage;

    public LocalArtifactStorageTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "hato_delivery_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storage = new LocalArtifactStorage(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // ignore cleanup errors in test
            }
        }
    }

    [Fact]
    public async Task SaveArtifactAsync_WritesAtomically()
    {
        var content = "SAMPLE_APK_BINARY_CONTENT"u8.ToArray();
        using var stream = new MemoryStream(content);
        var fileName = "hato-1.0.0-stage-b100-a1b2c3d.apk";

        await _storage.SaveArtifactAsync(fileName, stream);

        // Verify file exists at destination and no leftover .partial file exists
        var finalPath = Path.Combine(_tempDirectory, fileName);
        Assert.True(File.Exists(finalPath));
        Assert.Empty(Directory.GetFiles(_tempDirectory, "*.partial*"));

        var readBytes = await File.ReadAllBytesAsync(finalPath);
        Assert.Equal(content, readBytes);
    }

    [Theory]
    [InlineData("../sneaky.apk")]
    [InlineData("../../etc/passwd")]
    [InlineData("sub/../../escape.apk")]
    [InlineData("/absolute/path/file.apk")]
    public async Task PathTraversal_ThrowsArgumentException(string maliciousFileName)
    {
        using var stream = new MemoryStream("data"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.SaveArtifactAsync(maliciousFileName, stream));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.OpenReadStreamAsync(maliciousFileName));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.DeleteIfExistsAsync(maliciousFileName));
    }

    [Fact]
    public async Task OpenReadStreamAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var stream = await _storage.OpenReadStreamAsync("non-existent.apk");
        Assert.Null(stream);
    }

    [Fact]
    public async Task DeleteIfExistsAsync_RemovesFileIfExists()
    {
        var fileName = "to-delete.apk";
        using var stream = new MemoryStream("data"u8.ToArray());
        await _storage.SaveArtifactAsync(fileName, stream);

        Assert.True(await _storage.ExistsAsync(fileName));

        var deleted = await _storage.DeleteIfExistsAsync(fileName);
        Assert.True(deleted);
        Assert.False(await _storage.ExistsAsync(fileName));
    }
}
