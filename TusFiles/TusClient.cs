using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace qlik_qv_export
{
    /// <summary>
    /// A class to perform actions against a Tus enabled server.
    /// </summary>
    public class TusClient
    {
        /// <summary>
        /// A mutable dictionary of headers which will be included with all requests.
        /// </summary>
        public Dictionary<string, string> AdditionalHeaders { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Get or set the proxy to use when making requests.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Create a file at the Tus server.
        /// </summary>
        /// <param name="url">URL to the creation endpoint of the Tus server.</param>
        /// <param name="uploadLength">The byte size of the file which will be uploaded.</param>
        /// <param name="metadata">Metadata to be stored alongside the file.</param>
        /// <returns>The URL to the created file.</returns>
        /// <exception cref="Exception">Throws if the response doesn't contain the required information.</exception>
        public async Task<string> CreateAsync(string url, long uploadLength, CommunicationSupport commSup, string version,
            params (string key, string value)[] metadata)
        {
            var requestUri = new Uri(url);
            var client = new TusHttpClient
            {
                Proxy = Proxy
            };
            var request = new TusHttpRequest(url, RequestMethod.Post, AdditionalHeaders);

            request.AddHeader(TusHeaderNames.UploadLength, uploadLength.ToString());
            request.AddHeader(TusHeaderNames.ContentLength, "0");
            request.AddHeader(TusHeaderNames.UserAgent, "QDS/" + version);
            request.AddHeader(TusHeaderNames.UploadMetadata, string.Join(",", metadata
                .Select(md =>
                    $"{md.key.Replace(" ", "").Replace(",", "")} {Convert.ToBase64String(Encoding.UTF8.GetBytes(md.value))}")));

            var response = await client.PerformRequestAsync(request, commSup)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.Created)
                throw new Exception("TUS Client CreateAsync - CreateFileInServer failed. " + response.ResponseString);

            if (!response.Headers.ContainsKey("Location"))
                throw new Exception("TUS Client CreateAsync -Location Header Missing");

            if (!Uri.TryCreate(response.Headers["Location"], UriKind.RelativeOrAbsolute, out var locationUri))
                throw new Exception("TUS Client CreateAsync - Invalid Location Header");

            if (!locationUri.IsAbsoluteUri)
                locationUri = new Uri(requestUri, locationUri);

            return locationUri.ToString();
        }

        /// <summary>
        /// Upload a file to the Tus server.
        /// </summary>
        /// <param name="url">URL to a previously created file.</param>
        /// <param name="fileStream">The file to upload.</param>
        /// <param name="file">The file to upload.</param>

        /// <param name="chunkSize">The size, in megabytes, of each file chunk when uploading.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation with.</param>
        /// <returns>A <see cref="TusOperation{T}"/> which represents the upload operation.</returns>
        public TusOperation<Unit> UploadAsync(
            string url,
            FileStream fileStream,
            int chunkSize,
            CommunicationSupport commSup,
            string version,
            CancellationToken cancellationToken = default) =>
            UploadFileAsync(url, fileStream, chunkSize, commSup, version, cancellationToken);
        /// <summary>
        /// Upload a file to the Tus server.
        /// </summary>
        /// <param name="url">URL to a previously created file.</param>
        /// <param name="fileStream">A file stream of the file to upload. The stream will be closed automatically.</param>
        /// <param name="chunkSize">The size, in megabytes, of each file chunk when uploading.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation with.</param>
        /// <returns>A <see cref="TusOperation{T}"/> which represents the upload operation.</returns>
        public TusOperation<Unit> UploadFileAsync(
            string url,
            Stream fileStream,
            int chunkSize,
            CommunicationSupport commSup,
            string version,
            CancellationToken cancellationToken = default) => new TusOperation<Unit>(
            async reportProgress =>
            {
                try
                {
                    var offset = await GetFileOffset(url)
                        .ConfigureAwait(false);

                    var client = new TusHttpClient();
                    SHA1 sha = new SHA1Managed();

                    var uploadChunkSize = (int)Math.Ceiling(chunkSize * 1024.0 * 1024.0); // to MB

                    if (offset == fileStream.Length)
                        reportProgress(fileStream.Length, fileStream.Length);

                    var buffer = new byte[uploadChunkSize];

                    void OnProgress(long written, long total) =>
                        reportProgress(offset + written, fileStream.Length);

                    while (offset < fileStream.Length)
                    {
                        fileStream.Seek(offset, SeekOrigin.Begin);

                        var bytesRead = await fileStream.ReadAsync(buffer, 0, uploadChunkSize);
                        var segment = new ArraySegment<byte>(buffer, 0, bytesRead);
                        var sha1Hash = sha.ComputeHash(buffer, 0, bytesRead);

                        var request = new TusHttpRequest(url, RequestMethod.Patch, AdditionalHeaders, segment,
                            cancellationToken);
                        request.AddHeader(TusHeaderNames.UploadOffset, offset.ToString());
                        request.AddHeader(TusHeaderNames.UploadChecksum, $"sha1 {Convert.ToBase64String(sha1Hash)}");
                        request.AddHeader(TusHeaderNames.ContentType, "application/offset+octet-stream");
                        request.AddHeader(TusHeaderNames.UserAgent, "QDS/" + version);

                        try
                        {
                            request.UploadProgressed += OnProgress;
                            var response = await client.PerformRequestAsync(request)
                                .ConfigureAwait(false);
                            request.UploadProgressed -= OnProgress;
                            if (response.StatusCode != HttpStatusCode.NoContent)
                            {
                                throw new Exception("WriteFileInServer failed. " + response.ResponseString);
                            }
                            offset = long.Parse(response.Headers[TusHeaderNames.UploadOffset]);
                        }
                        catch (IOException ex)
                        {
                            if (ex.InnerException is SocketException socketException)
                            {
                                if (socketException.SocketErrorCode == SocketError.ConnectionReset)
                                {
                                    // retry by continuing the while loop
                                    // but get new offset from server to prevent Conflict error
                                    offset = await GetFileOffset(url)
                                        .ConfigureAwait(false);
                                }
                                else
                                {
                                    commSup.PrintMessage("Tus Client IO SocketException when uploading document. Exception " + ex.Message, true);
                                    throw;
                                }
                            }
                            else
                            {
                                commSup.PrintMessage("Tus Client IO when uploading document. Exception " + ex.Message, true);
                                throw;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("423"))//Special treatment for 423 Lock response
                            {
                                int retry = 0;
                                while (retry < 3)
                                {
                                    try
                                    {
                                        Thread.Sleep(3000);
                                        offset = await GetFileOffset(url)
                                                .ConfigureAwait(false);
                                        break;
                                    }
                                    catch
                                    {
                                        retry++;
                                    }
                                }
                                commSup.PrintMessage("Tus client got 423 lock from server. Retried " + retry++ + " times.", true);
                            }
                            else
                            {
                                commSup.PrintMessage("Tus client ended up in exception when uploading document. " + ex.Message, true);
                                throw ;
                            }
                        }
                    }

                    return Unit.Default;
                }
                finally
                {
                    fileStream.Dispose();
                }
            });

        private async Task<long> GetFileOffset(string url)
        {
            var client = new TusHttpClient();
            var request = new TusHttpRequest(url, RequestMethod.Head, AdditionalHeaders);

            var response = await client.PerformRequestAsync(request)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NoContent && response.StatusCode != HttpStatusCode.OK)
                throw new Exception("GetFileOffset failed. " + response.ResponseString);

            if (!response.Headers.ContainsKey(TusHeaderNames.UploadOffset))
                throw new Exception("Offset Header Missing");

            return long.Parse(response.Headers[TusHeaderNames.UploadOffset]);
        }
    }
}