namespace CarReports.Web.Services;

public interface IUploadCache
{
    Task<string> StoreAsync(Stream stream, string originalFileName, CancellationToken cancellationToken);
    bool TryGet(string token, out string filePath, out string originalFileName);
    void Remove(string token);
}
