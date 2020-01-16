using System;
using System.Text;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using static qlik_qv_export.Item;

namespace qlik_qv_export
{
    internal class Result
    {
        public Result(bool successful, string jsonResponse)
        {
            Successful = successful;
            JsonResponse = jsonResponse;
        }

        public bool Successful { get; }

        public string JsonResponse { get; }
    }
    public class CommunicationSupport
    {
        public static async Task WriteJsonRequest(string jsonRequest, WebRequest request)
        {
            if (jsonRequest != null)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(10000);//TODO Config?
                CancellationToken token = cts.Token;

                try
                {
                    byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);
                    request.ContentLength = requestBytes.Length;
                    request.ContentType = "application/json";

                    if (requestBytes.Length > 0)
                    {
                        using (Stream requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                        {
                            token.ThrowIfCancellationRequested();
                            await requestStream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new Exception("Request timed out." + e);
                    }
                    throw e;
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        public async Task<HttpResponseMessage> DistributeDocument(string fileNameAndPath, string cloudDeploymentResourceUrl, string sourceDocumentId, string jwtToken)
        {
            string multiCloudMachineName = cloudDeploymentResourceUrl.TrimEnd('/') + "/api/v1/";
            string qvDocName = Path.GetFileNameWithoutExtension(fileNameAndPath);
            var l_FileStream = new FileStream(fileNameAndPath, FileMode.Open, FileAccess.Read);
            var stream = new BufferedStream(l_FileStream, 8192);
            var content = new StreamContent(stream, 65536);

            try
            {
                content.Headers.Add("Content-Type", "binary/octet-stream");
                using (HttpClient cloudClient = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, multiCloudMachineName + "apps/import?mode=autoreplace&appId=" + sourceDocumentId + "&fallbackName=" + qvDocName)
                    {
                        Content = content
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                    HttpResponseMessage autoReplaceDocResponse = await cloudClient.SendAsync(request);
                    switch (autoReplaceDocResponse.StatusCode)
                    {
                        case HttpStatusCode.RequestEntityTooLarge:
                            PrintMessage("Failure - Could not upload document to engine, since engine reported that the app exceeds the maximum size. statusCode= " + autoReplaceDocResponse.StatusCode + ", reason= " + autoReplaceDocResponse.ReasonPhrase, false);
                            throw new HttpAppSizeException("App size exceeds max size");

                        case HttpStatusCode.GatewayTimeout:
                            PrintMessage("Failure - Could not upload document in engine, API gateway in QSEfe/Multicloud reported that it timed out when waiting for a response. statusCode= " + autoReplaceDocResponse.StatusCode + ", reason= " + autoReplaceDocResponse.ReasonPhrase, false);
                            WorkflowExceptionStrategy.ThrowException(autoReplaceDocResponse);
                            break;
                    }
                    if (!autoReplaceDocResponse.IsSuccessStatusCode)
                    {
                        PrintMessage("Failure - Could not upload document " + qvDocName + "to engine. statusCode= " + autoReplaceDocResponse.StatusCode + ", reason= " + autoReplaceDocResponse.ReasonPhrase, false);
                        WorkflowExceptionStrategy.ThrowException(autoReplaceDocResponse);
                    }

                    else
                    {
                        Console.WriteLine("Success - Document  " + qvDocName + " uploaded to engine");
                    }

                    var responseContent = await autoReplaceDocResponse.Content.ReadAsStringAsync() ?? "{}";

                    EngineDoc result = JsonConvert.DeserializeObject<EngineDoc>(responseContent);

                    HttpResponseMessage createItemResponse = await CreateItem(result, jwtToken, multiCloudMachineName, qvDocName);

                    return createItemResponse;
                }
            }
            catch (HttpAppSizeException)
            {
                PrintMessage("App " + qvDocName + " exceeds max size", false);

            }
            catch (WorkflowException e)
            {
                PrintMessage("Failure - Could not upload document " + qvDocName + " to engine, workflowException. Message =" + e.Message, false);
            }
            catch (TaskCanceledException e)
            {
                PrintMessage("Failure - Could not upload document " + qvDocName + " to engine, Connection timeout. Message =" + e.Message, false);
            }
            catch (Exception ex)
            {
                PrintMessage("Failure - Could not upload document " + qvDocName + " to engine, Other exception of unknown cause. Message =" + ex.Message, false);
            }
            finally
            {
                content.Dispose();
                stream.Dispose();
                l_FileStream.Dispose();
            }
            return null;
        }

        private string SetupJson(EngineDoc doc, string docName)
        {
            var ignoreNull = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            var item = new Itemobject();
            item.name = docName;
            item.resourceId = doc.Attributes.AppId;
            item.resourceType = doc.Attributes.Resourcetype;
            item.description = doc.Attributes.Description;
            item.resourceCustomAttributes = new Resourcecustomattributes();
            item.resourceCreatedAt = doc.Attributes.CreatedDate;
            item.resourceCreatedBySubject = doc.Attributes.Owner;
            item.resourceAttributes = new Resourceattributes
            {
                id = doc.Attributes.AppId,
                description = doc.Attributes.Description,
                thumbnail = doc.Attributes.Thumbnail,
                lastReloadTime = doc.Attributes.LastReloadTime,
                createdDate = doc.Attributes.CreatedDate,
                modifiedDate = doc.Attributes.ModifiedDate,
                owner = doc.Attributes.Owner,
                ownerId = doc.Attributes.OwnerId,
                dynamicColor = doc.Attributes.DynamicColor,
                published = doc.Attributes.Published,
                publishTime = doc.Attributes.PublishTime,
                hasSectionAccess = doc.Attributes.HasSectionAccess,
                encrypted = doc.Attributes.Encrypted,
                originAppId = doc.Attributes.OriginAppId,
                _resourcetype = doc.Attributes.Resourcetype

            };
            var json = JsonConvert.SerializeObject(item, ignoreNull);
            return json;
        }

        private async Task<HttpResponseMessage> CreateItem(EngineDoc doc, string jwtToken, string multiCloudMachineName, string docName)
        {
            string jsonRequest = SetupJson(doc, docName);

            using (HttpClient cloudClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, multiCloudMachineName + "items")
                {
                    Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                HttpResponseMessage createItemResponse = await cloudClient.SendAsync(request);
                var responseContent = await createItemResponse.Content.ReadAsStringAsync() ?? "{}";
                return createItemResponse;
            }
        }

        public void PrintMessage(string message, bool exit)
        {
            Console.WriteLine(message);
            if (exit)
            {
                Console.WriteLine("Press any key to continue");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }
    }
}

