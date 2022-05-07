namespace KekUploadLibrary;

public class ChunkedUploadStream : Stream
{
    private MemoryStream _stream;
    private long _position;
    private long _length;
    private long _chunkSize;
    private long _chunkIndex;
    private int _chunkCount;
    private string _fileHash;
    private string _extension;
    private HttpClient _client;
    private string _apiBaseUrl;
    private string _uploadStreamId;

    public ChunkedUploadStream(string fileHash, int chunks, int chunkSize, string extension, string apiBaseUrl)
    {
        _fileHash = fileHash;
        _chunkCount = chunks;
        _chunkSize = chunkSize;
        _extension = extension;
        _apiBaseUrl = apiBaseUrl;
        CanRead = false;
        CanWrite = true;
        _stream = new MemoryStream();
        _client = new HttpClient();
        var request = new HttpRequestMessage() {
            RequestUri = new Uri(_apiBaseUrl + "/c/" + extension),
            Method = HttpMethod.Post
        };
        
        HttpResponseMessage responseMessage;
        try
        {
            responseMessage = _client.Send(request);
            responseMessage.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new KekException("Could not create upload-stream!", e);
        }

        _uploadStreamId = new StreamReader(responseMessage.Content.ReadAsStream()).ReadToEnd();
    }
        
    public override void Flush()
    {
        
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

    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; }
    public override long Length { get; }
    public override long Position { get; set; }
}