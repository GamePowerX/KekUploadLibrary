namespace KekUploadLibrary;

public class UploadClient
{
    private string _apiBaseUrl;
    private int _chunkSize;
    
    public UploadClient(string apiBaseUrl, int chunkSize)
    {
        _apiBaseUrl = apiBaseUrl;
        _chunkSize = chunkSize;
    }

    public UploadClient(string apiBaseUrl)
    {
        _apiBaseUrl = apiBaseUrl;
        _chunkSize = 1024 * 1024 * 2;
    }

    public event UploadStreamCreateEventHandler? UploadStreamCreateEvent;
    public event UploadChunkCompleteEventHandler? UploadChunkCompleteEvent;

    public delegate void UploadStreamCreateEventHandler(object sender, UploadStreamCreateEventArgs args);
    public delegate void UploadChunkCompleteEventHandler(object sender, UploadChunkCompleteEventArgs args);

    protected virtual void OnUploadStreamCreateEvent(UploadStreamCreateEventArgs e)
    {
        if (UploadStreamCreateEvent != null)
        {
            UploadStreamCreateEvent(this, e);
        }
    }

    protected virtual void OnUploadChunkCompleteEvent(UploadChunkCompleteEventArgs e)
    {
        if (UploadChunkCompleteEvent != null)
        {
            UploadChunkCompleteEvent(this, e);
        }
    }

    public string UploadFile(string path)
    {
        var file = Path.GetFullPath(path);
        if(!File.Exists(file))
        {
            throw new KekException("The provided file does not exist!");
        }
        var fileInfo = new FileInfo(file);
        var client = new HttpClient();
        
        var request = new HttpRequestMessage() {
            RequestUri = new Uri(_apiBaseUrl + "/c/" + fileInfo.Extension[1..]),
            Method = HttpMethod.Post
        };
        var responseMessage = client.Send(request);

            if(!responseMessage.IsSuccessStatusCode)
            {
                throw new KekException("Could not create upload-stream!");
            } 

            var uploadStreamId = new StreamReader(responseMessage.Content.ReadAsStream()).ReadToEnd();
            
            OnUploadStreamCreateEvent(new UploadStreamCreateEventArgs(uploadStreamId));

            var stream = File.OpenRead(file);

            var fileSize = fileInfo.Length;
            int maxChunkSize = 1024 * _chunkSize;
            var chunks = (int)Math.Ceiling(fileSize/(double)maxChunkSize);

            for(int chunk = 0; chunk < chunks; chunk++) {
                var chunkSize = Math.Min(stream.Length-chunk*maxChunkSize, maxChunkSize);

                byte[] buf = new byte[chunkSize];

                int readBytes = 0;
                while(readBytes < chunkSize) readBytes += stream.Read(buf, readBytes, (int)Math.Min(stream.Length-(readBytes+chunk*chunkSize), chunkSize));

                var hash = Utils.HashBytes(buf);

                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage {
                    RequestUri = new Uri(_apiBaseUrl + "/u/" + uploadStreamId + "/" + hash),
                    Method = HttpMethod.Post,
                    Content = new ByteArrayContent(buf)
                };

                var responseMsg = client.Send(uploadRequest);
                if (!responseMsg.IsSuccessStatusCode)
                {
                    throw new KekException("Could not upload chunk!");
                }
                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk, chunks));
                //DrawTextProgressBar(chunk + 1, chunks);
            }
            
            var fileHash = Utils.HashFile(file);

            var finishRequest = new HttpRequestMessage {   
                RequestUri = new Uri(_apiBaseUrl + "/f/" + uploadStreamId + "/" + fileHash),
                Method = HttpMethod.Post
            };

            var finishResponse = client.Send(finishRequest);
            if (!finishResponse.IsSuccessStatusCode)
            {
                throw new KekException("Failed to send finish request!");
            }

            var downloadId = finishResponse.Content.ReadAsStringAsync().Result;
            return _apiBaseUrl + "/d/" + downloadId;
    }

    public string UploadBytes(byte[] data, string extension)
    {
        var client = new HttpClient();
        
        var request = new HttpRequestMessage() {
            RequestUri = new Uri(_apiBaseUrl + "/c/" + extension),
            Method = HttpMethod.Post
        };
        var responseMessage = client.Send(request);

            if(!responseMessage.IsSuccessStatusCode)
            {
                throw new KekException("Could not create upload-stream!");
            } 

            var uploadStreamId = new StreamReader(responseMessage.Content.ReadAsStream()).ReadToEnd();
            
            OnUploadStreamCreateEvent(new UploadStreamCreateEventArgs(uploadStreamId));

            var stream = new MemoryStream(data);

            var fileSize = data.Length;
            int maxChunkSize = 1024 * _chunkSize;
            var chunks = (int)Math.Ceiling(fileSize/(double)maxChunkSize);

            for(int chunk = 0; chunk < chunks; chunk++) {
                var chunkSize = Math.Min(stream.Length-chunk*maxChunkSize, maxChunkSize);

                byte[] buf = new byte[chunkSize];

                int readBytes = 0;
                while(readBytes < chunkSize) readBytes += stream.Read(buf, readBytes, (int)Math.Min(stream.Length-(readBytes+chunk*chunkSize), chunkSize));

                var hash = Utils.HashBytes(buf);

                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage {
                    RequestUri = new Uri(_apiBaseUrl + "/u/" + uploadStreamId + "/" + hash),
                    Method = HttpMethod.Post,
                    Content = new ByteArrayContent(buf)
                };

                var responseMsg = client.Send(uploadRequest);
                if (!responseMsg.IsSuccessStatusCode)
                {
                    throw new KekException("Could not upload chunk!");
                }
                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk, chunks));
                //DrawTextProgressBar(chunk + 1, chunks);
            }
            
            var fileHash = Utils.HashBytes(data);

            var finishRequest = new HttpRequestMessage {   
                RequestUri = new Uri(_apiBaseUrl + "/f/" + uploadStreamId + "/" + fileHash),
                Method = HttpMethod.Post
            };

            var finishResponse = client.Send(finishRequest);
            if (!finishResponse.IsSuccessStatusCode)
            {
                throw new KekException("Failed to send finish request!");
            }

            var downloadId = finishResponse.Content.ReadAsStringAsync().Result;
            return _apiBaseUrl + "/d/" + downloadId;
    }
}