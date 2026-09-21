namespace expense_management_app.Services.Storage;

public interface IObjectStorage
{
    Task UploadAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
    
    Task<Stream> DownloadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
    
    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}