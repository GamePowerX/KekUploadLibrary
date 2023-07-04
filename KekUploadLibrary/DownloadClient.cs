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
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="path">The path where the file should be saved.</param>
        /// <exception cref="KekException">Thrown when the file could not be successfully downloaded.</exception>
        public void DownloadFile(string downloadUrl, string path)
        {
            DownloadFile(downloadUrl, path, CancellationToken.None);
        }

        /// <summary>
        /// This method downloads a file from the KekUploadServer with a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="path">The path where the file should be saved.</param>
        /// <param name="token">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="KekException">Thrown when the file could not be successfully downloaded.</exception>
        public void DownloadFile(string downloadUrl, string path, CancellationToken token)
        {
            downloadUrl = downloadUrl.Replace("/e/", "/d/");
            var client = new HttpClientDownloadWithProgress(downloadUrl, path);
            if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
            var task = client.StartDownload();
            task.Wait(token);
            if (!task.IsCompletedSuccessfully) throw new KekException("Could not download the file!", task.Exception);
        }

        /// <summary>
        /// This method asynchronously downloads a file from the KekUploadServer.
        /// </summary>
        /// <param name="downloadUrl">The download url.</param>
        /// <param name="path">The path where the file should be saved.</param>
        public async Task DownloadFileAsync(string downloadUrl, string path)
        {
            downloadUrl = downloadUrl.Replace("/e/", "/d/");
            var client = new HttpClientDownloadWithProgress(downloadUrl, path);
            if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
            await client.StartDownload();
        }
        
        
    }
}