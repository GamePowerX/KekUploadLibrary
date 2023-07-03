using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SharpHash.Base;
using SharpHash.Interfaces;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class represents a stream that can be used to upload files to the KekUploadServer.
    /// </summary>
    public sealed class ChunkedUploadStream : Stream
    {
        /// <summary>
        /// This is the event handler for the <see cref="UploadChunkCompleteEvent"/>.
        /// </summary>
        public delegate void UploadChunkCompleteEventHandler(object sender, UploadChunkCompleteEventArgs args);

        /// <summary>
        /// This is the event handler for the <see cref="UploadCompleteEvent"/>.
        /// </summary>
        public delegate void UploadCompleteEventHandler(object sender, UploadCompleteEventArgs args);

        /// <summary>
        /// This is the event handler for the <see cref="UploadErrorEvent"/>.
        /// </summary>
        public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs args);

        /// <summary>
        /// This is the event handler for the <see cref="UploadStreamCreateEvent"/>.
        /// </summary>
        public delegate void UploadStreamCreateEventHandler(object sender, UploadStreamCreateEventArgs args);

        private readonly string _apiBaseUrl;
        private readonly int _chunkSize;
        private readonly HttpClient _client;
        private readonly string _extension;
        private readonly IHash _hash;
        private readonly string? _name;
        private readonly string _uploadStreamId;
        private readonly bool _withChunkHashing;
        private MemoryStream _stream;

        /// <summary>
        /// This is the constructor for the <see cref="ChunkedUploadStream"/> class.
        /// </summary>
        /// <param name="extension">The extension of the file to be uploaded.</param>
        /// <param name="apiBaseUrl">The base url of the api. (without trailing slash)</param>
        /// <param name="name">The name of the file to be uploaded.</param>
        /// <param name="chunkSize">The size of the chunks to be uploaded.</param>
        /// <param name="withChunkHashing">Whether or not to check whether a chunk was correctly uploaded. This is done by hashing the chunk and comparing it to the hash returned by the server.</param>
        /// <exception cref="KekException">Thrown when an error occurs while creating the upload stream.</exception>
        public ChunkedUploadStream(string extension, string apiBaseUrl, string? name = null, int chunkSize = 1024 * 1024 * 2, bool withChunkHashing = true)
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
            _withChunkHashing = withChunkHashing;
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

            var streamId = Utils.ParseUploadStreamId(new StreamReader(responseMessage.Content.ReadAsStreamAsync().Result).ReadToEnd());
            _uploadStreamId = streamId ?? throw new KekException("Could not create upload-stream!", new NullReferenceException("StreamId is null!"),
                RequestErrorResponse.ParseErrorResponse(responseMessage));
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }

        /// <summary>
        /// This event is fired when the upload stream was successfully created.
        /// </summary>
        public event UploadStreamCreateEventHandler? UploadStreamCreateEvent;
        
        /// <summary>
        /// This event is fired when a chunk was successfully uploaded.
        /// </summary>
        public event UploadChunkCompleteEventHandler? UploadChunkCompleteEvent;
        
        /// <summary>
        /// This event is fired when the upload was successfully completed.
        /// </summary>
        public event UploadCompleteEventHandler? UploadCompleteEvent;
        
        /// <summary>
        /// This event is fired when an error occurs while uploading.
        /// </summary>
        public event UploadErrorEventHandler? UploadErrorEvent;

        /// <summary>
        /// This is the event method for the <see cref="UploadStreamCreateEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnUploadStreamCreateEvent(UploadStreamCreateEventArgs e)
        {
            UploadStreamCreateEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This is the event method for the <see cref="UploadChunkCompleteEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnUploadChunkCompleteEvent(UploadChunkCompleteEventArgs e)
        {
            UploadChunkCompleteEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This is the event method for the <see cref="UploadCompleteEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnUploadCompleteEvent(UploadCompleteEventArgs e)
        {
            UploadCompleteEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This is the event method for the <see cref="UploadErrorEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnUploadErrorEvent(UploadErrorEventArgs e)
        {
            UploadErrorEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This method "flushes" the stream. This means that the current content of the stream is uploaded to the server.
        /// After this method is called, the stream is empty for the next content to be written.
        /// </summary>
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
                _hash.TransformBytes(buf);
                var hash = _withChunkHashing ? Utils.HashBytes(buf) : null;
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                    "u/" + _uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
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
                                "u/" + _uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
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

        /// <summary>
        /// This method "flushes" the stream asynchronously. This means that the current content of the stream is uploaded to the server.
        /// After this method is called, the stream is empty for the next content to be written.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
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
                _hash.TransformBytes(buf);
                var hash = _withChunkHashing ? Utils.HashBytes(buf) : null;
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                    "u/" + _uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
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
                                "u/" + _uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
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

        /// <summary>
        /// This method finishes the upload and returns the download URL.
        /// </summary>
        /// <returns>The download URL.</returns>
        /// <exception cref="KekException">If the finish request fails. Example: the hash is invalid.</exception>
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

        /// <summary>
        /// This method finishes the upload asynchronously without a cancellation token and returns the download URL.
        /// </summary>
        /// <returns>The download URL.</returns>
        /// <exception cref="KekException">If the finish request fails. Example: the hash is invalid.</exception>
        public async Task<string> FinishUploadAsync()
        {
            return await FinishUploadAsync(CancellationToken.None);
        }

        /// <summary>
        /// This method finishes the upload asynchronously and returns the download URL.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The download URL.</returns>
        /// <exception cref="KekException">If the finish request fails. Example: the hash is invalid.</exception>
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

        /// <summary>
        /// This method is used to get the name of the file.
        /// </summary>
        /// <returns>The name of the file or null if the name is not set.</returns>
        public string? GetName()
        {
            return _name;
        }

        /// <summary>
        /// This method is used to get the extension of the file.
        /// </summary>
        /// <returns>The extension of the file</returns>
        public string GetExtension()
        {
            return _extension;
        }
    }
}