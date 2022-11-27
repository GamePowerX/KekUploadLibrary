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
            var client = new HttpClient();
            if (item.Name == null) _withName = false;
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_apiBaseUrl + "/c/" + item.Extension +
                                     (_withName ? "/" + item.Name?.Replace("." + item.Extension, "") : "")),
                Method = HttpMethod.Post
            };
            HttpResponseMessage? responseMessage;
            try
            {
                var task = client.SendAsync(request);
                task.Wait();
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
                var chunkSize = Math.Min(stream.Length - chunk * maxChunkSize, maxChunkSize);
                var buf = new byte[chunkSize];

                var readBytes = 0;
                while (readBytes < chunkSize)
                    readBytes += stream.Read(buf, readBytes,
                        (int) Math.Min(stream.Length - (readBytes + chunk * chunkSize), chunkSize));
                var hash = Utils.HashBytes(buf);
                fileHash.TransformBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage
                {
                    RequestUri = new Uri(_apiBaseUrl + "/u/" + uploadStreamId + "/" + hash),
                    Method = HttpMethod.Post,
                    Content = new ByteArrayContent(buf)
                };
                HttpResponseMessage? responseMsg = null;
                responseMessage = null;
                try
                {
                    var task = client.SendAsync(uploadRequest);
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
                            uploadRequest = new HttpRequestMessage
                            {
                                RequestUri = new Uri(_apiBaseUrl + "/u/" + uploadStreamId + "/" + hash),
                                Method = HttpMethod.Post,
                                Content = new ByteArrayContent(buf)
                            };
                            var task = client.SendAsync(request);
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

            var finalHash = fileHash.TransformFinal().ToString().ToLower();

            var finishRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(_apiBaseUrl + "/f/" + uploadStreamId + "/" + finalHash),
                Method = HttpMethod.Post
            };

            HttpResponseMessage? finishResponse = null;
            try
            {
                var task = client.SendAsync(finishRequest);
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
            OnUploadCompleteEvent(new UploadCompleteEventArgs(item.FilePath, url));
            return url;
        }

        public async Task<string> UploadAsync(UploadItem item)
        {
            var client = new HttpClient();
            if (item.Name == null) _withName = false;
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_apiBaseUrl + "/c/" + item.Extension +
                                     (_withName ? "/" + item.Name?.Replace("." + item.Extension, "") : "")),
                Method = HttpMethod.Post
            };
            HttpResponseMessage? responseMessage;
            try
            {
                responseMessage = await client.SendAsync(request);
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
                var chunkSize = Math.Min(stream.Length - chunk * maxChunkSize, maxChunkSize);
                var buf = new byte[chunkSize];
                var readBytes = 0;
                while (readBytes < chunkSize)
                    readBytes += await stream.ReadAsync(buf, readBytes,
                        (int) Math.Min(stream.Length - (readBytes + chunk * chunkSize), chunkSize));
                var hash = Utils.HashBytes(buf);
                fileHash.TransformBytes(buf);
                // index is the number of bytes in the chunk
                var uploadRequest = new HttpRequestMessage
                {
                    RequestUri = new Uri(_apiBaseUrl + "/u/" + uploadStreamId + "/" + hash),
                    Method = HttpMethod.Post,
                    Content = new ByteArrayContent(buf)
                };
                HttpResponseMessage? responseMsg = null;
                responseMessage = null;
                try
                {
                    responseMsg = await client.SendAsync(uploadRequest);
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
                            uploadRequest = new HttpRequestMessage
                            {
                                RequestUri = new Uri(_apiBaseUrl + "/u/" + uploadStreamId + "/" + hash),
                                Method = HttpMethod.Post,
                                Content = new ByteArrayContent(buf)
                            };
                            responseMessage = await client.SendAsync(request);
                            responseMessage.EnsureSuccessStatusCode();
                            success = true;
                        }
                        catch (HttpRequestException ex)
                        {
                            OnUploadErrorEvent(new UploadErrorEventArgs(ex,
                                RequestErrorResponse.ParseErrorResponse(responseMessage)));
                            await Task.Delay(500);
                        }
                }

                OnUploadChunkCompleteEvent(new UploadChunkCompleteEventArgs(hash, chunk + 1, chunks));
            }

            var finalHash = fileHash.TransformFinal().ToString().ToLower();
            var finishRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(_apiBaseUrl + "/f/" + uploadStreamId + "/" + finalHash),
                Method = HttpMethod.Post
            };
            HttpResponseMessage? finishResponse = null;
            try
            {
                finishResponse = await client.SendAsync(finishRequest);
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