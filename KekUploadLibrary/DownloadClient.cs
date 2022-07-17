using KekUploadCLIClient;

namespace KekUploadLibrary;

public class DownloadClient
{
    public event HttpClientDownloadWithProgress.ProgressChangedHandler? ProgressChangedEvent;

    public void DownloadFile(string downloadUrl, string path)
    {
        DownloadFile(downloadUrl, path, new CancellationToken(false));
    }

    public void DownloadFile(string downloadUrl, string path, CancellationToken token)
    {
        downloadUrl = downloadUrl.Replace("/e/", "/d/");
        var client = new HttpClientDownloadWithProgress(downloadUrl, path, token);
        if (ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
        var task = client.StartDownload();
        task.Wait(token);
        if (!task.IsCompletedSuccessfully) throw new KekException("Could not download the file!", task.Exception);
    }
}