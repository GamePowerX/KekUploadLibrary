using System.Threading;
using System.Threading.Tasks;

namespace KekUploadLibrary
{
    public class DownloadClient
    {
        public event HttpClientDownloadWithProgress.ProgressChangedHandler? ProgressChangedEvent;

        public void DownloadFile(string downloadUrl, string path)
        {
            DownloadFile(downloadUrl, path, CancellationToken.None);
        }

        public void DownloadFile(string downloadUrl, string path, CancellationToken token)
        {
            downloadUrl = downloadUrl.Replace("/e/", "/d/");
            var client = new HttpClientDownloadWithProgress(downloadUrl, path);
            if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
            var task = client.StartDownload();
            task.Wait(token);
            if (!task.IsCompletedSuccessfully) throw new KekException("Could not download the file!", task.Exception);
        }

        public async Task DownloadFileAsync(string downloadUrl, string path)
        {
            downloadUrl = downloadUrl.Replace("/e/", "/d/");
            var client = new HttpClientDownloadWithProgress(downloadUrl, path);
            if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
            await client.StartDownload();
        }
    }
}