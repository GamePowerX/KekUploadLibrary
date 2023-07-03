using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SharpHash.Base;
using SharpHash.Interfaces;

namespace KekUploadLibrary
{
    public sealed class ChunkedUploadStream : Stream
    {
        public delegate void UploadChunkCompleteEventHandler(object sender, UploadChunkCompleteEventArgs args);

        public delegate void UploadCompleteEventHandler(object sender, UploadCompleteEventArgs args);

        public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs args);

        public delegate void UploadStreamCreateEventHandler(object sender, UploadStreamCreateEventArgs args);

        private readonly string _apiBaseUrl;
        private readonly int _chunkSize;
        private readonly HttpClient _client;
        private readonly string _extension;
        private readonly IHash _hash;
        private readonly string? _name;
        private readonly string _uploadStreamId;
        private MemoryStream _stream;

        public ChunkedUploadStream(int chunkSize, string extension, string apiBaseUrl, string? name)
        {
            _chunkSize = chunkSize;
            _extension = extension;
            _apiBaseUrl = apiBaseUrl;
            CanSeek = false;
            CanRead = false;
            CanWrite = true;
            _stream = new MemoryStream();
            _client = new HttpClient();
            _client.BaseAddress = new Uri(_apiBaseUrl);
            _name = name;
            Length = _stream.Length;
            _hash = HashFactory.Crypto.CreateSHA1();
            _hash.Initialize();
            var request = new HttpRequestMessage(HttpMethod.Post, "c/" + extension + (name == null ? "" : "/" + name));

            HttpResponseMessage? responseMessage = null;
            try
            {
                var task = _client.SendAsync(request);
                task.Wait();
                responseMessage = task.Result;
                responseMessage = responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Could not create upload-stream!", e,
                    RequestErrorResponse.ParseErrorResponse(responseMessage));
            }

            _uploadStreamId = new StreamReader(responseMessage.Content.ReadAsStreamAsync().Result).ReadToEnd();
        }

        public ChunkedUploadStream(string extension, string apiBaseUrl, string? name)
        {
            _chunkSize = 1024 * 1024 * 2;
            _extension = extension;
            _apiBaseUrl = apiBaseUrl;
            CanSeek = false;
            CanRead = false;
            CanWrite = true;
            _stream = new MemoryStream();
            _client = new HttpClient();
            _client.BaseAddress = new Uri(_apiBaseUrl);
            _name = name;
            Length = _stream.Length;
            _hash = HashFactory.Crypto.CreateSHA1();
            _hash.Initialize();
            var request = new HttpRequestMessage(HttpMethod.Post, "c/" + extension + (name == null ? "" : "/" + name));

            HttpResponseMessage? responseMessage = null;
            try
            {
                var task = _client.SendAsync(request);
                task.Wait();
                responseMessage = task.Result;
                responseMessage.EnsureSuccessStatusCode();
                Console.WriteLine("Initialization successful!");
                Console.WriteLine(responseMessage.Content.ReadAsStringAsync().Result);
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Could not create upload-stream!", e,
                    RequestErrorResponse.ParseErrorResponse(responseMessage));
            }

            var uploadStreamId =
                Utils.ParseUploadStreamId(
                    new StreamReader(responseMessage.Content.ReadAsStreamAsync().Result).ReadToEnd());
            _uploadStreamId = uploadStreamId ?? throw new KekException("Could not create upload-stream!");
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }

        public event UploadStreamCreateEventHandler? UploadStreamCreateEvent;
        public event UploadChunkCompleteEventHandler? UploadChunkCompleteEvent;
        public event UploadCompleteEventHandler? UploadCompleteEvent;
        public event UploadErrorEventHandler? UploadErrorEvent;

        private void OnUploadStreamCreateEvent(UploadStreamCreateEventArgs e)
        {
            UploadStreamCreateEvent?.Invoke(this, e);
        }

        private void OnUploadChunkCompleteEvent(UploadChunkCompleteEventArgs e)
        {
            UploadChunkCompleteEvent?.Invoke(this, e);
        }

        private void OnUploadCompleteEvent(UploadCompleteEventArgs e)
        {
            UploadCompleteEvent?.Invoke(this, e);
        }

        private void OnUploadErrorEvent(UploadErrorEventArgs e)
        {
            UploadErrorEvent?.Invoke(this, e);
        }

        public override void Flush()
        {
            var fileSize = _stream.Length;
            var maxChunkSize = _chunkSize;
            var chunks = (int) Math.Ceiling(fileSize / (double) maxChunkSize);

            for (var chunk = 0; chunk < chunks; chunk++)
            {
                var chunkSize = Math.Min(_stream.Length - chunk * maxChunkSize, maxChunkSize);
                var buf = new byte[chunkSize];
                _stream.Position = 0;
                var readBytes = 0;
                while (readBytes < chunkSize)
                    readBytes += _stream.Read(buf, readBytes,
                        (int) Math.Min(_stream.Length - (readBytes + chunk * chunkSize), chunkSize));
                var hash = Utils.HashBytes(buf);
                _hash.TransformBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                    "u/" + _uploadStreamId + "/" + hash)
                {
                    Content = new ByteArrayContent(buf)
                };
                HttpResponseMessage? responseMsg = null;
                HttpResponseMessage? responseMessage = null;
                try
                {
                    var task = _client.SendAsync(uploadRequest);
                    task.Wait();
                    responseMsg = task.Result;
                    responseMsg.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    OnUploadErrorEvent(
                        new UploadErrorEventArgs(e, RequestErrorResponse.ParseErrorResponse(responseMsg)));
                    var success = false;
                    while (!success)
                        try
                        {
                            uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                                "u/" + _uploadStreamId + "/" + hash)
                            {
                                Content = new ByteArrayContent(buf)
                            };
                            var task = _client.SendAsync(uploadRequest);
                            task.Wait();
                            responseMessage = task.Result;
                            responseMessage.EnsureSuccessStatusCode();
                            success = true;
                        }
                        catch (HttpRequestException ex)
                        {
                            OnUploadErrorEvent(new UploadErrorEventArgs(ex,
                                RequestErrorResponse.ParseErrorResponse(responseMessage)));
                            Thread.Sleep(500);
                        }
                }

                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk + 1, chunks));
            }

            _stream.Dispose();
            _stream = new MemoryStream();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            var fileSize = _stream.Length;
            var maxChunkSize = _chunkSize;
            var chunks = (int) Math.Ceiling(fileSize / (double) maxChunkSize);

            for (var chunk = 0; chunk < chunks; chunk++)
            {
                var chunkSize = Math.Min(_stream.Length - chunk * maxChunkSize, maxChunkSize);
                var buf = new byte[chunkSize];
                _stream.Position = 0;
                var readBytes = 0;
                while (readBytes < chunkSize)
                    readBytes += await _stream.ReadAsync(buf, readBytes,
                        (int) Math.Min(_stream.Length - (readBytes + chunk * chunkSize), chunkSize), cancellationToken);
                var hash = Utils.HashBytes(buf);
                _hash.TransformBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                    "u/" + _uploadStreamId + "/" + hash)
                {
                    Content = new ByteArrayContent(buf)
                };
                HttpResponseMessage? responseMsg = null;
                HttpResponseMessage? responseMessage = null;
                try
                {
                    responseMsg = await _client.SendAsync(uploadRequest, cancellationToken);
                    responseMsg.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    OnUploadErrorEvent(
                        new UploadErrorEventArgs(e, RequestErrorResponse.ParseErrorResponse(responseMsg)));
                    var success = false;
                    while (!success)
                        try
                        {
                            uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                                "u/" + _uploadStreamId + "/" + hash)
                            {
                                Content = new ByteArrayContent(buf)
                            };
                            responseMessage = await _client.SendAsync(uploadRequest, cancellationToken);
                            responseMessage.EnsureSuccessStatusCode();
                            success = true;
                        }
                        catch (HttpRequestException ex)
                        {
                            OnUploadErrorEvent(new UploadErrorEventArgs(ex,
                                RequestErrorResponse.ParseErrorResponse(responseMessage)));
                            Thread.Sleep(500);
                        }
                }

                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk + 1, chunks));
            }

            await _stream.DisposeAsync();
            _stream = new MemoryStream();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
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
        
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _stream.Write(buffer);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _stream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            await _stream.WriteAsync(buffer, cancellationToken);
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
            var finalHash = _hash.TransformFinal().ToString().ToLower();
            var finishRequest = new HttpRequestMessage(HttpMethod.Post,
                "f/" + _uploadStreamId + "/" + finalHash);

            HttpResponseMessage? finishResponse = null;
            try
            {
                var task = _client.SendAsync(finishRequest);
                task.Wait();
                finishResponse = task.Result;
                finishResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Failed to send finish request!", e,
                    RequestErrorResponse.ParseErrorResponse(finishResponse));
            }

            var downloadId = finishResponse.Content.ReadAsStringAsync().Result;
            var url = _apiBaseUrl + "/d/" + Utils.ParseDownloadId(downloadId);
            if (url == null) throw new KekException("Failed to parse download url!");
            OnUploadCompleteEvent(new UploadCompleteEventArgs(null, url));
            return url;
        }

        public async Task<string> FinishUploadAsync()
        {
            return await FinishUploadAsync(CancellationToken.None);
        }

        public async Task<string> FinishUploadAsync(CancellationToken cancellationToken)
        {
            var finalHash = _hash.TransformFinal().ToString().ToLower();
            var finishRequest = new HttpRequestMessage(HttpMethod.Post,
                "f/" + _uploadStreamId + "/" + finalHash);

            HttpResponseMessage? finishResponse = null;
            try
            {
                finishResponse = await _client.SendAsync(finishRequest, cancellationToken);
                finishResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Failed to send finish request!", e,
                    RequestErrorResponse.ParseErrorResponse(finishResponse));
            }

            var downloadId = await finishResponse.Content.ReadAsStringAsync();
            var url = _apiBaseUrl + "/d/" + Utils.ParseDownloadId(downloadId);
            if (url == null) throw new KekException("Failed to parse download url!");
            OnUploadCompleteEvent(new UploadCompleteEventArgs(null, url));
            return url;
        }

        public string? GetName()
        {
            return _name;
        }

        public string GetExtension()
        {
            return _extension;
        }
    }
}