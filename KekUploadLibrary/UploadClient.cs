using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SharpHash.Base;

namespace KekUploadLibrary
{
    public class UploadClient
    {
        public delegate void UploadChunkCompleteEventHandler(object sender, UploadChunkCompleteEventArgs args);

        public delegate void UploadCompleteEventHandler(object sender, UploadCompleteEventArgs args);

        public delegate void UploadErrorEventHandler(object sender, UploadErrorEventArgs args);

        public delegate void UploadStreamCreateEventHandler(object sender, UploadStreamCreateEventArgs args);

        private readonly string _apiBaseUrl;
        private readonly long _chunkSize;
        private bool _withName;

        public UploadClient(string apiBaseUrl, long chunkSize, bool withName)
        {
            _apiBaseUrl = apiBaseUrl;
            _chunkSize = chunkSize;
            _withName = withName;
        }

        public UploadClient(string apiBaseUrl, bool withName)
        {
            _apiBaseUrl = apiBaseUrl;
            _chunkSize = 1024 * 1024 * 2;
            _withName = withName;
        }

        public event UploadStreamCreateEventHandler? UploadStreamCreateEvent;
        public event UploadChunkCompleteEventHandler? UploadChunkCompleteEvent;
        public event UploadCompleteEventHandler? UploadCompleteEvent;
        public event UploadErrorEventHandler? UploadErrorEvent;

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

        public string Upload(UploadItem item)
        {
            return Upload(item, CancellationToken.None);
        }

        public string Upload(UploadItem item, CancellationToken token)
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
            var maxChunkSize = _chunkSize; //1024 * _chunkSize;
            var chunks = (int) Math.Ceiling(fileSize / (double) maxChunkSize);
            var fileHash = HashFactory.Crypto.CreateSHA1();
            fileHash.Initialize();


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
                        (int) Math.Min(stream.Length - (readBytes + chunk * chunkSize), chunkSize));
                var hash = Utils.HashBytes(buf);
                fileHash.TransformBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "u/" + uploadStreamId + "/" + hash)
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
                            uploadRequest = new HttpRequestMessage(HttpMethod.Post, "u/" + uploadStreamId + "/" + hash)
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

        public Task<string> UploadAsync(UploadItem item)
        {
            return UploadAsync(item, CancellationToken.None);
        }

        public async Task<string> UploadAsync(UploadItem item, CancellationToken token)
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
                var hash = Utils.HashBytes(buf);
                fileHash.TransformBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "u/" + uploadStreamId + "/" + hash)
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
                            uploadRequest = new HttpRequestMessage(HttpMethod.Post, "u/" + uploadStreamId + "/" + hash)
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

        private void SendCancellationRequest(string uploadStreamId)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_apiBaseUrl + "/r/" + uploadStreamId),
                Method = HttpMethod.Post
            };
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

        private async Task SendCancellationRequestAsync(string uploadStreamId)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_apiBaseUrl + "/r/" + uploadStreamId),
                Method = HttpMethod.Post
            };
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

        [Obsolete("Use Upload(UploadItem item) instead!")]
        public string UploadFile(string path)
        {
            return Upload(new UploadItem(path));
        }

        [Obsolete("Use Upload(UploadItem item) instead!")]
        public string UploadBytes(byte[] data, string extension)
        {
            return Upload(new UploadItem(data, extension));
        }


        [Obsolete("Use Upload(UploadItem item) instead!")]
        public string UploadStream(Stream stream, string extension)
        {
            return Upload(new UploadItem(stream, extension));
        }
    }
}