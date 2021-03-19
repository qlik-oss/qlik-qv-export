using qlik_qv_export.QMSAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Exception = System.Exception;

namespace qlik_qv_export
{
    class Program
    {
        private static Uri qms = new Uri("http://localhost:4799/QMS/Service");
        private static string cloudDeploymentUrl = string.Empty;
        private static string mountOrRootFolder = string.Empty;
        private static IQMS client;
        private static Guid qvsId;
        private static string qvsCluster = string.Empty;
        private static string jwtToken = string.Empty;
        private static string proxyname = string.Empty;
        private static string proxyport = string.Empty;
        private static string uploadpath = string.Empty;
        private static string handledDirectory = string.Empty;
        private static string mode = string.Empty;
        private static string appId = string.Empty;
        private static string space = string.Empty;
        private static string protocol = string.Empty;
        private static string qvwsMachine = string.Empty;
        private static string category = string.Empty;
        private static string csvFileName = string.Empty;
        private static List<DocumentNode> userDocs;
        private static CommunicationSupport commSupport;
        private static QVSSettings qvsSettings;
        private static List<DocumentFolder> userDocFolders;

        static void Main(string[] args)
        {
            string parameter = string.Empty;
            string parameterValue = string.Empty;
            string appDataPath = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(appDataPath, "qlik_qv_export_log.txt");
            List<string> parameterList = new List<string>();

            if (args.Length == 1 && args[0].Equals("-help"))
            {
                DisplayDocUploaderHelp();
            }

            try
            {
                foreach (var arg in args)
                {
                    parameter = arg.Substring(0, arg.IndexOf('='));
                    parameterValue = arg.Substring(arg.LastIndexOf('=') + 1);
                    parameterList.Add(parameter.ToLower() + "=" + parameterValue);
                    switch (parameter.ToLower())
                    {
                        case "-mode":
                            mode = parameterValue;
                            break;
                        case "-cloudurl":
                            cloudDeploymentUrl = parameterValue;
                            break;
                        case "-uploadpath":
                            uploadpath = parameterValue;
                            break;
                        case "-documentfolder":
                            mountOrRootFolder = parameterValue;
                            break;
                        case "-qvscluster":
                            qvsCluster = parameterValue;
                            break;
                        case "-handleddirectory":
                            handledDirectory = parameterValue;
                            break;
                        case "-api_key":
                            jwtToken = parameterValue;
                            break;
                        case "-proxyname":
                            proxyname = parameterValue;
                            break;
                        case "-proxyport":
                            proxyport = parameterValue;
                            break;
                        case "-appid":
                            appId = parameterValue;
                            break;
                        case "-space":
                            space = parameterValue;
                            break;
                        case "-protocol":
                            protocol = parameterValue;
                            break;
                        case "-qvwsmachine":
                            qvwsMachine = parameterValue;
                            break;
                        case "-category":
                            category = parameterValue;
                            break;
                        case "-filename":
                            csvFileName = parameterValue;
                            break;
                        default:
                            break;
                    }
                }
                commSupport = new CommunicationSupport(proxyname, proxyport, logPath);
            }
            catch (Exception e)
            {
                File.AppendAllText(logPath, DateTime.Now.ToString() + "\t Exception when reading parameters. Parameter: " + parameter + " Parameter value: " + parameterValue + "Exception " + e.Message + " Run help command for information about usage" + Environment.NewLine);
                Console.WriteLine("Exception when reading parameters. Parameter: " + parameter + " Parameter value: " + parameterValue + "Exception " + e.Message + " Run help command for information about usage", true);
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                Environment.Exit(0);
            }
            finally
            {
                File.AppendAllText(logPath, DateTime.Now.ToString() + "\t" + "Following parameters are set: " + Environment.NewLine);
                parameterList.ForEach(x => File.AppendAllText(logPath, DateTime.Now.ToString() + "\t" + x + Environment.NewLine));
                Console.WriteLine("Parameter log written to " + logPath, true);
            }
            
            if (mode.Equals("link"))
            {
                RunInLinkMode();
            }
            else if (mode.Equals("doc"))
            {
                RunInDocMode();
            }
            else if (mode.Equals("migrate"))
            {
                RunInMigrateMode();
            }
            else
            {
                commSupport.PrintMessage("'mode' parameter missing or faulty. Mode parameter value: " + mode, true);
            }
        }

        private static void RunInMigrateMode()
        {
            if (string.IsNullOrEmpty(cloudDeploymentUrl) || string.IsNullOrEmpty(uploadpath) || string.IsNullOrEmpty(jwtToken) || string.IsNullOrEmpty(appId))
            {
                commSupport.PrintMessage("cloudurl, uploadpath, appId and api_key are required parameters", true);
            }

            try
            {
                commSupport.PrintMessage("Ready to migrate", false);
                string result = MigrateFiles(commSupport, uploadpath, proxyname, proxyport);
                if(string.IsNullOrEmpty(result))
                {
                    commSupport.PrintMessage("Failed to migrate " + uploadpath, true);
                }
                else if (!string.IsNullOrEmpty(handledDirectory))
                {
                    CopyUploadedFileToHandledDirectory(handledDirectory, uploadpath);
                }
                commSupport.PrintMessage("Migration of " + uploadpath + " succeeded", true);
            }
            catch (Exception e)
            {
                commSupport.PrintMessage("Exception when uploading file to cloud: " + e.Message, true);
            }
        }

        private static void CopyUploadedFileToHandledDirectory(string handledDirectory, string uploadpath)
        {
            commSupport.PrintMessage("Copying file " + uploadpath + " to " + handledDirectory, false);
            try
            {
                string targetFolder = handledDirectory + @"\" + Path.GetFileName(uploadpath);
                File.Copy(uploadpath, targetFolder, true);
                commSupport.PrintMessage("File copied to " + handledDirectory, false);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private static void RunInDocMode()
        {
            if (string.IsNullOrEmpty(cloudDeploymentUrl) || string.IsNullOrEmpty(qvsCluster) || string.IsNullOrEmpty(jwtToken))
            {
                commSupport.PrintMessage("Please supply required parameters", true);
            }

            client = CreateQMSAPIClient(qms);

            try
            {
                SetUpStartValues();
                DistributeDocumentsOrFiles(commSupport, proxyname, proxyport);
            }
            catch (Exception e)
            {
                commSupport.PrintMessage("Exception: " + e.Message, true);
            }
        }

        private static void RunInLinkMode()
        {
            if (string.IsNullOrEmpty(qvsCluster) || string.IsNullOrEmpty(qvwsMachine)
                || string.IsNullOrEmpty(csvFileName))
            {
                commSupport.PrintMessage("Please supply required parameters", true);
            }
            client = CreateQMSAPIClient(qms);
            QlikQvExport qvExportLinks = new QlikQvExport(commSupport, client);
            qvExportLinks.ExportLinks(space, protocol, qvsCluster, qvwsMachine, category, csvFileName);
        }

        private static void SetUpStartValues()
        {
            client.ClearQVSCache(QVSCacheObjects.UserDocumentList);
            qvsId = FindServiceId(ServiceTypes.QlikViewServer, qvsCluster);
            qvsSettings = client.GetQVSSettings(qvsId, QVSSettingsScope.Folders);
            userDocFolders = client.GetUserDocumentFolders(qvsId, DocumentFolderScope.All);
            userDocs = GetUserDocuments();
        }

        private static Guid FindServiceId(ServiceTypes serviceType, string serviceName)
        {
            try
            {
                List<ServiceInfo> service = client.GetServices(serviceType);
                if (service != null && service.Count > 0)
                {
                    return service.FirstOrDefault(x => x.Name.Equals(serviceName)).ID;
                }
                else
                {
                    commSupport.PrintMessage("Could not find " + serviceName + " service. Make sure cluster name is correct", true);
                }
            }
            catch (Exception)
            {
                commSupport.PrintMessage("Exception when trying to fetch " + serviceName + ". Make sure cluster name is correct", true);
            }
            return Guid.Empty;
        }

        private static void DisplayDocUploaderHelp()
        {
            string url = "https://" + "mycloud.somecloud.se/";
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("qlik_qv_export can only be executed on the machine running the Qlikview Management service");
            Console.WriteLine("");
            Console.WriteLine("Usage when running in doc mode:");
            Console.WriteLine("-mode=<mode> -cloudUrl=<cloud API endpoint> -qvscluster=<qvs cluster name>  -API_key=<API Key> -documentFolder=<name of QVS folder>");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("-mode            [Required] Which mode should the program run in. 'doc' for uploading qlikview documents to cloud");
            Console.WriteLine("-cloudUrl        [Required] Url of cloud API endpoint, i.e target for document upload");
            Console.WriteLine("-qvscluster      [Required] Name of QlikView server cluster as displayed in QMC");
            Console.WriteLine("-API_key         [Required] The temporary API Key generated by cloud admin");
            Console.WriteLine("-documentFolder  [Optional] Name of QlikView server document folder, root or mount. All documents in folder will be uploaded. Leave empty for All Documents, type 'root' for Qvs root folder");
            Console.WriteLine("-proxyname       [Optional] Name of proxy machine");
            Console.WriteLine("-proxyport       [Optional] Proxy port");
            Console.WriteLine("");
            Console.WriteLine("Example: qlik_qv_export.exe -mode=doc -cloudUrl=" + url + " -qvscluster=QVS@machineOne  -API_key=myTempApiKey -documentFolder=myMount");
            DisplayMigrateHelp();
        }

        private static void DisplayLinkExportHelp()
        {
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Usage when running in link mode:");
            Console.WriteLine("-mode=<mode> -space=<name of space> -protocol=<http/https> -qvscluster=<qvs cluster name>  -qvwsmachine=<Qlikview webserver name> -category=<name of category as seen in accesspoint> -filename=<path to file containing exported links>");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("-mode        [Required] Which mode should the program run in. 'link' for exporting links to csv file.");
            Console.WriteLine("-filename    [Required] Name and path of csv file containing links");
            Console.WriteLine("-qvscluster  [Required] Name of Qvs cluster as displayed in QMC");
            Console.WriteLine("-qvwsmachine [Required] Name of Qvws as displayed in QMC");
            Console.WriteLine("-space       [Optional] The cloud space name");
            Console.WriteLine("-protocol    [Optional] The protocol used for connecting to Management Service. Can be http or https. Defaults to http");
            Console.WriteLine("-category    [Optional] Only add links belonging to specified category.");
            Console.WriteLine("");
            string path = @"C:\QvLinks\MyQvLinks.csv";
            Console.WriteLine("Example: qlik_qv_export.exe -mode=link -space=Common -protocol=http -qvscluster=QVS@machineOne  -qvwsmachine=QVWS@webServer -category=Sales -filename=" + path);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void DisplayMigrateHelp()
        {
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Usage when running in migrate mode:");
            Console.WriteLine("-mode=<mode> -cloudUrl=<cloud_address>  -appId=<cloud app id> -api_key=<api key> -uploadpath=<path to file to be uploaded> -handledDirectory=<directory to where to copy file after upload>");
            Console.WriteLine("");
            Console.WriteLine("Parameters:");
            Console.WriteLine("-mode                [Required] Which mode should the program run in. 'migrate' to upload BM to cloud.");
            Console.WriteLine("-cloudUrl            [Required] Url of cloud API endpoint, i.e target for document upload");
            Console.WriteLine("-appId               [Required] The id of the app to which the bookmark belongs");
            Console.WriteLine("-api_key             [Required] The temporary API Key generated by cloud admin");
            Console.WriteLine("-uploadpath          [Required] The full path to the file being uploaded");
            Console.WriteLine("-handledDirectory    [Optional] Directory to where to copy file after upload");
            Console.WriteLine("");
            Console.WriteLine("Example: qlik_qv_export.exe -mode=migrate -uploadpath=C:\\ALL_R&D.QVW.zip -cloudUrl=https://QloudyMcCloudFace.com/ -appId=2f6cd2fa-f759-4644-8225-b6ce1213142d -api_key=12345");
            DisplayLinkExportHelp();
        }

        private static string GetDocumentPath(DocumentNode userDoc)
        {

            string documentPath = string.Empty;

            DocumentFolder docFolder = userDocFolders.FirstOrDefault(x => x.ID.Equals(userDoc.FolderID));
            if (docFolder != null)
            {
                QVSMount temp = qvsSettings.Folders.UserDocumentMounts.FirstOrDefault(x => x.Name.Equals(docFolder.General.Path));
                string relativePath = string.IsNullOrEmpty(userDoc.RelativePath) ? "" : userDoc.RelativePath + "\\";
                if (temp != null)
                {
                    documentPath = temp.Path + "\\" + relativePath;
                }
                else
                {
                    documentPath = qvsSettings.Folders.UserDocumentRootFolder + "\\" + relativePath;
                }
            }
            return documentPath;
        }

        private static void DistributeDocumentsOrFiles(CommunicationSupport commSupport, string proxyName, string proxyPort)
        {
            commSupport.PrintMessage(userDocs.Count + " document(s) marked for upload", false);
            foreach (DocumentNode doc in userDocs)
            {
                string fullDocumentPath = GetDocumentPath(doc) + doc.Name;
                _ = commSupport.DistributeDocumentsOrFiles(fullDocumentPath, cloudDeploymentUrl, doc.ID.ToString(), jwtToken, "autoreplace", proxyName, proxyPort).GetAwaiter().GetResult();
            }
        }

        private static string MigrateFiles(CommunicationSupport commSupport, string filePath, string proxyName, string proxyPort)
        {
            return commSupport.DistributeDocumentsOrFiles(filePath, cloudDeploymentUrl, "", jwtToken, "autoreplace", proxyName, proxyPort, appId).GetAwaiter().GetResult();
        }

        private static Guid FindFolderId()
        {
            DocumentFolder docFolder = null;
            if (mountOrRootFolder.Equals("root"))
            {
                docFolder = userDocFolders.Find(x => string.IsNullOrEmpty(x.General.Path));
            }
            else
            {
                docFolder = userDocFolders.Find(x => x.General.Path.Equals(mountOrRootFolder));
            }

            if (docFolder != null)
            {
                return docFolder.ID;
            }

            return Guid.Empty;
        }

        private static List<DocumentNode> GetUserDocuments()
        {
            List<DocumentNode> userDocuments = client.GetUserDocuments(qvsId);//Need to fetch all for full recursive look up
            if (string.IsNullOrEmpty(mountOrRootFolder))
            {
                return userDocuments;
            }
            else
            {
                Guid folderId = FindFolderId();
                List<DocumentNode> retValue = new List<DocumentNode>();
                foreach (DocumentNode node in userDocuments)
                {
                    if (node.FolderID == folderId)
                    {
                        retValue.Add(node);
                    }
                }
                return retValue;
            }
        }

        private static IQMS CreateQMSAPIClient(Uri uri)
        {
            QMSClient client = null;

            try
            {
                client = new QMSClient("BasicHttpBinding_IQMS", uri.AbsoluteUri);
                ServiceKeyEndpointBehavior serviceKeyEndpointBehavior = new ServiceKeyEndpointBehavior();
                client.Endpoint.Behaviors.Add(serviceKeyEndpointBehavior);
                ServiceKeyClientMessageInspector.ServiceKey = client.GetTimeLimitedServiceKey();
            }
            catch (Exception e)
            {
                commSupport.PrintMessage("Exception when creating QMs client, run help command for information about usage. Exeception: " + e.Message, true);
            }
            return client;
        }
    }
}
