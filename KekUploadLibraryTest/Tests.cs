using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KekUploadLibrary;
using NUnit.Framework;

namespace KekUploadLibraryTest;

/// <summary>
/// Tests for the KekUploadLibrary
/// </summary>
public class Tests
{
    /// <summary>
    /// The base URL of the KekUploadServer API
    /// </summary>
    private const string ApiBaseUrl = "http://localhost:5254";
    private string _downloadTestUrl = "";

    /// <summary>
    /// Test setup
    /// </summary>
    [SetUp]
    public void Setup()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        _downloadTestUrl = client.Upload(new UploadItem("KekUploadLibraryTest"u8.ToArray(), "txt", "test"));
    }

    /// <summary>
    /// Tests the upload of a file
    /// </summary>
    [Test]
    public void UploadTest1()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        if (!File.Exists("test.txt")) File.WriteAllText("test.txt", "KekUploadLibraryTest");
        var result = client.Upload(new UploadItem("test.txt"));
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests the upload of a byte array
    /// </summary>
    [Test]
    public void UploadTest2()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        var result = client.Upload(new UploadItem([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "bin", "test"));
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests the upload of a stream
    /// </summary>
    [Test]
    public void UploadTest3()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        var result = client.Upload(new UploadItem(new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]), "bin",
            "test"));
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests the upload of a file without chunk hashing
    /// </summary>
    [Test]
    public void UploadTestWithoutChunkHashing()
    {
        var client = new UploadClient(ApiBaseUrl, true, withChunkHashing: false);
        var result = client.Upload(new UploadItem([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "bin", "test"));
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests the upload of a file with cancellation
    /// </summary>
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
        Assert.That(thread.ThreadState == ThreadState.Stopped);
        Assert.Equals("cancelled", result);
    }

    /// <summary>
    /// Tests the upload of a file without name
    /// </summary>
    [Test]
    public void UploadTestWithoutName()
    {
        var client = new UploadClient(ApiBaseUrl, false);
        if (!File.Exists("test.txt")) File.WriteAllText("test.txt", "KekUploadLibraryTest");
        var result = client.Upload(new UploadItem("test.txt"));
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests the download of a file
    /// </summary>
    [Test]
    public void DownloadTest()
    {
        var client = new DownloadClient();
        client.Download(_downloadTestUrl, new DownloadItem("test2.txt"));
        Assert.That(File.ReadAllText("test2.txt", Encoding.UTF8).Contains("KekUploadLibraryTest"));
    }

    /// <summary>
    /// Tests the upload and download of a byte array from a random string
    /// </summary>
    [Test]
    public void UploadAndDownloadTest()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        // use a random string to avoid caching
        var testString = "KekUploadLibraryTest" + " " + nameof(UploadAndDownloadTest) + " " + Guid.NewGuid() + " " + DateTime.Now;
        var result = client.Upload(new UploadItem(Encoding.UTF8.GetBytes(testString), "txt", "test"));
        var client2 = new DownloadClient();
        client2.Download(result, new DownloadItem("test3.txt"));
        Assert.That(File.ReadAllText("test3.txt", Encoding.UTF8).Contains(testString));
    }

    /// <summary>
    /// Tests the upload and download of a byte array from a random string without chunk hashing
    /// </summary>
    [Test]
    public void UploadAndDownloadTestWithoutChunkHashing()
    {
        var client = new UploadClient(ApiBaseUrl, true, withChunkHashing: false);
        // use a random string to avoid caching
        var testString = "KekUploadLibraryTest" + " " + nameof(UploadAndDownloadTest) + " " + Guid.NewGuid() + " " + DateTime.Now;
        var result = client.Upload(new UploadItem(Encoding.UTF8.GetBytes(testString), "txt", "test"));
        var client2 = new DownloadClient();
        client2.Download(result, new DownloadItem("test4.txt"));
        Assert.That(File.ReadAllText("test4.txt", Encoding.UTF8).Contains(testString));
    }

    /// <summary>
    /// Tests the upload and download of a large random byte array
    /// </summary>
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
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
        var downloadClient = new DownloadClient();
        downloadClient.Download(result, new DownloadItem("test.bin"));
        var downloadedData = File.ReadAllBytes("test.bin");
        Assert.Equals(data, downloadedData);
    }

    /// <summary>
    /// Tests uploading a string as byte array with a <see cref="ChunkedUploadStream"/>
    /// </summary>
    [Test]
    public void ChunkedUploadStreamTest()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test");
        stream.Write("KekUploadLibraryTest"u8);
        stream.Flush();
        stream.Write("123456789"u8);
        stream.Flush();
        var url = stream.FinishUpload();
        Assert.That(url.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests uploading a string as byte array with a <see cref="ChunkedUploadStream"/> and downloading it
    /// </summary>
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
        client.Download(url, new DownloadItem("test1.txt"));
        Assert.That(File.ReadAllText("test1.txt", Encoding.UTF8).Contains("KekUploadLibraryTest123456789"));
    }
    
    /// <summary>
    /// Tests uploading a string as byte array with a <see cref="ChunkedUploadStream"/> using async methods
    /// </summary>
    [Test]
    public async Task ChunkedUploadStreamTestAsync()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test");
        await stream.WriteAsync("KekUploadLibraryTest"u8.ToArray());
        await stream.FlushAsync();
        await stream.WriteAsync("123456789"u8.ToArray());
        await stream.FlushAsync();
        var url = await stream.FinishUploadAsync();
        Assert.That(url.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests uploading a string as byte array with a <see cref="ChunkedUploadStream"/> without chunk hashing using async methods
    /// </summary>
    [Test]
    public async Task ChunkedUploadStreamTestWithoutChunkHashing()
    {
        var stream = new ChunkedUploadStream("txt", ApiBaseUrl, "test", withChunkHashing: false);
        await stream.WriteAsync("KekUploadLibraryTest"u8.ToArray());
        await stream.FlushAsync();
        await stream.WriteAsync("123456789"u8.ToArray());
        await stream.FlushAsync();
        var url = await stream.FinishUploadAsync();
        Assert.That(url.Contains(ApiBaseUrl + "/d/"));
    }
    
    /// <summary>
    /// Tests uploading a string as byte array with a chunked upload stream using async methods and downloading it
    /// </summary>
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
        await client.DownloadAsync(url, new DownloadItem("test1.txt"));
        Assert.That((await File.ReadAllTextAsync("test1.txt", Encoding.UTF8)).Contains("KekUploadLibraryTest123456789"));
    }

    /// <summary>
    /// Tests asynchronous upload of a file
    /// </summary>
    [Test]
    public async Task UploadTestAsync()
    {
        var client = new UploadClient(ApiBaseUrl, true);
        if (!File.Exists("test.txt")) await File.WriteAllTextAsync("test.txt", "KekUploadLibraryTest");
        var result = await client.UploadAsync(new UploadItem("test.txt"));
        Assert.That(result.Contains(ApiBaseUrl + "/d/"));
    }

    /// <summary>
    /// Tests asynchronous download of a file
    /// </summary>
    [Test]
    public async Task DownloadTestAsync()
    {
        var client = new DownloadClient();
        await client.DownloadAsync(_downloadTestUrl, new DownloadItem("test2.txt"));
        Assert.That((await File.ReadAllTextAsync("test2.txt", Encoding.UTF8)).Contains("KekUploadLibraryTest"));
    }
    
    /// <summary>
    /// Tests asynchronous download of a file to a stream
    /// </summary>
    [Test]
    public async Task DownloadTestToStreamAsync()
    {
        var client = new DownloadClient();
        await using var stream = new MemoryStream();
        await client.DownloadAsync(_downloadTestUrl, new DownloadItem(stream));
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()).Contains("KekUploadLibraryTest"));
    }
    
    /// <summary>
    /// Tests asynchronous download of a file to a byte array
    /// </summary>
    [Test]
    public async Task DownloadTestToByteArrayAsync()
    {
        var client = new DownloadClient();
        var data = new byte[Encoding.UTF8.GetByteCount("KekUploadLibraryTest")];
        var downloadItem = new DownloadItem(data);
        await client.DownloadAsync(_downloadTestUrl, downloadItem);
        data = downloadItem.Data;
        Assert.That(data, Is.Not.Null);
        Assert.That(Encoding.UTF8.GetString(data!).Contains("KekUploadLibraryTest"));
    }
}