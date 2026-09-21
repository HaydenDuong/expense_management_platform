using expense_management_app.Services.Storage;
using expense_management_app.Options.Storage;
using Microsoft.Extensions.Options;

namespace expense_management_app.Infrastructure.Storage;

public sealed class LocalObjectStorage : IObjectStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalObjectStorage> _logger;

    public LocalObjectStorage(

        IOptions<LocalStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<LocalObjectStorage> logger)
    {
        var configuredRootPath = options.Value.RootPath;

        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredRootPath)
                ? configuredRootPath
                : Path.Combine(
                    environment.ContentRootPath,
                    configuredRootPath));

        _logger = logger;
    }

    public async Task UploadAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(content);

        if(!content.CanRead)
        {
            throw new ArgumentException(
                "Content stream must be readable.",
                nameof(content));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "Content type is required.",
                nameof(contentType));
        }

        var fullPath = ResolveFullPath(storageKey);

        var directoryPath = Path.GetDirectoryName(fullPath);

        if (directoryPath is null)
        {
            throw new InvalidOperationException("The storage path does not contain a parent directory");
        }

        Directory.CreateDirectory(directoryPath);

        var destinationStream = new FileStream(
            fullPath,                  
            FileMode.CreateNew,         
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        
        try
        {   
            await using (destinationStream)
            {
                await content.CopyToAsync(
                    destinationStream,
                    cancellationToken);
            }
        }

        catch
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(
                    cleanupException,
                    "Failed to remove partial local object {StorageKey}", storageKey);
            }

            throw;
        }
    }

    public Task<Stream> DownloadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
    
        var fullPath = ResolveFullPath(storageKey);

        File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string ResolveFullPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException(
                "Storage key is required.",
                nameof(storageKey));
        }

        if (Path.IsPathRooted(storageKey))
        {
            throw new ArgumentException(
                "Storage key must be relative.",
                nameof(storageKey));
        }

        var platformRelativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(
            Path.Combine(_rootPath, platformRelativePath));
        
        var relativePath = Path.GetRelativePath(_rootPath, fullPath);

        if (relativePath == ".." || 
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                "Storage key resolves outside the storage root.",
                nameof(storageKey));
        }

        return fullPath;
    }
}