using System;
using System.Threading;
using System.Threading.Tasks;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class is used to download files from the KekUploadServer.
    /// </summary>
    public class DownloadClient
    {
        /// <summary>
        /// This is the progress changed event. It is called every time the download progress changes.
        /// </summary>
        public event HttpClientDownloadWithProgress.ProgressChangedHandler? ProgressChangedEvent;

        /// <summary>
        /// This method downloads a file from the KekUploadServer without a <see cref="CancellationToken"/>.
        /// This method is obsolete. Use <see cref="Download(string,DownloadItem,CancellationToken)"/> instead.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="path">The path where the file should be saved.</param>
        /// <exception cref="KekException">Thrown when the file could not be successfully downloaded.</exception>
        [Obsolete("Use Download(string,DownloadItem,CancellationToken) instead!")]
        public void DownloadFile(string downloadUrl, string path)
        {
            Download(downloadUrl, new DownloadItem(path));
        }

        /// <summary>
        /// This method downloads a file from the KekUploadServer with a <see cref="CancellationToken"/>.
        /// This method is obsolete. Use <see cref="Download(string,DownloadItem,CancellationToken)"/> instead.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="path">The path where the file should be saved.</param>
        /// <param name="token">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="KekException">Thrown when the file could not be successfully downloaded.</exception>
        [Obsolete("Use Download(string,DownloadItem,CancellationToken) instead!")]
        public void DownloadFile(string downloadUrl, string path, CancellationToken token)
        {
            Download(downloadUrl, new DownloadItem(path), token);
        }

        /// <summary>
        /// This method asynchronously downloads a file from the KekUploadServer.
        /// This method is obsolete. Use <see cref="DownloadAsync(string,DownloadItem)"/> instead.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="path">The path where the file should be saved.</param>
        [Obsolete("Use DownloadAsync(string,DownloadItem) instead!")]
        public async Task DownloadFileAsync(string downloadUrl, string path)
        {
            await DownloadAsync(downloadUrl, new DownloadItem(path));
        }
        
        /// <summary>
        /// This method downloads a file from the KekUploadServer to the location from the given <see cref="DownloadItem"/>.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="downloadItem">The <see cref="DownloadItem"/>.</param>
        /// <param name="token">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="KekException">Thrown when the file could not be successfully downloaded.</exception>
        public void Download(string downloadUrl, DownloadItem downloadItem, CancellationToken token = default)
        {
            downloadUrl = downloadUrl.Replace("/e/", "/d/");
            var client = new HttpClientDownloadWithProgress(downloadUrl, downloadItem);
            if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
            var task = client.StartDownload();
            task.Wait(token);
            if (!task.IsCompletedSuccessfully) throw new KekException("Could not download the file!", task.Exception);
        }
        /// <summary>
        /// This method asynchronously downloads a file from the KekUploadServer to the location from the given <see cref="DownloadItem"/>.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="downloadItem">The <see cref="DownloadItem"/>.</param>
        public async Task DownloadAsync(string downloadUrl, DownloadItem downloadItem)
        {
            downloadUrl = downloadUrl.Replace("/e/", "/d/");
            var client = new HttpClientDownloadWithProgress(downloadUrl, downloadItem);
            if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
            await client.StartDownload();
        }
    }
}