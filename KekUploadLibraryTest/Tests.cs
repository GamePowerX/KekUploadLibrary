using System.IO;
using System.Text;
using System.Threading;
using KekUploadLibrary;
using NUnit.Framework;

namespace KekUploadLibraryTest;

public class Tests
{
    private string _downloadTestUrl = "";

    [SetUp]
    public void Setup()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", true);
        _downloadTestUrl = client.Upload(new UploadItem(Encoding.UTF8.GetBytes("KekUploadLibraryTest"), "txt", "test"));
    }

    [Test]
    public void UploadTest1()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", true);
        if (!File.Exists("test.txt")) File.WriteAllText("test.txt", "KekUploadLibraryTest");
        var result = client.Upload(new UploadItem("test.txt"));
        Assert.True(result.Contains("https://u.gamepowerx.tk/d/"));
    }

    [Test]
    public void UploadTest2()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", true);
        var result = client.Upload(new UploadItem(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, "bin", "test"));
        Assert.True(result.Contains("https://u.gamepowerx.tk/d/"));
    }

    [Test]
    public void UploadTest3()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", true);
        var result = client.Upload(new UploadItem(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }), "bin",
            "test"));
        Assert.True(result.Contains("https://u.gamepowerx.tk/d/"));
    }

    [Test]
    public void UploadTestWithCancellation()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", true);
        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;
        var data = new byte[1024 * 1024 * 1024];
        var result = "cancelled";
        var thread = new Thread(() =>
        {
            result = client.Upload(new UploadItem(data, "bin", "test"), token);
        });
        thread.Start();
        tokenSource.Cancel();
        thread.Join();
        Assert.True(thread.ThreadState == ThreadState.Stopped);
        Assert.AreEqual("cancelled", result);
    }

    [Test]
    public void UploadTestWithoutName()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", false);
        if (!File.Exists("test.txt")) File.WriteAllText("test.txt", "KekUploadLibraryTest");
        var result = client.Upload(new UploadItem("test.txt"));
        Assert.True(result.Contains("https://u.gamepowerx.tk/d/"));
    }

    [Test]
    public void DownloadTest()
    {
        var client = new DownloadClient();
        client.DownloadFile(_downloadTestUrl, "test2.txt");
        Assert.True(File.ReadAllText("test2.txt", Encoding.UTF8).Contains("KekUploadLibraryTest"));
    }

    [Test]
    public void UploadAndDownloadTest()
    {
        var client = new UploadClient("https://u.gamepowerx.tk", true);
        var testString = "ajuerteiuhsediuozthersodzheioarubz6wirubzaiuzjrepaiojrtzwrakesuhtzlkaser6tzopawres";
        var result = client.Upload(new UploadItem(Encoding.UTF8.GetBytes(testString), "txt", "test"));
        var client2 = new DownloadClient();
        client2.DownloadFile(result, "test3.txt");
        Assert.True(File.ReadAllText("test3.txt", Encoding.UTF8).Contains(testString));
    }

    [Test]
    public void ChunkedUploadStreamTest()
    {
        var stream = new ChunkedUploadStream("txt", "https://u.gamepowerx.tk", "test");
        stream.Write(Encoding.UTF8.GetBytes("KekUploadLibraryTest"));
        stream.Flush();
        stream.Write(Encoding.UTF8.GetBytes("123456789"));
        stream.Flush();
        var url = stream.FinishUpload();
        Assert.True(url.Contains("https://u.gamepowerx.tk/d/"));
    }

    [Test]
    public void ChunkedUploadStreamTestWithDownload()
    {
        var stream = new ChunkedUploadStream("txt", "https://u.gamepowerx.tk", "test1");
        stream.Write(Encoding.UTF8.GetBytes("KekUploadLibraryTest"));
        stream.Flush();
        stream.Write(Encoding.UTF8.GetBytes("123456789"));
        stream.Flush();
        var url = stream.FinishUpload();
        var client = new DownloadClient();
        client.DownloadFile(url, "test1.txt");
        Assert.True(File.ReadAllText("test1.txt", Encoding.UTF8).Contains("KekUploadLibraryTest123456789"));
    }
}