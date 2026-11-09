using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDayApi.Services;

public class AzureStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "raceday-images";
}

public record BlobContent(Stream Content, string ContentType);

public class StorageNotConfiguredException : Exception
{
    public StorageNotConfiguredException()
        : base("Azure Blob Storage is not configured. Set AzureStorage:ConnectionString.") { }
}

/// <summary>
/// Blob names are built from the event id or user id, so no extra database columns are needed:
/// an image exists if (and only if) the blob exists. Uploading again simply replaces it.
/// </summary>
public static class BlobNames
{
    public static string EventBanner(int eventId) => $"event-banners/{eventId}";

    public static string ProfilePicture(string role, int userId) => $"profile-pictures/{role.ToLowerInvariant()}-{userId}";
}

/// <summary>All image storage goes through this abstraction, so the MVC app never talks to Azure directly.</summary>
public interface IBlobStorageService
{
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default);
    Task<BlobContent?> DownloadAsync(string blobName, CancellationToken ct = default);
    Task DeleteAsync(string blobName, CancellationToken ct = default);
}

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialised;

    public AzureBlobStorageService(IOptions<AzureStorageOptions> options)
    {
        var o = options.Value;
        _container = new BlobContainerClient(o.ConnectionString, o.ContainerName);
    }

    // The container is private (no public access); images are only served through the API.
    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_initialised) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (!_initialised)
            {
                await _container.CreateIfNotExistsAsync(cancellationToken: ct);
                _initialised = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        var blob = _container.GetBlobClient(blobName);
        var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
        await blob.UploadAsync(content, options, ct);   // creates the blob, or replaces the existing one
    }

    public async Task<BlobContent?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            return new BlobContent(response.Value.Content, response.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }
}

/// <summary>Used when no connection string is configured, so the API still starts and explains what is missing.</summary>
public class UnconfiguredBlobStorageService : IBlobStorageService
{
    public Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
        => throw new StorageNotConfiguredException();

    public Task<BlobContent?> DownloadAsync(string blobName, CancellationToken ct = default)
        => throw new StorageNotConfiguredException();

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
        => throw new StorageNotConfiguredException();
}

public static class BlobStorageExtensions
{
    /// <summary>Deletes a blob but never throws (used for clean-up after the main operation has succeeded).</summary>
    public static async Task TryDeleteAsync(this IBlobStorageService storage, string blobName, ILogger logger)
    {
        try
        {
            await storage.DeleteAsync(blobName);
        }
        catch (StorageNotConfiguredException)
        {
            // Nothing can be stored, so there is nothing to clean up.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete blob {BlobName}", blobName);
        }
    }
}
