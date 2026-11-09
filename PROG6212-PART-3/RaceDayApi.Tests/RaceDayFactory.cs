using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDayApi.Data;
using RaceDayApi.Services;

namespace RaceDayApi.Tests;

/// <summary>A fake Azure Blob Storage so the tests need no cloud account.</summary>
public class InMemoryBlobStorage : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _blobs = new();

    public int Count => _blobs.Count;

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _blobs[blobName] = (ms.ToArray(), contentType);
    }

    public Task<BlobContent?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        if (!_blobs.TryGetValue(blobName, out var blob))
            return Task.FromResult<BlobContent?>(null);

        return Task.FromResult<BlobContent?>(new BlobContent(new MemoryStream(blob.Data), blob.ContentType));
    }

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Boots the real API in memory, but swaps SQL Server for an isolated in-memory database
/// and Azure Blob Storage for an in-memory fake, so the tests run anywhere (including GitHub Actions).
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public InMemoryBlobStorage Storage => Services.GetRequiredService<InMemoryBlobStorage>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<RaceDayContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddSingleton<InMemoryBlobStorage>();
            services.AddSingleton<IBlobStorageService>(sp => sp.GetRequiredService<InMemoryBlobStorage>());
        });
    }
}
