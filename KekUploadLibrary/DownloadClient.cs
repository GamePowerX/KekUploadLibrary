using KekUploadCLIClient;

namespace KekUploadLibrary;

public class DownloadClient
{
    public event HttpClientDownloadWithProgress.ProgressChangedHandler? ProgressChangedEvent;
    public void DownloadFile(string downloadUrl, string path)
    {
        downloadUrl = downloadUrl.Replace("/e/", "/d/");
        var client = new HttpClientDownloadWithProgress(downloadUrl, path);
        if(ProgressChangedEvent != null) client.ProgressChanged += ProgressChangedEvent;
        Task task = client.StartDownload();
        task.Wait();
        if (!task.IsCompletedSuccessfully)
        {
            throw new KekException("Could not download the file!");
        }
    }
}