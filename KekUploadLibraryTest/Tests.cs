using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KekUploadLibrary;
using NUnit.Framework;

namespace KekUploadLibraryTest;

public class Tests
{
    private const string ApiBaseUrl = "https://newupload.gamepowerx.com";
    private string _downloadTestUrl = "";

    [SetUp]
    public void Setup()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        _downloadTestUrl = client.Upload(new UploadItem(Encoding.UTF8.GetBytes("KekUploadLibraryTest"), "txt", "test"));
    }

    [Test]
    public void UploadTest1()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        if (!File.Exists("test.txt")) File.WriteAllText("test.txt", "KekUploadLibraryTest");
        var result = client.Upload(new UploadItem("test.txt"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public void UploadTest2()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        var result = client.Upload(new UploadItem(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10}, "bin", "test"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public void UploadTest3()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        var result = client.Upload(new UploadItem(new MemoryStream(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10}), "bin",
            "test"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public void UploadTestWithoutChunkHashing()
    {
        var client = new UploadClient(ApiBaseUrl, true, withChunkHashing: false);
        var result = client.Upload(new UploadItem(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10}, "bin", "test"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public void UploadTestWithCancellation()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;
        var data = new byte[1024 * 1024 * 1024];
        var result = "not completed";
        var thread = new Thread(() =>
        {
            try
            {
                result = client.Upload(new UploadItem(data, "bin", "test"), token);
            }
            catch (OperationCanceledException)
            {
                result = "cancelled";
            }
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
        var client = new UploadClient(ApiBaseUrl, false);
        if (!File.Exists("test.txt")) File.WriteAllText("test.txt", "KekUploadLibraryTest");
        var result = client.Upload(new UploadItem("test.txt"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
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
        var client = new UploadClient(ApiBaseUrl, true);
        // use a random string to avoid caching
        var testString = "KekUploadLibraryTest" + " " + nameof(UploadAndDownloadTest) + " " + Guid.NewGuid() + " " + DateTime.Now;
        var result = client.Upload(new UploadItem(Encoding.UTF8.GetBytes(testString), "txt", "test"));
        var client2 = new DownloadClient();
        client2.DownloadFile(result, "test3.txt");
        Assert.True(File.ReadAllText("test3.txt", Encoding.UTF8).Contains(testString));
    }

    [Test]
    public void UploadAndDownloadTestWithoutChunkHashing()
    {
        var client = new UploadClient(ApiBaseUrl, true, withChunkHashing: false);
        // use a random string to avoid caching
        var testString = "KekUploadLibraryTest" + " " + nameof(UploadAndDownloadTest) + " " + Guid.NewGuid() + " " + DateTime.Now;
        var result = client.Upload(new UploadItem(Encoding.UTF8.GetBytes(testString), "txt", "test"));
        var client2 = new DownloadClient();
        client2.DownloadFile(result, "test4.txt");
        Assert.True(File.ReadAllText("test4.txt", Encoding.UTF8).Contains(testString));
    }

    [Test]
    public void UploadAndDownloadTestWithLargeFile()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        var data = new byte[1024 * 1024 * 100]; // 100 MB
        // fill data with random bytes
        new Random().NextBytes(data);
        client.UploadChunkCompleteEvent += (_, args) =>
        {
            Console.WriteLine($"Uploaded {args.CurrentChunkCount} Chunks of {args.TotalChunkCount}");
        };
        var result = client.Upload(new UploadItem(data, "bin", "test"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
        var downloadClient = new DownloadClient();
        downloadClient.DownloadFile(result, "test.bin");
        var downloadedData = File.ReadAllBytes("test.bin");
        Assert.AreEqual(data, downloadedData);
    }

    [Test]
    public void ChunkedUploadStreamTest()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test");
        stream.Write("KekUploadLibraryTest"u8);
        stream.Flush();
        stream.Write("123456789"u8);
        stream.Flush();
        var url = stream.FinishUpload();
        Assert.True(url.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public void ChunkedUploadStreamTestWithDownload()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test1");
        stream.Write("KekUploadLibraryTest"u8);
        stream.Flush();
        stream.Write("123456789"u8);
        stream.Flush();
        var url = stream.FinishUpload();
        var client = new DownloadClient();
        client.DownloadFile(url, "test1.txt");
        Assert.True(File.ReadAllText("test1.txt", Encoding.UTF8).Contains("KekUploadLibraryTest123456789"));
    }
    
    [Test]
    public async Task ChunkedUploadStreamTestAsync()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test");
        await stream.WriteAsync("KekUploadLibraryTest"u8.ToArray());
        await stream.FlushAsync();
        await stream.WriteAsync("123456789"u8.ToArray());
        await stream.FlushAsync();
        var url = await stream.FinishUploadAsync();
        Assert.True(url.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public async Task ChunkedUploadStreamTestWithoutChunkHashing()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test", withChunkHashing: false);
        await stream.WriteAsync("KekUploadLibraryTest"u8.ToArray());
        await stream.FlushAsync();
        await stream.WriteAsync("123456789"u8.ToArray());
        await stream.FlushAsync();
        var url = await stream.FinishUploadAsync();
        Assert.True(url.Contains(ApiBaseUrl + "/d/"));
    }
    
    [Test]
    public async Task ChunkedUploadStreamTestWithDownloadAsync()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test1");
        await stream.WriteAsync("KekUploadLibraryTest"u8.ToArray());
        await stream.FlushAsync();
        await stream.WriteAsync("123456789"u8.ToArray());
        await stream.FlushAsync();
        var url = await stream.FinishUploadAsync();
        var client = new DownloadClient();
        await client.DownloadFileAsync(url, "test1.txt");
        Assert.True((await File.ReadAllTextAsync("test1.txt", Encoding.UTF8)).Contains("KekUploadLibraryTest123456789"));
    }

    [Test]
    public async Task UploadTestAsync()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        if (!File.Exists("test.txt")) await File.WriteAllTextAsync("test.txt", "KekUploadLibraryTest");
        var result = await client.UploadAsync(new UploadItem("test.txt"));
        Assert.True(result.Contains(ApiBaseUrl + "/d/"));
    }

    [Test]
    public async Task DownloadTestAsync()
    {
        var client = new DownloadClient();
        await client.DownloadFileAsync(_downloadTestUrl, "test2.txt");
        Assert.True((await File.ReadAllTextAsync("test2.txt", Encoding.UTF8)).Contains("KekUploadLibraryTest"));
    }
}