using System;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpHash.Base;

namespace KekUploadLibrary
{
    /// <summary>
    /// The main class for uploading data to the KekUploadServer.
    /// </summary>
    public class UploadClient
    {
        /// <summary>
        /// The event handler for the <see cref="UploadChunkCompleteEvent"/>.
        /// </summary>
        public delegate void UploadChunkCompleteEventHandler(object sender, UploadChunkCompleteEventArgs args);

        /// <summary>
        /// The event handler for the <see cref="UploadCompleteEvent"/>.
        /// </summary>
        public delegate void UploadCompleteEventHandler(object sender, UploadCompleteEventArgs args);

        /// <summary>
        /// The event handler for the <see cref="UploadErrorEvent"/>.
        /// </summary>
        public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs args);

        /// <summary>
        /// The event handler for the <see cref="UploadStreamCreateEvent"/>.
        /// </summary>
        public delegate void UploadStreamCreateEventHandler(object sender, UploadStreamCreateEventArgs args);

        /// <summary>
        /// The base url of the KekUploadServer API.
        /// </summary>
        private readonly string _apiBaseUrl;
        /// <summary>
        /// The size of a chunk in bytes.
        /// </summary>
        private readonly long _chunkSize;
        /// <summary>
        /// Whether the uploaded file should have a name.
        /// </summary>
        private bool _withName;
        /// <summary>
        /// Whether or not to check whether a chunk was correctly uploaded. This is done by hashing the chunk and comparing it to the hash returned by the server.
        /// </summary>
        private readonly bool _withChunkHashing;
        
        /// <summary>
        /// Creates a new instance of the <see cref="UploadClient"/> class.
        /// </summary>
        /// <param name="apiBaseUrl">The base url of the KekUploadServer API.</param>
        /// <param name="withName">Whether the uploaded file should have a name.</param>
        /// <param name="chunkSize">The size of the chunks in bytes.</param>
        /// <param name="withChunkHashing">Whether or not to check whether a chunk was correctly uploaded. This is done by hashing the chunk and comparing it to the hash returned by the server.</param>
        public UploadClient(string apiBaseUrl, bool withName, long chunkSize = 1024 * 1024 * 2, bool withChunkHashing = true)
        {
            _apiBaseUrl = apiBaseUrl;
            _chunkSize = chunkSize;
            _withName = withName;
            _withChunkHashing = withChunkHashing;
        }

        /// <summary>
        /// The event that is fired when the upload stream was successfully created.
        /// </summary>
        public event UploadStreamCreateEventHandler? UploadStreamCreateEvent;
        /// <summary>
        /// The event that is fired when a chunk was successfully uploaded.
        /// </summary>
        public event UploadChunkCompleteEventHandler? UploadChunkCompleteEvent;
        /// <summary>
        /// The event that is fired when the upload was successfully completed.
        /// </summary>
        public event UploadCompleteEventHandler? UploadCompleteEvent;
        /// <summary>
        /// The event that is fired when an error occurs while uploading.
        /// </summary>
        public event UploadErrorEventHandler? UploadErrorEvent;

        /// <summary>
        /// This is the event method for the <see cref="UploadStreamCreateEvent"/>.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnUploadStreamCreateEvent(UploadStreamCreateEventArgs e)
        {
            UploadStreamCreateEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This is the event method for the <see cref="UploadChunkCompleteEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnUploadChunkCompleteEvent(UploadChunkCompleteEventArgs e)
        {
            UploadChunkCompleteEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This is the event method for the <see cref="UploadCompleteEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnUploadCompleteEvent(UploadCompleteEventArgs e)
        {
            UploadCompleteEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This is the event method for the <see cref="UploadErrorEvent"/>.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnUploadErrorEvent(UploadErrorEventArgs e)
        {
            UploadErrorEvent?.Invoke(this, e);
        }

        /// <summary>
        /// This method uploads a <see cref="UploadItem"/> to the KekUploadServer.
        /// </summary>
        /// <param name="item">The <see cref="UploadItem"/> to upload.</param>
        /// <param name="token">The <see cref="CancellationToken"/> to cancel the upload.</param>
        /// <param name="useWebSocketUploader">Whether to use a websocket for uploading the file or not</param>
        /// <returns>The download url of the uploaded file.</returns>
        /// <exception cref="KekException">Thrown when the upload fails.</exception>
        public string Upload(UploadItem item, CancellationToken token = default, bool useWebSocketUploader = true)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(_apiBaseUrl);
            if (item.Name == null) _withName = false;
            var request = new HttpRequestMessage(HttpMethod.Post, "/c/" + item.Extension +
                                                                  (_withName
                                                                      ? "/" + item.Name?.Replace("." + item.Extension,
                                                                          "")
                                                                      : ""));
            HttpResponseMessage? responseMessage;
            try
            {
                var task = client.SendAsync(request, token);
                task.Wait(token);
                responseMessage = task.Result;
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Could not create upload-stream!", e);
            }

            var uploadStreamId =
                Utils.ParseUploadStreamId(
                    new StreamReader(responseMessage.Content.ReadAsStreamAsync().Result).ReadToEnd());
            if (uploadStreamId == null)
                throw new KekException("Could not create upload-stream!");

            OnUploadStreamCreateEvent(new UploadStreamCreateEventArgs(uploadStreamId));

            var stream = item.GetAsStream();

            var fileSize = stream.Length;
            var maxChunkSize = _chunkSize;
            var chunks = (int) Math.Ceiling(fileSize / (double) maxChunkSize);
            var fileHash = HashFactory.Crypto.CreateSHA1();
            fileHash.Initialize();

            if (useWebSocketUploader)
            {
                var webSocketUrl = ConvertToWebSocketUrl(_apiBaseUrl + "/ws");

                using var ws = new ClientWebSocket();

                ws.ConnectAsync(webSocketUrl, token).Wait(token);
                var connectionMessage = Encoding.UTF8.GetBytes("[KekUploadClient] Connected");
                ws.SendAsync(connectionMessage, WebSocketMessageType.Text, true, token).Wait(token);
                var buffer = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var result = ws.ReceiveAsync(buffer, token).Result;

                    if (result.MessageType != WebSocketMessageType.Text) continue;
                    var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine("Received message: " + receivedMessage);
                    if (receivedMessage.Equals("[KekUploadServer] Waiting for UploadStreamId"))
                    {
                        var streamIdMessage = Encoding.UTF8.GetBytes("[KekUploadClient] UploadStreamId: " + uploadStreamId);
                        ws.SendAsync(streamIdMessage, WebSocketMessageType.Text, true, token).Wait(token);
                    }
                    else if(receivedMessage.Equals("[KekUploadServer] Valid UploadStreamId specified. Ready for upload!"))
                    {
                        for (var chunk = 0; chunk < chunks; chunk++)
                        {
                            if (token.IsCancellationRequested)
                            {
                                SendCancellationRequest(uploadStreamId);
                                return "cancelled";
                            }
                            var chunkSize = Math.Min(stream.Length - chunk * maxChunkSize, maxChunkSize);
                            var buf = new byte[chunkSize];

                            var readBytes = 0;
                            while (readBytes < chunkSize)
                                readBytes += stream.Read(buf, readBytes,
                                    (int)Math.Min(stream.Length - (readBytes + chunk * chunkSize), chunkSize));

                            fileHash.TransformBytes(buf);
                            var hash = _withChunkHashing ? Utils.HashBytes(buf) : null;

                            ws.SendAsync(buf, WebSocketMessageType.Binary, true, token).Wait(token);
                            
                            OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk + 1, chunks));
                        }

                        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "[KekUploadClient] Upload Done", token).Wait(token);
                        break;
                    }
                }
            }
            else
            {


                for (var chunk = 0; chunk < chunks; chunk++)
                {
                    if (token.IsCancellationRequested)
                    {
                        SendCancellationRequest(uploadStreamId);
                        return "cancelled";
                    }

                    var chunkSize = Math.Min(stream.Length - chunk * maxChunkSize, maxChunkSize);
                    var buf = new byte[chunkSize];

                    var readBytes = 0;
                    while (readBytes < chunkSize)
                        readBytes += stream.Read(buf, readBytes,
                            (int)Math.Min(stream.Length - (readBytes + chunk * chunkSize), chunkSize));

                    fileHash.TransformBytes(buf);
                    var hash = _withChunkHashing ? Utils.HashBytes(buf) : null;
                    // index is the number of bytes in the chunk
                    var uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                        "u/" + uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
                    {
                        Content = new ByteArrayContent(buf)
                    };
                    HttpResponseMessage? responseMsg = null;
                    responseMessage = null;
                    try
                    {
                        var task = client.SendAsync(uploadRequest, token);
                        task.Wait(token);
                        responseMsg = task.Result;
                        responseMsg.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException e)
                    {
                        OnUploadErrorEvent(
                            new UploadErrorEventArgs(e, RequestErrorResponse.ParseErrorResponse(responseMsg)));
                        var success = false;
                        while (!success)
                        {
                            if (token.IsCancellationRequested)
                            {
                                SendCancellationRequest(uploadStreamId);
                                return "cancelled";
                            }

                            try
                            {
                                uploadRequest = new HttpRequestMessage(HttpMethod.Post,
                                    "u/" + uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
                                {
                                    Content = new ByteArrayContent(buf)
                                };
                                var task = client.SendAsync(uploadRequest, token);
                                task.Wait(token);
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
                    }

                    OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk + 1, chunks));
                }
            }

            var finalHash = fileHash.TransformFinal().ToString().ToLower();

            var finishRequest = new HttpRequestMessage(HttpMethod.Post, "f/" + uploadStreamId + "/" + finalHash);

            HttpResponseMessage? finishResponse = null;
            try
            {
                var task = client.SendAsync(finishRequest, token);
                task.Wait(token);
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
            OnUploadCompleteEvent(new UploadCompleteEventArgs(item.FilePath, url));
            return url;
        }
        
        private static Uri ConvertToWebSocketUrl(string httpUrl)
        {
            var uri = new Uri(httpUrl);

            var webSocketScheme = uri.Scheme == "https" ? "wss" : "ws";

            var webSocketUriBuilder = new UriBuilder(uri)
            {
                Scheme = webSocketScheme
            };

            return webSocketUriBuilder.Uri;
        }

        /// <summary>
        /// This method uploads a <see cref="UploadItem"/> asynchronously to the KekUploadServer.
        /// </summary>
        /// <param name="item">The <see cref="UploadItem"/> to upload.</param>
        /// <param name="token">The <see cref="CancellationToken"/> to cancel the upload.</param>
        /// <param name="useWebSocketUploader">Whether to use a websocket for uploading the file or not</param>
        /// <returns>The download url of the uploaded file.</returns>
        /// <exception cref="KekException">Thrown when the upload fails.</exception>
        public async Task<string> UploadAsync(UploadItem item, CancellationToken token = default, bool useWebSocketUploader = true)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(_apiBaseUrl);
            if (item.Name == null) _withName = false;
            var request = new HttpRequestMessage(HttpMethod.Post, "/c/" + item.Extension +
                                                                  (_withName
                                                                      ? "/" + item.Name?.Replace("." + item.Extension,
                                                                          "")
                                                                      : ""));
            HttpResponseMessage? responseMessage;
            try
            {
                responseMessage = await client.SendAsync(request, token);
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Could not create upload-stream!", e);
            }

            var uploadStreamId =
                Utils.ParseUploadStreamId(
                    await new StreamReader(responseMessage.Content.ReadAsStreamAsync().Result).ReadToEndAsync());
            if (uploadStreamId == null)
                throw new KekException("Could not create upload-stream!");

            OnUploadStreamCreateEvent(new UploadStreamCreateEventArgs(uploadStreamId));

            var stream = item.GetAsStream();

            var fileSize = stream.Length;
            var maxChunkSize = _chunkSize; //1024 * _chunkSize;
            var chunks = (int) Math.Ceiling(fileSize / (double) maxChunkSize);
            var fileHash = HashFactory.Crypto.CreateSHA1();
            fileHash.Initialize();

            for (var chunk = 0; chunk < chunks; chunk++)
            {
                if (token.IsCancellationRequested)
                {
                    await SendCancellationRequestAsync(uploadStreamId);
                    return "cancelled";
                }

                var chunkSize = Math.Min(stream.Length - chunk * maxChunkSize, maxChunkSize);
                var buf = new byte[chunkSize];

                var readBytes = 0;
                while (readBytes < chunkSize)
                    readBytes += await stream.ReadAsync(
                        buf.AsMemory(readBytes,
                            (int) Math.Min(stream.Length - (readBytes + chunk * chunkSize), chunkSize)), token);
                fileHash.TransformBytes(buf);
                var hash = _withChunkHashing ? Utils.HashBytes(buf) : null;
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "u/" + uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
                {
                    Content = new ByteArrayContent(buf)
                };
                HttpResponseMessage? responseMsg = null;
                responseMessage = null;
                try
                {
                    responseMsg = await client.SendAsync(uploadRequest, token);
                    responseMsg.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException e)
                {
                    OnUploadErrorEvent(
                        new UploadErrorEventArgs(e, RequestErrorResponse.ParseErrorResponse(responseMsg)));
                    var success = false;
                    while (!success)
                    {
                        if (token.IsCancellationRequested)
                        {
                            await SendCancellationRequestAsync(uploadStreamId);
                            return "cancelled";
                        }

                        try
                        {
                            uploadRequest = new HttpRequestMessage(HttpMethod.Post, "u/" + uploadStreamId + (_withChunkHashing ? "/" + hash : ""))
                            {
                                Content = new ByteArrayContent(buf)
                            };
                            responseMessage = await client.SendAsync(uploadRequest, token);
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
                }

                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk + 1, chunks));
            }

            var finalHash = fileHash.TransformFinal().ToString().ToLower();
            var finishRequest = new HttpRequestMessage(HttpMethod.Post, "f/" + uploadStreamId + "/" + finalHash);
            HttpResponseMessage? finishResponse = null;
            try
            {
                finishResponse = await client.SendAsync(finishRequest, token);
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
            OnUploadCompleteEvent(new UploadCompleteEventArgs(item.FilePath, url));
            return url;
        }

        /// <summary>
        /// This method cancels an upload.
        /// It sends a cancellation request to the KekUploadServer.
        /// It is used to cancel an upload when the <see cref="CancellationToken"/> in <see cref="Upload(UploadItem,CancellationToken,bool)"/> is cancelled.
        /// </summary>
        /// <param name="uploadStreamId">The upload stream id of the upload to cancel.</param>
        /// <exception cref="KekException">Thrown when the cancellation request fails.</exception>
        private static void SendCancellationRequest(string uploadStreamId)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "r/" + uploadStreamId);
            try
            {
                var responseMessage = client.SendAsync(request).Result;
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Could not send cancellation request!", e);
            }
        }

        /// <summary>
        /// This method cancels an upload asynchronously.
        /// It sends a cancellation request asynchronously to the KekUploadServer.
        /// It is used to cancel an upload when the <see cref="CancellationToken"/> in <see cref="UploadAsync(UploadItem,CancellationToken)"/> is cancelled.
        /// </summary>
        /// <param name="uploadStreamId">The upload stream id of the upload to cancel.</param>
        /// <exception cref="KekException">Thrown when the cancellation request fails.</exception>
        private static async Task SendCancellationRequestAsync(string uploadStreamId)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "r/" + uploadStreamId);
            try
            {
                var responseMessage = await client.SendAsync(request);
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                throw new KekException("Could not send cancellation request!", e);
            }
        }

        /// <summary>
        /// This method uploads a file to the KekUploadServer.
        /// It is obsolete, use <see cref="Upload(UploadItem,CancellationToken,bool)"/> instead!
        /// </summary>
        /// <param name="path">The path to the file to upload.</param>
        /// <returns>The download url of the uploaded file.</returns>
        [Obsolete("Use Upload(UploadItem item) instead!")]
        public string UploadFile(string path)
        {
            return Upload(new UploadItem(path));
        }

        /// <summary>
        /// This method uploads a byte array to the KekUploadServer.
        /// It is obsolete, use <see cref="Upload(UploadItem,CancellationToken,bool)"/> instead!
        /// </summary>
        /// <param name="data">The byte array to upload.</param>
        /// <param name="extension">The extension of the file to upload.</param>
        /// <returns>The download url of the uploaded file.</returns>
        [Obsolete("Use Upload(UploadItem item) instead!")]
        public string UploadBytes(byte[] data, string extension)
        {
            return Upload(new UploadItem(data, extension));
        }

        /// <summary>
        /// This method uploads a stream to the KekUploadServer.
        /// It is obsolete, use <see cref="Upload(UploadItem,CancellationToken,bool)"/> instead!
        /// </summary>
        /// <param name="stream">The stream to upload.</param>
        /// <param name="extension">The extension of the file to upload.</param>
        /// <returns>The download url of the uploaded file.</returns>
        [Obsolete("Use Upload(UploadItem item) instead!")]
        public string UploadStream(Stream stream, string extension)
        {
            return Upload(new UploadItem(stream, extension));
        }
    }
}