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
using System.Linq;
using System.Reflection;

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
        public async Task<string> DistributeDocument(string fileNameAndPath, string cloudDeploymentResourceUrl, string sourceDocumentId, string jwtToken)
        {
            PrintMessage("Deployment Name: " + cloudDeploymentResourceUrl, false);
            string multiCloudMachineName = cloudDeploymentResourceUrl.TrimEnd('/') + "/api/v1/";
            string qvDocName = Path.GetFileNameWithoutExtension(fileNameAndPath);

            try
            {
                var tusClient = new TusClient();
                tusClient.AdditionalHeaders.Add("Authorization", "Bearer " + jwtToken);

                Assembly assembly = Assembly.GetExecutingAssembly();
                string version = assembly.GetName().Version.ToString();

                string url;
                using (var l_FileStream = new FileStream(fileNameAndPath, FileMode.Open, FileAccess.Read))
                {
                    url = await tusClient.CreateAsync(multiCloudMachineName + "temp-contents/files", l_FileStream.Length, this, version);

                    int cloudChunkSize = 300;
                    PrintMessage("Upload file to Cloud - Chunk size set to " + cloudChunkSize, false);
                    await tusClient.UploadAsync(url, l_FileStream, cloudChunkSize, this, version);
                }
                EngineDoc result = await ImportApp(jwtToken, multiCloudMachineName, sourceDocumentId, qvDocName, url, version);

                CollectionEntity engineItem = await CreateEngineItem(result, jwtToken, multiCloudMachineName, qvDocName);
                if (engineItem == null)
                {
                    return string.Empty;
                }
            }
            catch (HttpAppSizeException)
            {
                throw;
            }
            catch (WorkflowException)
            {
                throw;
            }
            catch (TaskCanceledException e)
            {
                PrintMessage("Failure - Could not upload document to engine, Connection timeout. Message =" + e.Message, false);
                throw new AppUploadTimeoutException("Client connection timeout.");
            }
            catch (Exception)
            {
                throw;
            }
            return string.Empty;
        }

        private async Task<EngineDoc> ImportApp(string jwtToken, string multiCloudMachineName, string sourceDocumentId, string fileName, string url, string version)
        {
            string fileId = url.Substring(url.LastIndexOf('/') + 1);
            string additionalUri = "apps/import?fileId=" + fileId + "&mode=autoreplace" + "&appId=" + CalculateDocumentOrTagId(sourceDocumentId, fileName);

            using (HttpResponseMessage response = await SetupAndSendRequest(HttpMethod.Post, multiCloudMachineName + additionalUri, "", jwtToken, "UserAgent", "QDS/" + version))
            {
                try
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.RequestEntityTooLarge:
                            PrintMessage("Failure - Could not upload document to engine, since engine reported that the app exceeds the maximum size. statusCode= " + response.StatusCode + ", reason= " + response.ReasonPhrase, false);
                            throw new HttpAppSizeException("App size exceeds max size");

                        case HttpStatusCode.GatewayTimeout:
                            PrintMessage("Failure - Could not upload document in engine, API gateway in QSEfe/Multicloud reported that it timed out when waiting for a response. statusCode= " + response.StatusCode + ", reason= " + response.ReasonPhrase, false);
                            WorkflowExceptionStrategy.ThrowException(response);
                            break;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        PrintMessage("Failure - Could not upload document to engine. statusCode= " + response.StatusCode + ", reason= " + response.ReasonPhrase, false);
                        WorkflowExceptionStrategy.ThrowException(response);
                    }
                    else
                    {
                        PrintMessage("Success - Upload document " + fileName + " to engine", false);
                    }

                    var responseContent = await response.Content.ReadAsStringAsync() ?? "{}";
                    EngineDoc result = JsonConvert.DeserializeObject<EngineDoc>(responseContent);
                    return result;
                }
                catch (Exception e)
                {
                    PrintMessage("Failed to import app " + fileName + " to engine. Exception: " + e.Message, false);
                    return null;
                }
            }
        }

        private async Task<CollectionEntity> CreateEngineItem(EngineDoc doc, string jwtToken, string multiCloudMachineName, string docName)
        {
            CollectionEntity result = null;
            try
            {
                result = await GetItem(jwtToken, multiCloudMachineName, doc.Attributes.AppId, "qvapp");

                if (result != null && result.Id != null)
                {
                    return result;
                }

                string additionalUri = "items";
                string jsonRequest = SetupJson(doc, docName);
                using (HttpResponseMessage createItemResponse = await SetupAndSendRequest(HttpMethod.Post, multiCloudMachineName + additionalUri, jsonRequest, jwtToken))
                {
                    if (createItemResponse != null && createItemResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        PrintMessage("Failure - Document could not be created. StatusCode= " + createItemResponse.StatusCode + ", reason= " + createItemResponse.ReasonPhrase, false);
                    }
                    var responseContent = await createItemResponse.Content.ReadAsStringAsync() ?? "{}";
                    result = JsonConvert.DeserializeObject<CollectionEntity>(responseContent);
                }
            }
            catch (Exception e)
            {
                PrintMessage("Failure - Could not create item for doc upload. Exception= " + e.Message, false);
            }
            return result;
        }

        private string CalculateDocumentOrTagId(string arg1, string arg2)
        {
            StringBuilder documentId = new StringBuilder();
            using (MD5 md5 = MD5.Create())
            {
                byte[] buffer = md5.ComputeHash(Encoding.UTF8.GetBytes(arg1 + arg2));

                for (int i = 0; i < buffer.Length; i++)
                {
                    documentId.Append(buffer[i].ToString("x2"));
                }
            }
            return documentId.ToString();
        }

        private async Task<CollectionEntity> GetItem(string jwtToken, string multiCloudMachineName, string resourceId, string resourceType)
        {
            string additionalUri = "items?resourceType=" + resourceType + "&resourceId=" + resourceId;
            return await GenericGet(jwtToken, multiCloudMachineName, additionalUri);
        }

        private async Task<CollectionEntity> GenericGet(string jwtToken, string multiCloudMachineName, string additionalUri)
        {
            ElasticEntityListContainer<CollectionEntity> collectionResponse = null;
            try
            {
                using (HttpResponseMessage createTagResponse = await SetupAndSendRequest(HttpMethod.Get, multiCloudMachineName + additionalUri, "", jwtToken))
                {
                    if (createTagResponse.StatusCode == HttpStatusCode.OK)
                    {
                        var responseContent = await createTagResponse.Content.ReadAsStringAsync() ?? "{}";
                        collectionResponse = JsonConvert.DeserializeObject<ElasticEntityListContainer<CollectionEntity>>(responseContent);
                    }
                    else
                    {
                        PrintMessage("Cloud Native: Server returned " + createTagResponse.StatusCode + " when trying to get item", false);
                    }
                }
            }
            catch (Exception e)
            {
                PrintMessage("Could not get items. Exception= " + e.Message, false);
            }
            return collectionResponse == null ? null : collectionResponse.Data.FirstOrDefault();
        }

        private async Task<HttpResponseMessage> SetupAndSendRequest(HttpMethod method, string uri, string jsonRequest, string jwtToken, string additionalHeaderName = "", string additionalHeaderValue = "")
        {
            try
            {
                using (HttpClient cloudClient = new HttpClient())
                {
                    cloudClient.Timeout = TimeSpan.FromMinutes(10);
                    using (HttpRequestMessage request = new HttpRequestMessage(method, uri))
                    {
                        if (!string.IsNullOrEmpty(jsonRequest))
                        {
                            request.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                        }

                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
                        if (!string.IsNullOrEmpty(additionalHeaderValue))
                        {
                            request.Headers.Add(additionalHeaderName, additionalHeaderValue);
                        }
                        HttpResponseMessage createItemResponse = await cloudClient.SendAsync(request);
                        return createItemResponse;
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private JsonSerializerSettings IgnoreNullSetting()
        {
            var ignoreNull = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            return ignoreNull;
        }

        private string SetupJson(EngineDoc doc, string docName)
        {
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
            var json = JsonConvert.SerializeObject(item, IgnoreNullSetting());
            return json;
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

