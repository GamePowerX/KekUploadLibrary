KekUploadLibrary [![License](https://shields.io/github/license/CraftingDragon007/KekUploadLibrary)](https://github.com/CraftingDragon007/KekUploadLibrary/blob/master/LICENSE) [![Nuget](https://img.shields.io/nuget/v/CraftingDragon007.KekUploadLibrary)](https://www.nuget.org/packages/CraftingDragon007.KekUploadLibrary) [![Nuget](https://img.shields.io/nuget/dt/CraftingDragon007.KekUploadLibrary)](https://www.nuget.org/packages/CraftingDragon007.KekUploadLibrary)
====

A simple C# Library for [UploadServer](https://github.com/KotwOSS/kekupload-server)

# Installation

You can install this Library via NuGet:

```
PM> Install-Package CraftingDragon007.KekUploadLibrary
```

# Usage

Using the upload client:

```C#
using System;
using System.IO;
using KekUploadLibrary;

class Program
{
    static void Main(string[] args)
	{
	    var client = new UploadClient("<Your UploadServer URL>", <If you want to also upload the filenames>);
	    // Diffrent Events are available (Optional)
	    client.UploadCompleteEvent += (sender, e) => Console.WriteLine("Upload Complete: " + e.FileUrl);
	    client.UploadChunkCompleteEvent += (sender, e) => Console.WriteLine("Upload progress: {0}/{1}", e.CurrentChunkCount, e.TotalChunkCount);
	    client.UploadErrorEvent += (sender, e) => 
	    {
	        if(e.ErrorResponse != null)
	        {
	            Console.WriteLine("Error: " + e.ErrorResponse);
	            Console.WriteLine("Exception: " + e.Exception);
	        }else
	        {
	            Console.WriteLine("Error: " + e.Exception);
	        }
	    };
	    client.UploadStreamCreateEvent += (sender, e) => Console.WriteLine("Upload Stream created: " + e.UploadStreamId);
	    // Upload a file
	    client.Upload(new UploadItem("<Your File Path>"));
	    // Upload a stream
	    using(var stream = new FileStream("<FilePath>", FileMode.Open))
	    {
	        client.Upload(new UploadItem(stream, "<Extension>", "<Filename>")); // the filename is optional and must not contain the extension
	    }
	    // Upload a byte array
	    client.Upload(new UploadItem(File.ReadAllBytes("<FilePath>"), "<Extension>", "<Filename>")); // the filename is optional and must not contain the extension
	    // The upload method returns the download url of the uploaded file
	}
}
```

Using the chunked upload stream:

```C#
using System;
using System.IO;
using KekUploadLibrary;

class Program
{
    static void Main(string[] args)
    {
        var stream = new ChunkedUploadStream("<Extension>", "<Your UploadServer URL>", "<Filename>"); // the filname can be null and must not contain the extension
        // Diffrent Events are available (Optional)
        stream.UploadChunkCompleteEvent += (sender, e) => Console.WriteLine("Upload progress: {0}/{1}", e.CurrentChunkCount, e.TotalChunkCount);
        stream.UploadCompleteEvent += (sender, e) => Console.WriteLine("Upload Complete: " + e.FileUrl);
        stream.UploadErrorEvent += (sender, e) => 
        {
            if(eventArgs.ErrorResponse != null)
            {
                Console.WriteLine("Error: " + e.ErrorResponse);
                Console.WriteLine("Exception: " + e.Exception);
            }else
            {
                Console.WriteLine("Error: " + e.Exception);
            }
        };
        stream.UploadStreamCreateEvent += (sender, e) => Console.WriteLine("Upload Stream created: " + e.UploadStreamId);
        // Now you can write everything you want to the stream
        // The stream will be chunked and uploaded to the upload server
        // It will only be uploaded, when you flush the stream
        stream.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
        stream.Flush();
        // You can do this as often as you want
        stream.Write(new byte[] { 2, 5, 3, 4, 5, 0, 7, 8, 4, 10 }, 0, 10);
        stream.Flush();
        // When you are done, you can finish the upload with a final request
        stream.FinishUpload(); // This returns the download url of the uploaded bytes
        // Then you can dispose the stream
        stream.Dispose();
    }
}
```

Downloading a file:

```C#
using System;
using KekUploadLibrary;

class Program
{
    static void Main(string[] args)
    {
        var downloadClient = new DownloadClient();
        downloadClient.ProgressChangedEvent += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
        {
            Console.WriteLine($"Total file size: {totalFileSize}");
            Console.WriteLine($"Total bytes downloaded: {totalBytesDownloaded}");
            Console.WriteLine($"Progress percentage: {progressPercentage}");
        };
        downloadClient.Download("<Your download url>", new DownloadItem("<Your path>"));
    }
}
```


# License

This Library is licensed under the [GNU General Public License v3.0](https://www.gnu.org/licenses/gpl-3.0.en.html)

# Contributing

If you want to contribute to this Library, please open an issue or pull request on GitHub.

# Contributors

- [CraftingDragon007](https://github.com/CraftingDragon007)
- [KekOnTheWorld](https://github.com/KekOnTheWorld)

# Contact

[Email](mailto:craftingdragon007@outlook.com)
<br>
Discord: CraftingDragon007#9504