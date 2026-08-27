using Microsoft.Extensions.Configuration;
using ZEstate.Core.Interfaces;

namespace ZEstate.Infrastructure.Services;

// Saves to local disk under "FileStorage:RootPath" (default "App_Data/uploads").
// NOTE: on hosts with an ephemeral filesystem (e.g. Render's default disk) files
// won't survive a redeploy/restart - swap in a cloud-backed IFileStorage
// (S3/Azure Blob/etc.) before relying on this for real uploads in production.
public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IConfiguration configuration)
    {
        _rootPath = configuration["FileStorage:RootPath"] ?? "App_Data/uploads";
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_rootPath, storedName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return storedName;
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_rootPath, storagePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(stream);
    }

    public void Delete(string storagePath)
    {
        var fullPath = Path.Combine(_rootPath, storagePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
