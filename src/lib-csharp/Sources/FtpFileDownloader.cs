using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Velopack.Sources
{
    public class FtpFileDownloader : IFileDownloader
    {
        public async Task DownloadFile(string url, string targetFile, Action<int> progress, string? authorization = null, string? accept = null, double timeout = 30, CancellationToken cancelToken = default)
        {
            var ftpRequest = (FtpWebRequest)WebRequest.Create(url);
            ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
            ftpRequest.Timeout = (int)TimeSpan.FromMinutes(timeout).TotalMilliseconds;
            ftpRequest.UsePassive = true;
            
            if (!string.IsNullOrEmpty(authorization))
            {
                string[] credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorization)).Split(':');
                if (credentials.Length == 2)
                {
                    ftpRequest.Credentials = new NetworkCredential(credentials[0], credentials[1]);
                }
            }
            
            using (var response = (FtpWebResponse)await ftpRequest.GetResponseAsync())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = File.Create(targetFile))
            {
                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                long contentLength = response.ContentLength;
                int lastProgress = 0;
                
                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancelToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancelToken);
                    totalBytesRead += bytesRead;
                    
                    if (contentLength > 0)
                    {
                        var currentProgress = (int)((double)totalBytesRead / contentLength * 100);
                        if (currentProgress - lastProgress >= 3)
                        {
                            lastProgress = currentProgress;
                            progress?.Invoke(currentProgress);
                        }
                    }
                }
                
                if (lastProgress < 100)
                    progress?.Invoke(100);
            }
        }
        
        public async Task<byte[]> DownloadBytes(string url, string? authorization = null, string? accept = null, double timeout = 30)
        {
            var ftpRequest = (FtpWebRequest)WebRequest.Create(url);
            ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
            ftpRequest.Timeout = (int)TimeSpan.FromMinutes(timeout).TotalMilliseconds;
            ftpRequest.UsePassive = true;
            
            if (!string.IsNullOrEmpty(authorization))
            {
                string[] credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorization)).Split(':');
                if (credentials.Length == 2)
                {
                    ftpRequest.Credentials = new NetworkCredential(credentials[0], credentials[1]);
                }
            }
            
            using (var response = (FtpWebResponse)await ftpRequest.GetResponseAsync())
            using (var responseStream = response.GetResponseStream())
            using (var memoryStream = new MemoryStream())
            {
                await responseStream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
        
        public async Task<string> DownloadString(string url, string? authorization = null, string? accept = null, double timeout = 30)
        {
            byte[] bytes = await DownloadBytes(url, authorization, accept, timeout);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}