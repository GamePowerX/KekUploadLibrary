using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class is internally used by the <see cref="DownloadClient"/> to download files from the KekUploadServer.
    /// It is needed to report the download progress. Because the <see cref="HttpClient"/> does not support this.
    /// This class is based on the code from https://stackoverflow.com/a/43169927.
    /// </summary>
    public class HttpClientDownloadWithProgress : IDisposable
    {
        /// <summary>
        /// This is the delegate for the <see cref="ProgressChanged"/> event.
        /// </summary>
        public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded,
            double? progressPercentage);

        /// <summary>
        /// The url of the file to download.
        /// </summary>
        private readonly string _downloadUrl;
        /// <summary>
        /// The <see cref="DownloadItem"/> where the file is downloaded to.
        /// </summary>
        private readonly DownloadItem _downloadItem;

        /// <summary>
        /// The <see cref="HttpClient"/> used to download the file.
        /// </summary>
        private HttpClient? _httpClient;

        /// <summary>
        /// Creates a new instance of the <see cref="HttpClientDownloadWithProgress"/> class.
        /// </summary>
        /// <param name="downloadUrl">The url of the file to download.</param>
        /// <param name="downloadItem">The <see cref="DownloadItem"/> where the file is downloaded to.</param>
        public HttpClientDownloadWithProgress(string downloadUrl, DownloadItem downloadItem)
        {
            _downloadUrl = downloadUrl;
            _downloadItem = downloadItem;
        }

        /// <summary>
        /// This method is called when the <see cref="HttpClientDownloadWithProgress"/> is disposed.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// This event is triggered when the download progress changes.
        /// </summary>
        public event ProgressChangedHandler? ProgressChanged;

        /// <summary>
        /// This method starts the download.
        /// </summary>
        public async Task StartDownload()
        {
            _httpClient = new HttpClient {Timeout = TimeSpan.FromMinutes(5)};

            using var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            await DownloadFileFromHttpResponseMessage(response);
        }

        /// <summary>
        /// This method downloads the file from the <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseMessage"/> to download the file from.</param>
        private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await ProcessContentStream(totalBytes, contentStream);
            _downloadItem.Close();
        }

        /// <summary>
        /// This method processes the content stream.
        /// </summary>
        /// <param name="totalDownloadSize">The total size of the file to download.</param>
        /// <param name="contentStream">The content stream to process.</param>
        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
        {
            var totalBytesRead = 0L;
            var readCount = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;
            
            do
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    isMoreToRead = false;
                    TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    continue;
                }

                await _downloadItem.WriteDataAsync(buffer, 0, bytesRead);

                totalBytesRead += bytesRead;
                readCount += 1;

                if (readCount % 100 == 0)
                    TriggerProgressChanged(totalDownloadSize, totalBytesRead);
            } while (isMoreToRead);
        }

        /// <summary>
        /// This method triggers the <see cref="ProgressChanged"/> event.
        /// </summary>
        /// <param name="totalDownloadSize">The total size of the file to download.</param>
        /// <param name="totalBytesRead">The total bytes read.</param>
        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (ProgressChanged == null)
                return;

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double) totalBytesRead / totalDownloadSize.Value * 100, 2);

            ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
        }
    }
}