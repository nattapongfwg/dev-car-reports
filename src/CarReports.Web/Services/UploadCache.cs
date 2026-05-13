using System.Collections.Concurrent;

namespace CarReports.Web.Services;

public sealed class UploadCache : IUploadCache, IDisposable
{
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly string _root;
    private readonly Timer _cleanupTimer;
    private readonly ILogger<UploadCache> _logger;

    public UploadCache(IHostEnvironment env, ILogger<UploadCache> logger)
    {
        _logger = logger;
        _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(_root);
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<string> StoreAsync(Stream stream, string originalFileName, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var safeExt = Path.GetExtension(originalFileName);
        var path = Path.Combine(_root, $"{token}{safeExt}");

        await using (var fs = File.Create(path))
        {
            await stream.CopyToAsync(fs, cancellationToken);
        }

        _entries[token] = new CacheEntry(path, originalFileName, DateTime.UtcNow.Add(EntryTtl));
        return token;
    }

    public bool TryGet(string token, out string filePath, out string originalFileName)
    {
        if (_entries.TryGetValue(token, out var entry) && File.Exists(entry.Path) && DateTime.UtcNow < entry.Expires)
        {
            filePath = entry.Path;
            originalFileName = entry.OriginalFileName;
            return true;
        }
        filePath = string.Empty;
        originalFileName = string.Empty;
        return false;
    }

    public void Remove(string token)
    {
        if (_entries.TryRemove(token, out var entry))
        {
            TryDelete(entry.Path);
        }
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var (token, entry) in _entries)
        {
            if (now >= entry.Expires)
            {
                if (_entries.TryRemove(token, out var removed))
                {
                    TryDelete(removed.Path);
                }
            }
        }
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete cached upload {Path}", path); }
    }

    public void Dispose() => _cleanupTimer.Dispose();

    private sealed record CacheEntry(string Path, string OriginalFileName, DateTime Expires);
}
