using System.Net;
using Microsoft.Extensions.Logging;
using Velopack.Util;

namespace Velopack.Deployment;

public class FtpDownloadOptions : RepositoryOptions, IObjectDownloadOptions
{
    public string Url { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool PassiveMode { get; set; } = true;
}

public class FtpUploadOptions : FtpDownloadOptions, IObjectUploadOptions
{
    public int KeepMaxReleases { get; set; }
}

public class FtpClient
{
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly bool _passiveMode;
    private readonly double _timeout;

    public FtpClient(string baseUrl, string username, string password, bool passiveMode, double timeout)
    {
        _baseUrl = baseUrl.TrimEnd('/') + '/';
        _username = username;
        _password = password;
        _passiveMode = passiveMode;
        _timeout = timeout;
    }

    public async Task<byte[]> DownloadFileAsync(string key, CancellationToken cancellationToken = default)
    {
        var ftpUrl = _baseUrl + key;
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.DownloadFile;
        request.Timeout = (int)TimeSpan.FromMinutes(_timeout).TotalMilliseconds;
        request.UsePassive = _passiveMode;

        if (!string.IsNullOrEmpty(_username))
        {
            request.Credentials = new NetworkCredential(_username, _password);
        }

        try
        {
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            using var responseStream = response.GetResponseStream();
            using var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse response && 
                                     response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
        {
            return null; // Файл не найден
        }
    }

    public async Task UploadFileAsync(string key, string filePath, bool noCache = false, CancellationToken cancellationToken = default)
    {
        var ftpUrl = _baseUrl + key;
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.UploadFile;
        request.Timeout = (int)TimeSpan.FromMinutes(_timeout).TotalMilliseconds;
        request.UsePassive = _passiveMode;

        if (!string.IsNullOrEmpty(_username))
        {
            request.Credentials = new NetworkCredential(_username, _password);
        }

        using (var fileStream = File.OpenRead(filePath))
        {
            request.ContentLength = fileStream.Length;
            
            using (var requestStream = await request.GetRequestStreamAsync())
            {
                await fileStream.CopyToAsync(requestStream, cancellationToken);
            }
        }

        using (var response = (FtpWebResponse)await request.GetResponseAsync())
        {
            if (response.StatusCode != FtpStatusCode.ClosingData && 
                response.StatusCode != FtpStatusCode.CommandOK)
            {
                throw new Exception($"FTP upload failed with status: {response.StatusDescription}");
            }
        }
    }

    public async Task DeleteFileAsync(string key, CancellationToken cancellationToken = default)
    {
        var ftpUrl = _baseUrl + key;
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.DeleteFile;
        request.Timeout = (int)TimeSpan.FromMinutes(_timeout).TotalMilliseconds;
        request.UsePassive = _passiveMode;

        if (!string.IsNullOrEmpty(_username))
        {
            request.Credentials = new NetworkCredential(_username, _password);
        }

        try
        {
            using var response = (FtpWebResponse)await request.GetResponseAsync();
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse response && 
                                     response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
        {
            // file not found, ignore
        }
    }

    public async Task<bool> FileExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var ftpUrl = _baseUrl + key;
        var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
        request.Method = WebRequestMethods.Ftp.GetFileSize;
        request.Timeout = (int)TimeSpan.FromMinutes(_timeout).TotalMilliseconds;
        request.UsePassive = _passiveMode;

        if (!string.IsNullOrEmpty(_username))
        {
            request.Credentials = new NetworkCredential(_username, _password);
        }

        try
        {
            using var response = (FtpWebResponse)await request.GetResponseAsync();
            return true;
        }
        catch (WebException ex) when (ex.Response is FtpWebResponse response && 
                                     response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
        {
            return false;
        }
    }
}

public class FtpRepository : ObjectRepository<FtpDownloadOptions, FtpUploadOptions, FtpClient>
{
    public FtpRepository(ILogger logger) : base(logger)
    {
    }

    protected override FtpClient CreateClient(FtpDownloadOptions options)
    {
        return new FtpClient(
            options.Url,
            options.Username,
            options.Password,
            options.PassiveMode,
            options.Timeout);
    }

    protected override async Task DeleteObject(FtpClient client, string key)
    {
        await RetryAsync(() => client.DeleteFileAsync(key), "Deleting " + key);
    }

    protected override async Task<byte[]> GetObjectBytes(FtpClient client, string key)
    {
        return await RetryAsyncRet(
            async () => await client.DownloadFileAsync(key),
            $"Downloading {key}...");
    }

    protected override async Task SaveEntryToFileAsync(FtpDownloadOptions options, VelopackAsset entry, string filePath)
    {
        var client = CreateClient(options);
        var bytes = await GetObjectBytes(client, entry.FileName);
        
        if (bytes != null && bytes.Length > 0)
        {
            await File.WriteAllBytesAsync(filePath, bytes);
        }
        else
        {
            throw new FileNotFoundException($"File {entry.FileName} not found on FTP server");
        }
    }

    protected override async Task UploadObject(FtpClient client, string key, FileInfo f, bool overwriteRemote, bool noCache)
    {
        bool fileExists = await client.FileExistsAsync(key);
        
        if (fileExists)
        {
            if (!overwriteRemote)
            {
                Log.Warn($"File '{key}' exists in remote. Use 'overwrite' argument to replace remote file.");
                return;
            }
            
            Log.Info($"File '{key}' exists in remote, replacing...");
        }

        await RetryAsync(() => client.UploadFileAsync(key, f.FullName, noCache), "Uploading " + key + (noCache ? " (no-cache)" : ""));
    }
}