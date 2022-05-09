namespace KekUploadLibrary;

public class ChunkedUploadStream : Stream
{
    private MemoryStream _stream;
    private readonly int _chunkSize;
    private int _chunkCount;
    private string _fileHash;
    private string _extension;
    private readonly HttpClient _client;
    private readonly string _apiBaseUrl;
    private readonly string _uploadStreamId;

    public ChunkedUploadStream(string fileHash, int chunks, int chunkSize, string extension, string apiBaseUrl)
    {
        _fileHash = fileHash;
        _chunkCount = chunks;
        _chunkSize = chunkSize;
        _extension = extension;
        _apiBaseUrl = apiBaseUrl;
        CanSeek = false;
        CanRead = false;
        CanWrite = true;
        _stream = new MemoryStream();
        _client = new HttpClient();
        Length = _stream.Length;
        var request = new HttpRequestMessage() {
            RequestUri = new Uri(_apiBaseUrl + "/c/" + extension),
            Method = HttpMethod.Post
        };
        
        HttpResponseMessage? responseMessage = null;
        try
        {
            responseMessage = _client.Send(request);
            responseMessage.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new KekException("Could not create upload-stream!", e, RequestErrorResponse.ParseErrorResponse(responseMessage));
        }

        _uploadStreamId = new StreamReader(responseMessage.Content.ReadAsStream()).ReadToEnd();
    }
    
    public event UploadStreamCreateEventHandler? UploadStreamCreateEvent;
    public event UploadChunkCompleteEventHandler? UploadChunkCompleteEvent;
    public event UploadCompleteEventHandler? UploadCompleteEvent;
    public event UploadErrorEventHandler? UploadErrorEvent;

    public delegate void UploadStreamCreateEventHandler(object sender, UploadStreamCreateEventArgs args);
    public delegate void UploadChunkCompleteEventHandler(object sender, UploadChunkCompleteEventArgs args);
    public delegate void UploadCompleteEventHandler(object sender, UploadCompleteEventArgs args);
    public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs args);

    protected virtual void OnUploadStreamCreateEvent(UploadStreamCreateEventArgs e)
    {
        UploadStreamCreateEvent?.Invoke(this, e);
    }

    protected virtual void OnUploadChunkCompleteEvent(UploadChunkCompleteEventArgs e)
    {
        UploadChunkCompleteEvent?.Invoke(this, e);
    }
    
    protected virtual void OnUploadCompleteEvent(UploadCompleteEventArgs e)
    {
        UploadCompleteEvent?.Invoke(this, e);
    }
    
    protected virtual void OnUploadErrorEvent(UploadErrorEventArgs e)
    {
        UploadErrorEvent?.Invoke(this, e);
    }
        
    public override void Flush()
    {
        var fileSize = _stream.Length;
        var maxChunkSize = _chunkSize;
        var chunks = (int)Math.Ceiling(fileSize/(double)maxChunkSize);

        for(int chunk = 0; chunk < chunks; chunk++) {
            var chunkSize = Math.Min(_stream.Length-chunk*maxChunkSize, maxChunkSize);
            byte[] buf = new byte[chunkSize];

            int readBytes = 0;
            while(readBytes < chunkSize) readBytes += _stream.Read(buf, readBytes, (int)Math.Min(_stream.Length-(readBytes+chunk*chunkSize), chunkSize));
                var hash = Utils.HashBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage {
                    RequestUri = new Uri(_apiBaseUrl + "/u/" + _uploadStreamId + "/" + hash),
                    Method = HttpMethod.Post,
                    Content = new ByteArrayContent(buf)
                };
                HttpResponseMessage? responseMsg = null;
                HttpResponseMessage? responseMessage = null;
                try
                {
                    responseMsg = _client.Send(uploadRequest);
                    responseMsg.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    OnUploadErrorEvent(new UploadErrorEventArgs(e, RequestErrorResponse.ParseErrorResponse(responseMsg)));
                    var success = false;
                    while (!success)
                    {
                        try
                        {
                            responseMessage = _client.Send(uploadRequest);
                            responseMessage.EnsureSuccessStatusCode();
                            success = true;
                        }
                        catch (HttpRequestException ex)
                        {
                            OnUploadErrorEvent(new UploadErrorEventArgs(ex, RequestErrorResponse.ParseErrorResponse(responseMessage)));
                            Thread.Sleep(500);
                        }
                    }
                }
                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk, chunks));
        }
        _stream.Dispose();
        _stream = new MemoryStream();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    { 
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream.Write(buffer, offset, count);
    }
    
    public new void Dispose()
    {
        _client.Dispose();
        _stream.Dispose();
    }

    public new async void DisposeAsync()
    {
        await Task.Run(() => _client.Dispose());
        await _stream.DisposeAsync();
    }

    public string FinishUpload()
    {
        var finishRequest = new HttpRequestMessage {   
            RequestUri = new Uri(_apiBaseUrl + "/f/" + _uploadStreamId + "/" + _fileHash),
            Method = HttpMethod.Post
        };

        HttpResponseMessage? finishResponse = null;
        try
        {
            finishResponse = _client.Send(finishRequest);
            finishResponse.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new KekException("Failed to send finish request!", e, RequestErrorResponse.ParseErrorResponse(finishResponse));
        }
        var downloadId = finishResponse.Content.ReadAsStringAsync().Result;
        var url = _apiBaseUrl + "/d/" + Utils.ParseDownloadId(downloadId);
        if(url == null) throw new KekException("Failed to parse download url!");
        OnUploadCompleteEvent(new UploadCompleteEventArgs(null, url));
        return url;
    }

    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; }
    public override long Length { get; }
    public override long Position { get; set; }
}